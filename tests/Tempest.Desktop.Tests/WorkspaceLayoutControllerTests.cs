using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.App.Workspace.Layout;
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
        first.Controller.Apply(t => t.Float(Inspector, -900, 40, 460, 340));
        first.Controller.Apply(t => t.SetCollapsed(Explorer, true));
        await first.Controller.SaveAsync();

        var second = BuildRig(settings);
        await second.Controller.RestoreAsync(WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output));

        Assert.Contains(Output, second.Controller.Tree.DockedPanels);
        Assert.True(second.Controller.Tree.IsFloating(Inspector));
        Assert.True(second.Controller.Tree.PresentationOf(Explorer).IsCollapsed);
        Assert.Equal(-900, second.Controller.Tree.Floating.Single().X);
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
