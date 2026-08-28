using System.Net;
using System.Net.Http;
using System.Text;
using Tempest.Core.Api;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Api;

// Proves the late-bound query-and-action surface (ADR-0114) over real
// HTTP/Kestrel: the catch-all fallback serves a query registered AFTER
// StartAsync (the ordering the surface exists for), returns
// application/json for queries and text/plain for everything else, and
// binds a POST body through to the action delegate - while statically
// mapped behaviour (the OpenAPI document, 404s) is unchanged.
[Collection("Console output capture")]
public class ApiQueryHttpTests
{
    private const string CallerId = "query-caller";

    private static readonly Permission ReadPermission = new("companion.read");

    private sealed class GrantingIdentityService : IIdentityService
    {
        public IPrincipal GetPrincipal(string identityId) =>
            new PlatformPrincipal(new PlatformIdentity(identityId, identityId), [ReadPermission]);

        public IPrincipal EstablishCurrentPrincipal(string identityId) =>
            throw new InvalidOperationException("Never establishes the ambient principal - ADR-0052.");
    }

    private sealed class AuditRecorderStub : Tempest.Core.Audit.IAuditRecorder
    {
        public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EmptyCommandRegistry : ICommandRegistry
    {
        public void RegisterDescriptor(CommandDescriptor descriptor)
        {
        }

        public IReadOnlyList<CommandDescriptor> Items => [];

        public Task<CommandResult> InvokeAsync(string id, CancellationToken cancellationToken = default) =>
            throw new CommandNotFoundException(id);
    }

    private static (RestApiHostedService Service, ApiQueryRegistry QueryRegistry) BuildService()
    {
        var queryRegistry = new ApiQueryRegistry();
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(RestApiHostedService.PortConfigurationKey, "0"),
        ])).Build();

        var service = new RestApiHostedService(
            new ApiEndpointRegistry(),
            queryRegistry,
            new EmptyCommandRegistry(),
            new GrantingIdentityService(),
            new PermissionEvaluator(),
            new AuditRecorderStub(),
            configuration);

        return (service, queryRegistry);
    }

    [Fact]
    public async Task Query_RegisteredAfterStart_ServesJsonOverRealHttp()
    {
        var (service, queryRegistry) = BuildService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            // Registered after Kestrel started - the late-binding contract.
            queryRegistry.MapQuery("/api/v1/companion/cockpit", ReadPermission, _ => Task.FromResult("""{"health":"Unknown"}"""));

            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{service.BoundPort}/api/v1/companion/cockpit");
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, CallerId);

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("""{"health":"Unknown"}""", body);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Action_PostBody_ReachesTheActionDelegate()
    {
        var (service, queryRegistry) = BuildService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            string? received = null;
            queryRegistry.MapAction("/api/v1/companion/actions/echo", ReadPermission, (body, _) =>
            {
                received = body;
                return Task.FromResult(CommandResult.Success("acted"));
            });

            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{service.BoundPort}/api/v1/companion/actions/echo")
            {
                Content = new StringContent("""{"targetKind":"Document"}""", Encoding.UTF8, "application/json"),
            };
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, CallerId);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("acted", await response.Content.ReadAsStringAsync());
            Assert.Equal("""{"targetKind":"Document"}""", received);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UnknownPath_StillReturns404_WithNoIdentityRequired()
    {
        var (service, _) = BuildService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"http://127.0.0.1:{service.BoundPort}/api/v1/unmapped");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OpenApiDocument_IncludesQueryRoutes()
    {
        var (service, queryRegistry) = BuildService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            queryRegistry.MapQuery("/api/v1/companion/cockpit", ReadPermission, _ => Task.FromResult("{}"));

            using var client = new HttpClient();
            var body = await client.GetStringAsync($"http://127.0.0.1:{service.BoundPort}{RestApiHostedService.OpenApiPath}");

            Assert.Contains("/api/v1/companion/cockpit", body);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
}
