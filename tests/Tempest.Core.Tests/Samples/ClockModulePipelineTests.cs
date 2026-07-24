using Tempest.Core.DependencyInjection;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Modules;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves ClockModule travels through the complete, real, unmodified
// Platform Services pipeline - Discovery, Registration, Dependency
// Injection, Lifecycle - with no special-casing anywhere. Every piece
// composed below is the same production type TempestHost itself composes
// internally (see TempestHost.ExecuteStartupPhasesAsync); nothing here is
// a mock or a test double standing in for a real platform service.
public class ClockModulePipelineTests
{
    // ----------------------------------------------------------------
    // Successful discovery -> successful registration
    // ----------------------------------------------------------------

    [Fact]
    public void RealDiscoveryOutput_RegistersSuccessfully()
    {
        var descriptor = Assert.Single(
            new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]).DiscoverModules());

        var runtimeManager = new RuntimeModuleManager();

        var registered = runtimeManager.Register(descriptor);

        Assert.Equal("tempest.samples.clock", registered.Descriptor.Id);
        Assert.Equal(ModuleState.Registered, registered.State);
        Assert.True(runtimeManager.IsRegistered("tempest.samples.clock"));
    }

    // ----------------------------------------------------------------
    // Successful lifecycle execution, lifecycle ordering, and timestamp
    // recording - through the real, composed pipeline, not in isolation.
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullPipeline_DiscoveryThroughLifecycle_DrivesTheSameInstanceCorrectly()
    {
        var descriptor = Assert.Single(
            new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]).DiscoverModules());

        var runtimeManager = new RuntimeModuleManager();
        runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));
        var serviceProvider = new TempestServiceProvider(services);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Running, lifecycleManager.GetState("tempest.samples.clock"));

        // Modules are registered as singletons (ModuleServiceCollectionExtensions),
        // so resolving the concrete type again returns the exact instance
        // ModuleLifecycleManager itself drove - the real, non-mocked way to
        // observe what happened, mirroring ModuleLifecycleManagerTests' own
        // established composition pattern.
        var clock = Assert.IsType<ClockModule>(serviceProvider.GetService(typeof(ClockModule)));

        Assert.NotNull(clock.InitialisedAt);
        Assert.NotNull(clock.StartedAt);
        Assert.True(clock.InitialisedAt <= clock.StartedAt);
        Assert.True(clock.IsRunning);
        Assert.Null(clock.StoppedAt);

        await lifecycleManager.StopAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Stopped, lifecycleManager.GetState("tempest.samples.clock"));
        Assert.NotNull(clock.StoppedAt);
        Assert.True(clock.StartedAt <= clock.StoppedAt);
        Assert.False(clock.IsRunning);
    }

    // ----------------------------------------------------------------
    // The sample operates through the ordinary Runtime Host sequence,
    // with no special-casing - proven black-box, at the Host's own level.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithClockModule_ReachesRunningThenStopsGracefully_LikeAnyOtherModule()
    {
        var host = new TempestHostBuilder([typeof(ClockModule)]).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_WithClockModuleAndOtherModules_AllReachRunning_NoSpecialCasing()
    {
        // ClockModule alongside another, unrelated module type - proving
        // its presence neither requires nor causes any special handling
        // relative to any other module in the same batch.
        var host = new TempestHostBuilder([typeof(ClockModule), typeof(SampleModuleA)]).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }
}
