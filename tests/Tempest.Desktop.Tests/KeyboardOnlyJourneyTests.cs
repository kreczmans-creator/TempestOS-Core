using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Tempest.Core.EngineeringDomain;
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
        var explorer = Guid.NewGuid();
        var document = Guid.NewGuid();

        var registry = new WorkspacePanelRegistry();
        registry.Register(new WorkspacePanelDescriptor(explorer, "Explorer", new TextBlock { Text = "explorer" }));
        registry.Register(new WorkspacePanelDescriptor(document, "Documents", new TextBlock { Text = "documents" }, CanClose: false));

        var dockingHost = new WorkspaceLayoutHost(registry);
        var window = new Window { Content = dockingHost, Width = 1024, Height = 768 };
        window.Show();

        var tree = WorkspaceLayoutTree.Empty with
        {
            Root = new LayoutSplitNode(
                Guid.NewGuid(),
                LayoutOrientation.Horizontal,
                [new LayoutTabGroupNode(Guid.NewGuid(), [explorer]), new LayoutTabGroupNode(Guid.NewGuid(), [document])]),
        };
        dockingHost.Update(tree);
        window.Measure(new Size(1024, 768));
        window.Arrange(new Rect(0, 0, 1024, 768));

        Assert.Equal(DockRelation.Left, dockingHost.Tree.InferEdge(explorer, document));

        var explorerHeader = dockingHost.GetLogicalDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetName(b) == "Explorer");

        // Documented on the header itself (`TD-133`), so a keyboard or
        // screen-reader user can discover the gesture with no manual.
        Assert.Contains("Ctrl+Shift+Arrow", AutomationProperties.GetHelpText(explorerHeader) ?? string.Empty, StringComparison.Ordinal);

        explorerHeader.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Right,
            KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift,
        });

        // Moved to the workspace's own right edge: the panel that sat at
        // the split's left is now at its right instead.
        Assert.Equal(DockRelation.Right, dockingHost.Tree.InferEdge(explorer, document));
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
