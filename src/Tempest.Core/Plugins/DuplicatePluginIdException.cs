namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when Plugin Discovery finds two or more manifests sharing the same
/// declared <see cref="PluginManifest.Id"/>.
/// </summary>
/// <remarks>
/// ADR-0025, category 3 — isolated to the later candidate; the first manifest
/// encountered, in Plugin Discovery's own deterministic scan order (ordinal by
/// folder name), wins. Logged at <see cref="Logging.LogLevel.Warning"/>.
/// </remarks>
public sealed class DuplicatePluginIdException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicatePluginIdException"/> class.
    /// </summary>
    /// <param name="pluginId">The plugin ID that was found more than once.</param>
    public DuplicatePluginIdException(string pluginId)
        : base($"Duplicate plugin ID detected during discovery: '{pluginId}'.")
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Gets the plugin ID that was found more than once.
    /// </summary>
    public string PluginId { get; }
}
