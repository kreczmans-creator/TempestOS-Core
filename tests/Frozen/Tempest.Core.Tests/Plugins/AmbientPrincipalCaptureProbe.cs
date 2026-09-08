using System.Collections.Concurrent;

namespace Tempest.Core.Tests.Plugins;

// WP 13.10B (TD-51): an observable side effect a dynamically-built,
// hosted-service-only plugin type's own StartAsync body can call into,
// proving which ambient component principal (if any) was genuinely pushed
// onto ICurrentComponentAccessor by TempestHost's own
// hostedServiceComponentScopeProvider closure at the exact moment StartAsync
// actually ran - not merely that trust enforcement accepted the plugin.
// Mirrors ConstructorExecutionProbe.cs's own established shape exactly (a
// public, ConcurrentDictionary-backed static class, keyed by a per-test GUID
// probeId so parallel xUnit execution across test classes can never
// collide).
//
// Must be PUBLIC (not internal) - IL emitted into a separately-loaded
// dynamic plugin assembly calls RecordObservedIdentity by member token, and
// every existing DynamicPluginAssemblyBuilder helper that calls into
// already-compiled code only ever targets public members.
public static class AmbientPrincipalCaptureProbe
{
    private static readonly ConcurrentDictionary<string, string?> ObservedIdentities = new();

    public static void RecordObservedIdentity(string probeId, string? observedIdentityId) =>
        ObservedIdentities[probeId] = observedIdentityId;

    public static string? GetObservedIdentity(string probeId) =>
        ObservedIdentities.TryGetValue(probeId, out var value) ? value : null;
}
