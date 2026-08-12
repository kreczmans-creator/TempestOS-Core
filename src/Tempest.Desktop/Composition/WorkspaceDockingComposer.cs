using Tempest.App.Workspace;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Builds the three docked panels (Project Explorer/Property Inspector/
/// Output) and wires every resize/hide/collapse/pin/Auto-Hide-flyout
/// interaction between them and the <see cref="DockingGrid"/> — extracted,
/// `WP 12.0B` (`ADR-0103`), from <see cref="MainWindow"/>'s own previous
/// Docking Framework construction block, unmodified in behaviour. A
/// collaborator under `ADR-0103`: constructed once by
/// <see cref="MainWindow"/> (the composition root), declaring only the
/// dependencies it actually needs, never DI-registered, never referencing
/// <see cref="MainWindow"/> or any sibling collaborator back.
/// </summary>
internal sealed class WorkspaceDockingComposer
{
    private readonly DesktopPanelUiState _uiState;
    private WorkspaceDockPosition? _openFlyoutSlot;

    /// <summary>Gets the real Docking Framework grid (`WP 10.2B`) hosting every panel below.</summary>
    public DockingGrid Grid { get; } = new();

    /// <summary>Gets the Project Explorer's own panel host.</summary>
    public PanelHostControl ExplorerHost { get; }

    /// <summary>Gets the Property Inspector's own panel host.</summary>
    public PanelHostControl InspectorHost { get; }

    /// <summary>Gets the Output panel itself (`WP 10.2B`).</summary>
    public OutputPanel OutputPanel { get; } = new();

    /// <summary>Gets the Output panel's own rendered view.</summary>
    public OutputPanelView OutputView { get; } = new();

    /// <summary>Gets the Output panel's own panel host.</summary>
    public PanelHostControl OutputHost { get; }

    /// <summary>Gets whether an Auto-Hide flyout is currently open — the click-away/<c>Escape</c> gesture's own guard.</summary>
    public bool IsFlyoutOpen => Grid.IsFlyoutOpen;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceDockingComposer"/> class.</summary>
    public WorkspaceDockingComposer(IWorkspace workspace, ProjectExplorerView explorerView, PropertyInspectorView inspectorView, DocumentAreaView documentArea, DesktopPanelUiState uiState)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(explorerView);
        ArgumentNullException.ThrowIfNull(inspectorView);
        ArgumentNullException.ThrowIfNull(documentArea);
        ArgumentNullException.ThrowIfNull(uiState);

        _uiState = uiState;

        var explorerPlacement = workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id);
        var inspectorPlacement = workspace.Layout.GetPlacement(workspace.PropertyInspector.Id);

        ExplorerHost = new PanelHostControl(workspace.ProjectExplorer, explorerView);
        InspectorHost = new PanelHostControl(workspace.PropertyInspector, inspectorView);
        ExplorerHost.SetCollapsed(uiState.ExplorerCollapsed);
        ExplorerHost.SetPinned(uiState.ExplorerPinned);
        InspectorHost.SetCollapsed(uiState.InspectorCollapsed);
        InspectorHost.SetPinned(uiState.InspectorPinned);

        OutputHost = new PanelHostControl(OutputPanel, OutputView);
        OutputHost.SetCollapsed(uiState.OutputCollapsed);
        OutputHost.SetPinned(uiState.OutputPinned);
        if (uiState.OutputVisible)
            OutputPanel.ShowAsync().GetAwaiter().GetResult();

        Grid.SetLeftPanel(ExplorerHost, explorerPlacement.Size == 0 ? 240 : explorerPlacement.Size * 8, explorerPlacement.IsVisible);
        Grid.SetRightPanel(InspectorHost, inspectorPlacement.Size == 0 ? 240 : inspectorPlacement.Size * 8, inspectorPlacement.IsVisible);
        Grid.SetBottomPanel(OutputHost, uiState.OutputHeight, uiState.OutputVisible);
        Grid.SetCenterContent(documentArea);
        Grid.SetLeftCollapsed(ExplorerHost.IsStripShowing);
        Grid.SetRightCollapsed(InspectorHost.IsStripShowing);
        Grid.SetBottomCollapsed(OutputHost.IsStripShowing);

        // Resizing: persist the new width back into the real IWorkspaceLayout
        // (WorkspacePanelPlacement is an immutable record — "with" produces
        // the updated snapshot ADR-0064's own SaveAsync later serialises).
        Grid.LeftPanelResized += width =>
            workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id) with { Size = width });
        Grid.RightPanelResized += width =>
            workspace.Layout.SetPlacement(workspace.PropertyInspector.Id, workspace.Layout.GetPlacement(workspace.PropertyInspector.Id) with { Size = width });
        Grid.BottomPanelResized += height => _uiState.OutputHeight = height;

        ExplorerHost.HideRequested += () =>
        {
            Grid.SetLeftVisible(false);
            workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id) with { IsVisible = false });
        };
        InspectorHost.HideRequested += () =>
        {
            Grid.SetRightVisible(false);
            workspace.Layout.SetPlacement(workspace.PropertyInspector.Id, workspace.Layout.GetPlacement(workspace.PropertyInspector.Id) with { IsVisible = false });
        };
        OutputHost.HideRequested += () =>
        {
            Grid.SetBottomVisible(false);
            _uiState.OutputVisible = false;
        };

        // Collapse (`WP 10.2B`) — a manual, in-place shrink; Desktop-local
        // state only (`DesktopPanelUiState`, not `IWorkspaceLayout`).
        ExplorerHost.CollapseToggled += collapsed =>
        {
            _uiState.ExplorerCollapsed = collapsed;
            Grid.SetLeftCollapsed(ExplorerHost.IsStripShowing);
        };
        InspectorHost.CollapseToggled += collapsed =>
        {
            _uiState.InspectorCollapsed = collapsed;
            Grid.SetRightCollapsed(InspectorHost.IsStripShowing);
        };
        OutputHost.CollapseToggled += collapsed =>
        {
            _uiState.OutputCollapsed = collapsed;
            Grid.SetBottomCollapsed(OutputHost.IsStripShowing);
        };

        // Auto-Hide (`WP 10.2B`) — unpinning hands the dock column/row back
        // to the Document Area, leaving only the thin edge strip; closes
        // any open flyout for this slot when re-pinned.
        ExplorerHost.PinToggled += pinned =>
        {
            _uiState.ExplorerPinned = pinned;
            Grid.SetLeftCollapsed(ExplorerHost.IsStripShowing);
            if (pinned && _openFlyoutSlot == WorkspaceDockPosition.Left)
                CloseFlyout();
        };
        InspectorHost.PinToggled += pinned =>
        {
            _uiState.InspectorPinned = pinned;
            Grid.SetRightCollapsed(InspectorHost.IsStripShowing);
            if (pinned && _openFlyoutSlot == WorkspaceDockPosition.Right)
                CloseFlyout();
        };
        OutputHost.PinToggled += pinned =>
        {
            _uiState.OutputPinned = pinned;
            Grid.SetBottomCollapsed(OutputHost.IsStripShowing);
            if (pinned && _openFlyoutSlot == WorkspaceDockPosition.Bottom)
                CloseFlyout();
        };

        ExplorerHost.FlyoutRequested += () => ToggleFlyout(WorkspaceDockPosition.Left, ExplorerHost, Math.Max(explorerPlacement.Size == 0 ? 240 : explorerPlacement.Size * 8, 240));
        InspectorHost.FlyoutRequested += () => ToggleFlyout(WorkspaceDockPosition.Right, InspectorHost, Math.Max(inspectorPlacement.Size == 0 ? 240 : inspectorPlacement.Size * 8, 240));
        OutputHost.FlyoutRequested += () => ToggleFlyout(WorkspaceDockPosition.Bottom, OutputHost, Math.Max(_uiState.OutputHeight, 160));
    }

    /// <summary>Opens or closes the Auto-Hide flyout for <paramref name="slot"/> — a toggle, so clicking an already-open panel's own edge strip a second time closes it (`WP 10.2B`).</summary>
    private void ToggleFlyout(WorkspaceDockPosition slot, PanelHostControl host, double size)
    {
        if (_openFlyoutSlot == slot)
        {
            CloseFlyout();
            return;
        }

        Grid.ShowFlyout(host, slot, size);
        _openFlyoutSlot = slot;
    }

    /// <summary>Closes whichever Auto-Hide flyout is currently open, if any — a no-op otherwise.</summary>
    public void CloseFlyout()
    {
        Grid.HideFlyout();
        _openFlyoutSlot = null;
    }
}
