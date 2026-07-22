using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

// Test-only fixtures used exclusively to exercise ModuleLifecycleManager.
// None of these represent real application modules.

/// <summary>
/// Records lifecycle method invocations to a shared, resettable log so tests can
/// assert execution order across several modules instantiated independently by
/// <see cref="ModuleLifecycleManager"/> via reflection (no constructor injection
/// is available to hand fixtures a per-test instance).
/// </summary>
internal static class LifecycleTestLog
{
    private static List<string> _entries = new();

    public static void Reset() => _entries = new List<string>();

    public static void Record(string entry) => _entries.Add(entry);

    public static IReadOnlyList<string> Entries => _entries;
}

internal abstract class RecordingLifecycleModuleBase : IModule, IModuleLifecycle
{
    public abstract string Id { get; }

    public string Name => $"Recording Lifecycle Module ({Id})";

    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken)
    {
        LifecycleTestLog.Record($"{Id}:Initialise");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LifecycleTestLog.Record($"{Id}:Start");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        LifecycleTestLog.Record($"{Id}:Stop");
        return Task.CompletedTask;
    }

    public Task DisposeAsync(CancellationToken cancellationToken)
    {
        LifecycleTestLog.Record($"{Id}:Dispose");
        return Task.CompletedTask;
    }
}

internal sealed class RecordingLifecycleModuleAlpha : RecordingLifecycleModuleBase
{
    public override string Id => "lifecycle.alpha";
}

internal sealed class RecordingLifecycleModuleBeta : RecordingLifecycleModuleBase
{
    public override string Id => "lifecycle.beta";
}

internal sealed class RecordingLifecycleModuleGamma : RecordingLifecycleModuleBase
{
    public override string Id => "lifecycle.gamma";
}

internal sealed class ThrowingInitialiseLifecycleModule : IModule, IModuleLifecycle
{
    public string Id => "lifecycle.throwing-initialise";

    public string Name => "Throwing Initialise Lifecycle Module";

    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Simulated initialisation failure.");

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class NoLifecycleModule : IModule
{
    public string Id => "lifecycle.no-lifecycle";

    public string Name => "No Lifecycle Module";

    public string Version => "1.0.0";
}
