using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Quotations;
using Tempest.Desktop.Views;
using Tempest.Desktop.Views.Dashboards;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests.Dashboards;

/// <summary>
/// `v0.24.0` X7: the Business dashboard's forward cash panel, headless — six
/// months of opening cash, money in, money out and closing cash, each cell
/// naming its source (Xero, read at …; TempestOS; worked out) in its
/// automation name and tooltip; "unavailable" with the reason rather than
/// zero when there is no accounts reading; the figures behind each month,
/// the undated pipeline and what was left out; and a pipeline row opening its
/// quotation right up.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class BusinessDashboardForwardCashTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 2);
    private static readonly DateTimeOffset ReadAt = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid QuoteId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid InvoiceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);

    private static ForwardCashInputs Inputs(bool withReading) => new(
        AsOf,
        withReading
            ? new AccountsReading(
                [new BillDue("Hire Co", "B-NOV", AsOf, new DateOnly(2026, 11, 15), Gbp(600m), "AUTHORISED")],
                [new CategorisedRepeatingBill(new RepeatingBill("Contoso Cloud", "Software licence", Gbp(99m), "MONTHLY", new DateOnly(2026, 10, 20), "Software"), AccountsCategory.Software)],
                [new CashAccountBalance("Business Current Account", Gbp(10_000m), AsOf)],
                ReadAt, "Xero")
            : null,
        [new ForwardCashInvoice(InvoiceId, "Invoice INV-1 — client ACME1", new DateOnly(2026, 10, 30), Gbp(1_200m),
            new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, "Xero invoice", ReadAt), AwaitingApproval: false)],
        [
            new ForwardCashPipelineLine(QuoteId, "Q-1: Detailed design", new DateOnly(2026, 12, 15), "milestone 'Q-1' target date 2026-12-15", Gbp(3_000m)),
            new ForwardCashPipelineLine(QuoteId, "Q-1: Later stage", null, "no planned or target date", Gbp(800m)),
        ])
    {
        ReadingUnavailableReason = withReading ? null : "No accounts reading yet.",
        Exclusions = [new ForwardCashExclusion("Hourly lines on accepted quotes — billed from timesheets as time is booked, so not projected here", 1, Gbp(1_000m))],
    };

    [AvaloniaFact]
    public async Task ForwardCash_ShowsSixMonths_EachFigureWithItsSource_AndTheFiguresBehindThem()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var opened = new List<(Guid Id, string Kind)>();
            var view = await ShowAsync(host, ForwardCashProjection.Build(Inputs(withReading: true)), opened);

            var cells = CellNames(view);
            Assert.Equal(6 * 8, cells.Count);

            // October (from the 2nd): 10,000 + 1,200 - 99 = 11,101.
            Assert.Contains("Opening cash, Oct 2026 (from 2): £10,000.00 — Xero bank balances, read at 2026-10-02 08:00", cells);
            Assert.Contains("Invoices due (Xero), Oct 2026 (from 2): £1,200.00 — Xero invoices raised in TempestOS, status as read back, read at 2026-10-02 08:00", cells);
            Assert.Contains("Repeating bills (Xero), Oct 2026 (from 2): £99.00 — Xero repeating bills, read at 2026-10-02 08:00", cells);
            Assert.Contains("Closing cash, Oct 2026 (from 2): £11,101.00 — Worked out from the figures above", cells);
            // November: 11,101 - 600 - 99 = 10,402.
            Assert.Contains("Bills due (Xero), Nov 2026: £600.00 — Xero bills, read at 2026-10-02 08:00", cells);
            Assert.Contains("Closing cash, Nov 2026: £10,402.00 — Worked out from the figures above", cells);
            // December: + 3,000 expected (TempestOS) - 99 = 13,303.
            Assert.Contains("Expected milestone invoices (TempestOS), Dec 2026: £3,000.00 — TempestOS — accepted quotes not yet invoiced", cells);
            Assert.Contains("Closing cash, Dec 2026: £13,303.00 — Worked out from the figures above", cells);
            Assert.Contains(cells, c => c.StartsWith("Closing cash, Mar 2027: £13,006.00", StringComparison.Ordinal));
            Assert.DoesNotContain(cells, c => c.Contains("unavailable", StringComparison.Ordinal));

            var text = Text(view);
            Assert.Contains(BusinessDashboardView.ForwardCashPanelName, text, StringComparison.Ordinal);
            Assert.Contains("Bank balances, bills and repeating bills: Xero, read at 2026-10-02 08:00.", text, StringComparison.Ordinal);
            Assert.Contains("gross, including VAT", text, StringComparison.Ordinal);
            Assert.Contains("Quotes not yet accepted are not counted", text, StringComparison.Ordinal);
            Assert.Contains("Undated — accepted, not invoiced, no planned or target date: £800.00", text, StringComparison.Ordinal);
            Assert.Contains("Not counted: Hourly lines on accepted quotes", text, StringComparison.Ordinal);
            Assert.Contains("Bill due: Hire Co (B-NOV): £600.00", text, StringComparison.Ordinal);

            // A pipeline row opens its quotation; an invoice row its request.
            var openMilestone = OpenButtons(view).Single(b => AutomationProperties.GetName(b)!.Contains("Expected milestone invoice: Q-1: Detailed design", StringComparison.Ordinal));
            openMilestone.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var openInvoice = OpenButtons(view).Single(b => AutomationProperties.GetName(b)!.Contains("Invoice due: Invoice INV-1", StringComparison.Ordinal));
            openInvoice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal([(QuoteId, Quotation.CanonicalKind), (InvoiceId, InvoiceRequest.CanonicalKind)], opened);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ForwardCash_WithNoAccountsReading_SaysUnavailableWithTheReason_NeverZero_AndStillShowsTempestOSFigures()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var view = await ShowAsync(host, ForwardCashProjection.Build(Inputs(withReading: false)), []);

            var cells = CellNames(view);
            Assert.Contains("Opening cash, Oct 2026 (from 2): unavailable — Bank balances — No accounts reading yet.", cells);
            Assert.Contains("Bills due (Xero), Nov 2026: unavailable — Bills — No accounts reading yet.", cells);
            Assert.Contains("Closing cash, Mar 2027: unavailable — Worked out from the figures above — No accounts reading yet.", cells);
            Assert.Contains("Expected milestone invoices (TempestOS), Dec 2026: £3,000.00 — TempestOS — accepted quotes not yet invoiced", cells);
            Assert.Contains(cells, c => c.StartsWith("Invoices due (Xero), Oct 2026 (from 2): £1,200.00", StringComparison.Ordinal));
            Assert.DoesNotContain(cells, c => c.StartsWith("Closing cash", StringComparison.Ordinal) && c.Contains('£', StringComparison.Ordinal));
            Assert.Contains("Bank balances, bills and repeating bills: unavailable — No accounts reading yet.", Text(view), StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ForwardCash_Offline_ShowsTheLastReading_AndSaysTheRefreshFailed()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var inputs = Inputs(withReading: true) with
            {
                RefreshFailureReason = "Could not read bills: the network is unreachable.",
                RefreshFailedAtUtc = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero),
            };
            var view = await ShowAsync(host, ForwardCashProjection.Build(inputs), []);

            var text = Text(view);
            Assert.Contains("Bank balances, bills and repeating bills: Xero, read at 2026-10-02 08:00.", text, StringComparison.Ordinal);
            Assert.Contains("The latest refresh failed at 2026-10-02 09:00 (Could not read bills: the network is unreachable.); showing the last reading.", text, StringComparison.Ordinal);
            Assert.Contains("Closing cash, Oct 2026 (from 2): £11,101.00 — Worked out from the figures above", CellNames(view));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ThroughTheRealWindow_TheForwardCashPanel_IsOnTheBusinessDashboard_HonestlyUnavailableWithNoReading()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await DesktopTestHelpers.WaitUntilAsync(() => window.Ready.IsCompleted, 20, () => LayOut(window), DesktopTestHelpers.OpenPhaseOf(window), "window ready");

            await host.ShellNavigator!.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Dashboard");
            await DesktopTestHelpers.WaitUntilAsync(
                () => window.GetLogicalDescendants().OfType<BusinessDashboardView>().Any(v => CellNames(v).Count > 0),
                20, () => LayOut(window), DesktopTestHelpers.OpenPhaseOf(window), "forward cash table rendered");

            var dashboard = window.GetLogicalDescendants().OfType<BusinessDashboardView>().Single();
            var cells = CellNames(dashboard);
            Assert.Equal(6 * 8, cells.Count);
            Assert.All(cells.Where(c => c.StartsWith("Closing cash", StringComparison.Ordinal)), c => Assert.Contains(": unavailable — ", c, StringComparison.Ordinal));
            Assert.Contains(dashboard.GetLogicalDescendants().OfType<Control>(), c => AutomationProperties.GetName(c) == BusinessDashboardView.ForwardCashPanelName);
            Assert.Contains("Bank balances, bills and repeating bills: unavailable — No accounts reading yet.", Text(dashboard), StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task<BusinessDashboardView> ShowAsync(WorkspaceHost host, ForwardCashPicture picture, List<(Guid, string)> opened)
    {
        var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        // n6: local time on screen; pinned to UTC here so the expected words do not depend on the machine.
        var view = new BusinessDashboardView(new FixedReadModel(picture), domain, (id, kind) => opened.Add((id, kind))) { TimeZone = TimeZoneInfo.Utc };
        var window = new Window { Width = 1900, Height = 1200, Content = view };
        window.Show();
        await view.RefreshAsync();
        return view;
    }

    private static List<string> CellNames(Control view) =>
        [.. view.GetLogicalDescendants().OfType<Grid>()
            .Where(g => AutomationProperties.GetName(g) == "Forward cash table")
            .SelectMany(g => g.Children.OfType<TextBlock>())
            .Select(t => AutomationProperties.GetName(t))
            .Where(n => n is { Length: > 0 })
            .Select(n => n!)];

    private static IEnumerable<Button> OpenButtons(Control view) =>
        view.GetLogicalDescendants().OfType<Button>().Where(b => AutomationProperties.GetName(b) is { } name && name.StartsWith("Open ", StringComparison.Ordinal));

    private static string Text(Control view) => string.Join(" | ", view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();
        window.Width = 1900;
        window.Height = 1200;
        window.Measure(new Avalonia.Size(1900, 1200));
        window.Arrange(new Avalonia.Rect(0, 0, 1900, 1200));
    }

    /// <summary>An accounts read model answering a fixed forward cash picture and an unavailable snapshot.</summary>
    private sealed class FixedReadModel(ForwardCashPicture picture) : IAccountsReadModel, IForwardCashReadModel
    {
        public Task<AccountsSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AccountsSnapshot.Unavailable("No accounts reading yet.", null, AsOf));

        public Task<ForwardCashPicture> ReadForwardCashAsync(CancellationToken cancellationToken = default) => Task.FromResult(picture);
    }
}
