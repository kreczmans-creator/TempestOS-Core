namespace Tempest.Core.Plugins;

/// <summary>
/// A point-in-time, immutable record of one plugin candidate's outcome, as
/// tracked by the Plugin Registry.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Modules.ModuleLifecycleStatus"/>'s own snapshot shape —
/// an immutable value describing one candidate's final, terminal outcome for
/// this process run (ADR-0108: every state is reached at most once per run
/// and never re-entered), rather than something that changes over time in
/// place.
/// </remarks>
public sealed class PluginRegistryEntry
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginRegistryEntry"/> class.
    /// </summary>
    /// <param name="id">The manifest's own <c>Id</c> if known, else the candidate folder name.</param>
    /// <param name="name">The plugin's declared name, if known.</param>
    /// <param name="version">The plugin's declared version, if known.</param>
    /// <param name="state">The candidate's outcome.</param>
    /// <param name="detail">A human-readable reason, if any.</param>
    public PluginRegistryEntry(string id, string? name, string? version, PluginRegistryState state, string? detail)
    {
        Id = id;
        Name = name;
        Version = version;
        State = state;
        Detail = detail;
    }

    /// <summary>
    /// Gets the manifest's own <c>Id</c> if known, else the candidate folder name.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the plugin's declared name, if known.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the plugin's declared version, if known.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets the candidate's outcome.
    /// </summary>
    public PluginRegistryState State { get; }

    /// <summary>
    /// Gets a human-readable reason for this outcome, if any, mirroring
    /// ADR-0025's own logged detail.
    /// </summary>
    public string? Detail { get; }
}
