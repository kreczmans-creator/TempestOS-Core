using System.Collections.Concurrent;
using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

// Test-only fixtures used exclusively to exercise ModuleBase/ModuleLifecycleBase
// (the Module SDK, WP 4.1). None of these represent real application modules.

/// <summary>
/// A minimal module with no lifecycle behaviour, built directly on
/// <see cref="ModuleBase"/>.
/// </summary>
internal sealed class MinimalSdkModule : ModuleBase
{
    public MinimalSdkModule()
        : base("tempest.sdk.minimal", "Minimal SDK Module", "1.0.0")
    {
    }

    public MinimalSdkModule(string id, string name, string version)
        : base(id, name, version)
    {
    }
}

/// <summary>
/// Records which lifecycle phases have actually run, per module instance,
/// so tests can assert that only overridden phases did anything.
/// </summary>
internal static class SdkLifecycleLog
{
    private static ConcurrentBag<string> _entries = new();

    public static void Reset() => _entries = new ConcurrentBag<string>();

    public static void Record(string entry) => _entries.Add(entry);

    public static IReadOnlyCollection<string> Entries => _entries;
}

/// <summary>
/// A module built on <see cref="ModuleLifecycleBase"/> that overrides only
/// <c>StartAsync</c>, to prove the other three phases remain harmless
/// no-ops without needing to be written out explicitly.
/// </summary>
internal sealed class SdkModuleOverridingOnlyStart : ModuleLifecycleBase
{
    public SdkModuleOverridingOnlyStart()
        : base("tempest.sdk.only-start", "SDK Module Overriding Only Start", "1.0.0")
    {
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        SdkLifecycleLog.Record($"{Id}:Start");
        return Task.CompletedTask;
    }
}

/// <summary>
/// A module built on <see cref="ModuleLifecycleBase"/> that overrides every
/// phase, proving full overriding still works exactly like a hand-written
/// <see cref="IModuleLifecycle"/> implementation.
/// </summary>
internal sealed class SdkModuleOverridingEveryPhase : ModuleLifecycleBase
{
    public SdkModuleOverridingEveryPhase()
        : base("tempest.sdk.every-phase", "SDK Module Overriding Every Phase", "1.0.0")
    {
    }

    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        SdkLifecycleLog.Record($"{Id}:Initialise");
        return Task.CompletedTask;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        SdkLifecycleLog.Record($"{Id}:Start");
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        SdkLifecycleLog.Record($"{Id}:Stop");
        return Task.CompletedTask;
    }

    public override Task DisposeAsync(CancellationToken cancellationToken)
    {
        SdkLifecycleLog.Record($"{Id}:Dispose");
        return Task.CompletedTask;
    }
}
