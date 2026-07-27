namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin manifest's declared assembly file does not exist on disk.
/// </summary>
/// <remarks>
/// ADR-0025, category 5 — isolated to the one plugin, logged at
/// <see cref="Logging.LogLevel.Error"/>: more likely a genuine packaging or
/// deployment mistake than a malformed manifest.
/// </remarks>
public sealed class PluginAssemblyNotFoundException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginAssemblyNotFoundException"/> class.
    /// </summary>
    /// <param name="pluginId">The plugin's declared identifier.</param>
    /// <param name="assemblyPath">The resolved, absolute path that does not exist.</param>
    public PluginAssemblyNotFoundException(string pluginId, string assemblyPath)
        : base($"Plugin '{pluginId}' declares assembly '{assemblyPath}', which does not exist.")
    {
        PluginId = pluginId;
        AssemblyPath = assemblyPath;
    }

    /// <summary>
    /// Gets the plugin's declared identifier.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets the resolved, absolute assembly path that does not exist.
    /// </summary>
    public string AssemblyPath { get; }
}
