using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Quotations;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` X6: statuses read back from Xero on the read-back interval —
/// a bill approved and paid, a purchase order approved and billed or deleted,
/// a quote invoiced — shown on the badge, and the X1 settings read daily.
/// Never a write. Every test ends with no simulator violation.
/// </summary>
public sealed class XeroSyncReadBackTests
{
    [Fact]
    public async Task StatusesChangedInXero_AreReadBack_OnTheInterval_AndShownOnTheBadge()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var quoteId = kit.ExportQuote();
        var orderId = kit.IssueOrder("PO-2026-001");
        var deletedOrderId = kit.IssueOrder("PO-2026-002");
        var expenseId = kit.RecordExpense();
        await kit.SettleAsync();
        kit.SetQuoteStatus(quoteId, QuotationStatus.Sent);
        await kit.SettleAsync();
        kit.SetQuoteStatus(quoteId, QuotationStatus.Accepted);
        await kit.SettleAsync();

        // The bookkeeper works in Xero.
        var bill = Assert.Single(kit.Bills);
        var quote = Assert.Single(kit.LiveQuotes);
        kit.Simulator.ConvertQuoteToInvoiceInXero(quote.Id);
        var order = kit.LiveOrders.Single(o => o.Number == "PO-2026-001");
        kit.Simulator.ApproveInXero(order.Id);
        kit.Simulator.BillPurchaseOrderInXero(order.Id);
        kit.Simulator.DeleteInXero("PurchaseOrders", kit.LiveOrders.Single(o => o.Number == "PO-2026-002").Id);
        kit.Simulator.ApproveInXero(bill.Id);

        // Not due yet: nothing is read.
        var writesBefore = kit.Requests.Count(r => r.Method != HttpMethod.Get);
        Assert.Null((await kit.Engine.RunCycleAsync()).ReadBack);

        kit.Clock.Advance(kit.Options.ReadBackInterval);
        var report = (await kit.Engine.RunCycleAsync()).ReadBack;
        Assert.NotNull(report);
        Assert.False(report.Stopped);
        Assert.Equal(4, report.Changed.Count);

        Assert.Equal(XeroSyncBadge.AwaitingPayment, (await kit.Engine.GetStatusAsync(EngineTestKit.ExpenseRef(expenseId))).Badge);
        var orderStatus = await kit.Engine.GetStatusAsync(EngineTestKit.OrderRef(orderId));
        Assert.Equal(XeroSyncBadge.InXero, orderStatus.Badge);
        Assert.Equal("BILLED", orderStatus.XeroStatus);
        Assert.Equal(XeroSyncBadge.Voided, (await kit.Engine.GetStatusAsync(EngineTestKit.OrderRef(deletedOrderId))).Badge);
        var quoteStatus = await kit.Engine.GetStatusAsync(EngineTestKit.QuoteRef(quoteId));
        Assert.Equal(XeroSyncBadge.InXero, quoteStatus.Badge);
        Assert.Equal("INVOICED", quoteStatus.XeroStatus);
        Assert.Contains("twice", quoteStatus.Reason, StringComparison.OrdinalIgnoreCase);

        // Paid in Xero, read back on the next interval.
        kit.Simulator.PayInXero(bill.Id, new DateOnly(2026, 10, 20));
        kit.Clock.Advance(kit.Options.ReadBackInterval);
        await kit.Engine.RunCycleAsync();
        var paid = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.ExpenseRef(expenseId));
        Assert.Equal(XeroSyncBadge.Paid, paid.Status.Badge);
        Assert.Equal("Paid", paid.Label);

        // A deleted record is not read again; the read-back never writes.
        kit.Clock.Advance(kit.Options.ReadBackInterval);
        var mark = kit.Simulator.Requests.Count;
        await kit.Engine.RunCycleAsync();
        Assert.DoesNotContain(kit.Simulator.Requests.Skip(mark), r => r.Path.EndsWith(kit.Simulator.All("PurchaseOrders").Single(o => o.Number == "PO-2026-002").Id, StringComparison.Ordinal));
        Assert.Equal(writesBefore, kit.Requests.Count(r => r.Method != HttpMethod.Get));
        Assert.NotEmpty(kit.AuditRows(XeroReadBack.AuditStatusRead));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task TheReadBack_StopsAtOnce_WhenXeroIsUnavailable()
    {
        using var kit = await EngineTestKit.CreateAsync();
        kit.IssueOrder("PO-2026-001");
        kit.IssueOrder("PO-2026-002");
        await kit.SettleAsync();

        kit.Simulator.Inject(new Simulator.XeroFault(Simulator.XeroFaultKind.ServiceUnavailable, "PurchaseOrders/"));
        var mark = kit.Simulator.Requests.Count;
        var report = await kit.Engine.ReadBackNowAsync();

        Assert.NotNull(report);
        Assert.True(report.Stopped);
        Assert.Equal(0, report.Read);
        Assert.Single(kit.Simulator.Requests.Skip(mark));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task TheSettings_AreReadOnConnect_AndAgainOnceADayOld()
    {
        using var kit = await EngineTestKit.CreateAsync();

        await kit.Engine.RunCycleAsync();
        Assert.Equal(1, kit.Requests.Count(r => r.Path == "Organisation"));

        kit.Clock.Advance(TimeSpan.FromHours(12));
        await kit.Engine.RunCycleAsync();
        Assert.Equal(1, kit.Requests.Count(r => r.Path == "Organisation"));

        kit.Clock.Advance(TimeSpan.FromHours(12));
        await kit.Engine.RunCycleAsync();
        Assert.Equal(2, kit.Requests.Count(r => r.Path == "Organisation"));

        // A restart reads them again before its first write.
        kit.Restart();
        kit.IssueOrder();
        await kit.SettleAsync();
        Assert.Equal(3, kit.Requests.Count(r => r.Path == "Organisation"));
        Assert.Single(kit.LiveOrders);
        kit.AssertNoViolations();
    }
}
