using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
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
            var explorer = window.FindUnique<ProjectExplorerView>();
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

    /// <summary>
    /// `TD-38`. The Ribbon create of a duplicate Part — same name, same
    /// project — is refused, and the refusal reaches the status bar
    /// exactly as any other Ribbon failure does (`ActionOutcomeReportingTests`'s
    /// own established "ribbon dispatch is fire-and-forget, reported on the
    /// subscriber's own continuation" shape), rather than an unhandled
    /// exception. A Part is already created, through the same real ribbon
    /// path, before the duplicate is attempted.
    /// </summary>
    [AvaloniaFact]
    public async Task CreateMechanicalObject_ADuplicatePartInTheSameProject_IsRefused_OnTheStatusBar()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-0002", "Windows Test - TD-38");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            await host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await host.Workspace.Selection.ClearAsync();

            var explorer = window.FindUnique<ProjectExplorerView>();
            await explorer.LoadAsync();

            var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            ribbon.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Bracket" });

            // First Part: created through the real Ribbon button, exactly
            // as a user presses it — "a Part is already created in a test".
            // The success message the Ribbon reports is immediately
            // overwritten on the status bar's own "Selected object" field
            // by the create-then-select-it hand-off (`WP 17.9.4`'s own
            // "opened right up, not announced"), so the durable signal that
            // creation actually happened is the domain itself, not a status
            // bar snapshot raced against that hand-off.
            Click(ribbon, registry, "mechanical.create");

            var firstDeadline = DesktopTestHelpers.Deadline(2);
            EngineeringObjectIndexEntry? bracket = null;
            while (bracket is null && DateTime.UtcNow < firstDeadline)
            {
                bracket = (await domain.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Part))
                    .FirstOrDefault(entry => entry.DisplayName == "Bracket");
                await Task.Delay(10);
            }

            Assert.NotNull(bracket);

            // Second Part, same name, same project, through the identical
            // Ribbon path: refused, not created — and unlike the success
            // above, a refusal never triggers a follow-up selection change,
            // so the refusal message it sets is what a user actually sees
            // sitting on the status bar, not overwritten by anything else.
            Click(ribbon, registry, "mechanical.create");

            var secondDeadline = DesktopTestHelpers.Deadline(2);
            while (!statusBar.GetLogicalDescendants().OfType<TextBlock>()
                       .Any(t => (t.Text ?? string.Empty).Contains("already exists in project", StringComparison.Ordinal))
                   && DateTime.UtcNow < secondDeadline)
            {
                await Task.Delay(10);
            }

            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                text => text.Contains("A Part named 'Bracket' already exists in project", StringComparison.Ordinal));

            // And only one Bracket actually exists under the project.
            await explorer.LoadAsync();
            LayOut(window);
            var tree = explorer.GetLogicalDescendants().OfType<TreeView>().Single();
            var roots = ((IEnumerable<ExplorerNodeItem>)tree.ItemsSource!).ToList();
            var projectItem = Assert.Single(roots, r => r.Node.Id == project.Id);
            Assert.Single(projectItem.Children, c => c.Node.Title == "Bracket");
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
