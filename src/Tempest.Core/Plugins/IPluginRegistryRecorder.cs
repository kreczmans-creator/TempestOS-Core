namespace Tempest.Core.Plugins;

/// <summary>
/// The write side of the Plugin Registry, used only by Plugins-owned
/// discovery and loading services to record a candidate's outcome.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="IPluginRegistry"/> (the read side) so that
/// nothing outside <c>Tempest.Core.Plugins</c> is ever handed a reference
/// capable of mutating the registry — mirroring ADR-0017's own "structurally
/// unreachable, not merely conventionally respected" discipline, applied
/// here to a write capability rather than to the whole collaborator.
/// </remarks>
public interface IPluginRegistryRecorder
{
    /// <summary>
    /// Records one plugin candidate's outcome.
    /// </summary>
    /// <param name="entry">The outcome to record.</param>
    void Record(PluginRegistryEntry entry);
}
