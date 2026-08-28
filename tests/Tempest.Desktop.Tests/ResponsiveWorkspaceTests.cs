using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-70` closure — the workspace must stay usable as available screen
/// space shrinks (laptop, split-screen, smaller display), rather than
/// letting fixed-pixel side docks consume the window and the ribbon eat
/// the vertical space. The audited defect: both side docks were fixed at
/// 240 px regardless of window width, so at the application's own 960 px
/// minimum they took 50% of it, and the ribbon had no minimise
/// affordance at all.
/// </summary>
public sealed class ResponsiveWorkspaceTests
{
    private static DockingGrid BuildGrid(bool left = true, bool right = true, bool bottom = false)
    {
        var grid = new DockingGrid();
        grid.SetCenterContent(new Border());
        grid.SetLeftPanel(new Border(), 240, left);
        grid.SetRightPanel(new Border(), 240, right);
        grid.SetBottomPanel(new Border(), 160, bottom);
        return grid;
    }

    // ----------------------------------------------------------------
    // Side docks give space back rather than starving the Document Area
    // ----------------------------------------------------------------

    [AvaloniaTheory]
    [InlineData(1920.0)]
    [InlineData(1600.0)]
    [InlineData(1366.0)]
    [InlineData(1280.0)]
    [InlineData(1024.0)]
    [InlineData(960.0)]
    [InlineData(800.0)]
    public void AtEveryRealisticWidth_TheDocumentAreaKeepsItsMinimumWidth(double width)
    {
        var grid = BuildGrid();

        grid.ApplyResponsiveLayout(width, 800);

        var centre = width - grid.LeftWidth - grid.RightWidth - 8;
        Assert.True(
            centre >= DockingGrid.MinDocumentAreaWidth - 1,
            $"Document Area got {centre:F0}px at window width {width:F0} — below the {DockingGrid.MinDocumentAreaWidth}px floor.");
    }

    [AvaloniaFact]
    public void OnAWideWindow_BothPanelsKeepTheirOwnFullPreferredWidth()
    {
        var grid = BuildGrid();

        grid.ApplyResponsiveLayout(1920, 900);

        Assert.Equal(240, grid.LeftWidth);
        Assert.Equal(240, grid.RightWidth);
    }

    [AvaloniaFact]
    public void AtTheDefaultWidths_ACommonLaptopNeedsNoSqueezeAtAll()
    {
        // The floor is a safety net, not a behaviour change in the common
        // case: at the default 240px docks nothing is squeezed on any
        // ordinary display, so the fix introduces no surprise.
        var grid = BuildGrid();

        grid.ApplyResponsiveLayout(1366, 768);

        Assert.Equal(240, grid.LeftWidth);
        Assert.Equal(240, grid.RightWidth);
    }

    [AvaloniaFact]
    public void AfterTheUserWidensBothDocks_ANarrowerWindowSqueezesThePanels_NotTheDocumentArea()
    {
        // The real scenario the audit found: a user widens both docks on a
        // large monitor, then works on a laptop or in split screen. Before
        // `TD-70` the docks stayed at their full width and the Document
        // Area absorbed the entire shortfall.
        var grid = BuildGrid();
        grid.SetLeftWidth(420);
        grid.SetRightWidth(400);

        grid.ApplyResponsiveLayout(1100, 800);

        Assert.True(grid.LeftWidth < 420, $"left dock should have given space back, was {grid.LeftWidth}");
        Assert.True(grid.RightWidth < 400, $"right dock should have given space back, was {grid.RightWidth}");

        var centre = 1100 - grid.LeftWidth - grid.RightWidth - 8;
        Assert.True(centre >= DockingGrid.MinDocumentAreaWidth - 1, $"Document Area got {centre:F0}px.");
    }

    [AvaloniaFact]
    public void WhenTooNarrowToRead_APanelCollapsesToItsStrip_RatherThanBecomingAnUnusableSliver()
    {
        var grid = BuildGrid();

        grid.ApplyResponsiveLayout(640, 800);

        Assert.Equal(DockingGrid.CollapsedStripSize, grid.LeftWidth);
        Assert.Equal(DockingGrid.CollapsedStripSize, grid.RightWidth);

        // Still reachable — never hidden outright.
        Assert.True(grid.IsLeftVisible);
        Assert.True(grid.IsRightVisible);
    }

    [AvaloniaFact]
    public void TheUsersOwnPreferredWidth_IsRestoredInFull_WhenTheWindowGrowsBack()
    {
        var grid = BuildGrid();

        grid.SetLeftWidth(420);
        grid.SetRightWidth(400);

        grid.ApplyResponsiveLayout(1100, 800);
        Assert.True(grid.LeftWidth < 420);

        grid.ApplyResponsiveLayout(1920, 800);

        Assert.Equal(420, grid.LeftWidth);
        Assert.Equal(400, grid.RightWidth);
    }

    [AvaloniaFact]
    public void AHiddenPanel_ClaimsNoSpaceAtAll_AndTheVisibleOneKeepsMore()
    {
        var grid = BuildGrid(right: false);

        grid.ApplyResponsiveLayout(1000, 800);

        Assert.Equal(0, grid.RightWidth);
        Assert.True(grid.LeftWidth > DockingGrid.CollapsedStripSize);
    }

    [AvaloniaFact]
    public void TheBottomDock_GivesHeightBack_SoTheDocumentAreaKeepsItsMinimumHeight()
    {
        var grid = BuildGrid(bottom: true);

        grid.ApplyResponsiveLayout(1600, 300);

        var centre = 300 - grid.BottomHeight - 4;
        Assert.True(centre >= DockingGrid.MinDocumentAreaHeight - 1, $"Document Area got {centre:F0}px height.");
    }

    // ----------------------------------------------------------------
    // A splitter drag is a real preference, not a transient
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public void AfterASplitterDrag_HidingAndReshowingRestoresTheDraggedWidth_NotThePreDragOne()
    {
        var grid = BuildGrid();
        grid.ApplyResponsiveLayout(1920, 900);

        // A real GridSplitter drag mutates the column definition directly
        // and then raises DragCompleted — it never calls SetLeftWidth. Drive
        // exactly that path, or this test proves nothing about dragging.
        grid.ColumnDefinitions[0].Width = new GridLength(400, GridUnitType.Pixel);
        grid.NotifyLeftPanelResized();

        grid.SetLeftVisible(false);
        grid.SetLeftVisible(true);
        grid.ApplyResponsiveLayout(1920, 900);

        Assert.Equal(400, grid.LeftWidth);
    }

    // ----------------------------------------------------------------
    // Ribbon minimise
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task Ribbon_Minimise_HidesCommandContent_ButKeepsEveryTabReachable()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (Tempest.Core.Commands.ICommandRegistry)host.Services!.GetService(typeof(Tempest.Core.Commands.ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });
            var tabs = (TabControl)ribbon.Content!;
            var tabCount = tabs.Items.Count;

            Assert.False(ribbon.IsCollapsed);
            Assert.All(tabs.Items.OfType<TabItem>(), t => Assert.True(((Control)t.Content!).IsVisible));

            ribbon.SetCollapsed(true);

            Assert.True(ribbon.IsCollapsed);
            Assert.All(tabs.Items.OfType<TabItem>(), t => Assert.False(((Control)t.Content!).IsVisible));

            // No command becomes unreachable: every tab header survives.
            Assert.Equal(tabCount, tabs.Items.Count);

            ribbon.SetCollapsed(false);
            Assert.All(tabs.Items.OfType<TabItem>(), t => Assert.True(((Control)t.Content!).IsVisible));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Ribbon_MinimisedState_SurvivesARebuild()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (Tempest.Core.Commands.ICommandRegistry)host.Services!.GetService(typeof(Tempest.Core.Commands.ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            ribbon.SetCollapsed(true);
            ribbon.Rebuild();

            var tabs = (TabControl)ribbon.Content!;
            Assert.True(ribbon.IsCollapsed);
            Assert.All(tabs.Items.OfType<TabItem>(), t => Assert.False(((Control)t.Content!).IsVisible));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Ribbon_MinimiseIsReachableFromTheViewMenu_AndIsPersisted()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var ribbon = window.GetLogicalDescendants().OfType<RibbonView>().Single();

            var menu = window.GetLogicalDescendants().OfType<Menu>().Single();
            var view = menu.ItemsSource!.Cast<MenuItem>().Single(m => Equals(m.Header, "_View"));
            var minimise = view.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Minimise Ribbon"));

            Assert.False(ribbon.IsCollapsed);
            minimise.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(ribbon.IsCollapsed);

            // Persisted for the next session.
            var uiState = new DesktopPanelUiState((Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider)));
            await window.SaveDesktopUiStateAsync();
            await uiState.LoadAsync();
            Assert.True(uiState.RibbonCollapsed);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
