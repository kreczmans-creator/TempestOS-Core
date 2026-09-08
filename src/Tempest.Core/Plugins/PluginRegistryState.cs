namespace Tempest.Core.Plugins;

/// <summary>
/// The queryable outcome a <see cref="PluginRegistryEntry"/> records for one
/// plugin candidate's attempted run.
/// </summary>
/// <remarks>
/// <c>Plugin Platform Architecture.md</c>, Plugin Registry.
/// <b>Frozen by ADR-0146 (<c>WP 17.2A</c>).</b> This enum used to carry a
/// sixth value, <c>Loaded</c> — a candidate whose assembly was actually
/// loaded into the process (Phase 3.2) — and a seventh, <c>TrustDenied</c>
/// (ADR-0111/ADR-0112 category 17), for a candidate whose requested
/// capabilities exceeded its assigned trust tier's ceiling. Loading, trust
/// tiers and capability enforcement are all frozen at
/// <c>src/Frozen/Tempest.Core.Plugins</c>; nothing today ever produces
/// either value. <see cref="Discovered"/> replaces <c>Loaded</c> as the
/// terminal state for a candidate whose manifest fully validated — manifest
/// discovery, which stays live, goes no further than recording that it
/// found one.
/// </remarks>
public enum PluginRegistryState
{
    /// <summary>The plugin's manifest was discovered and fully validated (Phase 3.1). It is not loaded, signed, verified, scoped or enforced.</summary>
    Discovered,

    /// <summary>The plugin failed discovery for a reason not covered by the other states.</summary>
    Failed,

    /// <summary>The plugin declares a <c>MinimumPlatformVersion</c> incompatible with the running platform.</summary>
    Incompatible,

    /// <summary>The plugin was excluded because a declared dependency was missing, version-incompatible, or part of a cycle (ADR-0107).</summary>
    DependencyUnmet,

    /// <summary>The plugin was skipped via <c>Runtime:Plugins:Disabled</c> configuration.</summary>
    Disabled,
}
