namespace Tempest.Core.Plugins;

/// <summary>
/// The queryable outcome a <see cref="PluginRegistryEntry"/> records for one
/// plugin candidate's attempted run.
/// </summary>
/// <remarks>
/// <c>Plugin Platform Architecture.md</c>, Plugin Registry. Exactly five
/// values, the floor this Work Package's own brief named
/// (<c>Loaded</c>/<c>Failed</c>/<c>Disabled</c>/<c>Incompatible</c>/
/// <c>DependencyUnmet</c>). A sixth value, <c>TrustDenied</c>, is reserved
/// for a future trust-enforcement Work Package (`ADR-0111`/`ADR-0112`) and is
/// deliberately <b>not</b> added here — this Work Package implements no trust
/// logic, so nothing in it could ever produce such a value, and adding an
/// unreachable, untested enum value would itself be exactly the kind of
/// placeholder this Work Package's own brief forbids. This enum is designed
/// to be extended, not restructured, by whatever the sibling Trust &amp;
/// Isolation Architecture eventually decides.
/// </remarks>
public enum PluginRegistryState
{
    /// <summary>The plugin's assembly was successfully loaded (Phase 3.2).</summary>
    Loaded,

    /// <summary>The plugin failed discovery or loading for a reason not covered by the other states.</summary>
    Failed,

    /// <summary>The plugin declares a <c>MinimumPlatformVersion</c> incompatible with the running platform.</summary>
    Incompatible,

    /// <summary>The plugin was excluded because a declared dependency was missing, version-incompatible, or part of a cycle (ADR-0107).</summary>
    DependencyUnmet,

    /// <summary>The plugin was skipped via <c>Runtime:Plugins:Disabled</c> configuration.</summary>
    Disabled,
}
