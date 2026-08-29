using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace.Layout;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The tree-driven docking surface (`TD-72`) — the renderer that replaced
/// the compile-time five-column grid.
/// </summary>
/// <remarks>
/// These prove the renderer is a faithful function of the model: the same
/// tree always produces the same arrangement, every gesture produces a new
/// tree rather than a direct visual mutation, and the guarantees the old
/// fixed grid carried (`TD-70`'s responsive floor, collapse to a strip,
/// auto-hide flyouts) survive the replacement.
/// </remarks>
public sealed class WorkspaceLayoutHostTests
{
    private static readonly Guid Explorer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Document = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Inspector = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Output = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static WorkspacePanelRegistry BuildRegistry()
    {
        var registry = new WorkspacePanelRegistry();
        registry.Register(new WorkspacePanelDescriptor(Explorer, "Explorer", new TextBlock { Text = "explorer" }));
        registry.Register(new WorkspacePanelDescriptor(Document, "Documents", new TextBlock { Text = "documents" }, CanClose: false));
        registry.Register(new WorkspacePanelDescriptor(Inspector, "Inspector", new TextBlock { Text = "inspector" }));
        registry.Register(new WorkspacePanelDescriptor(Output, "Output", new TextBlock { Text = "output" }));
        return registry;
    }

    private static (WorkspaceLayoutHost Host, Window Window) Show(WorkspaceLayoutTree tree)
    {
        var host = new WorkspaceLayoutHost(BuildRegistry());
        var window = new Window { Content = host, Width = 1280, Height = 800 };
        window.Show();
        host.Update(tree);
        window.Measure(new Size(1280, 800));
        window.Arrange(new Rect(0, 0, 1280, 800));
        return (host, window);
    }

    private static WorkspaceLayoutTree Default() =>
        WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output);

    // ----------------------------------------------------------------
    // Structure
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public void EveryPanelInTheArrangement_IsRenderedSimultaneously()
    {
        var (host, _) = Show(Default());

        Assert.Equal(3, host.TabGroups.Count);
        Assert.Equal([Explorer, Document, Inspector], host.TabGroups.SelectMany(g => g.PanelIds));
    }

    [AvaloniaFact]
    public void ASplit_RendersARealDraggableSplitterBetweenEveryPairOfPanes()
    {
        var (host, _) = Show(Default());

        // Three panes, two splitters — the resize affordance is real, not
        // decorative.
        Assert.Equal(2, host.GetLogicalDescendants().OfType<GridSplitter>().Count());
    }

    [AvaloniaFact]
    public void ProportionalWeights_BecomeStarSizedColumns_SoALayoutSurvivesADifferentWindowSize()
    {
        var (host, _) = Show(Default());

        var grid = host.GetLogicalDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count > 1);
        var starColumns = grid.ColumnDefinitions.Where(c => c.Width.IsStar).ToList();

        Assert.Equal(3, starColumns.Count);
        Assert.True(starColumns[1].Width.Value > starColumns[0].Width.Value, "The document pane must take the largest share.");
    }

    [AvaloniaFact]
    public void ANestedVerticalSplit_RendersAsRows_InsideTheHorizontalOne()
    {
        var tree = Default();
        tree = tree.Dock(Output, tree.FindGroupContaining(Document)!.Id, DockRelation.Below);

        var (host, _) = Show(tree);

        var rowGrid = host.GetLogicalDescendants().OfType<Grid>().First(g => g.RowDefinitions.Count > 1);
        Assert.Equal(3, rowGrid.RowDefinitions.Count); // pane, splitter, pane
        Assert.Equal(4, host.TabGroups.Count);
    }

    [AvaloniaFact]
    public void TabbedPanels_ShareOnePane_AndOnlyTheSelectedOnesContentIsShown()
    {
        var tree = Default();
        tree = tree.Dock(Output, tree.FindGroupContaining(Inspector)!.Id, DockRelation.Into);

        var (host, _) = Show(tree);

        var tabbed = host.TabGroups.Single(g => g.PanelIds.Count == 2);
        Assert.Equal([Inspector, Output], tabbed.PanelIds);
        Assert.Equal(Output, tabbed.SelectedPanelId);

        // Both tab headers are reachable; one content is rendered.
        var headers = tabbed.GetLogicalDescendants().OfType<Button>()
            .Select(b => Avalonia.Automation.AutomationProperties.GetName(b) ?? string.Empty).ToList();
        Assert.Contains("Inspector", headers);
        Assert.Contains("Output", headers);
    }

    [AvaloniaFact]
    public void APanelMissingFromTheRegistry_RendersAnHonestMessage_RatherThanCrashingTheLayout()
    {
        var stranger = Guid.NewGuid();
        var host = new WorkspaceLayoutHost(BuildRegistry());
        var window = new Window { Content = host, Width = 800, Height = 600 };
        window.Show();

        host.Update(WorkspaceLayoutTree.Single(stranger));

        var texts = host.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty);
        Assert.Contains(texts, t => t.Contains("no longer available", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void AnEmptyArrangement_SaysSo_RatherThanRenderingNothing()
    {
        var host = new WorkspaceLayoutHost(BuildRegistry());
        var window = new Window { Content = host, Width = 800, Height = 600 };
        window.Show();

        host.Update(WorkspaceLayoutTree.Empty);

        Assert.Empty(host.TabGroups);
        var texts = host.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty);
        Assert.Contains(texts, t => t.Contains("closed", StringComparison.OrdinalIgnoreCase));
    }

    // ----------------------------------------------------------------
    // Gestures produce model operations
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public void ClickingATab_SelectsThatPanel_AndAnnouncesANewArrangement()
    {
        var tree = Default();
        tree = tree.Dock(Output, tree.FindGroupContaining(Inspector)!.Id, DockRelation.Into);

        var (host, _) = Show(tree);
        WorkspaceLayoutTree? announced = null;
        host.LayoutChanged += t => announced = t;

        var tabbed = host.TabGroups.Single(g => g.PanelIds.Count == 2);
        var inspectorTab = tabbed.GetLogicalDescendants().OfType<Button>()
            .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Inspector");

        inspectorTab.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.NotNull(announced);
        Assert.Equal(Inspector, announced!.FindGroupContaining(Inspector)!.SelectedPanelId);
    }

    [AvaloniaFact]
    public void ClosingAPanel_RemovesItFromTheArrangement()
    {
        var (host, _) = Show(Default());

        var inspectorGroup = host.TabGroups.Single(g => g.PanelIds.Contains(Inspector));
        var close = inspectorGroup.GetLogicalDescendants().OfType<Button>()
            .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Close Inspector");

        close.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.DoesNotContain(Inspector, host.Tree.AllPanels);
        Assert.Equal(2, host.TabGroups.Count);
    }

    [AvaloniaFact]
    public void APanelDeclaredUncloseable_OffersNoCloseButton()
    {
        var (host, _) = Show(Default());

        var documentGroup = host.TabGroups.Single(g => g.PanelIds.Contains(Document));
        var names = documentGroup.GetLogicalDescendants().OfType<Button>()
            .Select(b => Avalonia.Automation.AutomationProperties.GetName(b) ?? string.Empty);

        Assert.DoesNotContain(names, n => n.StartsWith("Close", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Collapse and auto-hide (`WP 10.2B` guarantees, preserved)
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public void ACollapsedPanel_ShrinksToItsStrip_AndExpandsBackInPlace()
    {
        var (host, _) = Show(Default().SetCollapsed(Explorer, true));

        var group = host.TabGroups.Single(g => g.PanelIds.Contains(Explorer));
        Assert.True(group.IsStripShowing);

        var expand = group.GetLogicalDescendants().OfType<Button>()
            .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Expand Explorer");
        expand.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.False(host.Tree.PresentationOf(Explorer).IsCollapsed);
        Assert.False(host.TabGroups.Single(g => g.PanelIds.Contains(Explorer)).IsStripShowing);
    }

    [AvaloniaFact]
    public void ACollapsedPane_TakesOnlyItsStripWidth_HandingTheRestBack()
    {
        var (host, _) = Show(Default().SetCollapsed(Explorer, true));

        var grid = host.GetLogicalDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count > 1);

        Assert.True(grid.ColumnDefinitions[0].Width.IsAbsolute);
        Assert.Equal(LayoutTabGroupView.StripSize, grid.ColumnDefinitions[0].Width.Value);
    }

    [AvaloniaFact]
    public void AnAutoHiddenPanel_ShowsAStrip_AndItsStripOpensAFlyoutRatherThanExpandingInPlace()
    {
        var (host, _) = Show(Default().SetPinned(Inspector, false));

        var group = host.TabGroups.Single(g => g.PanelIds.Contains(Inspector));
        Assert.True(group.IsStripShowing);

        var strip = group.GetLogicalDescendants().OfType<Button>()
            .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Expand Inspector");
        strip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.True(host.IsFlyoutOpen);
        Assert.Equal(Inspector, host.FlyoutPanelId);

        // Auto-hide is not the same as collapse: the panel is still unpinned.
        Assert.False(host.Tree.PresentationOf(Inspector).IsPinned);
    }

    [AvaloniaFact]
    public void HidingAFlyout_IsANoOp_WhenNoneIsOpen()
    {
        var (host, _) = Show(Default());

        host.HideFlyout();

        Assert.False(host.IsFlyoutOpen);
        Assert.Null(host.FlyoutPanelId);
    }

    [AvaloniaFact]
    public void RenderingANewArrangement_ClosesAnyOpenFlyout()
    {
        var (host, _) = Show(Default().SetPinned(Inspector, false));
        host.ShowFlyout(Inspector);
        Assert.True(host.IsFlyoutOpen);

        host.Update(Default());

        Assert.False(host.IsFlyoutOpen);
    }

    // ----------------------------------------------------------------
    // Responsive behaviour (`TD-70`, carried forward)
    // ----------------------------------------------------------------

    /// <summary>
    /// Resizing the window applies the responsive rule, with nothing
    /// calling <see cref="WorkspaceLayoutHost.ApplyResponsiveLayout"/>.
    /// </summary>
    /// <remarks>
    /// `TD-83` recorded exactly this smell against the old fixed grid: the
    /// responsive guarantee existed, was tested, and was invoked by nothing
    /// but its own tests, so the running application never applied it.
    /// `TD-72` closed that by subscribing the host to its own SizeChanged
    /// — and every responsive test still called the method directly, so
    /// deleting that one subscription line left all 260 Desktop tests green
    /// (mutation M1, this closure pass). The gap had been reintroduced one
    /// level up: the rule was wired, and nothing proved it. This test
    /// drives the real layout pass and never names the method, so it fails
    /// if the subscription is ever removed again.
    /// </remarks>
    [AvaloniaFact]
    public void ShrinkingTheWindow_AppliesTheResponsiveRule_WithoutAnyoneInvokingItDirectly()
    {
        var (host, _) = Show(Default());

        Assert.False(host.Tree.PresentationOf(Explorer).IsCollapsed);
        Assert.False(host.Tree.PresentationOf(Inspector).IsCollapsed);

        // A real resize, expressed the way the host actually experiences
        // one: its own bounds change during a layout pass. Driving the
        // Window's Width instead does nothing here — the headless harness
        // does not propagate it, and the host's bounds stay put, which is
        // how the first version of this test managed to fail against
        // correct code.
        host.Measure(new Size(600, 800));
        host.Arrange(new Rect(0, 0, 600, 800));

        Assert.True(
            host.Tree.PresentationOf(Explorer).IsCollapsed || host.Tree.PresentationOf(Inspector).IsCollapsed,
            "Shrinking the window must apply the responsive rule through the host's own SizeChanged subscription.");
    }

    [AvaloniaTheory]
    [InlineData(1920.0)]
    [InlineData(1600.0)]
    [InlineData(1366.0)]
    [InlineData(1280.0)]
    [InlineData(1024.0)]
    [InlineData(960.0)]
    [InlineData(800.0)]
    public void AtEveryRealisticWidth_TheWorkingPaneKeepsAUsableShare(double width)
    {
        var (host, _) = Show(Default());

        host.ApplyResponsiveLayout(width, 800);

        var root = (LayoutSplitNode)host.Tree.Root!;
        var documentIndex = root.Children.ToList().FindIndex(c => c.Panels.Contains(Document));

        // Either the document pane still has its minimum, or the side
        // panels have been collapsed to strips to give it back — never a
        // squeezed working pane with full-width side docks.
        var documentWidth = root.Weights[documentIndex] * width;
        var sidePanelsCollapsed =
            host.Tree.PresentationOf(Explorer).IsCollapsed || host.Tree.PresentationOf(Inspector).IsCollapsed;

        Assert.True(
            documentWidth >= WorkspaceLayoutHost.MinPrimaryPaneWidth || sidePanelsCollapsed,
            $"At {width}px the working pane had {documentWidth}px and no side panel gave way.");
    }

    [AvaloniaFact]
    public void OnAWideWindow_NothingIsCollapsedOnTheUsersBehalf()
    {
        var (host, _) = Show(Default());

        host.ApplyResponsiveLayout(1920, 1080);

        Assert.False(host.Tree.PresentationOf(Explorer).IsCollapsed);
        Assert.False(host.Tree.PresentationOf(Inspector).IsCollapsed);
    }

    [AvaloniaFact]
    public void APanelCollapsedForSpace_IsRestored_WhenTheRoomComesBack()
    {
        var (host, _) = Show(Default());

        // 700px is exactly the boundary — a 0.6 share is precisely the
        // minimum working width and each 0.2 side share is precisely the
        // minimum usable panel width — so the squeeze has to be below it.
        host.ApplyResponsiveLayout(600, 800);
        var collapsedWhenNarrow =
            host.Tree.PresentationOf(Explorer).IsCollapsed || host.Tree.PresentationOf(Inspector).IsCollapsed;

        host.ApplyResponsiveLayout(1920, 1080);

        Assert.True(collapsedWhenNarrow, "A narrow window must collapse a side panel to protect the working pane.");
        Assert.False(host.Tree.PresentationOf(Explorer).IsCollapsed);
        Assert.False(host.Tree.PresentationOf(Inspector).IsCollapsed);
    }

    [AvaloniaFact]
    public void APanelTheUserCollapsedThemselves_StaysCollapsedWhenTheWindowGrows()
    {
        // The layout may collapse a panel on the user's behalf and undo
        // that later; it must never undo a decision the user made.
        var (host, _) = Show(Default().SetCollapsed(Explorer, true));

        host.ApplyResponsiveLayout(1920, 1080);

        Assert.True(host.Tree.PresentationOf(Explorer).IsCollapsed);
    }
}
