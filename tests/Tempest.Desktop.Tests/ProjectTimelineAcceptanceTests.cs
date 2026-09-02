using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Timeline area, driven end to end through the real
/// <see cref="MainWindow"/>: <b>project → Timeline → set a milestone → add a
/// deliverable → link a task to it from the Tasks area → the milestone shows
/// what it is carrying → restart → it is all still there.</b>
/// </summary>
/// <remarks>
/// <para>
/// Nothing here calls <see cref="IProjectMilestoneService"/> directly for the
/// main journey. Every step goes through the rendered surface, because the
/// defect this Work Package closes was a pair of real domain types
/// (<c>Milestone</c>, <c>Deliverable</c>) that nothing in the product ever
/// created and no surface ever showed.
/// </para>
/// <para>
/// The restart is a second real <see cref="WorkspaceHost"/> over the same
/// persistence root, which is what a relaunch actually is.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectTimelineAcceptanceTests
{
    [AvaloniaFact]
    public async Task Journey_SetAMilestone_AddADeliverable_LinkWork_ThenRelaunch()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        Guid milestoneId;
        Guid deliverableId;

        // ============================================================
        // FIRST LAUNCH
        // ============================================================
        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var window = new MainWindow(first);

            var project = await first.ProjectDirectory!.CreateAsync("P-0081", "Apollo Pump Redesign");
            projectId = project.Id;

            await GoToTimelineAsync(first, window, projectId);

            var timeline = TimelineSurfaceOf(window);

            // --- 1. It starts genuinely empty, and says so ----------
            Assert.True(timeline.IsShowingEmptyState);
            Assert.Contains(
                ProjectTimelineView.EmptyHeadline,
                timeline.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty));

            // --- 2. Set one through the real button and dialogs -----
            await ClickAsync(timeline, "Set Milestone");
            await AnswerDialogAsync(window, "Critical Design Review");
            await AnswerDialogAsync(window, "2026-11-30");
            await RenderUntilAsync(window, () => TimelineSurfaceOrNull(window) is { } t && t.Milestones.Count == 1);

            timeline = TimelineSurfaceOf(window);
            var milestone = Assert.Single(timeline.Milestones);
            milestoneId = milestone.ObjectId;

            Assert.False(timeline.IsShowingEmptyState);
            Assert.Equal("Critical Design Review", milestone.DisplayName);
            Assert.Equal(new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero), milestone.TargetDate);

            // A milestone nobody has attached anything to says so, rather
            // than looking finished.
            Assert.False(milestone.HasLinkedWork);

            // --- 3. Add a deliverable against it --------------------
            await ClickWhenPresentAsync(() => TimelineSurfaceOf(window), "Add Deliverable");
            await AnswerDialogAsync(window, "Stress report");
            await RenderUntilAsync(window, () => TimelineSurfaceOrNull(window) is { Milestones.Count: 1 } t && t.Milestones[0].Deliverables.Count == 1);

            milestone = Assert.Single(TimelineSurfaceOf(window).Milestones);
            deliverableId = Assert.Single(milestone.Deliverables).ObjectId;

            Assert.Equal("Stress report", Assert.Single(milestone.Deliverables).DisplayName);
            Assert.True(milestone.HasLinkedWork);

            // --- 4. Link real work to the deliverable ---------------
            // Through the service rather than the surface: linking a task to
            // a milestone is the Tasks area's own action, and this Work
            // Package did not add a second way to do it.
            var task = await first.ProjectTaskWorkflow!.CreateAsync(projectId, "TSK-001", "Run the stress case");
            await first.ProjectTaskWorkflow!.ContributeToAsync(task.Id, deliverableId);

            await RenderUntilAsync(window, () => TimelineSurfaceOrNull(window) is { Milestones.Count: 1 } t && t.Milestones[0].Contributions.Count == 1);

            milestone = Assert.Single(TimelineSurfaceOf(window).Milestones);
            var contribution = Assert.Single(milestone.Contributions);

            // The work reached the milestone through the deliverable, and
            // the register kept which route it took.
            Assert.Equal(task.Id, contribution.ObjectId);
            Assert.True(contribution.IsIndirect);
            Assert.Equal(deliverableId, contribution.ViaDeliverableId);
            Assert.Equal(1, milestone.OpenContributionCount);
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

            await GoToTimelineAsync(second, window, projectId);
            await RenderUntilAsync(window, () => TimelineSurfaceOrNull(window) is { } t && t.Milestones.Count == 1);

            var milestone = Assert.Single(TimelineSurfaceOf(window).Milestones);

            Assert.Equal(milestoneId, milestone.ObjectId);
            Assert.Equal("Critical Design Review", milestone.DisplayName);
            Assert.Equal(new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero), milestone.TargetDate);

            // The deliverable, its parenting, and the contributing link all
            // came back through the production rehydration path (`TD-104`).
            Assert.Equal(deliverableId, Assert.Single(milestone.Deliverables).ObjectId);
            Assert.Equal(deliverableId, Assert.Single(milestone.Contributions).ViaDeliverableId);

            // And membership survived: the deliverable reaches the project
            // through its milestone, not through a field.
            Assert.Equal(projectId, await ProjectMembership.ResolveOwningProjectAsync(DomainOf(second).Repository, deliverableId));
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AMilestoneDateIsParsedStrictly_AndARubbishDateIsRefused()
    {
        // The date a project commits to must mean the same thing on every
        // machine that opens the file, so it is parsed exactly and in the
        // invariant culture rather than guessed at.
        Assert.Equal(
            new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero),
            ProjectDeliveryCoordinator.ParseTargetDate("2026-11-30"));

        Assert.Null(ProjectDeliveryCoordinator.ParseTargetDate("30/11/2026"));
        Assert.Null(ProjectDeliveryCoordinator.ParseTargetDate("November 30"));
        Assert.Null(ProjectDeliveryCoordinator.ParseTargetDate("2026-13-01"));
        Assert.Null(ProjectDeliveryCoordinator.ParseTargetDate(""));
        Assert.Null(ProjectDeliveryCoordinator.ParseTargetDate(null));
    }

    [AvaloniaFact]
    public async Task TheTimelineAreaIsMarkedImplemented_AndDrawsNoDeclaredCapabilityCard()
    {
        Assert.True(ProjectAreas.IsImplemented(ProjectArea.Timeline));
        Assert.Null(ProjectAreas.For(ProjectArea.Timeline).TrackedBy);

        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0081", "Apollo");
            await GoToTimelineAsync(host, window, project.Id);

            // Asserted against the tab's own content, not the window: the
            // project workspace builds every area's content up front, so
            // cards for the areas that genuinely are Declared legitimately
            // exist in the logical tree.
            var timelineTab = window.GetLogicalDescendants().OfType<TabItem>()
                .Distinct()
                .Single(tab => tab.Tag is ProjectArea.Timeline);

            Assert.IsType<ProjectTimelineView>(timelineTab.Content);

            Assert.DoesNotContain(
                ((Control)timelineTab.Content!).GetLogicalDescendants().OfType<Control>(),
                child => child is DeclaredCapabilityView);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static async Task GoToTimelineAsync(WorkspaceHost host, MainWindow window, Guid projectId)
    {
        await host.ShellNavigator!.OpenProjectAsync(projectId, ProjectArea.Timeline);
        await window.RenderCurrentModuleAsync();
    }

    private static ProjectTimelineView TimelineSurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<ProjectTimelineView>().Distinct().Single();

    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    private static List<string> ButtonCaptions(Control surface) =>
        [.. surface.GetLogicalDescendants().OfType<Button>().Select(b => b.Content?.ToString() ?? string.Empty)];

    private static async Task ClickAsync(Control surface, string caption)
    {
        var button = surface.GetLogicalDescendants().OfType<Button>()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

        Assert.True(button is not null, $"No '{caption}' button on this surface. Present: {string.Join(", ", ButtonCaptions(surface))}");

        button!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        // `TD-119`: no fixed wait. The click is dispatched fire-and-forget, so a
        // duration here would only be a guess; every caller now joins on the real
        // state its own assertion reads, through `RenderUntilAsync`.
    }

    /// <summary>Clicks <paramref name="caption"/> once it is actually present on the freshly re-queried surface.</summary>
    /// <remarks>
    /// `TD-119`. Row-level buttons such as "Add Deliverable" exist only once the
    /// row they belong to has rendered, which happens on an asynchronous
    /// continuation. <see cref="ClickAsync"/> deliberately still fails at once for
    /// a button that ought to be there already; this is for targets legitimately
    /// produced asynchronously. The surface is re-queried every iteration, the
    /// click is raised exactly once, and a button that never appears fails with
    /// the same message <see cref="ClickAsync"/> would give.
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
    /// `TD-119`. Rendering is a read — `ProjectWorkspaceView.RefreshAsync` lists
    /// and shows — so this loop cannot manufacture the state it waits for. It
    /// decides only *when* to assert; every assertion at the call sites is
    /// unchanged, and still fails on its own message if the state never arrives.
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

    /// <summary>The timeline surface, or null while the tree holds no single one.</summary>
    /// <remarks>
    /// Used only to decide when to stop re-rendering. Every assertion reads
    /// through <see cref="TimelineSurfaceOf"/> directly and unguarded, so a tree
    /// that never settles still fails there rather than being swallowed here.
    /// </remarks>
    private static ProjectTimelineView? TimelineSurfaceOrNull(MainWindow window)
    {
        var found = window.GetLogicalDescendants().OfType<ProjectTimelineView>().Distinct().ToList();
        return found.Count == 1 ? found[0] : null;
    }

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
}
