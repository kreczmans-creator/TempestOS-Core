namespace Tempest.App.Workspace;

/// <summary>
/// Tracks the Workspace's own current selection and publishes its own
/// change through the existing <see cref="Tempest.Core.Events.IEventBus"/>
/// — not a plain .NET event — consistent with how
/// <see cref="Tempest.Core.Navigation.NavigationRequestedEvent"/> already
/// publishes navigation changes.
/// </summary>
public interface ISelectionService
{
    /// <summary>Gets the current selection, or <see langword="null"/> if nothing is selected.</summary>
    WorkspaceSelection? Current { get; }

    /// <summary>
    /// Gets every currently selected item, in selection order. Empty if
    /// nothing is selected; mirrors <see cref="Current"/> alone whenever
    /// only <see cref="SelectAsync"/>/<see cref="ClearAsync"/> have ever
    /// been used — grows independently only once
    /// <see cref="ToggleSelectionAsync"/> is used (`WP 9.1A`, `ADR-0085`,
    /// resolving `FCR-0039`).
    /// </summary>
    IReadOnlyList<WorkspaceSelection> SelectedItems { get; }

    /// <summary>
    /// Sets the current selection to exactly this one item — replacing
    /// any existing multi-selection set with a singleton set containing
    /// only this item — and publishes <see cref="WorkspaceSelectionChangedEvent"/>
    /// and <see cref="WorkspaceSelectionSetChangedEvent"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    Task SelectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the current selection and the entire multi-selection set,
    /// and publishes <see cref="WorkspaceSelectionChangedEvent"/> (with a
    /// <see langword="null"/> <see cref="WorkspaceSelectionChangedEvent.Current"/>)
    /// and <see cref="WorkspaceSelectionSetChangedEvent"/> (with an empty
    /// <see cref="WorkspaceSelectionSetChangedEvent.Current"/>). A no-op
    /// if already clear.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles one item's own membership in the current multi-selection
    /// set — adds it if absent, removes it if present. <see cref="Current"/>
    /// becomes the newly toggled-in item on an add, or, on a remove, the
    /// item that was most recently toggled in among what remains (or
    /// <see langword="null"/> if the set becomes empty). Publishes
    /// <see cref="WorkspaceSelectionSetChangedEvent"/>, and — since
    /// <see cref="Current"/> itself also changes — <see cref="WorkspaceSelectionChangedEvent"/>
    /// too, mirroring <see cref="SelectAsync"/>'s own event
    /// (`WP 9.1A`, `ADR-0085`).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    Task ToggleSelectionAsync(Guid objectId, string kind, CancellationToken cancellationToken = default);
}
