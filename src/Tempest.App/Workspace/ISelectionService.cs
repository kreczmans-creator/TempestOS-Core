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

    /// <summary>Sets the current selection and publishes <see cref="WorkspaceSelectionChangedEvent"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    Task SelectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default);

    /// <summary>Clears the current selection and publishes <see cref="WorkspaceSelectionChangedEvent"/> (with a <see langword="null"/> <see cref="WorkspaceSelectionChangedEvent.Current"/>). A no-op if already clear.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
