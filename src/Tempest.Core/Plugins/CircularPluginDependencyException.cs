namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin participates in a circular dependency — two or more
/// plugins depend on each other, directly or transitively, with no valid
/// topological order.
/// </summary>
/// <remarks>
/// ADR-0107, category 14 — isolated to every plugin participating in the
/// cycle, logged at <see cref="Logging.LogLevel.Warning"/>. Never Host-fatal;
/// a cycle is a mutual defect between two or more optional plugins, not a
/// defect in the Host's own orchestration.
/// </remarks>
public sealed class CircularPluginDependencyException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CircularPluginDependencyException"/> class.
    /// </summary>
    /// <param name="pluginId">The plugin identifier this exception isolates.</param>
    /// <param name="cyclePath">The path of plugin identifiers that forms the cycle.</param>
    public CircularPluginDependencyException(string pluginId, IReadOnlyList<string> cyclePath)
        : base($"Plugin '{pluginId}' participates in a circular dependency: {string.Join(" -> ", cyclePath)}.")
    {
        PluginId = pluginId;
        CyclePath = cyclePath;
    }

    /// <summary>
    /// Gets the plugin identifier this exception isolates.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets the path of plugin identifiers that forms the cycle.
    /// </summary>
    public IReadOnlyList<string> CyclePath { get; }
}
