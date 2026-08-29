using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Shell;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Phase 7 (`TD-89`) — the seam that keeps the product spine from being
/// welded to today's compile-time docking geometry.
/// </summary>
/// <remarks>
/// <para>
/// The eventual TempestOS requirement is fully dockable workspaces
/// (`TD-72`, "Option C"): several modules and documents open at once,
/// side by side, with user-controlled layout. That work is a dedicated
/// Work Package and is deliberately <b>not</b> attempted here.
/// </para>
/// <para>
/// What matters now is that it remains possible. The investigation found
/// the shell already has the right shape — <c>MainWindow</c> hosts
/// whichever module the navigator reports in a plain content host, and
/// <see cref="DockingGrid"/> lives strictly <em>inside</em> the
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
    public async Task TheDockingGrid_LivesInsideTheEngineeringSurface_NotAroundTheModuleHost()
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
            Assert.NotNull(window.GetLogicalDescendants().OfType<DockingGrid>().SingleOrDefault());

            // Outside Engineering it is gone entirely, because it belongs to
            // that module rather than to the shell. A shell that always had
            // a docking grid in its tree would be a shell whose navigation
            // depended on it.
            await host.ShellNavigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            Assert.Null(window.GetLogicalDescendants().OfType<DockingGrid>().SingleOrDefault());

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

            var grid = window.GetLogicalDescendants().OfType<DockingGrid>().Single();

            Assert.NotEmpty(grid.ColumnDefinitions);
            Assert.NotEmpty(grid.RowDefinitions);
            Assert.NotEmpty(grid.GetLogicalDescendants().OfType<GridSplitter>().ToList());
            Assert.NotNull(window.GetLogicalDescendants().OfType<RibbonView>().SingleOrDefault());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
