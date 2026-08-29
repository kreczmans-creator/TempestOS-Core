using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Product Convergence programme's own Definition of Done (`TD-84`),
/// driven end to end through the real <see cref="MainWindow"/> over a real
/// <see cref="WorkspaceHost"/>.
/// </summary>
/// <remarks>
/// This is the acceptance journey, not a screen inventory: each step
/// asserts the <b>state and the surface together</b>, so a window that
/// merely renders a project-looking page without a real project context
/// fails, and a context that changes without the shell following fails
/// too.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProductSpineAcceptanceTests
{
    [AvaloniaFact]
    public async Task Journey_LaunchToProjectToEngineeringAndBack()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            // --- 1. Launch TempestOS -------------------------------------
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var context = host.ProjectContext!;
            var directory = host.ProjectDirectory!;

            Assert.Equal(ShellArea.Home, navigator.Current.Area);
            Assert.False(context.HasProject);

            // The global navigation rail is a real, present surface.
            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
            Assert.NotNull(rail);

            // --- 2. Select a project -------------------------------------
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();

            var browser = window.GetLogicalDescendants().OfType<ProjectBrowserView>().Single();
            Assert.NotNull(browser);

            var project = await directory.CreateAsync("P-0027", "Apollo Pump Redesign");

            // --- 3. Enter the Project Workspace --------------------------
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(project.Id, context.Current!.Id);

            var projectWorkspace = window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().Single();
            Assert.NotNull(projectWorkspace);

            // --- 4. The current project is visible in the shell ----------
            var statusBar = window.GetLogicalDescendants().OfType<StatusBarView>().Single();
            var statusText = statusBar.GetLogicalDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(statusText, t => t.Contains("P-0027 Apollo Pump Redesign", StringComparison.Ordinal));

            // --- 5. Enter Engineering from that project ------------------
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);
            Assert.Equal(project.Id, navigator.Current.ProjectId);

            // The real Engineering surface — ribbon and docking grid — is
            // what the Engineering module renders.
            Assert.NotNull(window.GetLogicalDescendants().OfType<RibbonView>().SingleOrDefault());
            Assert.NotNull(window.GetLogicalDescendants().OfType<Docking.WorkspaceLayoutHost>().SingleOrDefault());

            // --- 6. Return to the project without losing context ---------
            await navigator.ReturnToProjectAsync();
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(project.Id, context.Current!.Id);
            Assert.NotNull(window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().SingleOrDefault());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Journey_EngineeringWorkIsReflectedInProjectContext()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            _ = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var directory = host.ProjectDirectory!;

            var project = await directory.CreateAsync("P-0027", "Apollo Pump Redesign");
            await navigator.OpenProjectAsync(project.Id);

            var before = await directory.ListProjectContentsAsync(project.Id);

            // Engineering work, through the real domain the platform owns.
            var domain = host.Workspace!;
            var repositoryContext = (Tempest.Core.EngineeringDomain.EngineeringDomainContext)
                host.Services!.GetService(typeof(Tempest.Core.EngineeringDomain.EngineeringDomainContext));

            var factory = new Tempest.Core.EngineeringDomain.EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Part>(
                "Part", repositoryContext,
                (doc, rev) => new Tempest.Core.EngineeringDomain.Part(
                    doc, rev, repositoryContext, "PN-1001", "Impeller",
                    Tempest.Core.EngineeringDomain.EngineeringObjectMetadata.Empty));

            var part = (Tempest.Core.EngineeringDomain.Part)await factory.CreateAsync("Impeller — acceptance test.");
            await ((Tempest.Core.EngineeringDomain.IHasParent)part).MoveAsync(project.Id);

            // --- The work is reflected in the project's own context ------
            var after = await directory.ListProjectContentsAsync(project.Id);

            Assert.DoesNotContain(part.Id, before);
            Assert.Contains(part.Id, after);
            Assert.Equal(before.Count + 1, after.Count);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Journey_CloseAndReopen_RecoversProjectAndLocation()
    {
        // One persistence root stands for one machine across two launches.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            _ = new MainWindow(first);

            var project = await first.ProjectDirectory!.CreateAsync("P-0027", "Apollo Pump Redesign");
            projectId = project.Id;

            await first.ShellNavigator!.OpenProjectAsync(projectId, ProjectArea.Requirements);

            // Closing the application persists the spine.
            await first.ShutdownAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        // --- Reopen ------------------------------------------------------
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);
            await window.RenderCurrentModuleAsync();

            // The project and the location both came back.
            Assert.True(second.ProjectContext!.HasProject);
            Assert.Equal(projectId, second.ProjectContext.Current!.Id);
            Assert.Equal("P-0027 Apollo Pump Redesign", second.ProjectContext.Current.Label);

            Assert.Equal(ShellArea.ProjectWorkspace, second.ShellNavigator!.Current.Area);
            Assert.Equal(projectId, second.ShellNavigator.Current.ProjectId);
            Assert.Equal(ProjectArea.Requirements, second.ShellNavigator.Current.ProjectArea);

            // And the shell renders the recovered module, not a default one.
            Assert.NotNull(window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().SingleOrDefault());
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    /// <summary>
    /// Superseded and inverted, deliberately. This previously asserted that
    /// the rail's Engineering button routed to Projects when no project was
    /// open, which was correct under the product decision in force at the
    /// time. The current decision makes standalone engineering — quick
    /// calculations and calculation sets — a first-class workflow that
    /// requires no project (`TD-89`), so the button now goes to
    /// Engineering, in the standalone scope, and the assertion is
    /// strengthened rather than removed: the destination must be real, and
    /// it must know which scope it is in.
    /// </summary>
    [AvaloniaFact]
    public async Task TheRailEntersStandaloneEngineering_WhenNoProjectIsOpen()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;

            Assert.False(host.ProjectContext!.HasProject);

            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
            var engineering = rail.GetLogicalDescendants().OfType<Button>()
                .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Engineering");

            engineering.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);
            Assert.True(navigator.Current.IsStandaloneEngineering);
            Assert.Equal(EngineeringScopeKind.Standalone, host.EngineeringScope!.Current.Kind);

            // Still no project — standalone did not invent one.
            Assert.False(host.ProjectContext.HasProject);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
