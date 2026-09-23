using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The first manual Windows review of `v0.17.0` entered the Engineering
/// Workspace from a freshly created project and found the Project
/// Explorer and the Property Inspector absent — only the Cockpit, full
/// width — until a layout preset was applied by hand. This drives that
/// exact route through the real shell and asserts the two panels are
/// present, laid out at a real size, and not collapsed to a strip.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class EngineeringEntryLayoutTests
{
    [AvaloniaFact]
    public async Task EnteringEngineeringFromANewProject_ShowsExplorerAndInspector_AtARealSize()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var directory = host.ProjectDirectory!;

            // Home first, laid out at a real size — the state a user starts from.
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var composer = GetPrivateField<WorkspaceDockingComposer>(window, "_dockingComposer");
            Assert.True(window.WorkspaceLayout.IsPanelVisible(composer.ExplorerPanelId), "Explorer missing from the layout at Home.");
            Assert.True(window.WorkspaceLayout.IsPanelVisible(composer.InspectorPanelId), "Inspector missing from the layout at Home.");

            // Projects -> create -> open -> Enter Engineering, exactly as recorded.
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var project = await directory.CreateAsync("P-0001", "Windows Test - v0.17.0");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);
            Assert.True(window.WorkspaceLayout.IsPanelVisible(composer.ExplorerPanelId), "Explorer missing from the layout after entering Engineering from a project.");
            Assert.True(window.WorkspaceLayout.IsPanelVisible(composer.InspectorPanelId), "Inspector missing from the layout after entering Engineering from a project.");

            var explorer = window.FindUnique<ProjectExplorerView>();
            var inspector = window.FindUnique<PropertyInspectorView>();
            Assert.True(explorer.IsVisible && explorer.Bounds.Width > 100, $"Explorer not usable after entering Engineering: visible={explorer.IsVisible}, bounds={explorer.Bounds}.");
            Assert.True(inspector.IsVisible && inspector.Bounds.Width > 100, $"Inspector not usable after entering Engineering: visible={inspector.IsVisible}, bounds={inspector.Bounds}.");

            // And back out to the project and in again — the second entry is
            // the one that goes through a re-attached docking host.
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            explorer = window.FindUnique<ProjectExplorerView>();
            inspector = window.FindUnique<PropertyInspectorView>();
            Assert.True(explorer.IsVisible && explorer.Bounds.Width > 100, $"Explorer not usable on re-entry: bounds={explorer.Bounds}.");
            Assert.True(inspector.IsVisible && inspector.Bounds.Width > 100, $"Inspector not usable on re-entry: bounds={inspector.Bounds}.");
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
