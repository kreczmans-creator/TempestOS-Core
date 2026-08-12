using Tempest.App.Workspace;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Applies and resets the three named, fixed panel arrangements
/// (<see cref="PredefinedLayouts"/>, `WP 10.2B`) — extracted, `WP 12.0B`
/// (`ADR-0103`), from <see cref="MainWindow"/>'s own previous
/// <c>ApplyPreset</c>/<c>ResetLayout</c> members, unmodified in
/// behaviour. A collaborator under `ADR-0103`: constructed once by
/// <see cref="MainWindow"/> (the composition root), declaring only the
/// dependencies it actually needs, never DI-registered, never referencing
/// <see cref="MainWindow"/> or any sibling collaborator back.
/// </summary>
/// <remarks>
/// Takes a <see cref="closeFlyout"/> delegate rather than a direct
/// reference to <c>WorkspaceDockingComposer</c> — the composition root's
/// own wiring (`ADR-0103`'s "a collaborator never depends on a sibling
/// collaborator directly" rule), not a field or constructor reference
/// between the two collaborators.
/// </remarks>
internal sealed class WorkspaceLayoutPresetCoordinator
{
    private readonly IWorkspace _workspace;
    private readonly PanelHostControl _explorerHost;
    private readonly PanelHostControl _inspectorHost;
    private readonly PanelHostControl _outputHost;
    private readonly DockingGrid _docking;
    private readonly DesktopPanelUiState _uiState;
    private readonly OutputPanel _outputPanel;
    private readonly StatusBarView _statusBar;
    private readonly Action _closeFlyout;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceLayoutPresetCoordinator"/> class.</summary>
    public WorkspaceLayoutPresetCoordinator(
        IWorkspace workspace, PanelHostControl explorerHost, PanelHostControl inspectorHost, PanelHostControl outputHost,
        DockingGrid docking, DesktopPanelUiState uiState, OutputPanel outputPanel, StatusBarView statusBar, Action closeFlyout)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(explorerHost);
        ArgumentNullException.ThrowIfNull(inspectorHost);
        ArgumentNullException.ThrowIfNull(outputHost);
        ArgumentNullException.ThrowIfNull(docking);
        ArgumentNullException.ThrowIfNull(uiState);
        ArgumentNullException.ThrowIfNull(outputPanel);
        ArgumentNullException.ThrowIfNull(statusBar);
        ArgumentNullException.ThrowIfNull(closeFlyout);

        _workspace = workspace;
        _explorerHost = explorerHost;
        _inspectorHost = inspectorHost;
        _outputHost = outputHost;
        _docking = docking;
        _uiState = uiState;
        _outputPanel = outputPanel;
        _statusBar = statusBar;
        _closeFlyout = closeFlyout;
    }

    /// <summary>
    /// Applies one of the three named, fixed panel arrangements
    /// (<see cref="PredefinedLayouts"/>, `WP 10.2B`) — every value it sets
    /// already exists somewhere in <see cref="IWorkspaceLayout"/>
    /// (Explorer/Inspector) or <see cref="DesktopPanelUiState"/> (Output/
    /// Collapse/Auto-Hide); applying a preset introduces no new state of
    /// its own, only a fixed, named combination of existing state.
    /// </summary>
    public void Apply(PredefinedLayouts.WorkspaceLayoutPreset preset)
    {
        _closeFlyout();

        var explorerPlacement = PredefinedLayouts.ExplorerPlacement(preset, _workspace.ProjectExplorer.Id);
        var inspectorPlacement = PredefinedLayouts.InspectorPlacement(preset, _workspace.PropertyInspector.Id);
        var outputPlacement = PredefinedLayouts.OutputPanelPlacement(preset);
        var inspectorPinned = PredefinedLayouts.InspectorPinned(preset);

        _workspace.Layout.SetPlacement(_workspace.ProjectExplorer.Id, explorerPlacement);
        _workspace.Layout.SetPlacement(_workspace.PropertyInspector.Id, inspectorPlacement);

        _explorerHost.SetCollapsed(false);
        _explorerHost.SetPinned(true);
        _inspectorHost.SetCollapsed(false);
        _inspectorHost.SetPinned(inspectorPinned);
        _outputHost.SetCollapsed(false);
        _outputHost.SetPinned(true);

        _docking.SetLeftWidth(explorerPlacement.Size);
        _docking.SetLeftVisible(explorerPlacement.IsVisible);
        _docking.SetLeftCollapsed(false);

        _docking.SetRightWidth(inspectorPlacement.Size);
        _docking.SetRightVisible(inspectorPlacement.IsVisible);
        _docking.SetRightCollapsed(!inspectorPinned);

        _docking.SetBottomHeight(outputPlacement.Height);
        _docking.SetBottomVisible(outputPlacement.Visible);
        _docking.SetBottomCollapsed(false);

        if (outputPlacement.Visible)
            _outputPanel.ShowAsync().GetAwaiter().GetResult();
        else
            _outputPanel.HideAsync().GetAwaiter().GetResult();

        _uiState.ExplorerCollapsed = false;
        _uiState.ExplorerPinned = true;
        _uiState.InspectorCollapsed = false;
        _uiState.InspectorPinned = inspectorPinned;
        _uiState.OutputVisible = outputPlacement.Visible;
        _uiState.OutputHeight = outputPlacement.Height;
        _uiState.OutputCollapsed = false;
        _uiState.OutputPinned = true;
        _uiState.LastAppliedPreset = preset.ToString();

        _statusBar.SetText($"Layout: {preset}");
    }

    /// <summary>
    /// Resets every panel back to <see cref="IWorkspaceLayout.ResetToDefault"/>'s
    /// own documented default arrangement (Explorer/Inspector — unchanged
    /// since `WP 8.1A`), plus this Desktop-local defaults (Output hidden,
    /// nothing Collapsed, everything pinned) — the "reset workspace
    /// layout" scope item (`WP 10.2B`).
    /// </summary>
    public void Reset()
    {
        _closeFlyout();

        var defaults = _workspace.Layout.ResetToDefault();
        var explorerPlacement = defaults.GetPlacement(_workspace.ProjectExplorer.Id);
        var inspectorPlacement = defaults.GetPlacement(_workspace.PropertyInspector.Id);
        _workspace.Layout.SetPlacement(_workspace.ProjectExplorer.Id, explorerPlacement);
        _workspace.Layout.SetPlacement(_workspace.PropertyInspector.Id, inspectorPlacement);

        _explorerHost.SetCollapsed(false);
        _explorerHost.SetPinned(true);
        _inspectorHost.SetCollapsed(false);
        _inspectorHost.SetPinned(true);
        _outputHost.SetCollapsed(false);
        _outputHost.SetPinned(true);

        _docking.SetLeftWidth(explorerPlacement.Size == 0 ? 240 : explorerPlacement.Size);
        _docking.SetLeftVisible(explorerPlacement.IsVisible);
        _docking.SetLeftCollapsed(false);

        _docking.SetRightWidth(inspectorPlacement.Size == 0 ? 240 : inspectorPlacement.Size);
        _docking.SetRightVisible(inspectorPlacement.IsVisible);
        _docking.SetRightCollapsed(false);

        _docking.SetBottomHeight(160);
        _docking.SetBottomVisible(false);
        _docking.SetBottomCollapsed(false);
        _outputPanel.HideAsync().GetAwaiter().GetResult();

        _uiState.ExplorerCollapsed = false;
        _uiState.ExplorerPinned = true;
        _uiState.InspectorCollapsed = false;
        _uiState.InspectorPinned = true;
        _uiState.OutputVisible = false;
        _uiState.OutputHeight = 160;
        _uiState.OutputCollapsed = false;
        _uiState.OutputPinned = true;
        _uiState.LastAppliedPreset = null;

        _statusBar.SetText("Layout reset to default.");
    }
}
