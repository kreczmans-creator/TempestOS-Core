using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Tempest.Core.Api;
using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;
using Tempest.Core.Tests.Events;

namespace Tempest.Core.Tests.Api;

// Proves RestApiHostedService's own IHostedService contract directly -
// starts listening on an OS-assigned port, stops accepting connections
// once stopped, and isolates a genuine start failure (a port already in
// use) rather than throwing past this instance's own StartAsync in a way
// that would be indistinguishable from any other hosted service's own
// isolated failure (ADR-0021) - see ApiSampleModuleIntegrationTests for
// the full, real-Host, real-HTTP proof through TempestHost itself.
[Collection("Console output capture")]
public class RestApiHostedServiceTests
{
    private static RestApiHostedService BuildService(IConfigurationProvider configuration, IPermissionEvaluator? permissionEvaluator = null) =>
        new(
            new ApiEndpointRegistry(),
            new CommandRegistryStub(),
            new IdentityServiceStub(),
            permissionEvaluator ?? new PermissionEvaluatorStub(),
            new AuditRecorderStub(),
            configuration,
            new RecordingLevelLogger());

    // Every test that needs the listener to actually start opts in
    // explicitly via Runtime:RestApi:Enabled=true (D-024, Proposed) -
    // mirroring exactly what a real caller now has to do.
    private static IConfigurationProvider ConfigurationWithPort(string port, bool enabled = true) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(RestApiHostedService.PortConfigurationKey, port),
            new KeyValuePair<string, string>(RestApiHostedService.EnabledConfigurationKey, enabled.ToString()),
        ])).Build();

    // ----------------------------------------------------------------
    // D-024 (Proposed): the listener is disabled unless explicitly
    // enabled via configuration.
    // ----------------------------------------------------------------

    [Fact]
    public async Task StartAsync_EnabledKeyAbsent_DisabledByDefault_DoesNotBindAnything()
    {
        // The shipped default: no Runtime:RestApi:Enabled key at all.
        var configuration = new ConfigurationBuilder().Build();
        var service = BuildService(configuration);

        await service.StartAsync(CancellationToken.None);

        Assert.Null(service.BoundPort);

        // StopAsync must stay safe even though the service never started.
        await service.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("not-a-boolean")]
    public async Task StartAsync_EnabledKeyFalseOrBlankOrUnparseable_DoesNotBindAnything(string rawEnabledValue)
    {
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(RestApiHostedService.PortConfigurationKey, "0"),
            new KeyValuePair<string, string>(RestApiHostedService.EnabledConfigurationKey, rawEnabledValue),
        ])).Build();
        var service = BuildService(configuration);

        await service.StartAsync(CancellationToken.None);

        Assert.Null(service.BoundPort);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_EnabledKeyTrue_BindsToAnOsAssignedPort()
    {
        var service = BuildService(ConfigurationWithPort("0", enabled: true));

        await service.StartAsync(CancellationToken.None);
        try
        {
            Assert.NotNull(service.BoundPort);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // ----------------------------------------------------------------
    // TD-62: the OpenAPI document goes through the same identity +
    // permission pipeline as every command route.
    // ----------------------------------------------------------------

    [Fact]
    public async Task StartAsync_BindsToAnOsAssignedPort_ReachableOverRealHttp()
    {
        var service = BuildService(
            ConfigurationWithPort("0"),
            new PermissionEvaluatorStub(ApiRequestHandler.OpenApiDocumentPermission.Key));

        await service.StartAsync(CancellationToken.None);
        try
        {
            Assert.NotNull(service.BoundPort);

            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{service.BoundPort}{RestApiHostedService.OpenApiPath}");
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, "sample.identity");
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetOpenApiDocument_NoIdentityHeader_Returns401_NotTheDocument()
    {
        // TD-62: previously this returned 200 with the full route ->
        // required-permission map to any unauthenticated caller. It must
        // now behave exactly like an unauthenticated command-route
        // caller.
        var service = BuildService(ConfigurationWithPort("0"));

        await service.StartAsync(CancellationToken.None);
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"http://127.0.0.1:{service.BoundPort}{RestApiHostedService.OpenApiPath}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetOpenApiDocument_IdentityWithoutPermission_Returns403()
    {
        var service = BuildService(ConfigurationWithPort("0")); // PermissionEvaluatorStub grants nothing by default.

        await service.StartAsync(CancellationToken.None);
        try
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{service.BoundPort}{RestApiHostedService.OpenApiPath}");
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, "sample.identity");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetOpenApiDocument_AuthorisedCaller_ReturnsTheFullDocument()
    {
        var service = BuildService(
            ConfigurationWithPort("0"),
            new PermissionEvaluatorStub(ApiRequestHandler.OpenApiDocumentPermission.Key));

        await service.StartAsync(CancellationToken.None);
        try
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{service.BoundPort}{RestApiHostedService.OpenApiPath}");
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, "sample.identity");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("openapi", body);
            Assert.Contains("\"paths\"", body);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StopAsync_StopsAcceptingNewConnections()
    {
        var service = BuildService(ConfigurationWithPort("0"));
        await service.StartAsync(CancellationToken.None);
        var port = service.BoundPort!.Value;

        await service.StopAsync(CancellationToken.None);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        // Once stopped, the port either refuses the connection outright
        // (HttpRequestException) or the request simply never completes
        // within the client's own timeout (TaskCanceledException) -
        // both prove the same thing: no request is served post-Stop.
        var exception = await Record.ExceptionAsync(() =>
            client.GetAsync($"http://127.0.0.1:{port}{RestApiHostedService.OpenApiPath}"));

        Assert.True(exception is HttpRequestException or TaskCanceledException,
            $"Expected a connection failure, got: {exception}");
    }

    [Fact]
    public async Task StartAsync_PortAlreadyInUse_ThrowsRatherThanSilentlyListeningElsewhere()
    {
        // A genuine start failure (ADR-0021's own isolated-by-default
        // hosted-service failure model, exercised here directly against
        // this one instance - HostedServiceManager's own isolation of
        // that thrown exception is already proven generically by
        // TempestHostHostedServiceTests; this test only proves
        // RestApiHostedService itself surfaces the failure rather than
        // swallowing it).
        using var occupyingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        occupyingSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        occupyingSocket.Listen();
        var occupiedPort = ((IPEndPoint)occupyingSocket.LocalEndPoint!).Port;

        var service = BuildService(ConfigurationWithPort(occupiedPort.ToString()));

        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(CancellationToken.None));
    }

    [Fact]
    public void RestApiHostedService_IsNotCritical()
    {
        // Isolated by default (ADR-0021) - does not implement
        // ICriticalBackgroundService, mirroring NotificationSampleHostedService's
        // own default.
        Assert.False(typeof(Tempest.Core.BackgroundServices.ICriticalBackgroundService).IsAssignableFrom(typeof(RestApiHostedService)));
    }

    private sealed class CommandRegistryStub : ICommandRegistry
    {
        public void RegisterDescriptor(CommandDescriptor descriptor)
        {
        }

        public IReadOnlyList<CommandDescriptor> Items => [];

        public Task<CommandResult> InvokeAsync(string id, CancellationToken cancellationToken = default) =>
            throw new CommandNotFoundException(id);

        public CommandAvailability Evaluate(string id, CommandContext context) =>
            CommandAvailability.Blocked($"No command '{id}' is registered.");

        public Task<CommandInvocation> InvokeAsync(
            string id, CommandContext context, CommandParameterPrompt? prompt = null, CancellationToken cancellationToken = default) =>
            throw new CommandNotFoundException(id);
    }

    private sealed class IdentityServiceStub : IIdentityService
    {
        public IPrincipal GetPrincipal(string identityId) => new PlatformPrincipal(new PlatformIdentity(identityId, identityId), []);

        public IPrincipal EstablishCurrentPrincipal(string identityId) =>
            throw new InvalidOperationException("RestApiHostedService must never establish the ambient current principal - see ADR-0052.");
    }

    private sealed class PermissionEvaluatorStub : IPermissionEvaluator
    {
        private readonly HashSet<string> _grantedPermissionKeys;

        public PermissionEvaluatorStub(params string[] grantedPermissionKeys) =>
            _grantedPermissionKeys = new HashSet<string>(grantedPermissionKeys, StringComparer.Ordinal);

        public bool HasPermission(IPrincipal principal, Permission permission) => _grantedPermissionKeys.Contains(permission.Key);

        public void RequirePermission(IPrincipal principal, Permission permission)
        {
            if (!HasPermission(principal, permission))
                throw new PermissionDeniedException(principal, permission);
        }
    }

    private sealed class AuditRecorderStub : IAuditRecorder
    {
        public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
