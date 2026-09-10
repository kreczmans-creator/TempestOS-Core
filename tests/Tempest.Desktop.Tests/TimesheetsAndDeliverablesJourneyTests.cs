using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 19.0A` part 2 acceptance journeys (`ADR-0150`): through the real
/// window, driving the real Commercial-section pickers, the real Timesheets
/// area and the real project Deliverables tab.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class TimesheetsAndDeliverablesJourneyTests
{
    /// <summary>
    /// Acceptance 1 and 3: set client, rate card and dates through the
    /// editor's Commercial section; record three entries across two days;
    /// the week shows day and week totals and the utilisation; Amend one,
    /// Delete another; an invoiced entry (marked through the service)
    /// refuses both, named in the status bar; a fourth entry recorded
    /// through the service — not the view — shows up with no refresh call
    /// in this test; restart, and the week is still there.
    /// </summary>
    [AvaloniaFact]
    public async Task CommercialSectionThenTimesheets_FullJourney()
    {
        const string organisationId = "ORG-TS-JOURNEY";
        const string rateCardId = "CARD-TS-JOURNEY";
        const string grade = "Senior";

        // The current week, not a fixed date: `TimesheetWeekView` defaults
        // to today's own week on construction (no jump-to-date affordance),
        // so recording against — and later restarting into — the same week
        // this test actually runs in is what makes the restart assertion
        // land on the real default view rather than requiring week
        // navigation of its own.
        var monday = TimesheetWeek.WeekOf(DateOnly.FromDateTime(DateTime.Now));
        var tuesday = monday.AddDays(1);

        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid projectId;
        Guid invoicedEntryId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            await RegisterReleasedRateCardAndClientAsync(host, organisationId, rateCardId, grade);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-TS-1", "Timesheets Journey Project");
            projectId = project.Id;
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            // ---- Commercial section: client, rate card, dates ----
            var editor = ObjectEditorViewFor(window, project.Id, "Project");
            Assert.NotNull(editor);
            AssertSectionPresent(editor!, "Commercial");

            await ChangeClientViaRealDialogAsync(window, editor!, organisationId);
            await PinRateCardViaRealDialogAsync(window, editor!, rateCardId);

            var startDatePicker = editor!.GetLogicalDescendants().OfType<DatePicker>().First();
            var targetDatePicker = editor.GetLogicalDescendants().OfType<DatePicker>().Skip(1).First();
            startDatePicker.SelectedDate = new DateTimeOffset(new DateTime(2026, 3, 1));
            targetDatePicker.SelectedDate = new DateTimeOffset(new DateTime(2026, 9, 1));
            editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save Dates")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            await RenderUntilAsync(window, () =>
                ((Project)domain.Repository.FindAsync(project.Id).GetAwaiter().GetResult()!).StartDate == new DateOnly(2026, 3, 1));

            var afterCommercial = (Project)(await domain.Repository.FindAsync(project.Id).ConfigureAwait(true))!;
            Assert.Equal(organisationId, afterCommercial.ClientOrganisationId);
            Assert.NotNull(afterCommercial.RateCardPin);
            Assert.Equal(rateCardId, afterCommercial.RateCardPin!.RecordId);
            Assert.Equal(new DateOnly(2026, 3, 1), afterCommercial.StartDate);
            Assert.Equal(new DateOnly(2026, 9, 1), afterCommercial.TargetDate);

            // ---- rail → Timesheets ----
            await navigator.GoToModuleAsync(ShellArea.Timesheets);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var week = GetPrivateField<TimesheetWeekView>(window, "_timesheetWeekView");
            week.ParameterPrompt = StubAmendDeletePrompt();

            await RecordViaRealDialogAsync(window, week, projectId, monday, 4m, true, grade, "Design work");
            await RecordViaRealDialogAsync(window, week, projectId, monday, 2m, false, grade, "Admin");
            await RecordViaRealDialogAsync(window, week, projectId, tuesday, 3m, true, grade, "Review");

            var timesheets = (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));
            IReadOnlyList<TimesheetEntry> entries = [];
            await RenderUntilAsync(window, () =>
            {
                entries = timesheets.ListForPrincipalWeekAsync(host.SessionPrincipal!.IdentityId, TimesheetWeek.WeekOf(monday)).GetAwaiter().GetResult();
                return entries.Count == 3;
            });
            Assert.Equal(3, entries.Count);

            // Record's own one-off confirmation shares the status line with
            // the week's own persistent summary (`EvidenceWorkspaceView.OnCreateAsync`'s
            // own identical "Report after Refresh" shape) — a clean
            // navigate-away-and-back re-renders the summary with nothing to
            // clobber it.
            var nextWeek = week.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Next ▶"));
            nextWeek.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                week.WeekStart != monday
                && week.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("No time recorded", StringComparison.Ordinal)));
            var previousWeek = week.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "◀ Previous"));
            previousWeek.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                week.WeekStart == monday
                && week.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("billable.", StringComparison.Ordinal)));

            // Day and week totals, and the utilisation for the week.
            var texts = week.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(texts, t => t.Contains("9h total", StringComparison.Ordinal) && t.Contains("7h billable", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.Contains("Utilisation:", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.Contains("6h", StringComparison.Ordinal)); // Monday's own day total (4h + 2h)
            Assert.Contains(texts, t => t.Contains("3h", StringComparison.Ordinal)); // Tuesday's own day total

            // Captured before any mutation — a row's own task description
            // changes under Amend, so the id (never the description) is
            // what identifies an entry from here on.
            var designWorkEntryId = entries.First(e => e.TaskDescription == "Design work").Id;
            var adminEntryId = entries.First(e => e.TaskDescription == "Admin").Id;
            var reviewEntryId = entries.First(e => e.TaskDescription == "Review").Id;

            // ---- Amend "Design work" ----
            var amendButton = week.GetLogicalDescendants().OfType<Button>()
                .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Amend Design work");
            amendButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                ((TimesheetEntry)domain.Repository.FindAsync(designWorkEntryId).GetAwaiter().GetResult()!).TaskDescription == "Amended design work");

            // ---- Delete "Admin" ----
            var deleteButton = week.GetLogicalDescendants().OfType<Button>()
                .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Delete Admin");
            deleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => ((TimesheetEntry)domain.Repository.FindAsync(adminEntryId).GetAwaiter().GetResult()!).IsDeleted);

            // ---- Invoiced entry refuses both Amend and Delete, named in the status bar ----
            invoicedEntryId = reviewEntryId;
            var invoiceResult = await timesheets.MarkInvoicedAsync(invoicedEntryId, Guid.NewGuid()).ConfigureAwait(true);
            Assert.True(invoiceResult.Succeeded);

            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");

            var amendReview = week.GetLogicalDescendants().OfType<Button>()
                .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Amend Review");
            amendReview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("already invoiced", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.Contains("already invoiced", StringComparison.OrdinalIgnoreCase));

            var deleteReview = week.GetLogicalDescendants().OfType<Button>()
                .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Delete Review");
            deleteReview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("already invoiced", StringComparison.OrdinalIgnoreCase)));
            var stillThere = (TimesheetEntry)(await domain.Repository.FindAsync(invoicedEntryId).ConfigureAwait(true))!;
            Assert.False(stillThere.IsDeleted);

            // ---- Acceptance 3: a fourth entry recorded through the service, not the view, shows up with no refresh call in this test ----
            var direct = await timesheets.RecordAsync(projectId, monday, 1m, true, grade, "Feed test", CancellationToken.None).ConfigureAwait(true);
            Assert.True(direct.Succeeded, direct.Reason);
            await RenderUntilAsync(window, () =>
                week.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("Feed test", StringComparison.Ordinal)));
            Assert.Contains(
                week.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.Contains("Feed test", StringComparison.Ordinal));

            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
        catch
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
            throw;
        }

        // ============================================================
        // RESTART — a new host, the same persistence root.
        // ============================================================
        var secondHost = new WorkspaceHost(root);
        try
        {
            await secondHost.StartAsync();
            var window = new MainWindow(secondHost);
            var navigator = secondHost.ShellNavigator!;

            var project = await secondHost.ProjectDirectory!.FindAsync(projectId);
            Assert.NotNull(project);
            await navigator.OpenProjectAsync(project!.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.Timesheets);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var week = GetPrivateField<TimesheetWeekView>(window, "_timesheetWeekView");
            await RenderUntilAsync(window, () =>
                week.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("Amended design work", StringComparison.Ordinal)));

            var recovered = week.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(recovered, t => t.Contains("Amended design work", StringComparison.Ordinal));
            Assert.DoesNotContain(recovered, t => t.Contains("Admin", StringComparison.Ordinal));
        }
        finally
        {
            await secondHost.ShutdownAsync();
            await secondHost.DisposeAsync();
        }
    }

    /// <summary>Acceptance 2: complete a deliverable with one Issued evidence; the completion opens right up; a second Complete refuses, naming the first.</summary>
    [AvaloniaFact]
    public async Task Deliverables_CompleteWithIssuedEvidence_OpensRightUp_SecondCompleteRefusesNamingTheFirst()
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
            var project = await host.ProjectDirectory!.CreateAsync("P-DLV-1", "Deliverables Journey Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var milestone = await host.ProjectMilestoneWorkflow!.CreateMilestoneAsync(project.Id, "M-1", "Milestone One", DateTimeOffset.UtcNow.AddMonths(1));
            var deliverable = await host.ProjectMilestoneWorkflow!.CreateDeliverableAsync(project.Id, milestone.Id, "D-1", "Deliverable One");

            var evidence = await host.EvidenceService!.CreateAsync(project.Id, "Supporting Evidence", EvidenceClassification.Calculation);
            await host.EvidenceService!.RecordCheckAsync(evidence.Id, "Checker Name", "Checker Org", "Looks fine.", CheckOutcome.Accepted).ConfigureAwait(true);
            await host.EvidenceService!.IssueAsync(evidence.Id, "ISS-1", "A", "Client Co").ConfigureAwait(true);

            await navigator.OpenProjectAsync(project.Id, ProjectArea.Deliverables);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
            var deliverablesView = projectWorkspace.DeliverablesView;
            await RenderUntilAsync(window, () => deliverablesView.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, "Complete")));

            var prompt = GetPrivateField<DeliverableCompletionPrompt>(window, "_deliverableCompletionPrompt");

            var completeButton = deliverablesView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Complete"));
            completeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => prompt.IsVisible);

            var evidenceListBox = prompt.GetLogicalDescendants().OfType<ListBox>().First();
            await RenderUntilAsync(window, () => evidenceListBox.ItemsSource is not null && evidenceListBox.ItemsSource.Cast<ListBoxItem>().Any());
            var evidenceItem = evidenceListBox.ItemsSource!.Cast<ListBoxItem>().First(i => Equals(i.Content, evidence.DisplayName));
            evidenceListBox.SelectedItems!.Add(evidenceItem);

            var completeDialogButton = prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Complete"));
            completeDialogButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !prompt.IsVisible);

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            DeliverableCompletion? completion = null;
            await RenderUntilAsync(window, () =>
            {
                completion = domain.Repository.ListByKindAsync(DeliverableCompletion.CanonicalKind).GetAwaiter().GetResult()
                    .OfType<DeliverableCompletion>().FirstOrDefault(c => c.DeliverableId == deliverable.Id);
                return completion is not null;
            });
            Assert.NotNull(completion);
            Assert.Single(completion!.IssuedEvidenceIds);
            Assert.Equal(evidence.Id, completion.IssuedEvidenceIds[0]);

            // The completion opens right up.
            ObjectEditorView? editor = null;
            await RenderUntilAsync(window, () =>
            {
                editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
                return editor is not null;
            });
            Assert.NotNull(editor);

            // A second Complete refuses, naming the first.
            completeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => prompt.IsVisible);
            prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Complete")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !prompt.IsVisible);

            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains(completion!.Id.ToString(), StringComparison.Ordinal)));
            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.Contains(completion!.Id.ToString(), StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The new area and tab this Work Package adds lay out cleanly, at both
    /// window sizes — the same two sizes and the same soundness check
    /// `LayoutWalkTests` (`WP 19.3A`) applies, scoped here to exactly the
    /// two new surfaces: the shared walk fails earlier, at the very first
    /// rail entry (Home), on an unrelated, pre-existing docking-layout
    /// finding outside this Work Package's own files — see this Work
    /// Package's report.
    /// </summary>
    [AvaloniaFact]
    public async Task TimesheetsRailAndDeliverablesTab_LayOutCleanly_AtBothWindowSizes()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-LAYOUT-TS", "Layout Check Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            foreach (var (width, height) in new (double, double)[] { (1600, 900), (1180, 760) })
            {
                await navigator.GoToModuleAsync(ShellArea.Timesheets);
                await window.RenderCurrentModuleAsync();
                LayOutWindow(window, width, height);
                AssertLayoutIsSound(window, $"{width}x{height} · Timesheets");

                await navigator.OpenProjectAsync(project.Id, ProjectArea.Deliverables);
                await window.RenderCurrentModuleAsync();
                LayOutWindow(window, width, height);
                AssertLayoutIsSound(window, $"{width}x{height} · Deliverables");
            }
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ---------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------

    private static async Task RegisterReleasedRateCardAndClientAsync(WorkspaceHost host, string organisationId, string rateCardId, string grade)
    {
        var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
        var rateCards = (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

        await organisations.RegisterAsync(
            organisationId, new Organisation { Reference = organisationId, Name = "Journey Client Ltd" }, ReferenceProvenance.Unknown).ConfigureAwait(true);

        var card = new RateCard
        {
            Code = rateCardId,
            Name = "Journey Rate Card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership("owner-1", "Owner") },
            Entries = [new RateCardEntry("SVC-1", "Senior Engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: grade)],
        };
        await rateCards.RegisterAsync(rateCardId, card, new ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc")).ConfigureAwait(true);
        await host.ReferenceReview!.VerifyAsync(rateCards, rateCardId, new ReferenceReviewStatement("Consulted for the journey test.")).ConfigureAwait(true);
        await host.ReferenceReview!.ReleaseAsync(rateCards, rateCardId, "Released for the journey test.").ConfigureAwait(true);
    }

    /// <summary>A stub prompt for <c>timesheet.amend</c>/<c>timesheet.delete</c>: amend retitles to "Amended design work"; delete just confirms.</summary>
    private static Tempest.Core.Commands.CommandParameterPrompt StubAmendDeletePrompt() =>
        (descriptor, parameters, confirmationMessage, cancellationToken) =>
        {
            if (descriptor.Id == Tempest.Workspace.Timesheets.TimesheetCommandIds.Amend)
            {
                return Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string>
                {
                    ["hours"] = "5",
                    ["task"] = "Amended design work",
                    ["billable"] = "True",
                });
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string>());
        };

    private static async Task ChangeClientViaRealDialogAsync(MainWindow window, ObjectEditorView editor, string organisationId)
    {
        var changeClient = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Change Client"));
        changeClient.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var picker = GetPrivateField<OrganisationPicker>(window, "_organisationPicker");
        await RenderUntilAsync(window, () => picker.GetLogicalDescendants().OfType<ListBoxItem>().Any());

        var list = picker.GetLogicalDescendants().OfType<ListBox>().Single();
        list.SelectedItem = list.ItemsSource!.Cast<ListBoxItem>().First(i => ((string)i.Content!).Contains(organisationId, StringComparison.Ordinal));

        picker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Choose")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await RenderUntilAsync(window, () => !picker.IsVisible);
    }

    private static async Task PinRateCardViaRealDialogAsync(MainWindow window, ObjectEditorView editor, string rateCardId)
    {
        var changeRateCard = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Pin Rate Card"));
        changeRateCard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var picker = GetPrivateField<RateCardPicker>(window, "_rateCardPicker");
        await RenderUntilAsync(window, () => picker.GetLogicalDescendants().OfType<ListBoxItem>().Any());

        var list = picker.GetLogicalDescendants().OfType<ListBox>().Single();
        list.SelectedItem = list.ItemsSource!.Cast<ListBoxItem>().First(i => ((string)i.Content!).Contains(rateCardId, StringComparison.Ordinal));

        picker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Pin")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await RenderUntilAsync(window, () => !picker.IsVisible);
    }

    private static async Task RecordViaRealDialogAsync(
        MainWindow window, TimesheetWeekView week, Guid projectId, DateOnly date, decimal hours, bool billable, string grade, string task)
    {
        var recordButton = week.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Record"));
        recordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var prompt = GetPrivateField<TimesheetEntryPrompt>(window, "_timesheetEntryPrompt");
        await RenderUntilAsync(window, () => prompt.IsVisible);

        var projectCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().First();
        await RenderUntilAsync(window, () => projectCombo.ItemsSource is not null && projectCombo.ItemsSource.Cast<ComboBoxItem>().Any());
        projectCombo.SelectedItem = projectCombo.ItemsSource!.Cast<ComboBoxItem>().First(i => (Guid)i.Tag! == projectId);

        var gradeCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().Skip(1).First();
        await RenderUntilAsync(window, () => gradeCombo.ItemsSource is not null && gradeCombo.ItemsSource.Cast<string>().Any());
        gradeCombo.SelectedItem = grade;

        var datePicker = prompt.GetLogicalDescendants().OfType<DatePicker>().First();
        datePicker.SelectedDate = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));

        var hoursUpDown = prompt.GetLogicalDescendants().OfType<NumericUpDown>().First();
        hoursUpDown.Value = hours;

        var billableCheck = prompt.GetLogicalDescendants().OfType<CheckBox>().First();
        billableCheck.IsChecked = billable;

        var taskBox = prompt.GetLogicalDescendants().OfType<TextBox>().First();
        taskBox.Text = task;

        var record = prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Record"));
        record.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await RenderUntilAsync(window, () => !prompt.IsVisible);
    }

    private static ObjectEditorView? ObjectEditorViewFor(MainWindow window, Guid id, string kind)
    {
        var navigateInternal = GetPrivateMethod(window, "OpenCreatedObjectAsync");
        ((Task)navigateInternal.Invoke(window, [id, kind])!).GetAwaiter().GetResult();
        LayOut(window);

        return window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
    }

    private static void AssertSectionPresent(ObjectEditorView editor, string title) =>
        Assert.Contains(editor.GetLogicalDescendants().OfType<Expander>(), e => Equals(e.Header, title) && e.IsVisible);

    private static System.Reflection.MethodInfo GetPrivateMethod(object instance, string name) =>
        instance.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{name}' not found on {instance.GetType().Name}.");

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(20);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1900, 1050));
            window.Arrange(new Rect(0, 0, 1900, 1050));
        }
    }

    private static void LayOutWindow(Window window, double width, double height)
    {
        if (!window.IsVisible)
            window.Show();

        window.Width = width;
        window.Height = height;

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
        }
    }
}
