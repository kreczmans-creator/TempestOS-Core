using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.App.Workspace;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates the Docking Framework and Resizing (`WP 10.0B`'s own
/// "Demonstrate" list) directly against <see cref="DockingGrid"/> — no
/// Workspace instance required, since docking geometry is a pure
/// presentation-layer concern.
/// </summary>
public sealed class DockingGridTests
{
    [AvaloniaFact]
    public void MultiPanelLayout_CentreLeftAndRightAllPresentSimultaneously()
    {
        var grid = new DockingGrid();
        var left = new Border();
        var right = new Border();
        var centre = new Border();

        grid.SetLeftPanel(left, 240, visible: true);
        grid.SetRightPanel(right, 240, visible: true);
        grid.SetCenterContent(centre);

        Assert.Contains(left, grid.Children);
        Assert.Contains(right, grid.Children);
        Assert.Contains(centre, grid.Children);
        Assert.True(grid.IsLeftVisible);
        Assert.True(grid.IsRightVisible);
    }

    [AvaloniaFact]
    public void Docking_HidingAndReshowingAPanel_PreservesItsOwnLastWidth()
    {
        var grid = new DockingGrid();
        grid.SetLeftPanel(new Border(), initialWidth: 300, visible: true);

        Assert.Equal(300, grid.LeftWidth);

        grid.SetLeftVisible(false);
        Assert.False(grid.IsLeftVisible);
        Assert.Equal(0, grid.LeftWidth);

        grid.SetLeftVisible(true);
        Assert.True(grid.IsLeftVisible);
        Assert.Equal(300, grid.LeftWidth); // WP8.0A UI Architecture.md §2: "reopening restores the same width"
    }

    [AvaloniaFact]
    public void Resizing_LeftAndRightPanelsResizeIndependently()
    {
        var grid = new DockingGrid();
        grid.SetLeftPanel(new Border(), 200, true);
        grid.SetRightPanel(new Border(), 200, true);

        double? resizedLeftWidth = null;
        double? resizedRightWidth = null;
        grid.LeftPanelResized += w => resizedLeftWidth = w;
        grid.RightPanelResized += w => resizedRightWidth = w;

        // Drives the identical notification path GridSplitter.DragCompleted
        // fires in production (DockingGrid.NotifyLeftPanelResized) — a real
        // OS-level pointer drag is outside what a headless test environment
        // can simulate meaningfully for a GridSplitter specifically.
        grid.ColumnDefinitions[0].Width = new Avalonia.Controls.GridLength(360, Avalonia.Controls.GridUnitType.Pixel);
        grid.NotifyLeftPanelResized();

        Assert.Equal(360, grid.LeftWidth);
        Assert.Equal(360, resizedLeftWidth);
        Assert.Null(resizedRightWidth);
    }

    // ------------------------------------------------------------
    // Bottom dock (`WP 10.2B`) — WorkspaceDockPosition.Bottom, real for
    // the first time since the enum member's own introduction, `WP 8.0B`.
    // ------------------------------------------------------------

    [AvaloniaFact]
    public void BottomPanel_ShowsAlongsideLeftCentreAndRight_AllFourPresentSimultaneously()
    {
        var grid = new DockingGrid();
        var left = new Border();
        var right = new Border();
        var centre = new Border();
        var bottom = new Border();

        grid.SetLeftPanel(left, 240, visible: true);
        grid.SetRightPanel(right, 240, visible: true);
        grid.SetCenterContent(centre);
        grid.SetBottomPanel(bottom, 160, visible: true);

        Assert.Contains(bottom, grid.Children);
        Assert.True(grid.IsBottomVisible);
        Assert.Equal(160, grid.BottomHeight);
    }

    [AvaloniaFact]
    public void BottomPanel_HidingAndReshowing_PreservesItsOwnLastHeight()
    {
        var grid = new DockingGrid();
        grid.SetBottomPanel(new Border(), initialHeight: 200, visible: true);

        Assert.Equal(200, grid.BottomHeight);

        grid.SetBottomVisible(false);
        Assert.False(grid.IsBottomVisible);
        Assert.Equal(0, grid.BottomHeight);

        grid.SetBottomVisible(true);
        Assert.True(grid.IsBottomVisible);
        Assert.Equal(200, grid.BottomHeight);
    }

    [AvaloniaFact]
    public void BottomPanel_IsHiddenByDefault_UntilExplicitlyMadeVisible()
    {
        var grid = new DockingGrid();

        // Before any SetBottomPanel call at all — the bottom row starts
        // collapsed to zero height (WP 10.2B's own "no fourth
        // default-visible panel" default).
        Assert.False(grid.IsBottomVisible);
        Assert.Equal(0, grid.BottomHeight);
    }

    [AvaloniaFact]
    public void Resizing_BottomPanelResizesIndependentlyOfLeftAndRight()
    {
        var grid = new DockingGrid();
        grid.SetBottomPanel(new Border(), 160, true);

        double? resizedBottomHeight = null;
        grid.BottomPanelResized += h => resizedBottomHeight = h;

        grid.RowDefinitions[2].Height = new Avalonia.Controls.GridLength(240, Avalonia.Controls.GridUnitType.Pixel);
        grid.NotifyBottomPanelResized();

        Assert.Equal(240, grid.BottomHeight);
        Assert.Equal(240, resizedBottomHeight);
    }

    // ------------------------------------------------------------
    // Collapse (`WP 10.2B`) — a manual, in-place shrink to a thin strip,
    // distinct from Visible/Hidden.
    // ------------------------------------------------------------

    [AvaloniaFact]
    public void Collapse_LeftPanel_ShrinksToTheFixedStripWidth_AndExpandRestoresItsOwnPriorWidth()
    {
        var grid = new DockingGrid();
        grid.SetLeftPanel(new Border(), initialWidth: 300, visible: true);

        grid.SetLeftCollapsed(true);
        Assert.True(grid.IsLeftCollapsed);
        Assert.Equal(DockingGrid.CollapsedStripSize, grid.LeftWidth);
        Assert.True(grid.IsLeftVisible); // still visible — collapsed, not hidden

        grid.SetLeftCollapsed(false);
        Assert.False(grid.IsLeftCollapsed);
        Assert.Equal(300, grid.LeftWidth);
    }

    [AvaloniaFact]
    public void Collapse_IsANoOp_WhileThePanelIsAlreadyHidden()
    {
        var grid = new DockingGrid();
        grid.SetRightPanel(new Border(), initialWidth: 260, visible: false);

        grid.SetRightCollapsed(true);

        Assert.False(grid.IsRightVisible);
        Assert.Equal(0, grid.RightWidth); // still zero — no reachable collapsed-but-hidden state
    }

    [AvaloniaFact]
    public void Collapse_BottomPanel_ShrinksToTheFixedStripHeight()
    {
        var grid = new DockingGrid();
        grid.SetBottomPanel(new Border(), initialHeight: 180, visible: true);

        grid.SetBottomCollapsed(true);

        Assert.Equal(DockingGrid.CollapsedStripSize, grid.BottomHeight);
    }

    // ------------------------------------------------------------
    // Predefined-layout width/height setters (`WP 10.2B`) — change an
    // already-placed panel's own size without duplicating it in Children.
    // ------------------------------------------------------------

    [AvaloniaFact]
    public void SetLeftWidth_ChangesTheVisibleColumnWidth_WithoutAddingASecondChild()
    {
        var grid = new DockingGrid();
        var panel = new Border();
        grid.SetLeftPanel(panel, 240, visible: true);

        grid.SetLeftWidth(180);

        Assert.Equal(180, grid.LeftWidth);
        Assert.Single(grid.Children, c => c == panel);
    }

    // ------------------------------------------------------------
    // Auto-Hide flyout (`WP 10.2B`) — a temporary overlay, reusing the
    // same control already docked, restoring its own exact prior
    // placement on close.
    // ------------------------------------------------------------

    [AvaloniaFact]
    public void ShowFlyout_ThenHideFlyout_RestoresThePanelsOwnOriginalGridPlacement()
    {
        var grid = new DockingGrid();
        var panel = new Border();
        grid.SetLeftPanel(panel, 240, visible: true);

        var originalColumn = Grid.GetColumn(panel);

        grid.ShowFlyout(panel, WorkspaceDockPosition.Left, 280);
        Assert.True(grid.IsFlyoutOpen);
        Assert.Equal(280, panel.Width);

        grid.HideFlyout();
        Assert.False(grid.IsFlyoutOpen);
        Assert.Equal(originalColumn, Grid.GetColumn(panel));
        Assert.True(double.IsNaN(panel.Width)); // Width unset again — the column itself governs
    }

    [AvaloniaFact]
    public void ShowFlyout_CalledTwice_ClosesTheFirstBeforeOpeningTheSecond()
    {
        var grid = new DockingGrid();
        var left = new Border();
        var right = new Border();
        grid.SetLeftPanel(left, 240, visible: true);
        grid.SetRightPanel(right, 240, visible: true);

        grid.ShowFlyout(left, WorkspaceDockPosition.Left, 280);
        grid.ShowFlyout(right, WorkspaceDockPosition.Right, 280);

        Assert.True(grid.IsFlyoutOpen);
        Assert.Equal(0, left.ZIndex); // restored — no longer the open flyout
        Assert.Equal(100, right.ZIndex);
    }

    [AvaloniaFact]
    public void HideFlyout_WithNoFlyoutOpen_IsANoOp()
    {
        var grid = new DockingGrid();

        var exception = Record.Exception(() => grid.HideFlyout());

        Assert.Null(exception);
        Assert.False(grid.IsFlyoutOpen);
    }
}
