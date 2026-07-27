using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

// Shares SdkLifecycleLog's static state with ModuleSdkIntegrationTests, so
// both are placed in the same xUnit collection to guarantee they never run
// concurrently with each other - xUnit's default parallelism is per-class,
// not per-log, and these two classes touch the same static log.
[Collection("Module SDK lifecycle log")]
public class ModuleLifecycleBaseTests
{
    [Fact]
    public void Constructor_SetsIdNameAndVersion()
    {
        var module = new SdkModuleOverridingOnlyStart();

        Assert.Equal("tempest.sdk.only-start", module.Id);
        Assert.Equal("SDK Module Overriding Only Start", module.Name);
        Assert.Equal("1.0.0", module.Version);
    }

    [Fact]
    public void ModuleLifecycleBase_SatisfiesIModuleAndIModuleLifecycle()
    {
        var module = new SdkModuleOverridingOnlyStart();

        Assert.IsAssignableFrom<IModule>(module);
        Assert.IsAssignableFrom<IModuleLifecycle>(module);
    }

    [Fact]
    public async Task UnoverriddenPhases_CompleteAsNoOps_WithoutThrowing()
    {
        IModuleLifecycle module = new SdkModuleOverridingOnlyStart();

        var initialiseException = await Record.ExceptionAsync(() => module.InitialiseAsync(CancellationToken.None));
        var stopException = await Record.ExceptionAsync(() => module.StopAsync(CancellationToken.None));
        var disposeException = await Record.ExceptionAsync(() => module.DisposeAsync(CancellationToken.None));

        Assert.Null(initialiseException);
        Assert.Null(stopException);
        Assert.Null(disposeException);
    }

    [Fact]
    public async Task OnlyOverriddenPhase_ActuallyRuns()
    {
        SdkLifecycleLog.Reset();
        IModuleLifecycle module = new SdkModuleOverridingOnlyStart();

        await module.InitialiseAsync(CancellationToken.None);
        await module.StartAsync(CancellationToken.None);
        await module.StopAsync(CancellationToken.None);
        await module.DisposeAsync(CancellationToken.None);

        Assert.Single(SdkLifecycleLog.Entries);
        Assert.Contains("tempest.sdk.only-start:Start", SdkLifecycleLog.Entries);
    }

    [Fact]
    public async Task EveryPhaseOverridden_AllFourRun()
    {
        SdkLifecycleLog.Reset();
        IModuleLifecycle module = new SdkModuleOverridingEveryPhase();

        await module.InitialiseAsync(CancellationToken.None);
        await module.StartAsync(CancellationToken.None);
        await module.StopAsync(CancellationToken.None);
        await module.DisposeAsync(CancellationToken.None);

        Assert.Equal(4, SdkLifecycleLog.Entries.Count);
        Assert.Contains("tempest.sdk.every-phase:Initialise", SdkLifecycleLog.Entries);
        Assert.Contains("tempest.sdk.every-phase:Start", SdkLifecycleLog.Entries);
        Assert.Contains("tempest.sdk.every-phase:Stop", SdkLifecycleLog.Entries);
        Assert.Contains("tempest.sdk.every-phase:Dispose", SdkLifecycleLog.Entries);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenIdIsInvalid()
    {
        Assert.Throws<ArgumentException>(() => new InvalidIdLifecycleModule());
    }

    private sealed class InvalidIdLifecycleModule : ModuleLifecycleBase
    {
        public InvalidIdLifecycleModule()
            : base("", "Invalid", "1.0.0")
        {
        }
    }
}
