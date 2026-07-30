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
    private static RestApiHostedService BuildService(IConfigurationProvider configuration) =>
        new(
            new ApiEndpointRegistry(),
            new CommandRegistryStub(),
            new IdentityServiceStub(),
            new PermissionEvaluatorStub(),
            new AuditRecorderStub(),
            configuration,
            new RecordingLevelLogger());

    private static IConfigurationProvider ConfigurationWithPort(string port) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(RestApiHostedService.PortConfigurationKey, port),
        ])).Build();

    [Fact]
    public async Task StartAsync_BindsToAnOsAssignedPort_ReachableOverRealHttp()
    {
        var service = BuildService(ConfigurationWithPort("0"));

        await service.StartAsync(CancellationToken.None);
        try
        {
            Assert.NotNull(service.BoundPort);

            using var client = new HttpClient();
            var response = await client.GetAsync($"http://127.0.0.1:{service.BoundPort}{RestApiHostedService.OpenApiPath}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    }

    private sealed class IdentityServiceStub : IIdentityService
    {
        public IPrincipal GetPrincipal(string identityId) => new PlatformPrincipal(new PlatformIdentity(identityId, identityId), []);

        public IPrincipal EstablishCurrentPrincipal(string identityId) =>
            throw new InvalidOperationException("RestApiHostedService must never establish the ambient current principal - see ADR-0052.");
    }

    private sealed class PermissionEvaluatorStub : IPermissionEvaluator
    {
        public bool HasPermission(IPrincipal principal, Permission permission) => false;

        public void RequirePermission(IPrincipal principal, Permission permission) =>
            throw new PermissionDeniedException(principal, permission);
    }

    private sealed class AuditRecorderStub : IAuditRecorder
    {
        public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
