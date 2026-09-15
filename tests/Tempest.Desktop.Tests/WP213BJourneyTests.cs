using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Expenses;
using Tempest.Core.People;
using Tempest.Core.Projects;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Settings;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Expenses;
using Tempest.Workspace.PurchaseOrders;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 21.3B` acceptance journeys, through the real window: a billable
/// expense shows up in Business → Invoices' own "Available to invoice"
/// (mirrors <c>InvoicingJourneyTests</c>'s own shape); a purchase order is
/// raised, issued, received, its lines recorded as expenses and closed,
/// through the real Business → Purchase orders area; and Settings →
/// Principal's own "Switch person…" turns the Evidence Check refusal this
/// Work Package's brief names ("switch person first") into a second
/// principal's own successful, independent check.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WP213BJourneyTests
{
    /// <summary>Recording a billable expense with no completion at all still makes it "available to invoice" — the second, expense-only way into the invoicing seam this Work Package adds.</summary>
    [AvaloniaFact]
    public async Task RecordExpense_ShowsAvailableToInvoice_InBusinessInvoices()
    {
        const string organisationId = "ORG-213B-EXP";
        const string rateCardId = "CARD-213B-EXP";
        const string grade = "Senior";

        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            await RegisterReleasedRateCardAndClientAsync(host, organisationId, rateCardId, grade);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-213B-EXP", "Expense Journey Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var commercial = (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));
            Assert.True((await commercial.SetClientAsync(project.Id, organisationId)).Succeeded);
            Assert.True((await commercial.PinRateCardAsync(project.Id, rateCardId)).Succeeded);

            // The identical command "Record expense…" dispatches, at
            // Business → Timesheets and the project's own Details tab
            // alike (`WP 21.3B`).
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var recordResult = await commandDispatcher.DispatchAsync(
                new RecordExpenseCommand(
                    project.Id, DateOnly.FromDateTime(DateTime.UtcNow), "Site visit materials", ExpenseCategory.Materials,
                    new Money(200m, CurrencyCode.Gbp), new Money(40m, CurrencyCode.Gbp), billable: true),
                CancellationToken.None);
            Assert.True(recordResult.Succeeded, recordResult.Message);

            // ---- Business → Invoices' own "Available to invoice" shows it ----
            await navigator.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Invoices");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<InvoicingView>().Any());
            LayOut(window);

            var invoicing = window.GetLogicalDescendants().OfType<InvoicingView>().Single();
            await RenderUntilAsync(window, () =>
                invoicing.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("Site visit materials", StringComparison.Ordinal)));

            var texts = invoicing.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(texts, t => t.Contains("Site visit materials", StringComparison.Ordinal) && t.Contains("Materials", StringComparison.Ordinal));

            var availableGroup = invoicing.GetLogicalDescendants().OfType<Border>()
                .FirstOrDefault(b => AutomationProperties.GetName(b) == "Available to invoice");
            Assert.NotNull(availableGroup);
            Assert.Contains(
                availableGroup!.GetLogicalDescendants().OfType<Button>(),
                b => b.Content is string s && s.StartsWith("Raise invoice", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Raise, issue, receive, record as expenses, close — through the real Business → Purchase orders area, one status group at a time.</summary>
    [AvaloniaFact]
    public async Task RaisePurchaseOrder_IssueReceiveRecordAsExpensesClose_ThroughTheRealBusinessArea()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-213B-PO", "Purchase Order Journey Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var createResult = await commandDispatcher.DispatchAsync(
                new CreatePurchaseOrderCommand(project.Id, reference: null, "SUPPLIER-213B", expectedDelivery: null), CancellationToken.None);
            Assert.True(createResult.Succeeded, createResult.Message);
            var orderId = createResult.SubjectId!.Value;

            var lineResult = await commandDispatcher.DispatchAsync(
                new AddPurchaseOrderLineCommand(orderId, PurchaseOrder.CanonicalKind, "Fixings", 100m, new Money(2m, CurrencyCode.Gbp), VatRate.Standard),
                CancellationToken.None);
            Assert.True(lineResult.Succeeded, lineResult.Message);

            // ---- Business → Purchase orders — the real grouped list ----
            await navigator.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Purchase orders");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<PurchaseOrdersView>().Any());
            LayOut(window);

            var purchaseOrdersView = window.GetLogicalDescendants().OfType<PurchaseOrdersView>().Single();
            purchaseOrdersView.ParameterPrompt = AutoConfirmPrompt();

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            async Task<PurchaseOrder> CurrentOrderAsync() => (PurchaseOrder)(await domain.Repository.FindAsync(orderId).ConfigureAwait(true))!;

            await ClickRowActionAsync(window, purchaseOrdersView, orderId, "Issue");
            await RenderUntilAsync(window, () => CurrentOrderAsync().GetAwaiter().GetResult().Status == PurchaseOrderStatus.Issued);

            await ClickRowActionAsync(window, purchaseOrdersView, orderId, "Receive");
            await RenderUntilAsync(window, () => CurrentOrderAsync().GetAwaiter().GetResult().Status == PurchaseOrderStatus.Received);

            await ClickRowActionAsync(window, purchaseOrdersView, orderId, "Record as expenses");
            await RenderUntilAsync(window, () => CurrentOrderAsync().GetAwaiter().GetResult().ExpensesRecorded);

            var expenseService = (IExpenseService)host.Services!.GetService(typeof(IExpenseService));
            var projectExpenses = await expenseService.ListForProjectAsync(project.Id);
            Assert.Single(projectExpenses, e => e.NetAmount == new Money(200m, CurrencyCode.Gbp) && e.Billable);

            await ClickRowActionAsync(window, purchaseOrdersView, orderId, "Close");
            await RenderUntilAsync(window, () => CurrentOrderAsync().GetAwaiter().GetResult().Status == PurchaseOrderStatus.Closed);

            LayOut(window);
            var closedExpander = purchaseOrdersView.GetLogicalDescendants().OfType<Expander>().Single(e => AutomationProperties.GetName(e) == "Closed");
            Assert.Contains(closedExpander.GetLogicalDescendants().OfType<Border>(), b => Equals(b.Tag, orderId));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The recorder attempts to check their own evidence: refused, naming
    /// "switch person first"; Settings → Principal's own "Switch person…"
    /// confirms by name (no password); the second principal's identical
    /// check then succeeds.
    /// </summary>
    [AvaloniaFact]
    public async Task SwitchPerson_EvidenceCheckedByFirst_RefusedThenSucceedsForTheSecond()
    {
        const string secondIdentityId = "second-reviewer-213b";

        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            // `WP 21.3B`: the People directory seam — no Desktop surface
            // manages People on this branch (`WP 20.10F` not in its base),
            // so the one in-memory implementation is seeded directly for
            // this journey (`IPeopleDirectory`'s own remarks).
            var people = (IPeopleDirectory)host.Services!.GetService(typeof(IPeopleDirectory));
            ((InMemoryPeopleDirectory)people).Add(new Person("Second Reviewer", "Checker", null, secondIdentityId));

            // The independent-check rule must be on for the refusal this
            // journey proves. `IEvidenceService` resolved first so its own
            // constructor has registered the setting definition.
            _ = host.EvidenceService;
            var settingsProvider = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));
            await settingsProvider.SetValueAsync(EvidenceService.IndependentCheckSettingKey, bool.TrueString);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-213B-SWITCH", "Switch Person Journey Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var evidence = await host.EvidenceService!.CreateAsync(project.Id, "Deflection check", EvidenceClassification.Calculation);

            var openEvidenceRecord = GetPrivateMethod(window, "OpenEvidenceRecordAsync");
            await (Task)openEvidenceRecord.Invoke(window, [evidence.Id, Core.Evidence.Evidence.CanonicalKind])!;
            LayOut(window);
            var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
            Assert.NotNull(editor);

            var checkEntry = GetPrivateField<CheckEntry>(window, "_checkEntry");
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");

            // ---- The recorder attempts to check their own evidence: refused ----
            editor!.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => checkEntry.IsVisible);

            var firstAttemptBoxes = checkEntry.GetLogicalDescendants().OfType<TextBox>().ToList();
            firstAttemptBoxes[0].Text = "Self Reviewer";
            firstAttemptBoxes[1].Text = "Org";
            firstAttemptBoxes[2].Text = "Reviewing my own work.";
            checkEntry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !checkEntry.IsVisible);

            await RenderUntilAsync(window, () =>
                statusBar.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("switch person first", StringComparison.Ordinal)));
            Assert.Equal(EvidenceStatus.Draft, evidence.Status);

            // ---- Switch person, through the real Settings area ----
            await navigator.GoToModuleAsync(ShellArea.Settings);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var settingsView = GetPrivateField<SettingsView>(window, "_settingsView");
            await RenderUntilAsync(window, () =>
                settingsView.GetLogicalDescendants().OfType<ComboBox>().Any(c => AutomationProperties.GetName(c) == "Switch to"
                    && c.ItemsSource is not null && c.ItemsSource.Cast<object>().Any()));

            var switchSelector = settingsView.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Switch to");
            switchSelector.SelectedIndex = 0;

            var switchButton = settingsView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Switch person…"));
            switchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var confirmationDialog = GetPrivateField<ConfirmationDialog>(window, "_confirmationDialog");
            await RenderUntilAsync(window, () => confirmationDialog.IsVisible);
            confirmationDialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Switch")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !confirmationDialog.IsVisible);

            await RenderUntilAsync(window, () => host.SessionPrincipal?.IdentityId == secondIdentityId);
            Assert.Equal("Second Reviewer", host.SessionPrincipal!.Identity.DisplayName);

            // ---- The header shows who is signed in now ----
            var header = GetPrivateField<ShellHeaderView>(window, "_header");
            await RenderUntilAsync(window, () =>
                header.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text != null && t.Text.Contains("Second Reviewer", StringComparison.Ordinal)));

            // ---- The second principal's own identical check now succeeds ----
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            var openEvidenceRecordAgain = GetPrivateMethod(window, "OpenEvidenceRecordAsync");
            await (Task)openEvidenceRecordAgain.Invoke(window, [evidence.Id, Core.Evidence.Evidence.CanonicalKind])!;
            LayOut(window);

            var editorAgain = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
            Assert.NotNull(editorAgain);

            editorAgain!.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => checkEntry.IsVisible);

            var secondAttemptBoxes = checkEntry.GetLogicalDescendants().OfType<TextBox>().ToList();
            secondAttemptBoxes[0].Text = "Second Reviewer";
            secondAttemptBoxes[1].Text = "Org";
            secondAttemptBoxes[2].Text = "Independent review, second principal.";
            checkEntry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !checkEntry.IsVisible);

            await RenderUntilAsync(window, () => evidence.Status == EvidenceStatus.Checked);
            Assert.Equal(EvidenceStatus.Checked, evidence.Status);
            Assert.Equal(secondIdentityId, evidence.Check!.CheckerIdentityId);
            Assert.Equal(secondIdentityId, evidence.Check.RecordedByIdentityId);
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
        var organisations = (Tempest.Core.BusinessOperations.Crm.IOrganisationCatalog)host.Services!.GetService(typeof(Tempest.Core.BusinessOperations.Crm.IOrganisationCatalog));
        var rateCards = (Tempest.Core.BusinessGovernance.Pricing.IRateCardCatalog)host.Services!.GetService(typeof(Tempest.Core.BusinessGovernance.Pricing.IRateCardCatalog));

        await organisations.RegisterAsync(
            organisationId, new Tempest.Core.BusinessOperations.Crm.Organisation { Reference = organisationId, Name = "WP 21.3B Journey Client Ltd" },
            Tempest.Core.ReferenceData.ReferenceProvenance.Unknown).ConfigureAwait(true);

        var card = new Tempest.Core.BusinessGovernance.Pricing.RateCard
        {
            Code = rateCardId,
            Name = "WP 21.3B Journey Rate Card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = new Tempest.Core.BusinessGovernance.BusinessGovernanceFacts { Ownership = new Tempest.Core.BusinessGovernance.BusinessOwnership("owner-1", "Owner") },
            Entries = [new Tempest.Core.BusinessGovernance.Pricing.RateCardEntry("SVC-1", "Senior Engineering", Tempest.Core.BusinessGovernance.Pricing.PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: grade)],
        };
        await rateCards.RegisterAsync(rateCardId, card, new Tempest.Core.ReferenceData.ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc")).ConfigureAwait(true);
        await host.ReferenceReview!.VerifyAsync(rateCards, rateCardId, new Tempest.Core.ReferenceData.Review.ReferenceReviewStatement("Consulted for the journey test.")).ConfigureAwait(true);
        await host.ReferenceReview!.ReleaseAsync(rateCards, rateCardId, "Released for the journey test.").ConfigureAwait(true);
    }

    private static CommandParameterPrompt AutoConfirmPrompt() =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string>());

    /// <summary>Clicks the button captioned <paramref name="buttonContent"/> inside the one purchase-order row tagged <paramref name="orderId"/> — mirrors <c>InvoicingJourneyTests.ClickRequestActionAsync</c>'s own identical shape.</summary>
    private static async Task ClickRowActionAsync(MainWindow window, PurchaseOrdersView view, Guid orderId, string buttonContent)
    {
        await RenderUntilAsync(window, () => FindRow(view, orderId) is not null);
        var row = FindRow(view, orderId)!;
        await RenderUntilAsync(window, () => row.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, buttonContent)));
        row.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, buttonContent)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static Border? FindRow(Control root, Guid orderId) =>
        root.GetLogicalDescendants().OfType<Border>().FirstOrDefault(b => Equals(b.Tag, orderId));

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
            window.Measure(new Avalonia.Size(1900, 1050));
            window.Arrange(new Avalonia.Rect(0, 0, 1900, 1050));
        }
    }

    private static System.Reflection.MethodInfo GetPrivateMethod(object instance, string name) =>
        instance.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{name}' not found on {instance.GetType().Name}.");
}
