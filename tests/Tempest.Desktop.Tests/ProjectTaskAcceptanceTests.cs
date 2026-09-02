using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Tasks area, driven end to end through the real
/// <see cref="MainWindow"/>: <b>project → Tasks → create → assign →
/// change status → restart → the task is still there → reopen and
/// edit.</b>
/// </summary>
/// <remarks>
/// <para>
/// Nothing here calls <see cref="IProjectTaskService"/> directly for the
/// main journey. Every step goes through the rendered surface — the button
/// a user clicks, the dialog a user types into — because the defect this
/// Work Package closes was precisely a set of real domain types that no
/// surface reached (`TD-81`). A service test would have passed against the
/// broken product.
/// </para>
/// <para>
/// The restart is a second real <see cref="WorkspaceHost"/> over the same
/// persistence root, which is what a relaunch actually is.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectTaskAcceptanceTests
{
    // ================================================================
    // The journey
    // ================================================================

    [AvaloniaFact]
    public async Task Journey_CreateATask_AssignIt_MoveIt_Relaunch_ThenReopenAndEditIt()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        Guid taskId;

        // ============================================================
        // FIRST LAUNCH
        // ============================================================
        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var window = new MainWindow(first);

            // --- 1. A project, opened at its Tasks area --------------
            var project = await first.ProjectDirectory!.CreateAsync("P-0081", "Apollo Pump Redesign");
            projectId = project.Id;

            await GoToTasksAsync(first, window, projectId);

            var tasks = TasksSurfaceOf(window);

            // --- 2. It starts genuinely empty, and says so ----------
            Assert.True(tasks.IsShowingEmptyState);
            Assert.Contains(
                ProjectTasksView.EmptyHeadline,
                tasks.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty));

            // --- 3. Create one through the real button and dialog ---
            await ClickAsync(tasks, "New Task");
            await AnswerDialogAsync(window, "Balance the impeller");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);

            tasks = TasksSurfaceOf(window);
            var entry = Assert.Single(tasks.Entries);
            taskId = entry.ObjectId;

            Assert.False(tasks.IsShowingEmptyState);
            Assert.Equal("Balance the impeller", entry.DisplayName);
            Assert.Equal(TaskWorkState.Todo, entry.WorkState);
            Assert.True(entry.IsUnassigned);

            // --- 4. Assign it to whoever is using the product -------
            await ClickWhenPresentAsync(() => TasksSurfaceOf(window), "Assign to me");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && !s.Entries[0].IsUnassigned);

            entry = Assert.Single(TasksSurfaceOf(window).Entries);
            Assert.False(entry.IsUnassigned);
            Assert.Equal(first.SessionPrincipal!.Identity.Id, entry.AssignedToPrincipalId);

            // --- 5. Move it through its states ----------------------
            await ClickWhenPresentAsync(() => TasksSurfaceOf(window), "In progress");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && s.Entries[0].WorkState == TaskWorkState.InProgress);
            Assert.Equal(TaskWorkState.InProgress, Assert.Single(TasksSurfaceOf(window).Entries).WorkState);

            await ClickWhenPresentAsync(() => TasksSurfaceOf(window), "Done");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && s.Entries[0].WorkState == TaskWorkState.Done);

            entry = Assert.Single(TasksSurfaceOf(window).Entries);
            Assert.Equal(TaskWorkState.Done, entry.WorkState);
            Assert.False(entry.IsOpen);
        }
        finally
        {
            await first.DisposeAsync();
        }

        // ============================================================
        // RELAUNCH — a new host over the same store
        // ============================================================
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);

            // --- 6. The task came back, with everything it carried --
            await GoToTasksAsync(second, window, projectId);

            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);
            var tasks = TasksSurfaceOf(window);
            var entry = Assert.Single(tasks.Entries);

            Assert.Equal(taskId, entry.ObjectId);
            Assert.Equal("Balance the impeller", entry.DisplayName);
            Assert.Equal(TaskWorkState.Done, entry.WorkState);
            Assert.Equal(second.SessionPrincipal!.Identity.Id, entry.AssignedToPrincipalId);

            // Its project membership survived too — the task is in this
            // project's register, not merely somewhere in the store.
            Assert.Equal(projectId, await ProjectMembership.ResolveOwningProjectAsync(DomainOf(second).Repository, taskId));

            // --- 7. Reopen it --------------------------------------
            await ClickWhenPresentAsync(() => TasksSurfaceOf(window), "In progress");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && s.Entries[0].WorkState == TaskWorkState.InProgress);

            entry = Assert.Single(TasksSurfaceOf(window).Entries);
            Assert.Equal(TaskWorkState.InProgress, entry.WorkState);
            Assert.True(entry.IsOpen);

            // --- 8. Edit it ----------------------------------------
            await ClickWhenPresentAsync(() => TasksSurfaceOf(window), "Edit");
            await AnswerDialogAsync(window, "Balance and re-test the impeller");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && s.Entries[0].DisplayName == "Balance and re-test the impeller");

            entry = Assert.Single(TasksSurfaceOf(window).Entries);
            Assert.Equal(taskId, entry.ObjectId);
            Assert.Equal("Balance and re-test the impeller", entry.DisplayName);
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ADueDateSetThroughTheDialog_SurvivesARelaunch_AndReadsAsOverdue()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid projectId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var window = new MainWindow(first);

            projectId = (await first.ProjectDirectory!.CreateAsync("P-0082", "Dated work")).Id;
            await GoToTasksAsync(first, window, projectId);

            await ClickAsync(TasksSurfaceOf(window), "New Task");
            await AnswerDialogAsync(window, "Overdue work");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);

            await ClickWhenPresentAsync(() => TasksSurfaceOf(window), "Due date");
            await AnswerDialogAsync(window, "2020-01-01");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && s.Entries[0].DueDate is not null);

            var entry = Assert.Single(TasksSurfaceOf(window).Entries);
            Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), entry.DueDate);
            Assert.True(entry.IsOverdue);
        }
        finally
        {
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);
            await GoToTasksAsync(second, window, projectId);

            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);
            var entry = Assert.Single(TasksSurfaceOf(window).Entries);
            Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), entry.DueDate);
            Assert.True(entry.IsOverdue);

            // And the Cockpit's Overdue Actions card, which was an empty
            // placeholder for want of a due-date field, now reports it.
            var cockpit = ((Workspace)second.Workspace!).Cockpit;
            Assert.Contains(cockpit.OverdueActionLines, line => line.Contains("Overdue work", StringComparison.Ordinal));
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Project scoping, through the real shell
    // ================================================================

    [AvaloniaFact]
    public async Task TheTasksSurface_ShowsOnlyTheOpenProjectsTasks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var apollo = await host.ProjectDirectory!.CreateAsync("P-0083", "Apollo");
            var gemini = await host.ProjectDirectory!.CreateAsync("P-0084", "Gemini");

            await GoToTasksAsync(host, window, apollo.Id);
            await ClickAsync(TasksSurfaceOf(window), "New Task");
            await AnswerDialogAsync(window, "Apollo work");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && s.Entries[0].DisplayName == "Apollo work");

            await GoToTasksAsync(host, window, gemini.Id);
            await ClickAsync(TasksSurfaceOf(window), "New Task");
            await AnswerDialogAsync(window, "Gemini work");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && s.Entries[0].DisplayName == "Gemini work");

            Assert.Equal("Gemini work", Assert.Single(TasksSurfaceOf(window).Entries).DisplayName);

            await GoToTasksAsync(host, window, apollo.Id);
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);
            Assert.Equal("Apollo work", Assert.Single(TasksSurfaceOf(window).Entries).DisplayName);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ATaskHungDeepInsideTheProduct_StillShowsOnTheProjectsTasksSurface()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0085", "Nested");
            await GoToTasksAsync(host, window, project.Id);

            await ClickAsync(TasksSurfaceOf(window), "New Task");
            await AnswerDialogAsync(window, "Deep work");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);

            var taskId = Assert.Single(TasksSurfaceOf(window).Entries).ObjectId;

            // Move it three levels down, exactly as it would sit against a
            // real product structure.
            var domain = DomainOf(host);
            var assembly = await CreatePartAsync(domain, "ASM-1", "Pump", project.Id);
            var part = await CreatePartAsync(domain, "PRT-1", "Impeller", assembly);
            await ((IHasParent)(await domain.Repository.FindAsync(taskId))!).MoveAsync(part);

            await GoToTasksAsync(host, window, project.Id);
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);
            Assert.Equal(taskId, Assert.Single(TasksSurfaceOf(window).Entries).ObjectId);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // The surface itself
    // ================================================================

    [AvaloniaFact]
    public async Task TheTasksArea_IsAnOrdinarySurface_NotADeclaredCapabilityCard()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0086", "Real surface");
            await GoToTasksAsync(host, window, project.Id);

            var tasks = TasksSurfaceOf(window);

            // The area no longer claims a capability it does not have —
            // and it no longer draws the card that says so either.
            Assert.True(ProjectAreas.IsImplemented(ProjectArea.Tasks));
            Assert.Null(ProjectAreas.For(ProjectArea.Tasks).TrackedBy);
            Assert.Empty(tasks.GetLogicalDescendants().OfType<DeclaredCapabilityView>());

            // It is a plain surface inside the project workspace's own tab
            // host — no reserved slot, no window of its own (`TD-72`).
            Assert.IsType<ProjectWorkspaceView>(
                tasks.GetLogicalAncestors().OfType<ProjectWorkspaceView>().FirstOrDefault());
            Assert.Empty(window.GetLogicalDescendants().OfType<Window>());
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheBoardShowsEveryColumn_IncludingTheEmptyOnes()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0087", "Board");
            await GoToTasksAsync(host, window, project.Id);

            await ClickAsync(TasksSurfaceOf(window), "New Task");
            await AnswerDialogAsync(window, "Only task");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);

            var tasks = TasksSurfaceOf(window);
            Assert.False(tasks.IsShowingBoard);

            await ClickAsync(tasks, "View as board");

            Assert.True(tasks.IsShowingBoard);
            Assert.Equal(TaskWorkStates.All.Count, tasks.Board.Count);

            // Every column is on screen, so the board keeps its shape as
            // work moves rather than reflowing under the user.
            var headings = tasks.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            foreach (var descriptor in TaskWorkStates.All)
                Assert.Contains(headings, h => h.StartsWith(descriptor.Name, StringComparison.Ordinal));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task OnlyThePermittedMoves_AreOfferedAsButtons()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0088", "Transitions");
            await GoToTasksAsync(host, window, project.Id);

            await ClickAsync(TasksSurfaceOf(window), "New Task");
            await AnswerDialogAsync(window, "Only task");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);

            // A new task is Todo, so every state but Todo is reachable.
            var captions = ButtonCaptions(TasksSurfaceOf(window));
            Assert.Contains("In progress", captions);
            Assert.Contains("Done", captions);
            Assert.DoesNotContain("To do", captions);

            // Finished work offers only the two reopen moves — a button
            // whose only outcome is an error is never shown.
            await ClickWhenPresentAsync(() => TasksSurfaceOf(window), "Done");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { Entries.Count: 1 } s && s.Entries[0].WorkState == TaskWorkState.Done);

            captions = ButtonCaptions(TasksSurfaceOf(window));
            Assert.Contains("To do", captions);
            Assert.Contains("In progress", captions);
            Assert.DoesNotContain("Blocked", captions);
            Assert.DoesNotContain("Done", captions);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task OpeningTasksDoesNotDisturbDocumentsOrRequirements()
    {
        // The existing project areas must keep working. A regression here
        // is the difference between adding a feature and trading one.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0089", "Coexistence");

            await GoToTasksAsync(host, window, project.Id);
            Assert.Single(window.GetLogicalDescendants().OfType<ProjectTasksView>().Distinct());

            await host.ShellNavigator!.OpenProjectAsync(project.Id, ProjectArea.Documents);
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is null);
            Assert.Single(window.GetLogicalDescendants().OfType<ProjectDocumentsView>().Distinct());

            await host.ShellNavigator!.OpenProjectAsync(project.Id, ProjectArea.Requirements);
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is null);
            Assert.Single(window.GetLogicalDescendants().OfType<ProjectRequirementsView>().Distinct());

            // And navigating away and back keeps the project context.
            await GoToTasksAsync(host, window, project.Id);
            Assert.Equal(project.Id, host.ProjectContext!.Current!.Id);
            Assert.Equal(ProjectArea.Tasks, host.ShellNavigator!.Current.ProjectArea);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }


    [AvaloniaFact]
    public async Task TheTasksSurface_ActuallyRendersAtRealSize_NotJustInTheLogicalTree()
    {
        // Logical presence is not visual presence. Everything else in this
        // file asserts that a control exists and responds; this one shows
        // the window, runs a real layout pass, and asserts the user can
        // actually see the task and its actions — a card that exists at
        // 0x0 is indistinguishable, to a user, from one that is missing.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();

            var window = new MainWindow(host) { Width = 1400, Height = 900 };
            window.Show();

            var project = await host.ProjectDirectory!.CreateAsync("P-0090", "Rendered");
            await GoToTasksAsync(host, window, project.Id);

            await ClickAsync(TasksSurfaceOf(window), "New Task");
            await AnswerDialogAsync(window, "Balance the impeller");
            await RenderUntilAsync(window, () => TasksSurfaceOrNull(window) is { } s && s.Entries.Count == 1);

            await LayOutAsync(window);

            var tasks = TasksSurfaceOf(window);
            Assert.True(tasks.Bounds.Width > 0 && tasks.Bounds.Height > 0, $"The Tasks surface rendered at {tasks.Bounds}.");

            // The task the user just made is on screen with its title, its
            // state and its owner, all at a real size.
            AssertRendered(tasks, "Balance the impeller");
            AssertRendered(tasks, "TSK-001");
            AssertRendered(tasks, ProjectTasksView.Describe(TaskWorkState.Todo));
            AssertRendered(tasks, $"{ProjectTasksView.UnassignedLabel} · No due date");

            // And every action is a real, clickable button, not a
            // zero-height row.
            foreach (var caption in new[] { "Edit", "Assign to me", "Due date", "In progress", "Blocked", "Done" })
            {
                var button = tasks.GetLogicalDescendants().OfType<Button>()
                    .First(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

                Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0, $"'{caption}' rendered at {button.Bounds}.");
            }

            // The board renders every column, including the empty ones.
            await ClickAsync(tasks, "View as board");
            await LayOutAsync(window);

            foreach (var descriptor in TaskWorkStates.All)
                AssertRendered(tasks, $"{descriptor.Name} ({(descriptor.State == TaskWorkState.Todo ? 1 : 0)})");
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>Runs a real layout pass, twice, so content added during the render is measured too.</summary>
    private static async Task LayOutAsync(MainWindow window)
    {
        // `TD-119`/Class B: both passes are kept — content added during the
        // first render is measured by the second — but the queued dispatcher
        // work is now drained deterministically rather than guessed at with a
        // fixed delay. `RunJobs` is the repository's established drain
        // (`UndoRedoThreadingTests`, `WP-Z2`).
        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1400, 900));
            window.Arrange(new Rect(0, 0, 1400, 900));
        }
    }

    private static void AssertRendered(Control surface, string text)
    {
        var block = surface.GetLogicalDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => string.Equals(t.Text, text, StringComparison.Ordinal));

        Assert.True(block is not null, $"'{text}' is not on screen at all.");
        Assert.True(block!.Bounds.Width > 0 && block.Bounds.Height > 0, $"'{text}' rendered at {block.Bounds}.");
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static async Task GoToTasksAsync(WorkspaceHost host, MainWindow window, Guid projectId)
    {
        await host.ShellNavigator!.OpenProjectAsync(projectId, ProjectArea.Tasks);
        await window.RenderCurrentModuleAsync();
    }

    /// <summary>The one Tasks surface on screen.</summary>
    /// <remarks>
    /// Deduplicated by identity, not filtered by <c>Single()</c>: once a
    /// window is shown, a <see cref="TabControl"/> materialises the
    /// selected tab's content through its presenter as well, so the same
    /// control instance appears twice in the logical tree. That is one
    /// surface enumerated twice, not two surfaces — asserting `Single()`
    /// would fail on a shown window while the product was correct.
    /// </remarks>
    private static ProjectTasksView TasksSurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<ProjectTasksView>().Distinct().Single();

    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    private static List<string> ButtonCaptions(Control surface) =>
        [.. surface.GetLogicalDescendants().OfType<Button>().Select(b => b.Content?.ToString() ?? string.Empty)];

    /// <summary>Clicks the first button with <paramref name="caption"/>, exactly as a user would.</summary>
    private static async Task ClickAsync(Control surface, string caption)
    {
        var button = surface.GetLogicalDescendants().OfType<Button>()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

        Assert.True(button is not null, $"No '{caption}' button on this surface. Present: {string.Join(", ", ButtonCaptions(surface))}");

        button!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        // `TD-119`: no fixed wait. The click is dispatched fire-and-forget, so a
        // duration here would only be a guess; every caller now joins on the real
        // state its own assertion reads.
    }

    /// <summary>Clicks <paramref name="caption"/> once it is actually present on the freshly re-queried surface.</summary>
    /// <remarks>
    /// `TD-119`. This is the failure CI hit at <c>e7357b6</c>: "Due date" is a
    /// per-task-row button, so it does not exist until the row has rendered on an
    /// asynchronous continuation, and <see cref="ClickAsync"/> failed with
    /// "No 'Due date' button on this surface. Present: New Task, View as board".
    /// <see cref="ClickAsync"/> deliberately still fails at once for a button that
    /// ought to be there already; this is for targets legitimately produced
    /// asynchronously. The surface is re-queried every iteration, the click is
    /// raised exactly once and never retried, and a button that never appears
    /// fails with the same message <see cref="ClickAsync"/> would give.
    /// </remarks>
    private static async Task ClickWhenPresentAsync(Func<Control> surface, string caption)
    {
        Button? button;
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (true)
        {
            button = surface().GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

            if (button is not null || DateTime.UtcNow >= deadline)
                break;

            await Task.Delay(10);
        }

        Assert.True(button is not null, $"No '{caption}' button on this surface. Present: {string.Join(", ", ButtonCaptions(surface()))}");

        button!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>Re-renders the current module until <paramref name="condition"/> holds, or a two-second deadline expires.</summary>
    /// <remarks>
    /// `TD-119`. Rendering is a read, so this loop cannot manufacture the state it
    /// waits for; it decides only *when* to assert, and every assertion at the
    /// call sites is unchanged.
    /// </remarks>
    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (true)
        {
            await window.RenderCurrentModuleAsync();
            if (condition() || DateTime.UtcNow >= deadline)
                return;

            await Task.Delay(10);
        }
    }

    /// <summary>The tasks surface, or null while the tree holds no single one.</summary>
    private static ProjectTasksView? TasksSurfaceOrNull(MainWindow window)
    {
        var found = window.GetLogicalDescendants().OfType<ProjectTasksView>().Distinct().ToList();
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>Types <paramref name="answer"/> into the shell's input dialog and confirms it.</summary>
    private static async Task AnswerDialogAsync(MainWindow window, string answer)
    {
        var dialog = window.GetLogicalDescendants().OfType<InputDialog>().Single();

        // `TD-119`: the prompt is raised on an asynchronous continuation, so the
        // dialog need not be showing yet when this helper is called — a second,
        // distinct race from the fixed wait below, which remains disclosed debt.
        // Bounded wait on its real visibility before typing into it.
        var dialogDeadline = DateTime.UtcNow.AddSeconds(2);
        while (!dialog.IsVisible && DateTime.UtcNow < dialogDeadline)
            await Task.Delay(10);

        Assert.True(dialog.IsVisible, "The input dialog never became visible.");

        var textBox = dialog.GetLogicalDescendants().OfType<TextBox>().Single();
        textBox.Text = answer;

        var ok = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
        ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        // `TD-119`: no fixed wait. The dialog is answered exactly once above; the
        // work that releases is joined at the caller's own assertion.
    }

    private static async Task<Guid> CreatePartAsync(EngineeringDomainContext domain, string identifier, string name, Guid parentId)
    {
        var factory = new EngineeringObjectFactory<Part>(
            Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.Part, domain,
            (d, r) => new Part(d, r, domain, identifier, name, EngineeringObjectMetadata.Empty));

        var part = await factory.CreateAsync($"Part {identifier}.");
        await ((IHasParent)part).MoveAsync(parentId);
        return part.Id;
    }
}
