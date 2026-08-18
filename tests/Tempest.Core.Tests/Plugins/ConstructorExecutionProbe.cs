using System.Collections.Concurrent;

namespace Tempest.Core.Tests.Plugins;

// WP 13.9.6: an observable side effect a dynamically-built plugin module's
// constructor can call into, proving whether Activator.CreateInstance
// genuinely ran for that exact type during Module Discovery.
//
// Must be PUBLIC (not internal) - IL emitted into a separately-loaded dynamic
// plugin assembly calls RecordInvocation by member token; every existing
// DynamicPluginAssemblyBuilder helper that calls into already-compiled code
// only ever targets public members (ADR/convention already established).
// Keyed by a per-test GUID probeId, not a shared counter, so parallel xunit
// execution across this and other test classes can never collide.
public static class ConstructorExecutionProbe
{
    private static readonly ConcurrentDictionary<string, int> Counts = new();

    public static void RecordInvocation(string probeId) =>
        Counts.AddOrUpdate(probeId, 1, (_, c) => c + 1);

    public static int GetInvocationCount(string probeId) =>
        Counts.GetValueOrDefault(probeId, 0);
}
