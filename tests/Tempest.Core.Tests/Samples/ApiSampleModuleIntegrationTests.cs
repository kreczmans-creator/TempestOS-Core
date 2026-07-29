using System.Net;
using System.Net.Http;
using Tempest.Core.Api;
using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 6.3 end-to-end: ApiSampleModule maps a real HTTP route to
// ReportingSampleModule's own already-registered command, and the REST
// API's own hosted service (RestApiHostedService, driven by real Kestrel)
// authorizes, dispatches, and responds correctly through the real,
// unmodified TempestHost - genuine HTTP requests, not an in-process
// simulation, mirroring TempestHostHostedServiceTests' own "against the
// real, unmodified Host" testing philosophy. Hosted service discovery is
// explicitly scoped to [typeof(RestApiHostedService)] so no other test's
// own hosted service fixtures are pulled in, and no unrelated test
// accidentally starts a real HTTP listener - see HostedServiceDiscoveryServiceTests'
// own precedent for this isolation convention. Every test configures
// port 0 (an OS-assigned, collision-free ephemeral port), read back via
// RestApiHostedService.BoundPort, so no two tests can ever race for the
// same port.
[Collection("Console output capture")]
public class ApiSampleModuleIntegrationTests
{
    private static async Task<(RestApiHostedService HostedService, ITempestHost Host)> StartHostAsync(
        string persistenceRootPath, IEnumerable<string>? grantedPermissions = null)
    {
        var configurationEntries = new List<KeyValuePair<string, string>>
        {
            new(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
            new(RestApiHostedService.PortConfigurationKey, "0"),
        };

        var permissions = grantedPermissions?.ToList();
        if (permissions is { Count: > 0 })
        {
            configurationEntries.Add(new("Identity:Roles:ReportGenerator:Permissions", string.Join(',', permissions)));
            configurationEntries.Add(new($"Identity:Principals:{ReportingSampleModule.SampleIdentityId}:Roles", "ReportGenerator"));
        }

        var host = new TempestHostBuilder(
                discoveryCandidateTypesOverride: [typeof(ReportingSampleModule), typeof(ApiSampleModule)],
                pluginsRootPathOverride: null,
                hostedServiceCandidateTypesOverride: [typeof(RestApiHostedService)])
            .AddConfigurationSource(new MemoryConfigurationSource(configurationEntries))
            .Build();

        _ = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        var hostedService = (RestApiHostedService)host.Services!.GetService(typeof(RestApiHostedService));

        while (hostedService.BoundPort is null)
            await Task.Delay(5);

        return (hostedService, host);
    }

    private static async Task RunAgainstRealHttpAsync(IEnumerable<string>? grantedPermissions, Func<HttpClient, RestApiHostedService, ITempestHost, Task> body)
    {
        using var temp = new TempDirectory();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());
            var (hostedService, host) = await StartHostAsync(temp.Path, grantedPermissions);

            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{hostedService.BoundPort}/") };
            await body(client, hostedService, host);

            await host.StopAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task PostToMappedRoute_WithGrantedPermission_Returns200AndGeneratesTheReport()
    {
        await RunAgainstRealHttpAsync([ReportingSampleModule.GenerateReportPermissionKey], async (client, _, _) =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ApiSampleModule.GenerateReportRoutePath);
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, ReportingSampleModule.SampleIdentityId);

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Generated report", body);
        });
    }

    [Fact]
    public async Task PostToMappedRoute_NoIdentityHeader_Returns401()
    {
        await RunAgainstRealHttpAsync([ReportingSampleModule.GenerateReportPermissionKey], async (client, _, _) =>
        {
            var response = await client.PostAsync(ApiSampleModule.GenerateReportRoutePath, content: null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        });
    }

    [Fact]
    public async Task PostToMappedRoute_PermissionNotGranted_Returns403()
    {
        await RunAgainstRealHttpAsync(grantedPermissions: null, async (client, _, _) =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ApiSampleModule.GenerateReportRoutePath);
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, ReportingSampleModule.SampleIdentityId);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        });
    }

    [Fact]
    public async Task GetUnmappedPath_Returns404()
    {
        await RunAgainstRealHttpAsync([ReportingSampleModule.GenerateReportPermissionKey], async (client, _, _) =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/no-such-route");
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, ReportingSampleModule.SampleIdentityId);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    [Fact]
    public async Task GetOpenApiDocument_ReturnsAJsonDocumentDescribingTheMappedRoute()
    {
        await RunAgainstRealHttpAsync([ReportingSampleModule.GenerateReportPermissionKey], async (client, _, _) =>
        {
            var response = await client.GetAsync(RestApiHostedService.OpenApiPath);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(ApiSampleModule.GenerateReportRoutePath, body);
            Assert.Contains("openapi", body);
        });
    }

    [Fact]
    public async Task ConcurrentRequests_FromDifferentPrincipals_AreEachHandledIndependently()
    {
        // Proves this platform's first genuinely concurrent, per-request
        // scenario: two requests, in flight at the same time, each
        // resolving its own principal without either leaking into the
        // other - safe by construction, since ApiRequestHandler never
        // touches the shared ambient ICurrentPrincipalAccessor (see
        // ADR-0052).
        await RunAgainstRealHttpAsync([ReportingSampleModule.GenerateReportPermissionKey], async (client, _, _) =>
        {
            Task<HttpResponseMessage> SendAsGranted()
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ApiSampleModule.GenerateReportRoutePath);
                request.Headers.Add(ApiRequestHandler.IdentityHeaderName, ReportingSampleModule.SampleIdentityId);
                return client.SendAsync(request);
            }

            Task<HttpResponseMessage> SendAsUngranted()
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ApiSampleModule.GenerateReportRoutePath);
                request.Headers.Add(ApiRequestHandler.IdentityHeaderName, "sample.unrelated-user");
                return client.SendAsync(request);
            }

            var tasks = new List<Task<HttpResponseMessage>>();
            for (var i = 0; i < 10; i++)
            {
                tasks.Add(SendAsGranted());
                tasks.Add(SendAsUngranted());
            }

            var responses = await Task.WhenAll(tasks);

            for (var i = 0; i < responses.Length; i += 2)
            {
                Assert.Equal(HttpStatusCode.OK, responses[i].StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, responses[i + 1].StatusCode);
            }
        });
    }

    [Fact]
    public async Task GrantedRequest_RecordsAnAuditEntryCarryingTheCallerIdentityInDetail()
    {
        await RunAgainstRealHttpAsync(
            [ReportingSampleModule.GenerateReportPermissionKey, AuditQuery.QueryPermission.Key],
            async (client, _, host) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ApiSampleModule.GenerateReportRoutePath);
                request.Headers.Add(ApiRequestHandler.IdentityHeaderName, ReportingSampleModule.SampleIdentityId);
                await client.SendAsync(request);

                var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
                var records = await auditQuery.QueryAsync(new AuditQueryCriteria(action: ApiRequestHandler.RequestAuditAction));

                var record = Assert.Single(records);
                Assert.Equal(ReportingSampleModule.SampleIdentityId, record.Detail[ApiRequestHandler.CallerIdentityDetailKey]);
            });
    }

    // ----------------------------------------------------------------
    // Hosted-service failure isolation (ADR-0021): a genuine start
    // failure (the configured port already in use) must not fault the
    // whole Host - mirroring TempestHostHostedServiceTests'
    // own RunAsync_IsolatedHostedServiceFailure_HostStillReachesRunning.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_ConfiguredPortAlreadyInUse_HostStillReachesRunning_FailureIsolated()
    {
        using var temp = new TempDirectory();
        using var occupyingSocket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
        occupyingSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        occupyingSocket.Listen();
        var occupiedPort = ((IPEndPoint)occupyingSocket.LocalEndPoint!).Port;

        var host = new TempestHostBuilder(
                discoveryCandidateTypesOverride: [typeof(ReportingSampleModule), typeof(ApiSampleModule)],
                pluginsRootPathOverride: null,
                hostedServiceCandidateTypesOverride: [typeof(RestApiHostedService)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
                new KeyValuePair<string, string>(RestApiHostedService.PortConfigurationKey, occupiedPort.ToString()),
            ]))
            .Build();
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Stopped, host.State);
        Assert.Contains("failed to start; isolated", writer.ToString());
    }
}
