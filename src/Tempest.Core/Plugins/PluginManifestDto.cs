namespace Tempest.Core.Plugins;

/// <summary>
/// The raw, on-disk JSON shape of a plugin manifest, before validation.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="PluginManifest"/>: this type's fields
/// are all nullable, because deserialized JSON has not yet been validated.
/// <see cref="PluginManifest"/> is only ever constructed once every field here
/// has been confirmed present and well-formed — see
/// <see cref="PluginManifestDiscoveryService"/>.
/// </remarks>
internal sealed class PluginManifestDto
{
    /// <summary>Gets or sets the raw, unvalidated plugin identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the raw, unvalidated plugin name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the raw, unvalidated plugin version string.</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets the raw, unvalidated minimum platform version string.</summary>
    public string? MinimumPlatformVersion { get; set; }

    /// <summary>Gets or sets the raw, unvalidated, manifest-relative assembly file name.</summary>
    public string? AssemblyFileName { get; set; }

    /// <summary>Gets or sets the raw, unvalidated set of inter-plugin dependency declarations.</summary>
    public IReadOnlyList<PluginDependencyDto>? Dependencies { get; set; }

    /// <summary>Gets or sets the raw, unvalidated set of requested capability identifiers.</summary>
    public IReadOnlyList<string>? RequestedCapabilities { get; set; }

    /// <summary>Gets or sets the raw, unvalidated publisher free-text value.</summary>
    public string? Publisher { get; set; }

    /// <summary>Gets or sets the raw, unvalidated signature value.</summary>
    public string? Signature { get; set; }
}
