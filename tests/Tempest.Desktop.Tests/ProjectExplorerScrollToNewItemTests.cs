using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Scroll-to-new-item (`WP-Z4` Productisation Phase 1, backlog item 4) —
/// creating a real object and reloading the Explorer must select the
/// genuinely new node, not silently leave the previous selection (or none)
/// in place while the new object sits unseen in the tree.
/// </summary>
/// <remarks>
/// No structured "created object Id" reaches this view through the
/// generic Create path (see <see cref="ProjectExplorerView.LoadAsync"/>'s
/// own remarks) — this proves the before/after Id-diff fallback actually
/// finds and selects the one real node that is new, using a real
/// <see cref="CreateMechanicalObjectCommand"/> dispatch, never a
/// synthetic node.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectExplorerScrollToNewItemTests
{
    [AvaloniaFact]
    public async Task LoadAsync_AfterARealCreate_SelectsTheGenuinelyNewNode()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var view = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!);
            await view.LoadAsync();

            var tree = view.GetLogicalDescendants().OfType<TreeView>().Single();
            var idsBefore = new HashSet<Guid>();
            CollectIds((IEnumerable<ExplorerNodeItem>)tree.ItemsSource!, idsBefore);

            // Root nodes are live Projects only (MechanicalProductStructureNodeProvider)
            // — every other object needs a real ParentId to be reachable
            // from the tree at all, exactly as the real "Create" ribbon
            // commands always supply one from the current selection.
            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var parent = roots.First();

            var result = await commandDispatcher.DispatchAsync(
                new CreateMechanicalObjectCommand("Part", "Scroll-To-New-Item Test Part", parentId: parent.Id),
                CancellationToken.None);
            Assert.True(result.Succeeded, result.Message);

            await view.LoadAsync();

            var selected = tree.SelectedItem as ExplorerNodeItem;
            Assert.NotNull(selected);
            Assert.DoesNotContain(selected!.Node.Id, idsBefore);
            Assert.Equal("Scroll-To-New-Item Test Part", selected.Node.Title);

            // The real object is genuinely there, not a synthetic stand-in —
            // the same real child the real IProjectExplorer now reports
            // under the same real parent it was created against.
            var realChildren = await workspace.ProjectExplorer.GetChildrenAsync(parent.Id);
            Assert.Contains(realChildren, n => n.Id == selected.Node.Id);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void CollectIds(IEnumerable<ExplorerNodeItem> items, HashSet<Guid> into)
    {
        foreach (var item in items)
        {
            into.Add(item.Node.Id);
            CollectIds(item.Children, into);
        }
    }
}
