using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Shell;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The header's project chip as a real way back into the open project
/// (`WP-Z4` Productisation Phase 1, P0) — <c>IShellNavigator.ReturnToProjectAsync</c>
/// existed with zero Desktop call sites before this change, so entering
/// Engineering from a project had no path back at all.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ShellHeaderReturnToProjectTests
{
    [AvaloniaFact]
    public async Task ProjectChip_Disabled_WithNoProjectOpen()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var chip = window.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Return to project");
            Assert.False(chip.IsEnabled);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectChip_Click_FromEngineering_ActuallyReturnsToTheProjectWorkspace()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var navigator = host.ShellNavigator!;
            var directory = host.ProjectDirectory!;
            var window = new MainWindow(host);

            var project = await directory.CreateAsync("P-9000", "Chip Return Test");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            // Away from the project's own workspace — Engineering, the
            // navigation dead end this fix closes.
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);

            var chip = window.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Return to project");
            Assert.True(chip.IsEnabled);
            chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (navigator.Current.Area != ShellArea.ProjectWorkspace && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(project.Id, navigator.Current.ProjectId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
