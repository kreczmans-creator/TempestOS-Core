namespace Tempest.Core.Plugins;

/// <summary>
/// The queryable outcome a <see cref="PluginRegistryEntry"/> records for one
/// plugin candidate's attempted run.
/// </summary>
/// <remarks>
/// <c>Plugin Platform Architecture.md</c>, Plugin Registry. Originally five
/// values (<c>Loaded</c>/<c>Failed</c>/<c>Disabled</c>/<c>Incompatible</c>/
/// <c>DependencyUnmet</c>), extended additively here with a sixth,
/// <c>TrustDenied</c>, implementing ADR-0111/ADR-0112 category 17: a
/// plugin whose <c>RequestedCapabilities</c> exceeds its assigned trust
/// tier's ceiling, or whose module constructor requires a service type
/// outside the fixed always-allowed baseline and its own granted
/// <c>plugin.services.resolve:*</c> declarations, is recorded with this
/// state rather than the general-purpose <c>Failed</c> (ADR-0112's own
/// category table: "Recorded in the Plugin Registry… as
/// <c>PluginRegistryState.TrustDenied</c>" — stated only for category 17).
/// This enum remains designed to be extended, not restructured, by future
/// work.
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

    /// <summary>
    /// The plugin was denied at Plugin Loading (Phase 3.2) because a
    /// requested capability exceeded its assigned trust tier's ceiling, or a
    /// module constructor required a service type it was not granted
    /// (ADR-0111/ADR-0112, category 17).
    /// </summary>
    TrustDenied,
}
