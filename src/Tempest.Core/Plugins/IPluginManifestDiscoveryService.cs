namespace Tempest.Core.Plugins;

/// <summary>
/// Discovers <see cref="PluginManifest"/>s available to the platform, before
/// any plugin assembly is loaded.
/// </summary>
/// <remarks>
/// Host-owned, Phase 3.1 (ADR-0026) — mirrors
/// <see cref="Modules.IFrameworkDiscoveryService"/> directly: the same kind of
/// service, one phase earlier. Every plugin-scoped failure (ADR-0025) is
/// isolated internally — logged, and excluded from the returned list — never
/// thrown. This method throws only for a genuine defect in Plugin Discovery's
/// own orchestration, not attributable to any specific plugin (ADR-0025,
/// category 11).
/// </remarks>
public interface IPluginManifestDiscoveryService
{
    /// <summary>
    /// Discovers every valid, version-compatible plugin manifest, in
    /// deterministic order (ordinal by candidate folder name).
    /// </summary>
    /// <returns>
    /// The eligible plugin manifests, in deterministic discovery order.
    /// Possibly empty — an absent plugins directory, or one with no plugins,
    /// is a valid, non-error outcome.
    /// </returns>
    IReadOnlyList<PluginManifest> DiscoverManifests();
}
