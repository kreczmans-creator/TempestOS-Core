using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Tests.Logging;

namespace Tempest.Core.Tests.Modules;

public class ModuleLifecycleManagerTests
{
    public ModuleLifecycleManagerTests()
    {
        LifecycleTestLog.Reset();
    }

    private static ModuleDescriptor Describe<T>(string id) where T : IModule =>
        new(id, $"Module {id}", "1.0.0", typeof(T));

    /// <summary>
    /// Builds a <see cref="ModuleLifecycleManager"/> wired the way a real composition
    /// root would: descriptors registered with a <see cref="RuntimeModuleManager"/>,
    /// their concrete types registered into a <see cref="ServiceCollection"/> via
    /// <see cref="ModuleServiceCollectionExtensions.AddDiscoveredModules"/>, and a
    /// <see cref="TempestServiceProvider"/> built from that collection. This is WP 2.4's
    /// replacement for WP 2.3's direct <c>Activator.CreateInstance</c> call, so these
    /// tests exercise the exact same construction path production code would use.
    /// </summary>
    private static ModuleLifecycleManager BuildLifecycleManager(ILogger? logger, params ModuleDescriptor[] descriptors)
    {
        var runtimeManager = new RuntimeModuleManager();

        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return new ModuleLifecycleManager(runtimeManager, serviceProvider, logger);
    }

    private static ModuleLifecycleManager BuildLifecycleManager(params ModuleDescriptor[] descriptors) =>
        BuildLifecycleManager(logger: null, descriptors);

    [Fact]
    public async Task InitialiseAllAsync_RunsModulesInAscendingIdOrder()
    {
        var lifecycleManager = BuildLifecycleManager(
            Describe<RecordingLifecycleModuleGamma>("lifecycle.gamma"),
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"),
            Describe<RecordingLifecycleModuleBeta>("lifecycle.beta"));

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "lifecycle.alpha:Initialise", "lifecycle.beta:Initialise", "lifecycle.gamma:Initialise" },
            LifecycleTestLog.Entries);
    }

    [Fact]
    public async Task StartAllAsync_RunsModulesInAscendingIdOrder_AfterInitialise()
    {
        var lifecycleManager = BuildLifecycleManager(
            Describe<RecordingLifecycleModuleGamma>("lifecycle.gamma"),
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"),
            Describe<RecordingLifecycleModuleBeta>("lifecycle.beta"));

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        LifecycleTestLog.Reset();

        await lifecycleManager.StartAllAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "lifecycle.alpha:Start", "lifecycle.beta:Start", "lifecycle.gamma:Start" },
            LifecycleTestLog.Entries);

        Assert.All(lifecycleManager.Modules, status => Assert.Equal(ModuleState.Running, status.State));
    }

    [Fact]
    public async Task StopAllAsync_RunsModulesInDescendingIdOrder()
    {
        var lifecycleManager = BuildLifecycleManager(
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"),
            Describe<RecordingLifecycleModuleBeta>("lifecycle.beta"),
            Describe<RecordingLifecycleModuleGamma>("lifecycle.gamma"));

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);
        LifecycleTestLog.Reset();

        await lifecycleManager.StopAllAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "lifecycle.gamma:Stop", "lifecycle.beta:Stop", "lifecycle.alpha:Stop" },
            LifecycleTestLog.Entries);

        Assert.All(lifecycleManager.Modules, status => Assert.Equal(ModuleState.Stopped, status.State));
    }

    [Fact]
    public async Task DisposeAllAsync_RunsModulesInDescendingIdOrder()
    {
        var lifecycleManager = BuildLifecycleManager(
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"),
            Describe<RecordingLifecycleModuleBeta>("lifecycle.beta"),
            Describe<RecordingLifecycleModuleGamma>("lifecycle.gamma"));

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);
        await lifecycleManager.StopAllAsync(CancellationToken.None);
        LifecycleTestLog.Reset();

        await lifecycleManager.DisposeAllAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "lifecycle.gamma:Dispose", "lifecycle.beta:Dispose", "lifecycle.alpha:Dispose" },
            LifecycleTestLog.Entries);

        Assert.All(lifecycleManager.Modules, status => Assert.Equal(ModuleState.Disposed, status.State));
    }

    [Fact]
    public async Task FullLifecycle_TransitionsThroughEveryExpectedState()
    {
        var lifecycleManager = BuildLifecycleManager(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        Assert.Equal(ModuleState.Registered, lifecycleManager.GetState("lifecycle.alpha"));

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Initialised, lifecycleManager.GetState("lifecycle.alpha"));

        await lifecycleManager.StartAllAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Running, lifecycleManager.GetState("lifecycle.alpha"));

        await lifecycleManager.StopAllAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Stopped, lifecycleManager.GetState("lifecycle.alpha"));

        await lifecycleManager.DisposeAllAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Disposed, lifecycleManager.GetState("lifecycle.alpha"));
    }

    [Fact]
    public async Task InitialiseModuleAsync_ThrowsInvalidModuleLifecycleTransitionException_WhenAlreadyInitialised()
    {
        var lifecycleManager = BuildLifecycleManager(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        await lifecycleManager.InitialiseModuleAsync("lifecycle.alpha", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidModuleLifecycleTransitionException>(() =>
            lifecycleManager.InitialiseModuleAsync("lifecycle.alpha", CancellationToken.None));

        Assert.Equal("lifecycle.alpha", exception.ModuleId);
        Assert.Equal(ModuleState.Initialised, exception.CurrentState);
        Assert.Equal("Initialise", exception.AttemptedOperation);
    }

    [Fact]
    public async Task StartModuleAsync_ThrowsInvalidModuleLifecycleTransitionException_WhenNotYetInitialised()
    {
        var lifecycleManager = BuildLifecycleManager(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        var exception = await Assert.ThrowsAsync<InvalidModuleLifecycleTransitionException>(() =>
            lifecycleManager.StartModuleAsync("lifecycle.alpha", CancellationToken.None));

        Assert.Equal(ModuleState.Registered, exception.CurrentState);
    }

    [Fact]
    public async Task DisposeModuleAsync_ThrowsInvalidModuleLifecycleTransitionException_WhenAlreadyDisposed()
    {
        var lifecycleManager = BuildLifecycleManager(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        await lifecycleManager.DisposeModuleAsync("lifecycle.alpha", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidModuleLifecycleTransitionException>(() =>
            lifecycleManager.DisposeModuleAsync("lifecycle.alpha", CancellationToken.None));
    }

    [Fact]
    public async Task InitialiseAllAsync_MarksThrowingModuleFailed_AndContinuesWithOtherModules()
    {
        var lifecycleManager = BuildLifecycleManager(
            Describe<ThrowingInitialiseLifecycleModule>("lifecycle.throwing-initialise"),
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Failed, lifecycleManager.GetState("lifecycle.throwing-initialise"));
        Assert.Equal(ModuleState.Initialised, lifecycleManager.GetState("lifecycle.alpha"));

        var failedStatus = lifecycleManager.Modules.Single(status => status.Descriptor.Id == "lifecycle.throwing-initialise");
        Assert.IsType<InvalidOperationException>(failedStatus.FailureReason);
    }

    [Fact]
    public async Task InitialiseModuleAsync_PropagatesException_WhenCalledDirectly()
    {
        var lifecycleManager = BuildLifecycleManager(
            Describe<ThrowingInitialiseLifecycleModule>("lifecycle.throwing-initialise"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycleManager.InitialiseModuleAsync("lifecycle.throwing-initialise", CancellationToken.None));

        Assert.Equal(ModuleState.Failed, lifecycleManager.GetState("lifecycle.throwing-initialise"));
    }

    [Fact]
    public async Task InitialiseModuleAsync_MarksModuleFailed_WhenServiceProviderResolutionFails()
    {
        var lifecycleManager = BuildLifecycleManager(
            Describe<ModuleWithMissingDependency>("lifecycle.missing-dependency"));

        var exception = await Assert.ThrowsAsync<ServiceNotRegisteredException>(() =>
            lifecycleManager.InitialiseModuleAsync("lifecycle.missing-dependency", CancellationToken.None));

        Assert.Equal(typeof(IUnregisteredLifecycleDependency), exception.MissingServiceType);
        Assert.Equal(ModuleState.Failed, lifecycleManager.GetState("lifecycle.missing-dependency"));

        var status = lifecycleManager.Modules.Single(s => s.Descriptor.Id == "lifecycle.missing-dependency");
        Assert.Same(exception, status.FailureReason);
    }

    [Fact]
    public async Task InitialiseAllAsync_ThrowsOperationCanceledException_WhenTokenAlreadyCancelled()
    {
        var lifecycleManager = BuildLifecycleManager(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            lifecycleManager.InitialiseAllAsync(cts.Token));

        Assert.Empty(LifecycleTestLog.Entries);
        Assert.Equal(ModuleState.Registered, lifecycleManager.GetState("lifecycle.alpha"));
    }

    [Fact]
    public async Task NoLifecycleModule_ProgressesThroughStatesWithoutInvokingAnyMethod()
    {
        var lifecycleManager = BuildLifecycleManager(Describe<NoLifecycleModule>("lifecycle.no-lifecycle"));

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);
        await lifecycleManager.StopAllAsync(CancellationToken.None);
        await lifecycleManager.DisposeAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Disposed, lifecycleManager.GetState("lifecycle.no-lifecycle"));
        Assert.Empty(LifecycleTestLog.Entries);
    }

    [Fact]
    public void GetState_ThrowsArgumentException_WhenModuleUnknown()
    {
        var lifecycleManager = BuildLifecycleManager(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        Assert.Throws<ArgumentException>(() => lifecycleManager.GetState("missing"));
    }

    [Fact]
    public async Task InitialiseAllAsync_WithLogger_DoesNotThrowAndRecordsProgress()
    {
        var logger = new RecordingLogger();
        var lifecycleManager = BuildLifecycleManager(logger, Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Initialised, lifecycleManager.GetState("lifecycle.alpha"));
        Assert.NotEmpty(logger.Messages);
    }
}
