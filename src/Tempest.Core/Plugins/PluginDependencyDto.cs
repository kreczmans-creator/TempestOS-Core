namespace Tempest.Core.Plugins;

/// <summary>
/// The raw, on-disk JSON shape of a single declared plugin dependency, before validation.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="PluginDependency"/>, mirroring
/// <see cref="PluginManifestDto"/>'s own relationship to <see cref="PluginManifest"/>
/// exactly: this type's fields are all nullable, because deserialized JSON has
/// not yet been validated. <see cref="PluginDependency"/> is only ever
/// constructed once every field here has been confirmed present and
/// well-formed — see <see cref="PluginManifestDiscoveryService"/>.
/// </remarks>
internal sealed class PluginDependencyDto
{
    /// <summary>Gets or sets the raw, unvalidated dependency plugin identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the raw, unvalidated minimum version string.</summary>
    public string? MinimumVersion { get; set; }

    /// <summary>Gets or sets the raw, unvalidated maximum version string.</summary>
    public string? MaximumVersion { get; set; }
}
