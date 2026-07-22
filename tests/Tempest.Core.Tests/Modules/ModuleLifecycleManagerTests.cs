using Tempest.Core.Logging;
using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

public class ModuleLifecycleManagerTests
{
    public ModuleLifecycleManagerTests()
    {
        LifecycleTestLog.Reset();
    }

    private static ModuleDescriptor Describe<T>(string id) where T : IModule =>
        new(id, $"Module {id}", "1.0.0", typeof(T));

    private static RuntimeModuleManager BuildRegisteredModules(params ModuleDescriptor[] descriptors)
    {
        var manager = new RuntimeModuleManager();

        foreach (var descriptor in descriptors)
            manager.Register(descriptor);

        return manager;
    }

    [Fact]
    public async Task InitialiseAllAsync_RunsModulesInAscendingIdOrder()
    {
        var runtimeManager = BuildRegisteredModules(
            Describe<RecordingLifecycleModuleGamma>("lifecycle.gamma"),
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"),
            Describe<RecordingLifecycleModuleBeta>("lifecycle.beta"));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "lifecycle.alpha:Initialise", "lifecycle.beta:Initialise", "lifecycle.gamma:Initialise" },
            LifecycleTestLog.Entries);
    }

    [Fact]
    public async Task StartAllAsync_RunsModulesInAscendingIdOrder_AfterInitialise()
    {
        var runtimeManager = BuildRegisteredModules(
            Describe<RecordingLifecycleModuleGamma>("lifecycle.gamma"),
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"),
            Describe<RecordingLifecycleModuleBeta>("lifecycle.beta"));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

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
        var runtimeManager = BuildRegisteredModules(
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"),
            Describe<RecordingLifecycleModuleBeta>("lifecycle.beta"),
            Describe<RecordingLifecycleModuleGamma>("lifecycle.gamma"));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

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
        var runtimeManager = BuildRegisteredModules(
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"),
            Describe<RecordingLifecycleModuleBeta>("lifecycle.beta"),
            Describe<RecordingLifecycleModuleGamma>("lifecycle.gamma"));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

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
        var runtimeManager = BuildRegisteredModules(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

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
        var runtimeManager = BuildRegisteredModules(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

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
        var runtimeManager = BuildRegisteredModules(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

        var exception = await Assert.ThrowsAsync<InvalidModuleLifecycleTransitionException>(() =>
            lifecycleManager.StartModuleAsync("lifecycle.alpha", CancellationToken.None));

        Assert.Equal(ModuleState.Registered, exception.CurrentState);
    }

    [Fact]
    public async Task DisposeModuleAsync_ThrowsInvalidModuleLifecycleTransitionException_WhenAlreadyDisposed()
    {
        var runtimeManager = BuildRegisteredModules(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

        await lifecycleManager.DisposeModuleAsync("lifecycle.alpha", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidModuleLifecycleTransitionException>(() =>
            lifecycleManager.DisposeModuleAsync("lifecycle.alpha", CancellationToken.None));
    }

    [Fact]
    public async Task InitialiseAllAsync_MarksThrowingModuleFailed_AndContinuesWithOtherModules()
    {
        var runtimeManager = BuildRegisteredModules(
            Describe<ThrowingInitialiseLifecycleModule>("lifecycle.throwing-initialise"),
            Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Failed, lifecycleManager.GetState("lifecycle.throwing-initialise"));
        Assert.Equal(ModuleState.Initialised, lifecycleManager.GetState("lifecycle.alpha"));

        var failedStatus = lifecycleManager.Modules.Single(status => status.Descriptor.Id == "lifecycle.throwing-initialise");
        Assert.IsType<InvalidOperationException>(failedStatus.FailureReason);
    }

    [Fact]
    public async Task InitialiseModuleAsync_PropagatesException_WhenCalledDirectly()
    {
        var runtimeManager = BuildRegisteredModules(
            Describe<ThrowingInitialiseLifecycleModule>("lifecycle.throwing-initialise"));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycleManager.InitialiseModuleAsync("lifecycle.throwing-initialise", CancellationToken.None));

        Assert.Equal(ModuleState.Failed, lifecycleManager.GetState("lifecycle.throwing-initialise"));
    }

    [Fact]
    public async Task InitialiseAllAsync_ThrowsOperationCanceledException_WhenTokenAlreadyCancelled()
    {
        var runtimeManager = BuildRegisteredModules(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

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
        var runtimeManager = BuildRegisteredModules(Describe<NoLifecycleModule>("lifecycle.no-lifecycle"));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

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
        var runtimeManager = BuildRegisteredModules(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager);

        Assert.Throws<ArgumentException>(() => lifecycleManager.GetState("missing"));
    }

    [Fact]
    public async Task InitialiseAllAsync_WithLogger_DoesNotThrowAndRecordsProgress()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), $"tempest-lifecycle-tests-{Guid.NewGuid():N}");

        try
        {
            var logger = new LoggingService(logDirectory);
            var runtimeManager = BuildRegisteredModules(Describe<RecordingLifecycleModuleAlpha>("lifecycle.alpha"));
            var lifecycleManager = new ModuleLifecycleManager(runtimeManager, logger);

            await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

            Assert.Equal(ModuleState.Initialised, lifecycleManager.GetState("lifecycle.alpha"));
        }
        finally
        {
            if (Directory.Exists(logDirectory))
                Directory.Delete(logDirectory, recursive: true);
        }
    }
}
