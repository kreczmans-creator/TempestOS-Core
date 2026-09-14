using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.10H`, `TD-179`: the project workspace stops contradicting its own
/// "Archived — nothing here can be changed" banner. Opens a project closed
/// 91 days ago (Archive, not merely Closed — <see cref="ProjectArchival.ArchiveAfterDays"/>
/// is 90), through the real window, and walks the tabs this Work Package
/// owns: every write control it disables is checked for its disabled state
/// and its "Archived project — read only" tooltip; New Quote (a write the
/// brief's own list of controls to disable does not name — its service,
/// <see cref="QuotationService"/>, already refuses it) stays clickable.
/// Reopen is checked separately, below.
/// </summary>
/// <remarks>
/// <b>Reopen, on a project genuinely 91 days closed, cannot both stay
/// live and succeed.</b> <see cref="IProjectLifecycleService.ReopenAsync"/>
/// refuses once <see cref="ProjectArchival.IsArchived"/> is true — the
/// identical fact this test's own disabled-controls assertions depend on,
/// read from the same real clock. A reopen that succeeded would mean the
/// project was not actually Archive, and the controls above would not have
/// been disabled to begin with. So rather than fabricate a scenario the
/// domain's own 90-day rule forbids, this test proves the other honest
/// half: Reopen stays enabled (not disabled by the new archived-flag
/// mechanism — it is deliberately left out of every list of controls this
/// Work Package's brief disables) and reaches the real
/// <see cref="IProjectLifecycleService"/>, which answers with the same
/// refusal <c>PHYSICAL_REVIEW.md</c> §7c step D19 already documents
/// ("it is Archive and read-only. It cannot be reopened.") — a live
/// control giving an honest answer, not a dead one.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ArchivedProjectReadOnlyTests
{
    [AvaloniaFact]
    public async Task AnArchivedProject_DisablesEveryReachableWriteControl_WithTheTooltip_AndReopenStaysLiveButIsHonestlyRefused()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-ARCH-1", "Archived Project Journey");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            // ================================================================
            // One entry per tab, created while the project is still Open —
            // every write below is refused by the guard this Work Package
            // added to the services underneath, but the point of this test
            // is the shell: the control itself must read as disabled.
            // ================================================================
            var task = await host.ProjectTaskWorkflow!.CreateAsync(project.Id, "TSK-ARCH", "Balance the impeller");
            var milestone = await host.ProjectMilestoneWorkflow!.CreateMilestoneAsync(
                project.Id, "MS-ARCH", "Design freeze", DateTimeOffset.UtcNow.AddDays(30));

            var quotationService = (IQuotationService)host.Services!.GetService(typeof(IQuotationService));

            var draft = await quotationService.CreateAsync(project.Id, "Q-ARCH-DRAFT");
            Assert.True(draft.Succeeded, draft.Reason);
            var draftQuoteId = draft.Quotation!.Id;
            await quotationService.AddLineAsync(draftQuoteId, "Concept design", 10m, new Money(100m, draft.Quotation.Currency), null);

            var toBeSent = await quotationService.CreateAsync(project.Id, "Q-ARCH-SENT");
            Assert.True(toBeSent.Succeeded, toBeSent.Reason);
            var sentQuoteId = toBeSent.Quotation!.Id;
            await quotationService.AddLineAsync(sentQuoteId, "Detailed pack", null, null, new Money(2500m, toBeSent.Quotation.Currency));
            var sendResult = await quotationService.SendAsync(sentQuoteId);
            Assert.True(sendResult.Succeeded, sendResult.Reason);

            // ================================================================
            // Close the project 91 days ago — Archive, not merely Closed.
            // ================================================================
            var lifecycle = new ProjectLifecycleService(domain, new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-91)));
            var closed = await lifecycle.SignOffAsync(project.Id, "Closed for the archived-read-only journey test.");
            Assert.True(closed.Succeeded, closed.Reason);
            var closedOnBefore = ((Project)(await domain.Repository.FindAsync(project.Id))!).ClosedOn;

            // ================================================================
            // Re-enter the way the application does — walking two tabs
            // explicitly, then the shared workspace refresh (every tab's
            // own content is rebuilt on every entry, `ProjectWorkspaceView.RefreshAsync`)
            // covers the rest.
            // ================================================================
            await navigator.OpenProjectAsync(project.Id, ProjectArea.Tasks).ConfigureAwait(true);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
            var tasksView = projectWorkspace.TasksView;
            await RenderUntilAsync(window, () => tasksView.Entries.Count == 1);

            await navigator.OpenProjectAsync(project.Id, ProjectArea.Timeline).ConfigureAwait(true);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var timelineView = projectWorkspace.TimelineView;
            await RenderUntilAsync(window, () => timelineView.Milestones.Count == 1);
            LayOut(window);

            // ---- Tasks tab: New Task, and every per-entry write control ----
            AssertDisabledWithTooltip(tasksView, "New Task");
            AssertDisabledWithTooltip(tasksView, "Edit");
            AssertDisabledWithTooltip(tasksView, "Assign to me");
            AssertDisabledWithTooltip(tasksView, "Due date");
            AssertDisabledWithTooltip(tasksView, "Done"); // the work-state move a fresh Todo task offers

            // ---- Timeline tab: Set Milestone, Edit, Add Deliverable ----
            AssertDisabledWithTooltip(timelineView, "Set Milestone");
            AssertDisabledWithTooltip(timelineView, "Edit");
            AssertDisabledWithTooltip(timelineView, "Add Deliverable");

            // ---- Evidence tab: Create ----
            var evidenceView = window.GetLogicalDescendants().OfType<EvidenceWorkspaceView>().First();
            AssertDisabledWithTooltip(evidenceView, "Create");

            // ---- Quote tab: Add line, Send, Accept, Decline (and Edit/Remove on lines) ----
            var quoteView = projectWorkspace.QuoteView;
            await quoteView.SelectQuoteAsync(draftQuoteId).ConfigureAwait(true);
            LayOut(window);
            AssertDisabledWithTooltip(quoteView, "Add line");
            AssertDisabledWithTooltip(quoteView, "Edit");
            AssertDisabledWithTooltip(quoteView, "Remove");
            AssertDisabledWithTooltip(quoteView, "Send");

            // New Quote is deliberately not in the brief's own list of
            // controls to disable — QuotationService.CreateAsync already
            // refuses it on an archived project, so this proves the button
            // itself was left alone, not forgotten.
            var newQuoteButton = quoteView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Quote"));
            Assert.True(newQuoteButton.IsEnabled, "New Quote is not in this Work Package's own disabled-control list.");

            await quoteView.SelectQuoteAsync(sentQuoteId).ConfigureAwait(true);
            LayOut(window);
            AssertDisabledWithTooltip(quoteView, "Accept");
            AssertDisabledWithTooltip(quoteView, "Decline");

            // ---- Sign off tab: Reopen stays live, and is honestly refused ----
            var signOffView = window.GetLogicalDescendants().OfType<ProjectSignOffView>().First();
            var reopenButton = signOffView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Reopen"));
            Assert.True(reopenButton.IsVisible, "Reopen must still be visible once closed.");
            Assert.True(reopenButton.IsEnabled, "Reopen must stay enabled — it is not in this Work Package's own disabled-control list, and reaches a service that already refuses it correctly.");

            reopenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                signOffView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("cannot be reopened", StringComparison.Ordinal)));
            LayOut(window);

            Assert.Contains(
                signOffView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("cannot be reopened", StringComparison.Ordinal));

            var reloadedProject = (Project)(await domain.Repository.FindAsync(project.Id))!;
            Assert.Equal(closedOnBefore, reloadedProject.ClosedOn); // the refused Reopen changed nothing

            // The refused Reopen changed nothing domain-side, so every
            // control checked above is still exactly as disabled as it was.
            AssertDisabledWithTooltip(tasksView, "New Task");
            AssertDisabledWithTooltip(timelineView, "Set Milestone");
            AssertDisabledWithTooltip(evidenceView, "Create");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void AssertDisabledWithTooltip(Control root, string buttonContent)
    {
        var button = root.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, buttonContent));
        Assert.False(button.IsEnabled, $"'{buttonContent}' should be disabled on an archived project.");
        Assert.Equal("Archived project — read only", ToolTip.GetTip(button));
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
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

    /// <summary>A clock the test pins — mirrors <c>Tempest.Core.Tests.FakeTimeProvider</c>, which this assembly cannot reference (it is <see langword="internal"/> to a different assembly).</summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
