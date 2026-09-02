using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Shell;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The seam that keeps the product spine independent of the workspace
/// docking implementation (`TD-89`, carried forward through `TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// Full dockable workspaces were delivered by `TD-72`. What these tests
/// still hold is the boundary that made it cheap: docking belongs to the
/// Engineering surface, not to shell navigation.
/// </para>
/// <para>
/// What matters now is that it remains possible. The investigation found
/// the shell already has the right shape — <c>MainWindow</c> hosts
/// whichever module the navigator reports in a plain content host, and
/// <see cref="WorkspaceLayoutHost"/> lives strictly <em>inside</em> the
/// Engineering surface rather than above or around the module host. So
/// replacing the docking implementation later touches the Engineering
/// surface only, and never navigation.
/// </para>
/// <para>
/// These tests hold that seam in place. They are structural on purpose:
/// their whole job is to fail if a future change starts routing shell
/// navigation through the docking grid, which is exactly the coupling
/// that would make Option C expensive.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ShellDockingSeamTests
{
    [AvaloniaFact]
    public async Task TheLayoutHost_LivesInsideTheEngineeringSurface_NotAroundTheModuleHost()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            // In Engineering, the grid is present — it is the Engineering
            // Workspace's own layout.
            await host.ShellNavigator!.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            Assert.NotNull(window.GetLogicalDescendants().OfType<WorkspaceLayoutHost>().SingleOrDefault());

            // Outside Engineering it is gone entirely, because it belongs to
            // that module rather than to the shell. A shell that always had
            // a docking grid in its tree would be a shell whose navigation
            // depended on it.
            await host.ShellNavigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            Assert.Null(window.GetLogicalDescendants().OfType<WorkspaceLayoutHost>().SingleOrDefault());

            // The navigation rail, by contrast, is shell furniture and is
            // present in both.
            Assert.NotNull(window.GetLogicalDescendants().OfType<GlobalNavigationRail>().SingleOrDefault());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task EveryModule_IsHostedThroughTheSameSingleContentSeam()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;

            var project = await host.ProjectDirectory!.CreateAsync("P-0700", "Seam");

            // One module surface is on screen at a time, and swapping
            // modules swaps exactly that one content host — the property a
            // future multi-document docking shell replaces, and the reason
            // it can be replaced in one place.
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            Assert.NotNull(window.GetLogicalDescendants().OfType<ProjectBrowserView>().SingleOrDefault());
            Assert.Null(window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().SingleOrDefault());

            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            Assert.NotNull(window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().SingleOrDefault());
            Assert.Null(window.GetLogicalDescendants().OfType<ProjectBrowserView>().SingleOrDefault());

            await navigator.GoToModuleAsync(ShellArea.Commercial);
            await window.RenderCurrentModuleAsync();
            Assert.NotNull(window.GetLogicalDescendants().OfType<DeclaredCapabilityView>().SingleOrDefault());
            Assert.Null(window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().SingleOrDefault());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheEngineeringSurface_KeepsItsResponsiveAndCollapsibleBehaviour()
    {
        // Phase 7's own constraint: preserve resizing, collapsing,
        // responsive behaviour, ribbon minimisation and persisted splitter
        // preferences. Nothing in this pass touched them — this asserts the
        // grid is still the real, configured one rather than a stub the
        // spine work left behind.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            await host.ShellNavigator!.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            var layoutHost = window.GetLogicalDescendants().OfType<WorkspaceLayoutHost>().Single();

            // The arrangement is a real split with real, draggable
            // splitters, and every panel is present — the layout is not a
            // stub the spine work left behind.
            Assert.NotEmpty(layoutHost.GetLogicalDescendants().OfType<GridSplitter>().ToList());
            Assert.NotEmpty(layoutHost.TabGroups);
            Assert.NotNull(window.GetLogicalDescendants().OfType<RibbonView>().SingleOrDefault());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
