namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin's declared assembly file exists but fails to load —
/// for example, a corrupt file or a missing native dependency.
/// </summary>
/// <remarks>
/// ADR-0025, category 6 — isolated to the one plugin, logged at
/// <see cref="Logging.LogLevel.Error"/>.
/// </remarks>
public sealed class PluginAssemblyLoadException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginAssemblyLoadException"/> class.
    /// </summary>
    /// <param name="pluginId">The plugin's declared identifier.</param>
    /// <param name="assemblyPath">The assembly path that failed to load.</param>
    /// <param name="innerException">The exception the underlying load attempt threw.</param>
    public PluginAssemblyLoadException(string pluginId, string assemblyPath, Exception innerException)
        : base($"Plugin '{pluginId}' assembly '{assemblyPath}' failed to load.", innerException)
    {
        PluginId = pluginId;
        AssemblyPath = assemblyPath;
    }

    /// <summary>
    /// Gets the plugin's declared identifier.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets the assembly path that failed to load.
    /// </summary>
    public string AssemblyPath { get; }
}
