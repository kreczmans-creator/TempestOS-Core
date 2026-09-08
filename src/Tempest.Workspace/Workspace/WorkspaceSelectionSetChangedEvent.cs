using Tempest.Core.Events;

namespace Tempest.App.Workspace;

/// <summary>
/// Published through the existing <see cref="IEventBus"/> whenever
/// <see cref="ISelectionService"/>'s own current multi-selection set
/// changes — fires alongside the existing, unchanged
/// <see cref="WorkspaceSelectionChangedEvent"/> (never in its place), so
/// every subscriber written against the frozen `WP8.0B` single-selection
/// contract keeps working completely unaffected (`WP 9.1A`, `ADR-0085`).
/// </summary>
public sealed class WorkspaceSelectionSetChangedEvent : IEvent
{
    /// <summary>Initialises a new instance of the <see cref="WorkspaceSelectionSetChangedEvent"/> class.</summary>
    /// <param name="previous">The selection set before this change. Never <see langword="null"/> — empty if nothing was selected.</param>
    /// <param name="current">The selection set after this change. Never <see langword="null"/> — empty if the set is now clear.</param>
    public WorkspaceSelectionSetChangedEvent(IReadOnlyList<WorkspaceSelection> previous, IReadOnlyList<WorkspaceSelection> current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the selection set before this change. Never <see langword="null"/> — empty if nothing was selected.</summary>
    public IReadOnlyList<WorkspaceSelection> Previous { get; }

    /// <summary>Gets the selection set after this change. Never <see langword="null"/> — empty if the set is now clear.</summary>
    public IReadOnlyList<WorkspaceSelection> Current { get; }
}
