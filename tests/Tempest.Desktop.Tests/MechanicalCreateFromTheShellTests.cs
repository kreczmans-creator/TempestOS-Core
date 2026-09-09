using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Desktop.Views;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 17.9.2`. The first Windows review of `v0.17.0` pressed "Create
/// Mechanical Object" with a project open, was told a Part had been made,
/// and could not find it anywhere. This walks the same path through the
/// shell's own context and the real command registry, and asserts the
/// Part is under the project in the Project Explorer and selected.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class MechanicalCreateFromTheShellTests
{
    [AvaloniaFact]
    public async Task CreateMechanicalObject_WithAProjectOpenAndNothingSelected_LandsUnderTheProject_AndIsShown()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-0001", "Windows Test - v0.17.0");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);

            await host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await host.Workspace.Selection.ClearAsync();

            // The Explorer shows the Mechanical area before the user presses
            // Create, exactly as the Ribbon tab switch loads it in the shell.
            var explorer = window.GetLogicalDescendants().OfType<ProjectExplorerView>().Single();
            await explorer.LoadAsync();

            // The context the Palette (and, through ProjectIdSource, the
            // Ribbon) hands to a binding now carries the open project.
            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            var context = palette.ContextSource!();
            Assert.Empty(context.Selection);
            Assert.Equal(project.Id, context.ProjectId);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var invocation = await registry.InvokeAsync(
                "mechanical.create",
                context,
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Bracket" }));

            Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
            Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
            Assert.Contains("Created Part 'Bracket'", invocation.Result.Message, StringComparison.Ordinal);
            Assert.Contains("under 'Windows Test - v0.17.0'", invocation.Result.Message, StringComparison.Ordinal);

            await explorer.LoadAsync();
            LayOut(window);

            var tree = explorer.GetLogicalDescendants().OfType<TreeView>().Single();
            var roots = ((IEnumerable<ExplorerNodeItem>)tree.ItemsSource!).ToList();
            var projectItem = Assert.Single(roots, r => r.Node.Id == project.Id);
            var bracket = Assert.Single(projectItem.Children, c => c.Node.Title == "Bracket");
            Assert.Equal("Part", bracket.Node.Kind);

            // Not an orphan: this harness seeds sample objects that hang from
            // nothing, so the category may exist, but the Bracket is not in it.
            var orphans = roots.SingleOrDefault(r => r.Node.Id == MechanicalProductStructureNodeProvider.NotInAnyProjectNodeId);
            Assert.DoesNotContain(orphans?.Children ?? [], c => c.Node.Title == "Bracket");
            Assert.Same(bracket, tree.SelectedItem);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task APartThatHangsFromNothing_IsStillFindable_UnderNotInAnyProject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            // The exact command the first Windows review's Ribbon issued.
            var result = await dispatcher.DispatchAsync(new CreateMechanicalObjectCommand("Part", "Lost Part"), CancellationToken.None);
            Assert.True(result.Succeeded, result.Message);
            Assert.Contains("Not in any project", result.Message, StringComparison.Ordinal);

            var view = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!);
            await view.LoadAsync();

            var tree = view.GetLogicalDescendants().OfType<TreeView>().Single();
            var roots = ((IEnumerable<ExplorerNodeItem>)tree.ItemsSource!).ToList();
            var category = Assert.Single(roots, r => r.Node.Id == MechanicalProductStructureNodeProvider.NotInAnyProjectNodeId);
            Assert.Contains(category.Children, c => c.Node.Title == "Lost Part");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 3; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1900, 1050));
            window.Arrange(new Avalonia.Rect(0, 0, 1900, 1050));
        }
    }
}
