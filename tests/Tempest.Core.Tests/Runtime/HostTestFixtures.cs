using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Runtime;

// Test-only IModule fixtures used exclusively to exercise TempestHost/
// TempestHostBuilder. None of these represent real application modules, and
// none is shared with the Modules test fixtures — TempestHostTests runs
// sequentially within its own class (xUnit's default per-class collection),
// so the static coordination below is safe without cross-test interference,
// mirroring the same pattern LifecycleTestLog already establishes for
// ModuleLifecycleManagerTests.

/// <summary>
/// Coordinates a test with a module whose <see cref="IModuleLifecycle.InitialiseAsync"/>
/// blocks until released, so a test can deterministically observe the host
/// mid-Module-Initialisation before signalling cancellation or a shutdown
/// request.
/// </summary>
internal static class BlockingModuleGate
{
    private static TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static void Reset()
    {
        _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public static Task WaitUntilEnteredAsync() => _entered.Task;

    public static void Release() => _release.TrySetResult();

    public static async Task EnterAndWaitForReleaseAsync()
    {
        _entered.TrySetResult();
        await _release.Task.ConfigureAwait(false);
    }
}

internal sealed class BlockingHostTestModule : IModule, IModuleLifecycle
{
    public string Id => "host-test.blocking";

    public string Name => "Blocking Host Test Module";

    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken) =>
        BlockingModuleGate.EnterAndWaitForReleaseAsync();

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class HealthyHostTestModuleAlpha : IModule, IModuleLifecycle
{
    public string Id => "host-test.alpha";

    public string Name => "Healthy Host Test Module Alpha";

    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class HealthyHostTestModuleBeta : IModule, IModuleLifecycle
{
    public string Id => "host-test.beta";

    public string Name => "Healthy Host Test Module Beta";

    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// A module whose <see cref="IModuleLifecycle.InitialiseAsync"/> always fails,
/// used to verify that an individual module failure is isolated (ADR-0013)
/// and does not prevent the host from reaching <see cref="Tempest.Core.Runtime.HostState.Running"/>.
/// </summary>
internal sealed class ThrowingHostTestModule : IModule, IModuleLifecycle
{
    public string Id => "host-test.throwing";

    public string Name => "Throwing Host Test Module";

    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Simulated module initialisation failure.");

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Records how many times <see cref="IModuleLifecycle.DisposeAsync"/> has been
/// invoked across every instance, used to verify disposal is attempted for
/// modules that were never started.
/// </summary>
internal static class DisposalCounter
{
    private static int _count;

    public static void Reset() => Interlocked.Exchange(ref _count, 0);

    public static void RecordDispose() => Interlocked.Increment(ref _count);

    public static int Count => _count;
}

internal sealed class DisposalTrackingHostTestModule : IModule, IModuleLifecycle
{
    public string Id => "host-test.disposal-tracking";

    public string Name => "Disposal Tracking Host Test Module";

    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken) =>
        BlockingModuleGate.EnterAndWaitForReleaseAsync();

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken)
    {
        DisposalCounter.RecordDispose();
        return Task.CompletedTask;
    }
}
