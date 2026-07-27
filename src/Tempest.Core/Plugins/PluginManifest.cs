using Tempest.Core.Modules;

namespace Tempest.Core.Plugins;

/// <summary>
/// Describes a plugin found by an <see cref="IPluginManifestDiscoveryService"/>,
/// before its assembly is ever loaded.
/// </summary>
/// <remarks>
/// An immutable snapshot, mirroring <see cref="Modules.ModuleDescriptor"/>
/// exactly, but describing something not yet loaded into the process rather
/// than something already reflectable — see <c>Plugin Manifest
/// Architecture.md</c>'s "The Manifest describes. The Runtime decides."
/// <see cref="AssemblyPath"/> is not itself a manifest field: it is the
/// fully-resolved, absolute form of <see cref="AssemblyFileName"/>, computed
/// once at discovery time (relative to the manifest's own folder) exactly as
/// <see cref="Modules.ModuleDescriptor.ModuleType"/> captures something derived
/// at discovery time rather than declared directly in <see cref="IModule"/>.
/// </remarks>
public sealed class PluginManifest
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginManifest"/> class.
    /// </summary>
    /// <param name="id">The plugin's unique identifier.</param>
    /// <param name="name">The plugin's human-readable name.</param>
    /// <param name="version">The plugin's own version string.</param>
    /// <param name="minimumPlatformVersion">The minimum platform version the plugin requires.</param>
    /// <param name="assemblyFileName">The declared, manifest-relative assembly file name.</param>
    /// <param name="assemblyPath">The resolved, absolute path to the plugin's assembly.</param>
    public PluginManifest(
        string id,
        string name,
        string version,
        Version minimumPlatformVersion,
        string assemblyFileName,
        string assemblyPath)
    {
        Id = id;
        Name = name;
        Version = version;
        MinimumPlatformVersion = minimumPlatformVersion;
        AssemblyFileName = assemblyFileName;
        AssemblyPath = assemblyPath;
    }

    /// <summary>
    /// Gets the plugin's unique identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the plugin's human-readable name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the plugin's own version string.
    /// </summary>
    /// <remarks>
    /// A plain string, matching <see cref="IModule.Version"/>'s existing,
    /// unvalidated-format convention exactly — not cross-checked here; a
    /// mismatch against the loaded module's own <see cref="IModule.Version"/>
    /// is a matter for a future consumer, not this value object.
    /// </remarks>
    public string Version { get; }

    /// <summary>
    /// Gets the minimum platform version this plugin declares it requires.
    /// </summary>
    public Version MinimumPlatformVersion { get; }

    /// <summary>
    /// Gets the declared assembly file name, relative to the manifest's own folder.
    /// </summary>
    public string AssemblyFileName { get; }

    /// <summary>
    /// Gets the fully-resolved, absolute path to the plugin's assembly,
    /// computed at discovery time from the manifest's own folder and
    /// <see cref="AssemblyFileName"/>.
    /// </summary>
    public string AssemblyPath { get; }
}
