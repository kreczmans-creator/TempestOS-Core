using Tempest.Core.Api;
using Tempest.Core.Identity;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves IApiEndpointRegistry is wired into the
// real, unmodified TempestHost exactly as Service Registration
// Matrix.md specifies - resolvable, ordinary singleton semantics, and a
// real MapCommand round trip through the container-resolved instance.
// Uses the single-argument TempestHostBuilder constructor, which scopes
// hosted service discovery to an empty candidate list (see that
// constructor's own remarks) - so RestApiHostedService itself never
// starts here; ApiSampleModuleIntegrationTests is where the real,
// listening hosted service is exercised end-to-end.
public class ApiHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        await body(host);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public Task Host_RegistersIApiEndpointRegistry_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var endpointRegistry = host.Services!.GetService(typeof(IApiEndpointRegistry));

            Assert.IsType<ApiEndpointRegistry>(endpointRegistry);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ResolvingIApiEndpointRegistryTwice_ReturnsTheSameInstance() =>
        RunAgainstRunningHostAsync(host =>
        {
            var first = host.Services!.GetService(typeof(IApiEndpointRegistry));
            var second = host.Services!.GetService(typeof(IApiEndpointRegistry));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ApiEndpointRegistry_CanRoundTripAMapCommandCallThroughTheRealContainerResolvedInstance() =>
        RunAgainstRunningHostAsync(host =>
        {
            var endpointRegistry = (IApiEndpointRegistry)host.Services!.GetService(typeof(IApiEndpointRegistry));

            endpointRegistry.MapCommand("GET", "/api/v1/registration-round-trip", "tempest.tests.round-trip", new Permission("tempest.tests.round-trip"));

            var route = Assert.Single(endpointRegistry.Routes);
            Assert.Equal("/api/v1/registration-round-trip", route.Path);

            return Task.CompletedTask;
        });
}
