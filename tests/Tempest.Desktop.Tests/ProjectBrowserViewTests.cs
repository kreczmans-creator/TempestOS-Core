using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Shell;
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
            var deadline = DesktopTestHelpers.Deadline(2);
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

    /// <summary>
    /// The real journey through <see cref="ProjectsAreaView"/> (`WP
    /// 19.10Q`): Projects → Open (a group whose visible set is a snapshot
    /// taken at selection time) → New Project… → name it → OK. The
    /// project must open right up on its own Quote tab — the "open a
    /// quotation" default — with <see cref="ProjectBrowserView.ProjectOpened"/>
    /// firing exactly once, no explicit re-open and no hand-rolled
    /// <see cref="MainWindow.RenderCurrentModuleAsync"/> beyond the one a
    /// real click already causes through <c>MainWindowComposer.Wire</c>'s
    /// own subscription. The shared browser's own list must agree with
    /// its filter at that exact moment too, not merely once the later,
    /// fire-and-forget change-feed reaction catches up.
    /// </summary>
    [AvaloniaFact]
    public async Task CreateFromOpenGroup_OpensRightUpOnTheQuoteTab_AndTheBrowserAgrees()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<ProjectsAreaView>().Single().SelectNode("Open");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<ProjectBrowserView>().Any());
            LayOut(window);

            var browser = window.GetLogicalDescendants().OfType<ProjectBrowserView>().Single();
            var openedCount = 0;
            browser.ProjectOpened += () => openedCount++;

            var newButton = browser.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Project…"));
            newButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var newProjectPrompt = GetPrivateField<NewProjectPrompt>(window, "_newProjectPrompt");
            await RenderUntilAsync(window, () => newProjectPrompt.IsVisible);
            var nameBox = newProjectPrompt.GetLogicalDescendants().OfType<TextBox>().First();
            nameBox.Text = "Open Group Project";
            var okButton = newProjectPrompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
            okButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !newProjectPrompt.IsVisible);

            var projectId = Guid.Empty;
            await RenderUntilAsync(window, () =>
            {
                var found = host.ProjectDirectory!.ListAsync().GetAwaiter().GetResult()
                    .FirstOrDefault(p => p.DisplayName == "Open Group Project");
                if (found is null)
                    return false;

                projectId = found.Id;
                return true;
            });
            Assert.NotEqual(Guid.Empty, projectId);

            await RenderUntilAsync(window, () =>
                navigator.Current is { Area: ShellArea.ProjectWorkspace, ProjectArea: ProjectArea.Quote } location && location.ProjectId == projectId);
            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(ProjectArea.Quote, navigator.Current.ProjectArea);
            Assert.Equal(projectId, navigator.Current.ProjectId);
            Assert.Equal(1, openedCount);

            // The browser's own list agrees with its filter at the exact
            // moment the project opened — the "Open" group's visible set
            // was extended synchronously as part of the create path
            // (`ProjectsAreaView.OnProjectCreated`), not left to
            // `OnWorkspaceChanged`'s later, fire-and-forget reaction.
            var current = GetPrivateField<IReadOnlyList<ProjectSummary>>(browser, "_current");
            Assert.Contains(current, p => p.Id == projectId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1400, 900));
            window.Arrange(new Rect(0, 0, 1400, 900));
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
    }
}
