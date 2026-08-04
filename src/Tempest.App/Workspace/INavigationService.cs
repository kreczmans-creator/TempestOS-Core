namespace Tempest.App.Workspace;

/// <summary>
/// Workspace-scoped navigation — built on top of the existing
/// <see cref="Tempest.Core.Navigation.INavigationProvider"/>. That
/// interface answers "what top-level areas exist"; this one additionally
/// answers "open this specific object," "focus it if already open," and
/// "jump to a related object," none of which
/// <see cref="Tempest.Core.Navigation.INavigationProvider"/> itself is
/// scoped to handle.
/// </summary>
public interface INavigationService
{
    /// <summary>Gets every registered top-level area. Delegates directly to <see cref="Tempest.Core.Navigation.INavigationProvider.Items"/>.</summary>
    IReadOnlyList<Tempest.Core.Navigation.NavigationItem> Areas { get; }

    /// <summary>Switches the Project Explorer's own current top-level area. Delegates to <see cref="Tempest.Core.Navigation.INavigationProvider.Navigate"/>.</summary>
    /// <exception cref="Tempest.Core.Navigation.NavigationItemNotFoundException"><paramref name="areaId"/> is not registered.</exception>
    Task SwitchAreaAsync(string areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens <paramref name="objectId"/> in a new Document Area tab, or
    /// focuses its existing tab if already open — never opens a second tab
    /// for the same object.
    /// </summary>
    /// <exception cref="WorkspaceViewFactoryNotFoundException">No <see cref="IWorkspaceViewFactory"/> is registered for <paramref name="kind"/>.</exception>
    Task<IWorkspaceView> OpenAsync(Guid objectId, string kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens <paramref name="targetObjectId"/> in a <b>new</b> tab, alongside
    /// whatever is already open — the Digital Thread panel's own "jump to"
    /// action (`ADR-0065`), never replacing the source's own tab.
    /// </summary>
    /// <exception cref="WorkspaceViewFactoryNotFoundException">No <see cref="IWorkspaceViewFactory"/> is registered for <paramref name="targetKind"/>.</exception>
    Task<IWorkspaceView> JumpToAsync(Guid targetObjectId, string targetKind, CancellationToken cancellationToken = default);

    /// <summary>Closes the view identified by <paramref name="viewId"/>. A no-op if not open.</summary>
    Task CloseAsync(Guid viewId, CancellationToken cancellationToken = default);
}
