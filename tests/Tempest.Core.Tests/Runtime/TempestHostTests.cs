using Tempest.Core.Configuration;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

public class TempestHostTests
{
    // ----------------------------------------------------------------
    // Construction / Composition Root
    // ----------------------------------------------------------------

    [Fact]
    public void Build_ProducesHost_ThatHasNotStartedAnything()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        Assert.Equal(HostState.Created, host.State);
    }

    // ----------------------------------------------------------------
    // Startup
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_HappyPath_ReachesRunningThenStopsGracefully()
    {
        var host = new TempestHostBuilder(
            [typeof(HealthyHostTestModuleAlpha), typeof(HealthyHostTestModuleBeta)])
            .Build();

        var runTask = host.RunAsync();

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_LogsEveryLifecyclePhase()
    {
        var host = new TempestHostBuilder([typeof(HealthyHostTestModuleAlpha)]).Build();
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var runTask = host.RunAsync();
            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();

        Assert.Contains("Configuration Built", output);
        Assert.Contains("Logging Built", output);
        Assert.Contains("Platform version resolved", output);
        Assert.Contains("Module Discovery", output);
        Assert.Contains("Module Registration", output);
        Assert.Contains("Platform Services Registered", output);
        Assert.Contains("Dependency Injection Built", output);
        Assert.Contains("Module Initialisation", output);
        Assert.Contains("Host -> Running", output);
        Assert.Contains("Host -> Stopping", output);
        Assert.Contains("Host -> Stopped", output);
    }

    [Fact]
    public async Task RunAsync_WithNoModules_StillReachesRunning()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        var runTask = host.RunAsync();

        // No module signals readiness, but Running is reached once Module
        // Initialisation (an empty batch) completes - poll briefly rather than
        // relying on a fixed sleep.
        while (host.State != HostState.Running)
            await Task.Delay(5);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    // ----------------------------------------------------------------
    // Composition root: uses the existing DI container, does not redesign it
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_ConfigurationFailure_IsHostFatal_TransitionsToFaulted()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>("Duplicate", "one"),
                new KeyValuePair<string, string>("Duplicate", "two"),
            ]))
            .Build();

        await Assert.ThrowsAsync<DuplicateConfigurationKeyException>(() => host.RunAsync());

        Assert.Equal(HostState.Faulted, host.State);
    }

    // ----------------------------------------------------------------
    // Module failures remain isolated (ADR-0013) - do not weaken this distinction
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_IndividualModuleFailure_DoesNotFaultTheHost()
    {
        var host = new TempestHostBuilder(
            [typeof(ThrowingHostTestModule), typeof(HealthyHostTestModuleAlpha)])
            .Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    // ----------------------------------------------------------------
    // Cancellation - observed only between atomic operations
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CallerTokenAlreadyCancelled_ThrowsOperationCanceledException_HostReachesStopped()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => host.RunAsync(cts.Token));

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_CallerTokenCancelledMidStartup_NeverReachesFaulted()
    {
        BlockingModuleGate.Reset();

        var host = new TempestHostBuilder([typeof(BlockingHostTestModule)]).Build();
        using var cts = new CancellationTokenSource();

        var runTask = host.RunAsync(cts.Token);

        await BlockingModuleGate.WaitUntilEnteredAsync();
        cts.Cancel();
        BlockingModuleGate.Release();

        await Assert.ThrowsAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task StopAsync_CalledDuringStarting_IsAnEarlyShutdownRequest_NotAFault()
    {
        BlockingModuleGate.Reset();

        var host = new TempestHostBuilder([typeof(BlockingHostTestModule)]).Build();

        var runTask = host.RunAsync();

        await BlockingModuleGate.WaitUntilEnteredAsync();
        var stopTask = host.StopAsync();
        BlockingModuleGate.Release();

        await stopTask;
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task StopAsync_CalledWhileRunning_CompletesGracefully_WithoutThrowing()
    {
        var host = new TempestHostBuilder([typeof(HealthyHostTestModuleAlpha)]).Build();

        var runTask = host.RunAsync();

        while (host.State != HostState.Running)
            await Task.Delay(5);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.StopAsync();
            await runTask;
        });

        Assert.Null(exception);
        Assert.Equal(HostState.Stopped, host.State);
    }

    // ----------------------------------------------------------------
    // State machine: illegal transitions throw descriptive exceptions
    // ----------------------------------------------------------------

    [Fact]
    public async Task StopAsync_BeforeRunAsyncWasEverCalled_ThrowsInvalidHostStateTransitionException()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        var exception = await Assert.ThrowsAsync<InvalidHostStateTransitionException>(() => host.StopAsync());

        Assert.Equal(HostState.Created, exception.CurrentState);
        Assert.Equal("Stop", exception.AttemptedOperation);
    }

    [Fact]
    public async Task RunAsync_CalledTwice_ThrowsInvalidHostStateTransitionException()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        var firstRun = host.RunAsync();
        await host.StopAsync();
        await firstRun;

        await Assert.ThrowsAsync<InvalidHostStateTransitionException>(() => host.RunAsync());
    }

    [Fact]
    public async Task RunAsync_CalledAgainAfterFaulting_ThrowsInvalidHostStateTransitionException()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>("Duplicate", "one"),
                new KeyValuePair<string, string>("Duplicate", "two"),
            ]))
            .Build();

        await Assert.ThrowsAsync<DuplicateConfigurationKeyException>(() => host.RunAsync());

        await Assert.ThrowsAsync<InvalidHostStateTransitionException>(() => host.RunAsync());
    }

    [Fact]
    public async Task RunAsync_AfterHostIsDisposed_ThrowsInvalidHostStateTransitionException()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();
        await host.DisposeAsync();

        await Assert.ThrowsAsync<InvalidHostStateTransitionException>(() => host.RunAsync());
    }

    [Fact]
    public async Task StopAsync_AfterHostIsDisposed_ThrowsInvalidHostStateTransitionException()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();
        await host.DisposeAsync();

        var exception = await Assert.ThrowsAsync<InvalidHostStateTransitionException>(() => host.StopAsync());

        Assert.Equal(HostState.Disposed, exception.CurrentState);
    }

    // ----------------------------------------------------------------
    // Single-use host behaviour (ADR-0015: restart is prohibited)
    // ----------------------------------------------------------------

    [Fact]
    public async Task Host_CannotBeRestarted_AfterReachingStopped()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        var firstRun = host.RunAsync();
        await host.StopAsync();
        await firstRun;

        Assert.Equal(HostState.Stopped, host.State);
        await Assert.ThrowsAsync<InvalidHostStateTransitionException>(() => host.RunAsync());
    }

    // ----------------------------------------------------------------
    // Disposal ordering / guaranteed cleanup / host lifetime
    // ----------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_WithoutEverCallingRunAsync_IsPermitted()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        var exception = await Record.ExceptionAsync(() => host.DisposeAsync().AsTask());

        Assert.Null(exception);
        Assert.Equal(HostState.Disposed, host.State);
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_IsIdempotent()
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();

        await host.DisposeAsync();
        var exception = await Record.ExceptionAsync(() => host.DisposeAsync().AsTask());

        Assert.Null(exception);
        Assert.Equal(HostState.Disposed, host.State);
    }

    [Fact]
    public async Task DisposeAsync_AfterGracefulStop_DoesNotThrow_AndRemainsDisposed()
    {
        var host = new TempestHostBuilder([typeof(HealthyHostTestModuleAlpha)]).Build();

        var runTask = host.RunAsync();
        await host.StopAsync();
        await runTask;

        var exception = await Record.ExceptionAsync(() => host.DisposeAsync().AsTask());

        Assert.Null(exception);
        Assert.Equal(HostState.Disposed, host.State);
    }

    [Fact]
    public async Task DisposeAsync_CalledDuringStarting_WaitsForShutdownThenDisposesConstructedModules()
    {
        DisposalCounter.Reset();
        BlockingModuleGate.Reset();

        var host = new TempestHostBuilder([typeof(DisposalTrackingHostTestModule)]).Build();
        var runTask = host.RunAsync();

        await BlockingModuleGate.WaitUntilEnteredAsync();

        var disposeTask = host.DisposeAsync().AsTask();
        BlockingModuleGate.Release();

        await disposeTask;
        await runTask;

        Assert.Equal(HostState.Disposed, host.State);
        Assert.Equal(1, DisposalCounter.Count);
    }

    [Fact]
    public async Task DisposeAsync_CalledWhileStillRunning_WaitsForControlledShutdownThenDisposes()
    {
        var host = new TempestHostBuilder([typeof(HealthyHostTestModuleAlpha)]).Build();

        var runTask = host.RunAsync();

        while (host.State != HostState.Running)
            await Task.Delay(5);

        await host.DisposeAsync();

        Assert.Equal(HostState.Disposed, host.State);
        await runTask;
    }
}
