using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Project Explorer's per-row child counts (`WP-Z4` Productisation
/// Phase 1, backlog item 3) — no App-layer change was needed:
/// <see cref="ProjectExplorerView.LoadAsync"/> already loads the whole
/// tree eagerly, so every node's real child count was already sitting in
/// its own <see cref="ExplorerNodeItem.Children"/> by the time it renders.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectExplorerChildCountTests
{
    [AvaloniaFact]
    public async Task LoadAsync_AnyNodeWithChildren_DisplaysItsRealChildCount()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

            var view = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!);
            await view.LoadAsync();

            var tree = view.GetLogicalDescendants().OfType<TreeView>().Single();
            var roots = (IReadOnlyList<ExplorerNodeItem>)tree.ItemsSource!;
            var withChildren = FindFirstWithChildren(roots);
            if (withChildren is null)
                return; // no container node in this sample set — honestly nothing to prove here.

            // The count in the row's own display text must equal the real,
            // independently-fetched child count for that exact node — not
            // merely "some number", and not the count of a different node.
            var realChildren = await workspace.ProjectExplorer.GetChildrenAsync(withChildren.Node.Id);
            Assert.Equal($"({realChildren.Count})", withChildren.Display[withChildren.Display.LastIndexOf('(')..]);
            Assert.Equal(realChildren.Count, withChildren.Children.Count);

            // A leaf (no children) never carries a parenthesised count at all.
            var leaf = FindFirstLeaf(roots);
            if (leaf is not null)
                Assert.DoesNotContain("(", leaf.Display, StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static ExplorerNodeItem? FindFirstWithChildren(IEnumerable<ExplorerNodeItem> items)
    {
        foreach (var item in items)
        {
            if (item.Node.HasChildren)
                return item;

            var found = FindFirstWithChildren(item.Children);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static ExplorerNodeItem? FindFirstLeaf(IEnumerable<ExplorerNodeItem> items)
    {
        foreach (var item in items)
        {
            if (!item.Node.HasChildren)
                return item;

            var found = FindFirstLeaf(item.Children);
            if (found is not null)
                return found;
        }

        return null;
    }
}
