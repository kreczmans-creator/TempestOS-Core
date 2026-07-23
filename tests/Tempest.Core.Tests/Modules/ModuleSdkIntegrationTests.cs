using Tempest.Core.DependencyInjection;
using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

/// <summary>
/// Proves SDK-authored modules (built on <see cref="ModuleBase"/>/
/// <see cref="ModuleLifecycleBase"/>) are discovered, registered, and driven
/// through the full runtime pipeline exactly like a hand-written
/// <see cref="IModule"/>/<see cref="IModuleLifecycle"/> implementation — no
/// special-casing, no behavioural difference, zero regression.
/// </summary>
public class ModuleSdkIntegrationTests
{
    public ModuleSdkIntegrationTests()
    {
        SdkLifecycleLog.Reset();
    }

    [Fact]
    public void Discovery_FindsAModuleBuiltOnModuleBase_WithCorrectMetadata()
    {
        var discovery = new ReflectionFrameworkDiscoveryService();

        var result = discovery.DiscoverModules(new[] { typeof(MinimalSdkModule) });

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.sdk.minimal", descriptor.Id);
        Assert.Equal("Minimal SDK Module", descriptor.Name);
        Assert.Equal("1.0.0", descriptor.Version);
        Assert.Equal(typeof(MinimalSdkModule), descriptor.ModuleType);
    }

    [Fact]
    public void Discovery_FindsAModuleBuiltOnModuleLifecycleBase_WithCorrectMetadata()
    {
        var discovery = new ReflectionFrameworkDiscoveryService();

        var result = discovery.DiscoverModules(new[] { typeof(SdkModuleOverridingOnlyStart) });

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.sdk.only-start", descriptor.Id);
    }

    [Fact]
    public async Task FullPipeline_SdkModule_RunsThroughInitialiseStartStopDispose()
    {
        var descriptor = new ModuleDescriptor(
            "tempest.sdk.every-phase",
            "SDK Module Overriding Every Phase",
            "1.0.0",
            typeof(SdkModuleOverridingEveryPhase));

        var runtimeManager = new RuntimeModuleManager();
        runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));
        var serviceProvider = new TempestServiceProvider(services);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Running, lifecycleManager.GetState("tempest.sdk.every-phase"));

        await lifecycleManager.StopAllAsync(CancellationToken.None);
        await lifecycleManager.DisposeAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Disposed, lifecycleManager.GetState("tempest.sdk.every-phase"));
        Assert.Equal(4, SdkLifecycleLog.Entries.Count);
        Assert.Contains("tempest.sdk.every-phase:Initialise", SdkLifecycleLog.Entries);
        Assert.Contains("tempest.sdk.every-phase:Start", SdkLifecycleLog.Entries);
        Assert.Contains("tempest.sdk.every-phase:Stop", SdkLifecycleLog.Entries);
        Assert.Contains("tempest.sdk.every-phase:Dispose", SdkLifecycleLog.Entries);
    }

    [Fact]
    public async Task FullPipeline_SdkModuleOverridingOnlyStart_DoesNotFail_AndReachesRunning()
    {
        var descriptor = new ModuleDescriptor(
            "tempest.sdk.only-start",
            "SDK Module Overriding Only Start",
            "1.0.0",
            typeof(SdkModuleOverridingOnlyStart));

        var runtimeManager = new RuntimeModuleManager();
        runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));
        var serviceProvider = new TempestServiceProvider(services);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Running, lifecycleManager.GetState("tempest.sdk.only-start"));
        Assert.Single(SdkLifecycleLog.Entries);
        Assert.Contains("tempest.sdk.only-start:Start", SdkLifecycleLog.Entries);
    }
}
