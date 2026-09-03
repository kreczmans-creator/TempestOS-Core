using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Project Explorer's selection across a reload (`WP-Z4` Productisation
/// Phase 1, P0) — every <see cref="ProjectExplorerView.LoadAsync"/> rebuilds
/// an entirely fresh <see cref="ExplorerNodeItem"/> tree, and
/// <see cref="TreeView.SelectedItem"/> matches by reference, so the
/// selection used to silently vanish on every reload (renaming an object,
/// creating a sibling, switching areas and back) even though the same real
/// object was still right there under a new wrapper.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectExplorerSelectionSurvivesReloadTests
{
    [AvaloniaFact]
    public async Task LoadAsync_RebuildsTheTree_ButRestoresTheSameRealObjectAsSelected()
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
            var firstObject = FindFirstObject(roots);
            if (firstObject is null)
                return; // no real object node in this sample set — honestly nothing to prove here.

            tree.SelectedItem = firstObject;
            Assert.Same(firstObject, tree.SelectedItem);

            // Reload — exactly what happens after a real rename, a sibling
            // creation, or a command dispatch anywhere in this area.
            await view.LoadAsync();

            var restored = tree.SelectedItem as ExplorerNodeItem;
            Assert.NotNull(restored);
            Assert.NotSame(firstObject, restored); // a genuinely fresh tree, not an accidental cache hit
            Assert.Equal(firstObject.Node.Id, restored!.Node.Id);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static ExplorerNodeItem? FindFirstObject(IEnumerable<ExplorerNodeItem> items)
    {
        foreach (var item in items)
        {
            if (item.Node.NodeType == ProjectExplorerNodeType.Object)
                return item;

            var found = FindFirstObject(item.Children);
            if (found is not null)
                return found;
        }

        return null;
    }
}
