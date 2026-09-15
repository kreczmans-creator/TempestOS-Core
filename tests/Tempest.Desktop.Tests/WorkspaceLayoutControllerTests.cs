using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.Workspace.Layout;
using Tempest.Core.Events;
using Tempest.Core.Settings;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The one owner of the workspace arrangement (`TD-72`): drag-to-dock,
/// undock-to-float, and persistence.
/// </summary>
/// <remarks>
/// Every gesture here is exercised through the controller's own public
/// operations rather than by synthesising raw pointer input, because what
/// must be proven is that a gesture produces the right <em>model</em>
/// change — the renderer is then a pure function of that, and is proven
/// separately.
/// </remarks>
public sealed class WorkspaceLayoutControllerTests
{
    private static readonly Guid Explorer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Document = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Inspector = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Output = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private sealed record Rig(WorkspaceLayoutController Controller, Window Window, List<FloatingLayoutWindow> Floated);

    private static Rig BuildRig(ISettingsProvider? settings = null)
    {
        var registry = new WorkspacePanelRegistry();
        registry.Register(new WorkspacePanelDescriptor(Explorer, "Explorer", new TextBlock()));
        registry.Register(new WorkspacePanelDescriptor(Document, "Documents", new TextBlock(), CanClose: false));
        registry.Register(new WorkspacePanelDescriptor(Inspector, "Inspector", new TextBlock()));
        registry.Register(new WorkspacePanelDescriptor(Output, "Output", new TextBlock()));

        var store = new WorkspaceLayoutStore(settings ?? NewSettings());
        var floated = new List<FloatingLayoutWindow>();

        // A recording factory: floating is observed as a model change plus
        // a window request, without opening real top-level windows in a
        // headless run.
        var controller = new WorkspaceLayoutController(registry, store, model =>
        {
            floated.Add(model);
            return new FloatingPanelWindow(model, registry);
        });

        var window = new Window { Content = controller.Host, Width = 1280, Height = 800 };
        window.Show();
        controller.Load(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));
        // Force a real layout pass so the rendered panes have genuine
        // bounds — drop targeting is geometry, so a test of it against
        // zero-sized panes would prove nothing.
        //
        // WP 16.5B: Avalonia 11.3.20's headless backend defers part of the
        // layout pass onto the dispatcher queue, where 11.2.3 applied it
        // synchronously within Measure/Arrange; without draining it first,
        // `Bounds` on the tab groups below reads back as a zero-sized rect
        // at the origin. `Dispatcher.UIThread.RunJobs()` is this
        // repository's own established drain for exactly this
        // (`ProjectTaskAcceptanceTests.LayOutAsync`, `UndoRedoThreadingTests`).
        Dispatcher.UIThread.RunJobs();
        controller.Host.Measure(new Size(1280, 800));
        controller.Host.Arrange(new Rect(0, 0, 1280, 800));
        Dispatcher.UIThread.RunJobs();

        return new Rig(controller, window, floated);
    }

    private static ISettingsProvider NewSettings() =>
        new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

    // ----------------------------------------------------------------
    // Drag to dock
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public void DraggingAPanelOntoAnothersCentre_TabsThemTogether()
    {
        var rig = BuildRig();
        var explorerGroup = rig.Controller.Tree.FindGroupContaining(Explorer)!;

        rig.Controller.Apply(t => t.Dock(Inspector, explorerGroup.Id, DockRelation.Into));

        var group = rig.Controller.Tree.FindGroupContaining(Inspector)!;
        Assert.Equal([Explorer, Inspector], group.PanelIds);
        Assert.Equal(2, rig.Controller.Host.TabGroups.Count);
    }

    [AvaloniaFact]
    public void DraggingAPanelOntoAnothersEdge_SplitsTowardsThatEdge()
    {
        var rig = BuildRig();
        var documentGroup = rig.Controller.Tree.FindGroupContaining(Document)!;

        rig.Controller.Apply(t => t.Dock(Output, documentGroup.Id, DockRelation.Below));

        var vertical = rig.Controller.Tree.Root!.DescendantsAndSelf
            .OfType<LayoutSplitNode>()
            .Single(s => s.Orientation == LayoutOrientation.Vertical);

        Assert.Equal([Document, Output], vertical.Panels);
        Assert.Equal(4, rig.Controller.Host.TabGroups.Count);
    }

    [AvaloniaFact]
    public void APressWithoutMovement_IsNotADrag_SoClickingATabNeverRedocksIt()
    {
        var rig = BuildRig();

        // Below the threshold, the gesture resolves to nothing at all.
        Assert.Null(rig.Controller.UpdateDrag(new Point(10, 10)));
        Assert.False(rig.Controller.IsDragging);
        Assert.Null(rig.Controller.DraggingPanelId);
    }

    [AvaloniaFact]
    public void EveryRenderedPane_IsADropCandidate_WithARealRectangle()
    {
        var rig = BuildRig();

        var candidates = rig.Controller.CurrentCandidates();

        Assert.Equal(3, candidates.Count);
        Assert.All(candidates, c => Assert.True(c.Width > 0 && c.Height > 0));

        // The three panes tile the window left to right, in layout order.
        var ordered = candidates.OrderBy(c => c.X).ToList();
        Assert.True(ordered[0].X < ordered[1].X && ordered[1].X < ordered[2].X);
    }

    // ----------------------------------------------------------------
    // Floating
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public void UndockingAPanel_OpensAWindowForIt_AndRemovesItFromTheDockedTree()
    {
        var rig = BuildRig();

        rig.Controller.Apply(t => t.Float(Inspector, 300, 200, 420, 320));

        Assert.DoesNotContain(Inspector, rig.Controller.Tree.DockedPanels);
        Assert.True(rig.Controller.Tree.IsFloating(Inspector));
        Assert.Single(rig.Floated);
        Assert.Single(rig.Controller.FloatingWindows);
        Assert.Equal(2, rig.Controller.Host.TabGroups.Count);
    }

    [AvaloniaFact]
    public void AFloatingPanel_CanBeDockedBackIn_AndItsWindowIsClosed()
    {
        var rig = BuildRig();
        rig.Controller.Apply(t => t.Float(Inspector, 300, 200, 420, 320));

        var explorerGroup = rig.Controller.Tree.FindGroupContaining(Explorer)!;
        rig.Controller.Apply(t => t.Dock(Inspector, explorerGroup.Id, DockRelation.Into));

        Assert.False(rig.Controller.Tree.IsFloating(Inspector));
        Assert.Empty(rig.Controller.FloatingWindows);
        Assert.Contains(Inspector, rig.Controller.Tree.DockedPanels);
    }

    [AvaloniaFact]
    public void MovingAFloatingWindow_IsRecordedInScreenCoordinates_SoASecondMonitorSurvives()
    {
        var rig = BuildRig();
        rig.Controller.Apply(t => t.Float(Inspector, 300, 200, 420, 320));
        var windowId = rig.Controller.Tree.Floating.Single().Id;

        rig.Controller.Apply(t => t.MoveFloating(windowId, -1800, 60, 500, 400));

        var model = rig.Controller.Tree.Floating.Single();
        Assert.Equal(-1800, model.X);
        Assert.Equal(60, model.Y);
    }

    // ----------------------------------------------------------------
    // A panel can never be lost (`WP 20.10D`, PO finding T4)
    // ----------------------------------------------------------------

    /// <summary>
    /// Read T4 literally: docking the Requirements tree beside a
    /// requirement's editor "disappeared somewhere and broken away". Before
    /// this Work Package, every release outside every drop-target candidate
    /// floated the dragged panel — including a one-pixel miss in the 4px
    /// splitter gutter between two panes, indistinguishable from someone
    /// actually tearing it out. Released inside the workspace but over no
    /// target must instead change nothing.
    /// </summary>
    [AvaloniaFact]
    public void AnAccidentalMissInsideTheWorkspace_ChangesNothing_AndAnnouncesIt()
    {
        var rig = BuildRig();
        var announcements = new List<string>();
        rig.Controller.Announced += announcements.Add;

        var candidates = rig.Controller.CurrentCandidates().OrderBy(c => c.X).ToList();
        var gapX = (candidates[0].X + candidates[0].Width + candidates[1].X) / 2;
        var gapY = candidates[0].Y + candidates[0].Height / 2;

        rig.Controller.BeginDrag(Explorer);
        rig.Controller.CompleteDrag(new Point(gapX, gapY));

        Assert.False(rig.Controller.Tree.IsFloating(Explorer));
        Assert.Contains(Explorer, rig.Controller.Tree.DockedPanels);
        Assert.Empty(rig.Controller.FloatingWindows);
        var announcement = Assert.Single(announcements);
        Assert.Contains("stays where it was", announcement, StringComparison.Ordinal);
    }

    /// <summary>
    /// The one deliberate gesture that still means "give this its own
    /// window": released past the workspace's own edge entirely, not
    /// merely over no candidate.
    /// </summary>
    [AvaloniaFact]
    public void ATearOutPastTheWorkspacesOwnEdge_FloatsThePanel_AndAnnouncesIt()
    {
        var rig = BuildRig();
        var announcements = new List<string>();
        rig.Controller.Announced += announcements.Add;

        rig.Controller.BeginDrag(Explorer);
        rig.Controller.CompleteDrag(new Point(-50, 100));

        Assert.True(rig.Controller.Tree.IsFloating(Explorer));
        Assert.Single(rig.Controller.FloatingWindows);
        var announcement = Assert.Single(announcements);
        Assert.Contains("undocked into its own window", announcement, StringComparison.Ordinal);
    }

    /// <summary>
    /// The floating window's own OS-level close (the title bar's close
    /// button — never routed through the model directly) redocks its
    /// content rather than discarding it, at the position it was docked at
    /// before it floated — Document | Inspector, the arrangement it tore
    /// out of.
    /// </summary>
    [AvaloniaFact]
    public void ClosingAFloatingWindowFromOutsideTheModel_RedocksItsContent_AtItsLastPosition()
    {
        var rig = BuildRig();
        rig.Controller.BeginDrag(Inspector);
        rig.Controller.CompleteDrag(new Point(-50, 100));
        Assert.True(rig.Controller.Tree.IsFloating(Inspector));

        var windowId = rig.Controller.Tree.Floating.Single().Id;
        var window = rig.Controller.FloatingWindows[windowId];

        window.Close();

        Assert.False(rig.Controller.Tree.IsFloating(Inspector));
        Assert.Contains(Inspector, rig.Controller.Tree.DockedPanels);
        Assert.Empty(rig.Controller.FloatingWindows);

        var order = rig.Controller.Tree.Root!.Panels.ToList();
        Assert.True(order.IndexOf(Inspector) > order.IndexOf(Document), "Inspector was not redocked beside Document, where it started.");
    }

    /// <summary>The "Show Panel" command's own half of "never lost": redocks a floating panel at its remembered position.</summary>
    [AvaloniaFact]
    public void RedockFloating_DocksAFloatingPanelBackIn_AndClosesItsWindow()
    {
        var rig = BuildRig();
        rig.Controller.BeginDrag(Explorer);
        rig.Controller.CompleteDrag(new Point(-50, 100));
        Assert.True(rig.Controller.Tree.IsFloating(Explorer));

        rig.Controller.RedockFloating(Explorer);

        Assert.False(rig.Controller.Tree.IsFloating(Explorer));
        Assert.Contains(Explorer, rig.Controller.Tree.DockedPanels);
        Assert.Empty(rig.Controller.FloatingWindows);
    }

    /// <summary>A panel already docked is unaffected by "Show Panel"'s own redock path — nothing to redock.</summary>
    [AvaloniaFact]
    public void RedockFloating_OnAPanelThatIsNotFloating_IsANoOp()
    {
        var rig = BuildRig();
        var before = rig.Controller.Tree;

        rig.Controller.RedockFloating(Explorer);

        Assert.Same(before, rig.Controller.Tree);
    }

    /// <summary>
    /// Reset Layout must be able to answer "where did my panel go" with
    /// "it's docked" for every panel that existed — not only the ones the
    /// default preset happens to know about (a floating attachment viewer,
    /// registered long after the default was fixed, say).
    /// </summary>
    [AvaloniaFact]
    public void ResetTo_FoldsAPanelTheDefaultTreeDoesNotPlace_BackIntoTheDockedTree()
    {
        var registry = new WorkspacePanelRegistry();
        registry.Register(new WorkspacePanelDescriptor(Explorer, "Explorer", new TextBlock()));
        registry.Register(new WorkspacePanelDescriptor(Document, "Documents", new TextBlock(), CanClose: false));
        registry.Register(new WorkspacePanelDescriptor(Inspector, "Inspector", new TextBlock()));
        registry.Register(new WorkspacePanelDescriptor(Output, "Output", new TextBlock()));
        var stranger = Guid.NewGuid();
        registry.Register(new WorkspacePanelDescriptor(stranger, "Attachment", new TextBlock()));

        var controller = new WorkspaceLayoutController(registry, new WorkspaceLayoutStore(NewSettings()), model => new FloatingPanelWindow(model, registry));
        var window = new Window { Content = controller.Host, Width = 1280, Height = 800 };
        window.Show();
        controller.Load(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

        controller.Apply(t => t.Dock(stranger, t.FindGroupContaining(Document)!.Id, DockRelation.Below));
        controller.Apply(t => t.Float(stranger, 300, 200, 420, 320));
        Assert.True(controller.Tree.IsFloating(stranger));

        controller.ResetTo(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

        Assert.False(controller.Tree.IsFloating(stranger));
        Assert.Contains(stranger, controller.Tree.AllPanels);
        Assert.Empty(controller.FloatingWindows);
        Assert.Contains(Explorer, controller.Tree.DockedPanels);
    }

    /// <summary>
    /// A saved arrangement can name a floating window's own bounds from a
    /// monitor that is no longer connected; restoring it must still place
    /// the window somewhere reachable rather than off whatever screen the
    /// machine actually has now.
    /// </summary>
    [AvaloniaFact]
    public async Task RestoringASavedFloatingWindow_WithBoundsOffAnyRealScreen_PlacesItOnAScreenAnyway()
    {
        var settings = NewSettings();
        var first = BuildRig(settings);
        first.Controller.Apply(t => t.Float(Inspector, -50000, -50000, 420, 320));
        await first.Controller.SaveAsync();

        var second = BuildRig(settings);
        await second.Controller.RestoreAsync(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

        Assert.True(second.Controller.Tree.IsFloating(Inspector));
        var windowId = second.Controller.Tree.Floating.Single().Id;
        var window = second.Controller.FloatingWindows[windowId];

        // Headless reports one screen; the clamp's own off-screen branch is
        // proved directly in FloatingWindowPlacementTests — this proves the
        // wiring calls it at all, for the restore path and not only a drag.
        var screen = window.Screens.All.Single();
        var bounds = new PixelRect(window.Position.X, window.Position.Y, (int)window.Width, (int)window.Height);
        Assert.True(screen.WorkingArea.Intersects(bounds), $"Restored floating window at {bounds} does not land on the one real screen {screen.WorkingArea}.");
    }

    // ----------------------------------------------------------------
    // Toggling panels
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public void TogglingAPanel_RemovesItThenRestoresItToItsOwnEdge()
    {
        var rig = BuildRig();

        rig.Controller.TogglePanel(Inspector, DockRelation.Right);
        Assert.False(rig.Controller.IsPanelVisible(Inspector));

        rig.Controller.TogglePanel(Inspector, DockRelation.Right);
        Assert.True(rig.Controller.IsPanelVisible(Inspector));

        var order = rig.Controller.Tree.Root!.Panels.ToList();
        Assert.True(order.IndexOf(Inspector) > order.IndexOf(Document), "A restored Inspector belongs on the right of the document.");
    }

    // ----------------------------------------------------------------
    // Persistence
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task AnArrangement_SurvivesASaveAndRestore_Exactly()
    {
        var settings = NewSettings();
        var first = BuildRig(settings);

        first.Controller.Apply(t => t.Dock(Output, t.FindGroupContaining(Document)!.Id, DockRelation.Below));
        // A position that genuinely lands on a real screen (`WP 20.10D`'s
        // own off-screen clamp — proved separately by
        // RestoringASavedFloatingWindow_WithBoundsOffAnyRealScreen_PlacesItOnAScreenAnyway
        // — would otherwise reposition this one and this test would no
        // longer be proving round-trip fidelity).
        first.Controller.Apply(t => t.Float(Inspector, 300, 40, 460, 340));
        first.Controller.Apply(t => t.SetCollapsed(Explorer, true));
        await first.Controller.SaveAsync();

        var second = BuildRig(settings);
        await second.Controller.RestoreAsync(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

        Assert.Contains(Output, second.Controller.Tree.DockedPanels);
        Assert.True(second.Controller.Tree.IsFloating(Inspector));
        Assert.True(second.Controller.Tree.PresentationOf(Explorer).IsCollapsed);
        Assert.Equal(300, second.Controller.Tree.Floating.Single().X);
    }

    [AvaloniaFact]
    public async Task WithNothingSaved_TheFallbackArrangementIsUsed()
    {
        var rig = BuildRig();
        var fallback = WorkspaceLayoutPresets.Build(WorkspaceLayoutPreset.Review, Explorer, Document, Inspector, Output);

        await rig.Controller.RestoreAsync(fallback);

        Assert.Contains(Output, rig.Controller.Tree.AllPanels);
    }

    [AvaloniaFact]
    public async Task ASavedLayoutNamingAPanelThisBuildNoLongerHas_StillOpens_WithoutThatPanel()
    {
        var settings = NewSettings();
        var stranger = Guid.NewGuid();

        // A layout written by a build that had one more panel than this one.
        var store = new WorkspaceLayoutStore(settings);
        var saved = WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output);
        saved = saved.Dock(stranger, saved.FindGroupContaining(Document)!.Id, DockRelation.Below);
        await store.SaveAsync(saved);

        var rig = BuildRig(settings);
        await rig.Controller.RestoreAsync(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

        Assert.DoesNotContain(stranger, rig.Controller.Tree.AllPanels);
        Assert.Contains(Document, rig.Controller.Tree.AllPanels);
        Assert.Contains(Explorer, rig.Controller.Tree.AllPanels);
    }

    [AvaloniaFact]
    public async Task ACorruptSavedLayout_FallsBackRatherThanOpeningAnEmptyWorkspace()
    {
        var settings = NewSettings();
        settings.RegisterDefinition(new SettingDefinition(WorkspaceLayoutStore.SettingKey, "Workspace Layout", string.Empty));
        await settings.SetValueAsync(WorkspaceLayoutStore.SettingKey, "{ not json at all");

        var rig = BuildRig(settings);
        await rig.Controller.RestoreAsync(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

        Assert.Contains(Document, rig.Controller.Tree.AllPanels);
        Assert.NotNull(rig.Controller.Tree.Root);
    }

    [AvaloniaFact]
    public async Task ASavedLayoutWhoseEveryPanelHasGone_FallsBackToTheDefault()
    {
        var settings = NewSettings();
        var store = new WorkspaceLayoutStore(settings);
        await store.SaveAsync(WorkspaceLayoutTree.Single(Guid.NewGuid()));

        var rig = BuildRig(settings);
        await rig.Controller.RestoreAsync(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

        Assert.Contains(Document, rig.Controller.Tree.AllPanels);
        Assert.Contains(Explorer, rig.Controller.Tree.AllPanels);
    }
}
