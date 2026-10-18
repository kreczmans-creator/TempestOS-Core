using Tempest.Core.Events;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` X6: the sync engine makes everything run by itself, end to end
/// over the simulator — saved changes planned and sent, the start-up scan,
/// one request at a time, statuses read back, the X1 settings kept fresh, and
/// the badge answered from local state. Every test ends with no simulator
/// violation.
/// </summary>
public sealed class XeroSyncEngineTests
{
    [Fact]
    public async Task AnExportedQuote_GoesToXeroByItself_ThenFollowsSentAndAccepted()
    {
        using var kit = await EngineTestKit.CreateAsync();

        var id = kit.ExportQuote();
        await kit.SettleAsync();

        // The X1 settings are read before the session's first write (§8).
        Assert.Equal(["Organisation", "TaxRates", "Accounts"], kit.Requests.Take(3).Select(r => r.Path));

        var quote = Assert.Single(kit.LiveQuotes);
        Assert.Equal("DRAFT", quote.Status);
        Assert.Equal("P0012-Q-001", quote.Number);
        Assert.Single(quote.Attachments);
        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.QuoteRef(id));
        Assert.Equal(XeroSyncBadge.InXeroDraft, status.Status.Badge);
        Assert.Equal("In Xero (draft)", status.Label);
        Assert.Equal("P0012-Q-001", status.Status.XeroNumber);

        kit.SetQuoteStatus(id, QuotationStatus.Sent);
        await kit.SettleAsync();
        Assert.Equal("SENT", Assert.Single(kit.LiveQuotes).Status);

        kit.SetQuoteStatus(id, QuotationStatus.Accepted);
        await kit.SettleAsync();
        Assert.Equal("ACCEPTED", Assert.Single(kit.LiveQuotes).Status);

        status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.QuoteRef(id));
        Assert.Equal(XeroSyncBadge.InXero, status.Status.Badge);
        Assert.Equal("ACCEPTED", status.Status.XeroStatus);

        Assert.Equal(4, kit.AuditRows(XeroSyncService.AuditEnqueued).Count); // push, PDF, SENT, ACCEPTED
        Assert.Equal(4, kit.AuditRows(XeroSyncService.AuditSucceeded).Count);
        Assert.Empty(kit.AuditRows(XeroSyncService.AuditFailed));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task APurchaseOrderAndAnExpense_GoToXeroAsDrafts_WithTheirPdfAndReceipt()
    {
        using var kit = await EngineTestKit.CreateAsync();

        var orderId = kit.IssueOrder();
        var expenseId = kit.RecordExpense();
        await kit.SettleAsync();

        var order = Assert.Single(kit.LiveOrders);
        Assert.Equal("DRAFT", order.Status);
        Assert.Equal("PO-2026-001", order.Number);
        Assert.Equal(kit.SupplierContactId, order.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Single(order.Attachments);

        var bill = Assert.Single(kit.Bills);
        Assert.Equal("DRAFT", bill.Status);
        Assert.Equal(kit.GeneralContactId, bill.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Single(bill.Attachments);

        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.OrderRef(orderId))).Badge);
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.ExpenseRef(expenseId))).Badge);

        // Cancelled in TempestOS → deleted in Xero; nothing else is sent again.
        kit.Orders[orderId] = kit.Orders[orderId] with { Status = Tempest.Core.PurchaseOrders.PurchaseOrderStatus.Cancelled };
        kit.Saved(Tempest.Core.PurchaseOrders.PurchaseOrder.CanonicalKind, orderId, WorkspaceChangeType.StatusChanged);
        await kit.SettleAsync();
        Assert.Empty(kit.LiveOrders);
        Assert.Single(kit.WritesTo("PurchaseOrders"), w => w.Method == HttpMethod.Put && w.Path == "PurchaseOrders");
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AChangeSavedJustBeforeARestart_IsSentByTheStartUpScan()
    {
        using var kit = await EngineTestKit.CreateAsync();

        // Saved, and the process stopped before anything was planned: no event survives.
        var id = Guid.NewGuid();
        kit.Quotes[id] = EngineTestKit.Quote(id);
        kit.Files.Store(EngineTestKit.QuoteRef(id), "sheet", "quote.pdf");
        Assert.Empty(await kit.Outbox.ListAsync([]));

        var engine = kit.Restart();
        await engine.StartAsync();
        Assert.NotEmpty(await kit.Outbox.ListAsync([]));

        await kit.SettleAsync();
        Assert.Equal("DRAFT", Assert.Single(kit.LiveQuotes).Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ARecordSavedUnheard_IsPickedUpByThePeriodicFullScan()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        // Saved while nothing was listening (no change event).
        var id = Guid.NewGuid();
        kit.Orders[id] = EngineTestKit.Order(id);
        Assert.Equal(0, (await kit.Engine.RunCycleAsync()).Planned);
        Assert.Empty(kit.LiveOrders);

        kit.Clock.Advance(kit.Options.FullScanInterval);
        await kit.SettleAsync();
        Assert.Equal("DRAFT", Assert.Single(kit.LiveOrders).Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task OfflineWork_IsQueued_ShowsWaiting_AndIsSentInOrderPerDocumentOnceConnected()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Secrets.RemoveAsync(Tempest.Core.Invoicing.Xero.Contacts.XeroContactLinker.TenantIdSecretKey);

        var id = kit.ExportQuote();
        await kit.Engine.RunCycleAsync();
        kit.SetQuoteStatus(id, QuotationStatus.Sent);
        await kit.Engine.RunCycleAsync();
        kit.SetQuoteStatus(id, QuotationStatus.Accepted);
        await kit.Engine.RunCycleAsync();

        Assert.Empty(kit.Requests);
        var waiting = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.QuoteRef(id));
        Assert.Equal(XeroSyncBadge.NeedsReauthorisation, waiting.Status.Badge);
        Assert.Equal("Waiting for authorisation", waiting.Label);
        Assert.Equal(
            [XeroOperation.PushQuote, XeroOperation.UploadAttachment, XeroOperation.SetQuoteStatus, XeroOperation.SetQuoteStatus],
            (await kit.Outbox.ListForDocumentAsync(EngineTestKit.QuoteRef(id))).Where(e => e.State == XeroOutboxState.Pending).Select(e => e.Operation));

        await kit.Secrets.SetAsync(Tempest.Core.Invoicing.Xero.Contacts.XeroContactLinker.TenantIdSecretKey, EngineTestKit.TenantId);
        await kit.SettleAsync();

        var writes = kit.WritesTo("Quotes");
        Assert.Equal(4, writes.Count);
        Assert.Equal("Quotes", writes[0].Path);
        Assert.Contains("/Attachments/", writes[1].Path, StringComparison.Ordinal);
        Assert.Equal("SENT", writes[2].JsonBody!["Quotes"]![0]!["Status"]!.GetValue<string>());
        Assert.Equal("ACCEPTED", writes[3].JsonBody!["Quotes"]![0]!["Status"]!.GetValue<string>());
        Assert.Equal("ACCEPTED", Assert.Single(kit.LiveQuotes).Status);
        kit.AssertNoViolations();
    }
}
