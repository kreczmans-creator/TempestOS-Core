namespace Tempest.Core.Plugins;

/// <summary>
/// Describes one plugin's declared dependency on another plugin.
/// </summary>
/// <remarks>
/// ADR-0107. An immutable value, mirroring <see cref="PluginManifest"/>'s own
/// constructor-sets-everything shape. The "minimum required, maximum optional"
/// asymmetry deliberately mirrors <see cref="PluginManifest.MinimumPlatformVersion"/>'s
/// own established convention, applied here to a different axis — one plugin's
/// compatibility with another, not with the platform.
/// </remarks>
public sealed class PluginDependency
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginDependency"/> class.
    /// </summary>
    /// <param name="id">The depended-upon plugin's unique identifier.</param>
    /// <param name="minimumVersion">The minimum version of the depended-upon plugin required.</param>
    /// <param name="maximumVersion">
    /// The maximum version of the depended-upon plugin permitted, or
    /// <see langword="null"/> if unbounded above.
    /// </param>
    public PluginDependency(string id, Version minimumVersion, Version? maximumVersion)
    {
        Id = id;
        MinimumVersion = minimumVersion;
        MaximumVersion = maximumVersion;
    }

    /// <summary>
    /// Gets the depended-upon plugin's unique identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the minimum version of the depended-upon plugin required.
    /// </summary>
    public Version MinimumVersion { get; }

    /// <summary>
    /// Gets the maximum version of the depended-upon plugin permitted, or
    /// <see langword="null"/> if unbounded above.
    /// </summary>
    public Version? MaximumVersion { get; }
}
