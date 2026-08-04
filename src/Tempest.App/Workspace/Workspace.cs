namespace Tempest.App.Workspace;

/// <summary>
/// The concrete <see cref="IWorkspace"/> implementation — a plain aggregate
/// root composed once by <see cref="WorkspaceManager.StartAsync"/>, holding
/// no lifecycle verbs of its own (`WP8.0B Workspace Contracts.md` §1).
/// </summary>
internal sealed class Workspace : IWorkspace
{
    private readonly NavigationService _navigationService;

    /// <summary>Initialises a new instance of the <see cref="Workspace"/> class.</summary>
    public Workspace(
        IWorkspaceState state,
        NavigationService navigationService,
        ISelectionService selection,
        ProjectExplorer projectExplorer,
        IPropertyInspector propertyInspector,
        EngineeringCockpit cockpit)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(projectExplorer);
        ArgumentNullException.ThrowIfNull(propertyInspector);
        ArgumentNullException.ThrowIfNull(cockpit);

        State = state;
        _navigationService = navigationService;
        Selection = selection;
        ProjectExplorer = projectExplorer;
        ProjectExplorerConcrete = projectExplorer;
        PropertyInspector = propertyInspector;
        Cockpit = cockpit;
    }

    /// <inheritdoc />
    /// <remarks>Delegates directly to <see cref="IWorkspaceState.Layout"/> — one source of truth, not a second, independently-mutable copy.</remarks>
    public IWorkspaceLayout Layout => State.Layout;

    /// <inheritdoc />
    public IWorkspaceState State { get; }

    /// <inheritdoc />
    public INavigationService Navigation => _navigationService;

    /// <inheritdoc />
    public ISelectionService Selection { get; }

    /// <inheritdoc />
    public IProjectExplorer ProjectExplorer { get; }

    /// <summary>
    /// Gets the concrete <see cref="Workspace.ProjectExplorer"/> — internal,
    /// same-assembly-only access to the members
    /// (<see cref="Workspace.ProjectExplorer"/>'s own <c>CurrentPath</c>,
    /// <c>EnterAsync</c>, <c>ExitAsync</c>, <c>FilterAsync</c>) that are not
    /// part of the twelve `WP8.0B Workspace Contracts.md` interfaces,
    /// mirroring <see cref="WorkspaceManager.StatusBar"/>'s own identical
    /// precedent.
    /// </summary>
    internal ProjectExplorer ProjectExplorerConcrete { get; }

    /// <summary>
    /// Gets the concrete <see cref="NavigationService"/> — internal,
    /// same-assembly-only access to <c>History</c>, <c>RecentItems</c>,
    /// <c>GoBackAsync</c>, <c>GoForwardAsync</c>, none of which are part of
    /// the twelve `WP8.0B Workspace Contracts.md` interfaces, mirroring
    /// <see cref="WorkspaceManager.StatusBar"/>'s own identical precedent.
    /// </summary>
    internal NavigationService NavigationServiceConcrete => _navigationService;

    /// <summary>
    /// Gets the Engineering Cockpit — the Workspace's own default landing
    /// screen (`ADR-0069`). Not one of the twelve `WP8.0B Workspace
    /// Contracts.md` interfaces, mirroring <see cref="ProjectExplorerConcrete"/>'s
    /// own identical precedent.
    /// </summary>
    internal EngineeringCockpit Cockpit { get; }

    /// <inheritdoc />
    public IPropertyInspector PropertyInspector { get; }

    /// <inheritdoc />
    public IReadOnlyList<IWorkspaceView> OpenViews => _navigationService.OpenViews;

    /// <inheritdoc />
    public IWorkspaceView? ActiveView => _navigationService.ActiveView;
}
