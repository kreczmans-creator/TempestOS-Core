using System.Reflection;

namespace Tempest.Core.Plugins;

/// <summary>
/// Loads each eligible plugin's declared assembly file into the process.
/// </summary>
/// <remarks>
/// Host-owned, Phase 3.2 (ADR-0026). Every plugin-scoped failure (ADR-0025,
/// categories 5-6) is isolated: logged, and excluded from the returned list —
/// never thrown. Loading is the harder-to-reverse half of the two-phase split
/// ADR-0026 establishes (mirroring Module Discovery/Module Registration): once
/// an assembly is loaded, it cannot be unloaded without a full process
/// restart (ADR-0015).
/// </remarks>
public interface IPluginAssemblyLoader
{
    /// <summary>
    /// Loads each manifest's declared assembly, in the given order.
    /// </summary>
    /// <param name="manifests">
    /// The eligible plugin manifests to load, typically produced by
    /// <see cref="IPluginManifestDiscoveryService.DiscoverManifests"/>.
    /// </param>
    /// <returns>
    /// The assemblies that loaded successfully, in the same order as
    /// <paramref name="manifests"/>. A plugin whose assembly could not be
    /// found or failed to load is isolated and simply absent from this list —
    /// never represented by a null entry or a thrown exception.
    /// </returns>
    IReadOnlyList<Assembly> LoadPlugins(IReadOnlyList<PluginManifest> manifests);
}
