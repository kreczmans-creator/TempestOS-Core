namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin declares a dependency on another plugin that is not
/// present among the surviving, individually-valid candidate set.
/// </summary>
/// <remarks>
/// ADR-0107, category 12 — isolated to the dependent plugin, logged at
/// <see cref="Logging.LogLevel.Warning"/>. Never Host-fatal. The missing
/// dependency's own absence (if it failed validation itself) was already
/// logged separately, at its own category's severity, when it was excluded.
/// </remarks>
public sealed class MissingPluginDependencyException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="MissingPluginDependencyException"/> class.
    /// </summary>
    /// <param name="pluginId">The dependent plugin's declared identifier.</param>
    /// <param name="missingDependencyId">The missing dependency's declared identifier.</param>
    public MissingPluginDependencyException(string pluginId, string missingDependencyId)
        : base($"Plugin '{pluginId}' depends on '{missingDependencyId}', which is not present among eligible plugins.")
    {
        PluginId = pluginId;
        MissingDependencyId = missingDependencyId;
    }

    /// <summary>
    /// Gets the dependent plugin's declared identifier.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets the missing dependency's declared identifier.
    /// </summary>
    public string MissingDependencyId { get; }
}
