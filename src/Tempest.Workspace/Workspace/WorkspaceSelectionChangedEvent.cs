using Tempest.Core.Events;

namespace Tempest.App.Workspace;

/// <summary>
/// Published through the existing <see cref="IEventBus"/> whenever
/// <see cref="ISelectionService"/>'s own current selection changes —
/// mirrors <see cref="Tempest.Core.Navigation.NavigationRequestedEvent"/>'s
/// own established precedent (`WP 5.0A`) rather than exposing a bespoke
/// .NET event on <see cref="ISelectionService"/> itself.
/// </summary>
public sealed class WorkspaceSelectionChangedEvent : IEvent
{
    /// <summary>Initialises a new instance of the <see cref="WorkspaceSelectionChangedEvent"/> class.</summary>
    /// <param name="previous">The selection before this change, or <see langword="null"/> if there was none.</param>
    /// <param name="current">The selection after this change, or <see langword="null"/> if it was cleared.</param>
    public WorkspaceSelectionChangedEvent(WorkspaceSelection? previous, WorkspaceSelection? current)
    {
        Previous = previous;
        Current = current;
    }

    /// <summary>Gets the selection before this change, or <see langword="null"/> if there was none.</summary>
    public WorkspaceSelection? Previous { get; }

    /// <summary>Gets the selection after this change, or <see langword="null"/> if it was cleared.</summary>
    public WorkspaceSelection? Current { get; }
}
