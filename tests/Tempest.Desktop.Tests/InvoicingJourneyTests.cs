using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Projects;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 19.1A` part 3 acceptance journeys (`ADR-0151`): through the real
/// window, driving the real Deliverables tab's own Raise action, the real
/// Invoicing area's Send/Reconcile now/Void, and the real Settings
/// dialog's own Invoicing section — the fake connector throughout.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class InvoicingJourneyTests
{
    /// <summary>
    /// Acceptance 1: a completed deliverable with a fixed price, alongside
    /// three unbilled timesheet entries → Deliverables tab → Raise invoice
    /// → the request opens right up with four lines and the total → rail →
    /// Invoicing → Send → Sent with the fake's own invoice number; the
    /// timesheet entries now show invoiced; Raise again on the same,
    /// now-invoiced completion → refusal, naming the first request.
    /// </summary>
    [AvaloniaFact]
    public async Task RaiseSendJourney_FullHappyPath_ThroughDeliverablesAndInvoicingArea()
    {
        const string organisationId = "ORG-INV-1";
        const string rateCardId = "CARD-INV-1";
        const string grade = "Senior";

        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            await RegisterReleasedRateCardAndClientAsync(host, organisationId, rateCardId, grade);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-INV-1", "Invoicing Journey Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var commercial = (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));
            Assert.True((await commercial.SetClientAsync(project.Id, organisationId)).Succeeded);
            Assert.True((await commercial.PinRateCardAsync(project.Id, rateCardId)).Succeeded);

            var timesheets = (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));
            var monday = TimesheetWeek.WeekOf(DateOnly.FromDateTime(DateTime.Now));
            Assert.True((await timesheets.RecordAsync(project.Id, monday, 2m, true, grade, "Design", CancellationToken.None)).Succeeded);
            Assert.True((await timesheets.RecordAsync(project.Id, monday, 3m, true, grade, "Build", CancellationToken.None)).Succeeded);
            Assert.True((await timesheets.RecordAsync(project.Id, monday.AddDays(1), 1m, true, grade, "Test", CancellationToken.None)).Succeeded);

            var milestone = await host.ProjectMilestoneWorkflow!.CreateMilestoneAsync(project.Id, "M-1", "Milestone One", DateTimeOffset.UtcNow.AddMonths(1));
            var deliverable = await host.ProjectMilestoneWorkflow!.CreateDeliverableAsync(project.Id, milestone.Id, "D-1", "Deliverable One");

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

            var fixedPriceBox = prompt.GetLogicalDescendants().OfType<TextBox>().First();
            fixedPriceBox.Text = "500 GBP";

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

            // ---- Deliverables tab → Raise invoice ----
            await ClickButtonWithContentAsync(window, deliverablesView, "Raise invoice");

            InvoiceRequest? request = null;
            await RenderUntilAsync(window, () =>
            {
                request = domain.Repository.ListChildrenAsync(project.Id).GetAwaiter().GetResult().OfType<InvoiceRequest>().FirstOrDefault();
                return request is not null;
            });
            Assert.NotNull(request);
            Assert.Equal(4, request!.Lines.Count);
            Assert.Equal(new Money(2m * 150m + 3m * 150m + 1m * 150m + 500m, CurrencyCode.Gbp), request.Total);

            // The request opens right up, in the Object Editor (Product
            // Owner guard, `WP 17.9.4`). Its own four lines and total are
            // asserted on the domain object directly, just above — not
            // through the editor's own "Lines"/"Connector" sections
            // (`KindEditorDeclarations.InvoiceRequest`'s own
            // `EditorSectionKeys.InvoiceLines`/`InvoicingExternal`): this
            // Work Package found no rendering wired for either key in
            // `ObjectEditorView.cs` (only Identity/Description/WhereUsed/
            // Commercial/BillOfMaterials/Lifecycle are ever checked there) —
            // a gap in a file outside this Work Package's own "files you
            // own" list, disclosed in this Work Package's report rather
            // than worked around here (`brief-common.md`'s own Kill
            // switch).
            ObjectEditorView? editor = null;
            await RenderUntilAsync(window, () =>
            {
                editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
                return editor is not null;
            });
            Assert.NotNull(editor);
            AssertSectionPresent(editor!, "Identity");

            // ---- rail → Invoicing → Send ----
            await navigator.GoToModuleAsync(ShellArea.Invoicing);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var invoicingView = GetPrivateField<InvoicingView>(window, "_invoicingView");
            invoicingView.ParameterPrompt = AutoConfirmPrompt();

            await ClickRequestActionAsync(window, invoicingView, request.Id, "Send");

            await RenderUntilAsync(window, () =>
                ((InvoiceRequest)domain.Repository.FindAsync(request.Id).GetAwaiter().GetResult()!).Status == InvoiceRequestStatus.Sent);

            var sent = (InvoiceRequest)(await domain.Repository.FindAsync(request.Id).ConfigureAwait(true))!;
            Assert.NotNull(sent.ExternalInvoiceNumber);
            Assert.NotNull(sent.ExternalId);

            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains(sent.ExternalInvoiceNumber!, StringComparison.Ordinal)));
            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.Contains("Sent", StringComparison.Ordinal) && t.Text.Contains(sent.ExternalInvoiceNumber!, StringComparison.Ordinal));

            // ---- the timesheet entries now show invoiced ----
            var entries = await timesheets.ListForPrincipalWeekAsync(host.SessionPrincipal!.IdentityId, monday).ConfigureAwait(true);
            Assert.Equal(3, entries.Count);
            Assert.All(entries, e => Assert.NotNull(e.InvoicedBy));

            // ---- Raise again on the same, now-invoiced completion → refusal ----
            await navigator.OpenProjectAsync(project.Id, ProjectArea.Deliverables);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            await ClickButtonWithContentAsync(window, deliverablesView, "Raise invoice");
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("already invoiced", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.Contains("already invoiced", StringComparison.OrdinalIgnoreCase) && t.Text.Contains(request.Id.ToString("N"), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Acceptance 2: the fake told to reject, time out, be unavailable, and
    /// require re-authorisation — each of the Invoicing area's own Send
    /// outcomes, and Reconcile now resolving an Unknown request to Sent.
    /// </summary>
    [AvaloniaFact]
    public async Task SendOutcomes_RejectedUnknownUnavailableReauthorise_ThroughInvoicingArea()
    {
        const string organisationId = "ORG-INV-2";
        const string rateCardId = "CARD-INV-2";
        const string grade = "Senior";

        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            await RegisterReleasedRateCardAndClientAsync(host, organisationId, rateCardId, grade);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-INV-2", "Invoicing Outcomes Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var commercial = (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));
            Assert.True((await commercial.SetClientAsync(project.Id, organisationId)).Succeeded);
            Assert.True((await commercial.PinRateCardAsync(project.Id, rateCardId)).Succeeded);

            var milestone = await host.ProjectMilestoneWorkflow!.CreateMilestoneAsync(project.Id, "M-2", "Milestone Two", DateTimeOffset.UtcNow.AddMonths(1));

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var deliverableService = (IDeliverableService)host.Services!.GetService(typeof(IDeliverableService));
            var invoicingService = (IInvoicingService)host.Services!.GetService(typeof(IInvoicingService));
            var fakeConnector = (FakeInvoicingConnector)host.Services!.GetService(typeof(IInvoicingConnector));

            async Task<InvoiceRequest> RaiseFixedPriceRequestAsync(string identifier, string name, decimal price)
            {
                var deliverable = await host.ProjectMilestoneWorkflow!.CreateDeliverableAsync(project.Id, milestone.Id, identifier, name);
                var completed = await deliverableService.CompleteAsync(
                    deliverable.Id, project.Id, DateOnly.FromDateTime(DateTime.Now), fixedPriceValue: new Money(price, CurrencyCode.Gbp));
                Assert.True(completed.Succeeded, completed.Reason);

                var raised = await invoicingService.RaiseFromCompletionAsync(completed.Completion!.Id);
                Assert.True(raised.Succeeded, raised.Reason);
                return raised.Request!;
            }

            await navigator.GoToModuleAsync(ShellArea.Invoicing);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var invoicingView = GetPrivateField<InvoicingView>(window, "_invoicingView");
            invoicingView.ParameterPrompt = AutoConfirmPrompt();
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");

            // ---- Rejected, with the reason shown ----
            var rejected = await RaiseFixedPriceRequestAsync("D-REJ", "Reject Deliverable", 100m);
            fakeConnector.ScriptNextCreate(ConnectorOutcome.Rejected, "Not registered for this client.");
            await ClickRequestActionAsync(window, invoicingView, rejected.Id, "Send");
            await RenderUntilAsync(window, () =>
                ((InvoiceRequest)domain.Repository.FindAsync(rejected.Id).GetAwaiter().GetResult()!).Status == InvoiceRequestStatus.Rejected);
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("Not registered for this client.", StringComparison.Ordinal)));
            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.Contains("Rejected", StringComparison.Ordinal) && t.Text.Contains("Not registered for this client.", StringComparison.Ordinal));

            // ---- Unknown ("response lost, reconciling") → Reconcile now → Sent ----
            var timedOut = await RaiseFixedPriceRequestAsync("D-UNK", "Unknown Deliverable", 200m);
            fakeConnector.ScriptNextCreate(ConnectorOutcome.Unknown, "The gateway timed out.");
            await ClickRequestActionAsync(window, invoicingView, timedOut.Id, "Send");
            await RenderUntilAsync(window, () =>
                ((InvoiceRequest)domain.Repository.FindAsync(timedOut.Id).GetAwaiter().GetResult()!).Status == InvoiceRequestStatus.Unknown);
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("response was lost", StringComparison.OrdinalIgnoreCase)));

            await ClickRequestActionAsync(window, invoicingView, timedOut.Id, "Reconcile now");
            await RenderUntilAsync(window, () =>
                ((InvoiceRequest)domain.Repository.FindAsync(timedOut.Id).GetAwaiter().GetResult()!).Status == InvoiceRequestStatus.Sent);

            // ---- Unavailable → stays Draft, with the message ----
            var unavailable = await RaiseFixedPriceRequestAsync("D-UNAV", "Unavailable Deliverable", 300m);
            fakeConnector.ScriptNextCreate(ConnectorOutcome.Unavailable, "The connector could not be reached.");
            await ClickRequestActionAsync(window, invoicingView, unavailable.Id, "Send");
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("try again later", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(
                InvoiceRequestStatus.Draft,
                ((InvoiceRequest)(await domain.Repository.FindAsync(unavailable.Id).ConfigureAwait(true))!).Status);

            // ---- Reauthorise, with a link to Settings ----
            var reauthorise = await RaiseFixedPriceRequestAsync("D-REAUTH", "Reauthorise Deliverable", 400m);
            fakeConnector.ScriptNextCreate(ConnectorOutcome.Reauthorise, "The stored token has expired.");
            await ClickRequestActionAsync(window, invoicingView, reauthorise.Id, "Send");
            await RenderUntilAsync(window, () =>
                ((InvoiceRequest)domain.Repository.FindAsync(reauthorise.Id).GetAwaiter().GetResult()!).Status == InvoiceRequestStatus.Reauthorise);
            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("Settings", StringComparison.Ordinal)));
            Assert.Contains(
                statusBar.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.Contains("re-authorising", StringComparison.OrdinalIgnoreCase) && t.Text.Contains("Settings", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Acceptance 3: Settings — choose Fake, Authorise → Authorised; the poll interval saves.</summary>
    [AvaloniaFact]
    public async Task Settings_ChooseFakeAuthoriseAndSavePollInterval()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);

            var settingsDialog = GetPrivateField<SettingsDialog>(window, "_settingsDialog");
            var showTask = settingsDialog.ShowAsync();
            await RenderUntilAsync(window, () => settingsDialog.IsVisible);

            var connectorCombo = settingsDialog.GetLogicalDescendants().OfType<ComboBox>()
                .Single(c => c.Items.OfType<ComboBoxItem>().Any(i => Equals(i.Content, "Fake")));
            connectorCombo.SelectedItem = connectorCombo.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Content, "Fake"));

            var authoriseButton = settingsDialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Authorise"));
            authoriseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                settingsDialog.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Authorised."));
            Assert.Contains(settingsDialog.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Authorised.");

            var pollMinutes = settingsDialog.GetLogicalDescendants().OfType<NumericUpDown>().Last();
            pollMinutes.Value = 30m;

            var saveButton = settingsDialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
            saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(await showTask);

            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
            Assert.Equal("Fake", await settingsProvider.GetValueAsync(InvoicingService.ConnectorConfigurationKey).ConfigureAwait(true));
            Assert.Equal("30", await settingsProvider.GetValueAsync(InvoiceReconciliationService.PollMinutesConfigurationKey).ConfigureAwait(true));
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
            organisationId, new Organisation { Reference = organisationId, Name = "Invoicing Journey Client Ltd" }, ReferenceProvenance.Unknown).ConfigureAwait(true);

        var card = new RateCard
        {
            Code = rateCardId,
            Name = "Invoicing Journey Rate Card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership("owner-1", "Owner") },
            Entries = [new RateCardEntry("SVC-1", "Senior Engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: grade)],
        };
        await rateCards.RegisterAsync(rateCardId, card, new ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc")).ConfigureAwait(true);
        await host.ReferenceReview!.VerifyAsync(rateCards, rateCardId, new ReferenceReviewStatement("Consulted for the journey test.")).ConfigureAwait(true);
        await host.ReferenceReview!.ReleaseAsync(rateCards, rateCardId, "Released for the journey test.").ConfigureAwait(true);
    }

    private static CommandParameterPrompt AutoConfirmPrompt() =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string>());

    private static async Task ClickButtonWithContentAsync(MainWindow window, Control root, object content)
    {
        await RenderUntilAsync(window, () => root.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, content)));
        root.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, content)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>Clicks the button captioned <paramref name="buttonContent"/> inside the one request row tagged <paramref name="requestId"/> — a request's own <see cref="InvoiceRequest.DisplayName"/> is not unique enough (several requests raised moments apart, for the same project, share the same date-stamped name), so <see cref="InvoicingView"/>'s own row <c>Border</c> is found by its <c>Tag</c> instead.</summary>
    private static async Task ClickRequestActionAsync(MainWindow window, InvoicingView invoicingView, Guid requestId, string buttonContent)
    {
        await RenderUntilAsync(window, () => FindRequestRow(invoicingView, requestId) is not null);
        var row = FindRequestRow(invoicingView, requestId)!;
        await RenderUntilAsync(window, () => row.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, buttonContent)));
        row.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, buttonContent)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static Border? FindRequestRow(Control root, Guid requestId) =>
        root.GetLogicalDescendants().OfType<Border>().FirstOrDefault(b => Equals(b.Tag, requestId));

    private static void AssertSectionPresent(ObjectEditorView editor, string title) =>
        Assert.Contains(editor.GetLogicalDescendants().OfType<Expander>(), e => Equals(e.Header, title) && e.IsVisible);

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
}
