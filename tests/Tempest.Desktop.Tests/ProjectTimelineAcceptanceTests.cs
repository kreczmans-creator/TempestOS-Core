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
            await window.RenderCurrentModuleAsync();

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
            await ClickAsync(TimelineSurfaceOf(window), "Add Deliverable");
            await AnswerDialogAsync(window, "Stress report");
            await window.RenderCurrentModuleAsync();

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

            await window.RenderCurrentModuleAsync();

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
        await Task.Delay(20);
    }

    private static async Task AnswerDialogAsync(MainWindow window, string answer)
    {
        var dialog = window.GetLogicalDescendants().OfType<InputDialog>().Single();

        var textBox = dialog.GetLogicalDescendants().OfType<TextBox>().Single();
        textBox.Text = answer;

        var ok = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
        ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        await Task.Delay(50);
    }
}
