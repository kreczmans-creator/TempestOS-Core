using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Settings;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Docking;
using Tempest.Workspace;
using Tempest.Workspace.Layout;
using Tempest.Workspace.Mechanical;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.2B` acceptance 3: a keyboard-only journey selects a Digital
/// Thread edge and moves a docked panel — one real, end-to-end keyboard
/// interaction per half, never a simulated pointer event, proving `TD-128`
/// (edge keyboard reach) and `TD-133` (keyboard docking moves).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class KeyboardOnlyJourneyTests
{
    [AvaloniaFact]
    public async Task AKeyboardOnlyJourney_SelectsADigitalThreadEdge()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = await DigitalThreadGraphView.TryCreateAsync(target.Id, target.Kind!, domainContext, (_, _) => { });
            Assert.NotNull(view);

            // A real, shown TopLevel — real bubble routing from the edge's
            // own hit-test `Line` up to the graph root's own `KeyDown`
            // handler needs one.
            var window = new Window { Content = view };
            window.Show();

            if (view!.Model.Edges.Count == 0)
                return; // no relationships on this particular sample object — honestly nothing to prove here.

            var edge = view.Model.Edges[0];
            var sourceName = view.Model.Nodes.First(n => n.ObjectId == edge.SourceId).DisplayName;
            var targetName = view.Model.Nodes.First(n => n.ObjectId == edge.TargetId).DisplayName;

            var hitTestLine = view.GetLogicalDescendants().OfType<Line>()
                .Single(l => l.Tag is DigitalThreadEdgeSnapshot snapshot && snapshot.Equals(edge));

            // Keyboard reach (`TD-128`): a real Tab stop with a real,
            // "<from> → <to>" accessible name — not merely clickable.
            Assert.True(hitTestLine.Focusable);
            Assert.Equal($"{sourceName} → {targetName}", AutomationProperties.GetName(hitTestLine));

            Assert.Null(view.Model.SelectedEdge);

            hitTestLine.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal(edge, view.Model.SelectedEdge);

            // "Enter selects the edge's target": keyboard focus moves on
            // to the node the edge points at, so a keyboard-only user can
            // keep exploring outward with no pointer input at all.
            var targetBorder = FindNodeBorder(view, edge.TargetId, targetName);
            Assert.True(targetBorder.IsFocused);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public void AKeyboardOnlyJourney_MovesADockedPanelToTheOppositeEdge()
    {
        var rig = BuildDockingRig(new LayoutSplitNode(
            Guid.NewGuid(),
            LayoutOrientation.Horizontal,
            [new LayoutTabGroupNode(Guid.NewGuid(), [Explorer]), new LayoutTabGroupNode(Guid.NewGuid(), [Document])]));

        Assert.Equal(DockRelation.Left, rig.Controller.Tree.InferEdge(Explorer, Document));

        var explorerHeader = rig.Controller.Host.FindPanelHeader(Explorer)!;

        // Documented on the header itself (`TD-133`, `ADR-0153` decision
        // 8), so a keyboard or screen-reader user can discover every one
        // of these gestures with no manual.
        var help = AutomationProperties.GetHelpText(explorerHeader) ?? string.Empty;
        Assert.Contains("Ctrl+Shift+Arrow", help, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Shift+,", help, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Shift+.", help, StringComparison.Ordinal);

        FocusAndSettle(explorerHeader);
        Assert.True(explorerHeader.IsFocused);

        PressGesture(explorerHeader, Key.Right);

        // Moved to the workspace's own right edge: the panel that sat at
        // the split's left is now at its right instead.
        Assert.Equal(DockRelation.Right, rig.Controller.Tree.InferEdge(Explorer, Document));

        // `PHYSICAL_REVIEW` §7j K3 / `ADR-0153` decision 7: the header the
        // user was operating is destroyed by the re-render this gesture
        // causes, so the journey only continues if focus lands on its
        // replacement. Before `WP 21.0K` it did not — the gesture was
        // applied by the host, never reaching the controller's own restore.
        var moved = rig.Controller.Host.FindPanelHeader(Explorer);
        Assert.NotNull(moved);
        Assert.True(moved!.IsFocused);
    }

    /// <summary>
    /// `ADR-0153` decision 8, closing `TD-133`'s own named residual: with
    /// a docked panel's tab header focused, <c>Ctrl+Shift+,</c> and
    /// <c>Ctrl+Shift+.</c> walk that tab along its own strip — no pointer,
    /// and focus still on the tab that moved, so a second press continues
    /// the walk.
    /// </summary>
    [AvaloniaFact]
    public void AKeyboardOnlyJourney_ReordersATabWithinItsOwnGroup()
    {
        var group = Guid.NewGuid();
        var rig = BuildDockingRig(new LayoutTabGroupNode(group, [Explorer, Document, Inspector]));

        var explorerHeader = rig.Controller.Host.FindPanelHeader(Explorer)!;
        FocusAndSettle(explorerHeader);
        Assert.True(explorerHeader.IsFocused);

        PressGesture(explorerHeader, Key.OemPeriod);

        Assert.Equal([Document, Explorer, Inspector], TabOrder(rig, group));
        var afterFirst = rig.Controller.Host.FindPanelHeader(Explorer);
        Assert.NotNull(afterFirst);
        Assert.True(afterFirst!.IsFocused);

        // The rendered strip, not only the model: the header the user now
        // sees second is the Explorer's own.
        Assert.Equal([Document, Explorer, Inspector], rig.Controller.Host.TabGroups.Single().PanelIds);

        PressGesture(afterFirst, Key.OemPeriod);
        Assert.Equal([Document, Inspector, Explorer], TabOrder(rig, group));

        // Off the end: nothing moves, nothing is lost, focus is not thrown
        // away either.
        PressGesture(rig.Controller.Host.FindPanelHeader(Explorer)!, Key.OemPeriod);
        Assert.Equal([Document, Inspector, Explorer], TabOrder(rig, group));
        Assert.True(rig.Controller.Host.FindPanelHeader(Explorer)!.IsFocused);

        PressGesture(rig.Controller.Host.FindPanelHeader(Explorer)!, Key.OemComma);
        Assert.Equal([Document, Explorer, Inspector], TabOrder(rig, group));
        Assert.True(rig.Controller.Host.FindPanelHeader(Explorer)!.IsFocused);
    }

    private static readonly Guid Explorer = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid Document = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000b");
    private static readonly Guid Inspector = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000c");

    private sealed record DockingRig(WorkspaceLayoutController Controller, Window Window);

    /// <summary>
    /// The real production shape: a <see cref="WorkspaceLayoutController"/>
    /// owning the arrangement, its <see cref="WorkspaceLayoutHost"/> inside
    /// a real, shown, activated window laid out for real — a bare host on
    /// its own is not something this product ever builds, and a keyboard
    /// gesture's focus behaviour is only meaningful against the owner that
    /// restores it.
    /// </summary>
    private static DockingRig BuildDockingRig(WorkspaceLayoutNode root)
    {
        var registry = new WorkspacePanelRegistry();
        registry.Register(new WorkspacePanelDescriptor(Explorer, "Explorer", new TextBlock { Text = "explorer" }));
        registry.Register(new WorkspacePanelDescriptor(Document, "Documents", new TextBlock { Text = "documents" }, CanClose: false));
        registry.Register(new WorkspacePanelDescriptor(Inspector, "Inspector", new TextBlock { Text = "inspector" }));

        var controller = new WorkspaceLayoutController(
            registry,
            new WorkspaceLayoutStore(new SettingsProvider(new InMemoryPersistenceStore(), new EventBus())));

        var window = new Window { Content = controller.Host, Width = 1024, Height = 768 };
        window.Show();
        window.Activate();

        controller.Load(WorkspaceLayoutTree.Empty.WithPrimaryRoot(root));
        Dispatcher.UIThread.RunJobs();
        controller.Host.Measure(new Size(1024, 768));
        controller.Host.Arrange(new Rect(0, 0, 1024, 768));
        Dispatcher.UIThread.RunJobs();

        return new DockingRig(controller, window);
    }

    private static IReadOnlyList<Guid> TabOrder(DockingRig rig, Guid groupId) =>
        rig.Controller.Tree.FindNode(groupId) is LayoutTabGroupNode group ? group.PanelIds : [];

    /// <summary>A real <c>Ctrl+Shift+</c><paramref name="key"/> press, settled the way the re-render it causes settles.</summary>
    private static void PressGesture(Control header, Key key)
    {
        header.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift,
        });

        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Focuses <paramref name="control"/> and pumps the dispatcher until <see cref="Control.IsFocused"/> reflects it (headless Avalonia updates the <c>:focus</c> pseudo-class through a second, separately-queued job).</summary>
    private static void FocusAndSettle(Control control)
    {
        control.Focus();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static Border FindNodeBorder(DigitalThreadGraphView view, Guid objectId, string displayName) =>
        view.GetLogicalDescendants().OfType<Border>()
            .Single(b => (b.Width is 158 or 188) && b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == displayName) && Equals(b.Tag, objectId));

    private static async Task<(EngineeringDomainContext DomainContext, IEngineeringObject Target)> GetRealMechanicalObjectAsync(WorkspaceHost host)
    {
        var workspace = host.Workspace!;
        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var objectNode = await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots);
        Assert.NotNull(objectNode);

        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        var target = await domainContext.Repository.FindAsync(objectNode!.Id);
        Assert.NotNull(target);

        return (domainContext, target!);
    }

    private static async Task<ProjectExplorerNode?> FindFirstObjectNodeAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstObjectNodeAsync(explorer, await explorer.GetChildrenAsync(node.Id));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }
}
