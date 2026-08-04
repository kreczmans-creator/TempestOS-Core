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
        IProjectExplorer projectExplorer,
        IPropertyInspector propertyInspector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(projectExplorer);
        ArgumentNullException.ThrowIfNull(propertyInspector);

        State = state;
        _navigationService = navigationService;
        Selection = selection;
        ProjectExplorer = projectExplorer;
        PropertyInspector = propertyInspector;
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

    /// <inheritdoc />
    public IPropertyInspector PropertyInspector { get; }

    /// <inheritdoc />
    public IReadOnlyList<IWorkspaceView> OpenViews => _navigationService.OpenViews;

    /// <inheritdoc />
    public IWorkspaceView? ActiveView => _navigationService.ActiveView;
}
