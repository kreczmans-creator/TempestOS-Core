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
using Tempest.Core.Invoicing;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Views;
using Tempest.Desktop.Views.Dashboards;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests.Dashboards;

/// <summary>
/// The `WP 19.7B` acceptance: through the real window, a fixture of six
/// open projects (one per <c>ProjectHealthStatus</c>), a billable project's
/// own three timesheet entries, a directly-constructed old Sent invoice
/// request and an old Sent quotation (past their own chase thresholds —
/// <see cref="IQuotationService"/>/<see cref="IInvoicingService"/> both
/// stamp their own "sent" moment at the real wall clock, which a Desktop
/// test cannot rewind; <see cref="EngineeringObjectFactory{T}"/>'s own
/// public constructor accepts an explicit past date directly, the
/// identical technique <c>AccountsReadModelTests</c> uses from
/// <c>Tempest.Core.Tests</c>, where the two Kinds' own internal mutators
/// are reachable instead), and a Fake accounts reading with two
/// subscriptions and one bill — every tile, list and chart on all four
/// dashboards reads real data matching hand-computed values, the Gantt
/// carries its own two bars in the right order, the cash-flow chart
/// carries twelve points, and a representative row on each dashboard
/// opens its own object right up.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class DashboardsTests
{
    private const string OrganisationId = "ORG-DASH";
    private const string RateCardId = "CARD-DASH";

    [AvaloniaFact]
    public async Task AllFourDashboards_ShowRealDataMatchingHandComputedValues()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var fixture = await BuildFixtureAsync(host);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);
            var navigator = host.ShellNavigator!;

            // ---------------------------------------------------------
            // Home
            // ---------------------------------------------------------
            await navigator.GoToModuleAsync(ShellArea.Home);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var home = window.GetLogicalDescendants().OfType<HomeDashboardView>().Single();
            var homeButtons = home.GetLogicalDescendants().OfType<Button>().ToList();
            var homeText = string.Join(" | ", home.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

            Assert.Contains(homeButtons, b => AutomationName(b).StartsWith("Overdue: 2", StringComparison.Ordinal));
            Assert.Contains(homeButtons, b => AutomationName(b).StartsWith("Due today: 0", StringComparison.Ordinal));
            Assert.Contains(homeButtons, b => AutomationName(b).StartsWith("Due this week: 2", StringComparison.Ordinal));
            Assert.Contains(homeButtons, b => AutomationName(b).StartsWith("Approvals: 0", StringComparison.Ordinal));
            Assert.Contains(homeButtons, b => AutomationName(b).StartsWith("Finance: 2", StringComparison.Ordinal));

            Assert.Contains("Quotes: 2 open", homeText, StringComparison.Ordinal);
            Assert.Contains("Invoices: 1 sent", homeText, StringComparison.Ordinal);
            Assert.DoesNotContain("Invoices: unavailable", homeText, StringComparison.Ordinal);

            // 6: DASH-F, DASH-D, DASH-A, DASH-B — plus the two projects the
            // host's own sample data seeds (both On track, uncomplicated;
            // see the Projects-dashboard tile's own identical remark).
            Assert.Contains("6 open project(s) in total.", homeText, StringComparison.Ordinal);

            // Every open project's own milestone appears in "Upcoming
            // milestones" (four: DASH-A, DASH-D and DASH-E's own default
            // "Unquoted" milestone — DASH-A and DASH-D share that title —
            // plus DASH-C's own quote-reference milestone).
            var milestoneRows = home.GetLogicalDescendants().OfType<TextBlock>()
                .Count(t => t.Text is { } text && text.Contains("Unquoted", StringComparison.Ordinal));
            Assert.True(milestoneRows >= 2, $"Expected at least two 'Unquoted' milestone rows on Home; text was: {homeText}");
            Assert.Contains(fixture.BlockedQuoteReference, homeText, StringComparison.Ordinal);

            // The task list carries every open task — including DASH-A's
            // own overdue deliverable, opened right up from here.
            Assert.Contains("Late footing check", homeText, StringComparison.Ordinal);
            var openLateFooting = homeButtons.Single(b =>
                AutomationName(b).StartsWith("Open ", StringComparison.Ordinal) && AutomationName(b).Contains("Late footing check", StringComparison.Ordinal));
            openLateFooting.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => window.LastOpenPhase.StartsWith("opened (", StringComparison.Ordinal));
            Assert.Contains(fixture.LateFootingDeliverableId.ToString("N"), window.LastOpenPhase, StringComparison.OrdinalIgnoreCase);

            // ---------------------------------------------------------
            // Projects dashboard
            // ---------------------------------------------------------
            await navigator.GoToModuleAsync(ShellArea.Projects);
            await window.RenderCurrentModuleAsync();
            window.GetLogicalDescendants().OfType<ProjectsAreaView>().Single().SelectNode("Dashboard + Reports");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<ProjectsDashboardView>().Any());
            LayOut(window);

            var projectsDashboard = window.GetLogicalDescendants().OfType<ProjectsDashboardView>().Single();
            var projectsText = string.Join(" | ", projectsDashboard.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            var projectsTiles = projectsDashboard.GetLogicalDescendants().OfType<Border>()
                .Where(b => Avalonia.Automation.AutomationProperties.GetName(b) is { Length: > 0 })
                .ToList();

            // `Active` (and Home's own "N open project(s) in total.",
            // below) counts every live open project the host's own sample
            // data seeds too (`SAMPLE-PROJ-001`, `MECH-PROJ-001` —
            // `EngineeringDomainSampleModule`/`MechanicalProductStructureSampleModule`),
            // not only this fixture's own six — the two seeded projects
            // carry no milestone/deliverable/quotation of their own, so
            // every other figure below (the reason-carrying lists, the
            // Gantt, the task buckets) is unaffected and stays exact.
            Assert.Contains(projectsTiles, t => AutomationName(t) == "Active: 8");
            Assert.Contains(projectsTiles, t => AutomationName(t) == "At risk: 1");
            Assert.Contains(projectsTiles, t => AutomationName(t) == "On hold: 1");
            Assert.Contains(projectsTiles, t => AutomationName(t) == "Ready to invoice: 1");

            Assert.Contains("Blocked Bridge", projectsText, StringComparison.Ordinal);
            Assert.Contains("no deliverable has been started", projectsText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("At-risk Bridge", projectsText, StringComparison.Ordinal);
            Assert.Contains("At risk:", projectsText, StringComparison.Ordinal);
            Assert.Contains("Ready Bridge", projectsText, StringComparison.Ordinal);
            Assert.Contains("ready to invoice", projectsText, StringComparison.OrdinalIgnoreCase);

            // The Gantt: exactly Overdue Bridge (DASH-A) then At-risk
            // Bridge (DASH-D), the only two projects carrying both a start
            // and a target date, in start-date order — scoped to the
            // Gantt's own host, since "At-risk Bridge" also appears, once,
            // in the "At risk" reason-carrying list above it.
            var ganttHost = GetPrivateField<ScrollViewer>(projectsDashboard, "_ganttScroll");
            var ganttLabels = ganttHost.GetLogicalDescendants().OfType<TextBlock>()
                .Select(t => t.Text)
                .Where(t => t is "Overdue Bridge" or "At-risk Bridge")
                .ToList();
            Assert.Equal(["Overdue Bridge", "At-risk Bridge"], ganttLabels);
            Assert.Contains("recorded 6.5h", projectsText, StringComparison.Ordinal);

            var openBlocked = projectsDashboard.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationName(b).StartsWith("Open Blocked Bridge", StringComparison.Ordinal));
            openBlocked.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => navigator.Current.ProjectId == fixture.BlockedProjectId);
            Assert.Equal(fixture.BlockedProjectId, navigator.Current.ProjectId);

            // ---------------------------------------------------------
            // Engineering dashboard
            // ---------------------------------------------------------
            await navigator.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Dashboard + Reports");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<EngineeringDashboardView>().Any());
            LayOut(window);

            var engineeringDashboard = window.GetLogicalDescendants().OfType<EngineeringDashboardView>().Single();
            var engineeringText = string.Join(" | ", engineeringDashboard.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

            Assert.Contains("Late footing check", engineeringText, StringComparison.Ordinal);
            Assert.Contains("Due soon deliverable", engineeringText, StringComparison.Ordinal);
            Assert.Contains("Nothing awaiting check.", engineeringText, StringComparison.Ordinal);
            Assert.Contains("Nothing awaiting issue.", engineeringText, StringComparison.Ordinal);

            var openDueSoon = engineeringDashboard.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationName(b) == "Open Due soon deliverable");
            openDueSoon.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => window.LastOpenPhase.StartsWith("opened (", StringComparison.Ordinal));
            Assert.Contains(fixture.DueSoonDeliverableId.ToString("N"), window.LastOpenPhase, StringComparison.OrdinalIgnoreCase);

            // ---------------------------------------------------------
            // Business dashboard
            // ---------------------------------------------------------
            await navigator.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Dashboard & Reports");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<BusinessDashboardView>().Any());
            LayOut(window);

            var businessDashboard = window.GetLogicalDescendants().OfType<BusinessDashboardView>().Single();
            var businessTiles = businessDashboard.GetLogicalDescendants().OfType<Border>()
                .Where(b => Avalonia.Automation.AutomationProperties.GetName(b) is { Length: > 0 })
                .ToList();
            var businessText = string.Join(" | ", businessDashboard.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

            // `WP 19.10P` (D1): the tile's own displayed value now goes
            // through MoneyDisplay, a symbol before the amount rather than
            // the ISO code after it.
            Assert.Contains(businessTiles, t => AutomationName(t) == "Invoiced: £2,000.00");
            Assert.Contains(businessTiles, t => AutomationName(t) == "Overdue: £2,000.00");
            Assert.Contains(businessTiles, t => AutomationName(t) == "Due 30: £0.00");
            Assert.Contains(businessTiles, t => AutomationName(t) == "Due 90: £0.00");

            Assert.Contains(fixture.ReceivableClientId, businessText, StringComparison.Ordinal);
            Assert.Contains("Contoso Cloud", businessText, StringComparison.Ordinal);
            Assert.Contains("Acme Hardware Co", businessText, StringComparison.Ordinal);
            Assert.Contains("Acme Ltd", businessText, StringComparison.Ordinal);
            Assert.Contains("2 open quote(s)", businessText, StringComparison.Ordinal);
            Assert.Contains("Old chase quote", businessText, StringComparison.Ordinal);
            Assert.DoesNotContain("DASH-A own quote", businessText, StringComparison.Ordinal);

            var cashFlowPoints = businessDashboard.GetLogicalDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Count();
            Assert.Equal(12, cashFlowPoints);

            var openReceivable = businessDashboard.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationName(b).StartsWith("Open ", StringComparison.Ordinal) && AutomationName(b).Contains(fixture.ReceivableClientId, StringComparison.Ordinal));
            openReceivable.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => window.LastOpenPhase.StartsWith("opened (", StringComparison.Ordinal));
            Assert.Contains(fixture.ReceivableInvoiceId.ToString("N"), window.LastOpenPhase, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task BusinessDashboard_WithNoAccountsReading_ShowsUnavailableWithReason()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);

            await host.ShellNavigator!.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Dashboard & Reports");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<BusinessDashboardView>().Any());
            LayOut(window);

            var dashboard = window.GetLogicalDescendants().OfType<BusinessDashboardView>().Single();
            var text = string.Join(" | ", dashboard.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            var tiles = dashboard.GetLogicalDescendants().OfType<Border>()
                .Where(b => Avalonia.Automation.AutomationProperties.GetName(b) is { Length: > 0 })
                .ToList();

            Assert.Contains("Accounts reading unavailable", text, StringComparison.Ordinal);
            Assert.Contains("No accounts reading yet", text, StringComparison.Ordinal);
            Assert.Contains(tiles, t => Avalonia.Automation.AutomationProperties.GetName(t) == "Invoiced: unavailable");
            Assert.Contains(tiles, t => Avalonia.Automation.AutomationProperties.GetName(t) == "Overdue: unavailable");
            Assert.Contains(tiles, t => Avalonia.Automation.AutomationProperties.GetName(t) == "Due 30: unavailable");
            Assert.Contains(tiles, t => Avalonia.Automation.AutomationProperties.GetName(t) == "Due 90: unavailable");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // Fixture.
    // ================================================================

    private sealed record Fixture(
        Guid BlockedProjectId, string BlockedQuoteReference, string ChaseQuoteReference,
        Guid LateFootingDeliverableId, Guid DueSoonDeliverableId,
        Guid ReceivableInvoiceId, string ReceivableClientId, DateOnly ReceivableDueDate);

    private static async Task<Fixture> BuildFixtureAsync(WorkspaceHost host)
    {
        var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        var projectDirectory = host.ProjectDirectory!;
        var lifecycle = (IProjectLifecycleService)host.Services!.GetService(typeof(IProjectLifecycleService));
        var commercial = (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));
        var quotations = (IQuotationService)host.Services!.GetService(typeof(IQuotationService));
        var deliverables = (IDeliverableService)host.Services!.GetService(typeof(IDeliverableService));
        var timesheets = (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));
        var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
        var rateCards = (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await organisations.RegisterAsync(
            OrganisationId, new Organisation { Reference = OrganisationId, Name = "Dashboard Client Ltd" }, ReferenceProvenance.Unknown).ConfigureAwait(true);

        var card = new RateCard
        {
            Code = RateCardId,
            Name = "Dashboard Rate Card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership("owner-1", "Owner") },
            Entries = [new RateCardEntry("SVC-1", "Senior Engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: "Senior")],
        };
        await rateCards.RegisterAsync(RateCardId, card, new ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc")).ConfigureAwait(true);
        await host.ReferenceReview!.VerifyAsync(rateCards, RateCardId, new ReferenceReviewStatement("Consulted for the dashboard fixture.")).ConfigureAwait(true);
        await host.ReferenceReview!.ReleaseAsync(rateCards, RateCardId, "Released for the dashboard fixture.").ConfigureAwait(true);

        // ---- DASH-A: Overdue, a fresh open quote, three timesheet entries ----
        var projectA = await projectDirectory.CreateAsync("DASH-A", "Overdue Bridge");
        Assert.True((await commercial.SetClientAsync(projectA.Id, OrganisationId)).Succeeded);
        Assert.True((await commercial.PinRateCardAsync(projectA.Id, RateCardId)).Succeeded);
        Assert.True((await commercial.SetDatesAsync(projectA.Id, today.AddDays(-30), today.AddDays(20))).Succeeded);

        var lateFooting = await deliverables.AddDeliverableAsync(projectA.Id, "Late footing check", today.AddDays(-10));

        Assert.True((await timesheets.RecordAsync(projectA.Id, today, 2m, true, "Senior", "Site visit")).Succeeded);
        Assert.True((await timesheets.RecordAsync(projectA.Id, today, 1.5m, true, "Senior", "Calc review")).Succeeded);
        Assert.True((await timesheets.RecordAsync(projectA.Id, today.AddDays(-1), 3m, true, "Senior", "Drafting")).Succeeded);

        var freshQuote = await quotations.CreateAsync(projectA.Id);
        Assert.True(freshQuote.Succeeded, freshQuote.Reason);
        await quotations.AddLineAsync(freshQuote.Quotation!.Id, "DASH-A own quote", 4m, new Money(100m, CurrencyCode.Gbp), null).ConfigureAwait(true);
        await quotations.SendAsync(freshQuote.Quotation.Id).ConfigureAwait(true);

        // ---- DASH-B: On hold ----
        var projectB = await projectDirectory.CreateAsync("DASH-B", "Held Bridge");
        Assert.True((await lifecycle.HoldAsync(projectB.Id, "Client paused.")).Succeeded);

        // ---- DASH-C: Blocked — an accepted quote, no deliverable started ----
        var projectC = await projectDirectory.CreateAsync("DASH-C", "Blocked Bridge");
        Assert.True((await commercial.SetClientAsync(projectC.Id, OrganisationId)).Succeeded);
        Assert.True((await commercial.PinRateCardAsync(projectC.Id, RateCardId)).Succeeded);
        var blockedQuote = await quotations.CreateAsync(projectC.Id);
        Assert.True(blockedQuote.Succeeded, blockedQuote.Reason);
        await quotations.AddLineAsync(blockedQuote.Quotation!.Id, "Detailed design", 8m, new Money(120m, CurrencyCode.Gbp), null).ConfigureAwait(true);
        await quotations.SendAsync(blockedQuote.Quotation.Id).ConfigureAwait(true);
        var acceptedQuote = await quotations.AcceptAsync(blockedQuote.Quotation.Id);
        Assert.True(acceptedQuote.Succeeded, acceptedQuote.Reason);

        // ---- DASH-D: At risk — a deliverable due within seven days ----
        var projectD = await projectDirectory.CreateAsync("DASH-D", "At-risk Bridge");
        Assert.True((await commercial.SetDatesAsync(projectD.Id, today.AddDays(-10), today.AddDays(30))).Succeeded);
        var dueSoon = await deliverables.AddDeliverableAsync(projectD.Id, "Due soon deliverable", today.AddDays(3));

        // ---- DASH-E: Ready to invoice — a completion, not yet invoiced ----
        var projectE = await projectDirectory.CreateAsync("DASH-E", "Ready Bridge");
        var finished = await deliverables.AddDeliverableAsync(projectE.Id, "Finished deliverable", today.AddDays(60));
        var completed = await deliverables.CompleteAsync(finished.Id, projectE.Id, today);
        Assert.True(completed.Succeeded, completed.Reason);

        // ---- DASH-F: On track — nothing outstanding ----
        await projectDirectory.CreateAsync("DASH-F", "OnTrack Bridge");

        // ---- An old Sent quotation, more than seven days old, to chase ----
        const string chaseReference = "Q-CHASE-OLD";
        var chaseLine = new QuotationLine(Guid.NewGuid(), "Old chase line", 5m, new Money(100m, CurrencyCode.Gbp), null, new Money(500m, CurrencyCode.Gbp), QuotationLineBasis.Hourly);
        await new EngineeringObjectFactory<Quotation>(
            Quotation.CanonicalKind, domain,
            (doc, rev) => new Quotation(
                doc, rev, domain, identifier: null, "Old chase quote", EngineeringObjectMetadata.Empty,
                chaseReference, today.AddDays(-10), "ORG-CHASE", CurrencyCode.Gbp, 30, terms: null,
                [chaseLine], status: QuotationStatus.Sent, sentOn: today.AddDays(-10)))
            .CreateAsync("Dashboard fixture — old quote.").ConfigureAwait(true);

        // ---- An old Sent invoice request, past terms (overdue) ----
        const string receivableClient = "ORG-INV-FIXTURE";
        var receivableAmount = new Money(2000m, CurrencyCode.Gbp);
        var invoiceLines = new List<InvoiceRequestLine> { new("Fixture", Guid.NewGuid(), "Fixture line", 1m, receivableAmount, receivableAmount) };
        var receivableInvoice = (InvoiceRequest)await new EngineeringObjectFactory<InvoiceRequest>(
            InvoiceRequest.CanonicalKind, domain,
            (doc, rev) => new InvoiceRequest(
                doc, rev, domain, identifier: null, "Old overdue invoice", EngineeringObjectMetadata.Empty,
                receivableClient, purchaseOrderReference: null, CurrencyCode.Gbp, invoiceLines, receivableAmount,
                status: InvoiceRequestStatus.Sent, issuedDate: today.AddDays(-40), sentAtUtc: DateTimeOffset.UtcNow.AddDays(-40)))
            .CreateAsync("Dashboard fixture — old invoice.").ConfigureAwait(true);

        // ---- The accounts reading (Product Owner comment item 8: two subscriptions, one bill) ----
        var connector = (FakeInvoicingConnector)host.Services!.GetService(typeof(IInvoicingConnector));
        connector.ScriptBillsDue([new BillDue("Acme Ltd", "INV-BILL-1", today, today.AddDays(20), new Money(300m, CurrencyCode.Gbp), "AUTHORISED")]);
        connector.ScriptRepeatingBills(
        [
            new RepeatingBill("Contoso Cloud", "Software licence", new Money(99m, CurrencyCode.Gbp), "MONTHLY", today.AddDays(10), "Software Subscription"),
            new RepeatingBill("Acme Hardware Co", "Workstation lease", new Money(150m, CurrencyCode.Gbp), "MONTHLY", today.AddDays(15), "Hardware"),
        ]);
        connector.ScriptCashPosition([new CashAccountBalance("Business Current Account", new Money(5000m, CurrencyCode.Gbp), today)]);

        var refreshService = (AccountsRefreshService)host.Services!.GetService(typeof(AccountsRefreshService));
        await refreshService.RefreshNowAsync().ConfigureAwait(true);

        return new Fixture(
            projectC.Id, blockedQuote.Quotation.Reference, chaseReference,
            lateFooting.Id, dueSoon.Id,
            receivableInvoice.Id, receivableClient, today.AddDays(-10));
    }

    // ================================================================
    // Local helpers — mirrors AccountsSettingsJourneyTests' own identical shapes.
    // ================================================================

    private static string AutomationName(Control control) => Avalonia.Automation.AutomationProperties.GetName(control) ?? string.Empty;

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

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        window.Width = 1900;
        window.Height = 1200;

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1900, 1200));
            window.Arrange(new Rect(0, 0, 1900, 1200));
        }
    }
}
