using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Projects;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// <see cref="ProjectBrowserView"/>'s own "Create your first project" /
/// "New Project…" journey (`WP-Z4` Productisation Phase 1, P1-1) —
/// creating a project must actually land the user inside it, not merely
/// repopulate the list they were already looking at.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectBrowserViewTests
{
    [AvaloniaFact]
    public async Task CreateAsync_ActuallyOpensTheNewProject_NotJustRefreshesTheList()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var directory = host.ProjectDirectory!;
            var navigator = host.ShellNavigator!;
            var projectContext = host.ProjectContext!;

            Assert.False(projectContext.HasProject);

            // Mirrors MainWindow.PromptForNewProjectAsync's own real shape:
            // creates the project with the suggested identifier and returns
            // whether it was created, never touching navigation itself —
            // navigating into it is ProjectBrowserView's own job.
            Func<string, string, Task<bool>> promptForNewProject = async (identifier, _) =>
            {
                await directory.CreateAsync(identifier, "New From Test").ConfigureAwait(true);
                return true;
            };

            var browser = new ProjectBrowserView(directory, navigator, promptForNewProject);
            await browser.RefreshAsync();

            var newButton = browser.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Project…"));
            newButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // The click handler is `async void` over real disk I/O
            // (`EngineeringDocumentStore`) — bounded poll, the same
            // remedy `ObjectEditorViewTests` already uses for the
            // identical reason (`TD-119`).
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (!projectContext.HasProject && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.True(projectContext.HasProject);
            Assert.Equal("New From Test", projectContext.Current!.DisplayName);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
