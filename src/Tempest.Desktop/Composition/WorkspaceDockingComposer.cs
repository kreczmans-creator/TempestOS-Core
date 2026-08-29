using Avalonia.Controls;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Layout;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Composes the Engineering Workspace's own dockable surfaces and the
/// layout that arranges them (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// Before `TD-72` this class wired three named panels into three fixed
/// slots of a compile-time grid, and every panel operation was a distinct
/// method per slot (<c>SetLeftVisible</c>, <c>SetRightCollapsed</c>, …).
/// It now registers panels and hands the arrangement to
/// <see cref="WorkspaceLayoutController"/>: the panels no longer know
/// where they are, and the layout no longer knows what they contain.
/// </para>
/// <para>
/// That separation is what makes the layout extensible. A new surface
/// registers a <see cref="WorkspacePanelDescriptor"/> here and immediately
/// gains docking, tabbing, splitting, floating, collapse, auto-hide and
/// persistence, with no change to this class beyond the registration
/// itself.
/// </para>
/// </remarks>
internal sealed class WorkspaceDockingComposer
{
    private readonly DesktopPanelUiState _uiState;

    /// <summary>The Output panel's own model.</summary>
    public OutputPanel OutputPanel { get; } = new();

    /// <summary>The Output panel's own view.</summary>
    public OutputPanelView OutputView { get; } = new();

    /// <summary>The layout controller that owns the arrangement.</summary>
    public WorkspaceLayoutController Layout { get; }

    /// <summary>The control the Engineering surface hosts — the rendered layout.</summary>
    public Control View => Layout.Host;

    /// <summary>The Project Explorer's own panel id.</summary>
    public Guid ExplorerPanelId { get; }

    /// <summary>The Property Inspector's own panel id.</summary>
    public Guid InspectorPanelId { get; }

    /// <summary>The Document Area's own panel id — a panel like any other, with no privileged slot.</summary>
    public Guid DocumentPanelId { get; }

    /// <summary>The Output panel's own panel id.</summary>
    public Guid OutputPanelId => OutputPanel.Id;

    /// <summary>Gets whether an auto-hide flyout is currently open.</summary>
    public bool IsFlyoutOpen => Layout.Host.IsFlyoutOpen;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceDockingComposer"/> class.</summary>
    public WorkspaceDockingComposer(
        IWorkspace workspace,
        ProjectExplorerView explorerView,
        PropertyInspectorView inspectorView,
        DocumentAreaView documentArea,
        DesktopPanelUiState uiState,
        IWorkspaceLayoutStore layoutStore)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(explorerView);
        ArgumentNullException.ThrowIfNull(inspectorView);
        ArgumentNullException.ThrowIfNull(documentArea);
        ArgumentNullException.ThrowIfNull(uiState);
        ArgumentNullException.ThrowIfNull(layoutStore);

        _uiState = uiState;

        ExplorerPanelId = workspace.ProjectExplorer.Id;
        InspectorPanelId = workspace.PropertyInspector.Id;
        DocumentPanelId = DocumentAreaPanelId;

        var registry = new WorkspacePanelRegistry();
        registry.Register(new WorkspacePanelDescriptor(ExplorerPanelId, workspace.ProjectExplorer.Title, explorerView));
        registry.Register(new WorkspacePanelDescriptor(DocumentPanelId, "Documents", documentArea, CanClose: false, CanFloat: false));
        registry.Register(new WorkspacePanelDescriptor(InspectorPanelId, workspace.PropertyInspector.Title, inspectorView));
        registry.Register(new WorkspacePanelDescriptor(OutputPanelId, OutputPanel.Title, OutputView));

        Registry = registry;
        Layout = new WorkspaceLayoutController(registry, layoutStore);

        // A resize or a dock is session state, written on the shell's own
        // shutdown save rather than on every pixel of every drag.
        Layout.LayoutChanged += _ => _uiState.LayoutIsUserArranged = true;
        Layout.LayoutChanged += tree => SyncWorkspacePlacements(workspace, tree);

        // The workspace carries a real arrangement from construction, not
        // from a later window event. A window that exists but whose layout
        // is empty is a window whose panels, splitters and menu toggles all
        // behave as though nothing is docked — `RestoreLayoutAsync` then
        // replaces this with the user's own saved arrangement once the
        // settings substrate is readable.
        Layout.Load(DefaultLayout());
    }

    /// <summary>The registered panels — the extension point a future surface joins through.</summary>
    public WorkspacePanelRegistry Registry { get; }

    /// <summary>
    /// The Document Area's own fixed panel id. Fixed rather than generated
    /// so a saved layout still finds it after a restart.
    /// </summary>
    public static Guid DocumentAreaPanelId { get; } = Guid.Parse("d0c00000-0000-4000-8000-000000000001");

    /// <summary>The arrangement a first run — or a reset — opens with.</summary>
    public WorkspaceLayoutTree DefaultLayout() =>
        WorkspaceLayoutPresets.Default(ExplorerPanelId, DocumentPanelId, InspectorPanelId, OutputPanelId);

    /// <summary>
    /// Restores the saved arrangement, or carries a returning user's
    /// pre-`TD-72` panel preferences into the new model on first run.
    /// </summary>
    public Task RestoreLayoutAsync(CancellationToken cancellationToken = default) =>
        Layout.RestoreAsync(MigratedDefault(), cancellationToken);

    /// <summary>The default arrangement with the user's own existing preferences applied — see <see cref="WorkspaceLayoutMigration"/>.</summary>
    private WorkspaceLayoutTree MigratedDefault() =>
        WorkspaceLayoutMigration.FromLegacyPreferences(
            DefaultLayout(),
            [
                new LegacyPanelPreference(ExplorerPanelId, IsVisible: true, _uiState.ExplorerCollapsed, _uiState.ExplorerPinned, 0),
                new LegacyPanelPreference(InspectorPanelId, IsVisible: true, _uiState.InspectorCollapsed, _uiState.InspectorPinned, 0),
                new LegacyPanelPreference(OutputPanelId, _uiState.OutputVisible, _uiState.OutputCollapsed, _uiState.OutputPinned, _uiState.OutputHeight),
            ]);

    /// <summary>Applies <paramref name="preset"/>, replacing the current arrangement.</summary>
    public void ApplyPreset(WorkspaceLayoutPreset preset)
    {
        Layout.Load(WorkspaceLayoutPresets.Build(preset, ExplorerPanelId, DocumentPanelId, InspectorPanelId, OutputPanelId));
        _uiState.LastAppliedPreset = preset.ToString();
    }

    /// <summary>Returns to the default arrangement.</summary>
    public void ResetLayout()
    {
        Layout.Load(DefaultLayout());
        _uiState.LastAppliedPreset = null;
    }

    /// <summary>Closes whichever auto-hide flyout is open, if any — a no-op otherwise.</summary>
    public void CloseFlyout() => Layout.Host.HideFlyout();

    /// <summary>
    /// Keeps the frozen `WP8.0B` <see cref="IWorkspaceLayout"/> contract
    /// truthful as a projection of the layout tree (`TD-72`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IWorkspaceLayout"/> speaks in edges, sizes and
    /// visibility, and Workspace-layer consumers still read it. Rather than
    /// freezing it out or leaving it stale — which would have made it a
    /// second, disagreeing account of where the panels are — it is derived
    /// from the tree after every change. The tree is the model; this is a
    /// view of it.
    /// </para>
    /// <para>
    /// An arrangement the old contract cannot express — a panel in a tab
    /// group, or floating — reports the nearest honest answer: visible,
    /// with whatever edge it is nearest to. That is a real limitation of
    /// the old shape, disclosed rather than papered over.
    /// </para>
    /// </remarks>
    private void SyncWorkspacePlacements(IWorkspace workspace, WorkspaceLayoutTree tree)
    {
        Project(workspace, tree, workspace.ProjectExplorer.Id, WorkspaceDockPosition.Left);
        Project(workspace, tree, workspace.PropertyInspector.Id, WorkspaceDockPosition.Right);
    }

    private void Project(IWorkspace workspace, WorkspaceLayoutTree tree, Guid panelId, WorkspaceDockPosition fallbackEdge)
    {
        var placement = workspace.Layout.GetPlacement(panelId);

        var edge = tree.InferEdge(panelId, DocumentPanelId) switch
        {
            DockRelation.Left => WorkspaceDockPosition.Left,
            DockRelation.Right => WorkspaceDockPosition.Right,
            DockRelation.Above or DockRelation.Below => WorkspaceDockPosition.Bottom,
            _ => fallbackEdge,
        };

        workspace.Layout.SetPlacement(panelId, placement with
        {
            IsVisible = tree.Contains(panelId),
            DockPosition = edge,
            Size = Math.Round(tree.ShareOf(panelId) * NominalWorkspaceWidth),
        });
    }

    /// <summary>
    /// The window extent the projected <see cref="WorkspacePanelPlacement.Size"/>
    /// is expressed against. The old contract's size is unitless by its own
    /// documentation; a nominal extent keeps the projected number stable
    /// and comparable rather than changing every time the window resizes.
    /// </summary>
    private const double NominalWorkspaceWidth = 1280;
}
