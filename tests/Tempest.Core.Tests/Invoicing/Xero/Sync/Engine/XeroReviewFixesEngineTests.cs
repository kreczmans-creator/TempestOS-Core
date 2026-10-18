using System.Text;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Events;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` review-board fixes F2, end to end through the X6 engine over the
/// simulator: M2 (offline with an expired token queues, never waits for
/// authorisation), M6 (a refused attachment never holds its record's later
/// edits), M7 (unit prices at four places, so Xero's totals are TempestOS's)
/// and m19 (long line descriptions fit Xero). Each ends with no simulator
/// violation.
/// </summary>
public sealed class XeroReviewFixesEngineTests
{
    // ------------------------------------------------------------------ M2

    [Fact]
    public async Task M2_OfflineWithAnExpiredToken_TheQuoteIsQueued_NotWaitingForAuthorisation_AndGoesOnceOnline()
    {
        var tokenEndpoint = new TerminalHandler { Throw = new HttpRequestException("offline") };
        using var kit = await EngineTestKit.CreateAsync(tokenEndpoint: tokenEndpoint);
        await kit.Secrets.SetAsync("Invoicing:Xero:ExpiresAtUtc", DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));

        var id = kit.ExportQuote();
        await kit.SettleAsync();

        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.QuoteRef(id));
        Assert.Equal(XeroSyncBadge.Queued, status.Status.Badge);
        Assert.Equal("Queued", status.Label);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.WaitingForAuthorisation]));
        Assert.Empty(kit.AuditRows(XeroSyncService.AuditReauthorisationRequired));
        Assert.Empty(kit.Requests); // nothing reached Xero without a token
        Assert.Empty(kit.LiveQuotes);

        // Back online: the token endpoint renews the token, and the queued work goes.
        tokenEndpoint.Throw = null;
        tokenEndpoint.Respond = _ => TerminalHandler.Json(
            System.Net.HttpStatusCode.OK, $$"""{"access_token":"{{XeroTestAuthoriser.AccessToken}}","refresh_token":"rotated","expires_in":1800}""");
        kit.Clock.Advance(TimeSpan.FromMinutes(30));
        await kit.SettleAsync();

        Assert.Single(kit.LiveQuotes);
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.QuoteRef(id))).Badge);
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------------ M6

    [Fact]
    public async Task M6_An11MbReceipt_IsNotAttached_ButNeverHoldsTheBill_AnAmendmentStillGoes()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var id = kit.RecordExpense();
        kit.Files.Store(EngineTestKit.ExpenseRef(id), new string('x', 11 * 1024 * 1024), "receipt.jpg", "image/jpeg");
        await kit.SettleAsync();

        var bill = Assert.Single(kit.Bills);
        Assert.Empty(bill.Attachments);
        Assert.DoesNotContain(kit.WritesTo("Invoices"), w => w.Path.Contains("/Attachments/", StringComparison.Ordinal));
        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.ExpenseRef(id));
        Assert.Equal(XeroSyncBadge.InXeroDraft, status.Status.Badge);
        Assert.Contains(XeroAttachmentRefusal.NotePrefix, status.Status.Reason, StringComparison.Ordinal);
        Assert.Contains("11 MB", status.Status.Reason, StringComparison.Ordinal);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));

        // The expense is amended: the bill follows, the refused receipt holding nothing up.
        kit.Expenses[id] = kit.Expenses[id] with { NetAmount = 150m, VatAmount = 30m };
        kit.Saved(Tempest.Core.Expenses.ProjectExpense.CanonicalKind, id);
        await kit.SettleAsync();

        bill = Assert.Single(kit.Bills);
        Assert.Equal(150m, bill.Body["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Equal(30m, bill.Body["LineItems"]![0]!["TaxAmount"]!.GetValue<decimal>());
        Assert.Contains(XeroAttachmentRefusal.NotePrefix, (await kit.Engine.GetDocumentStatusAsync(EngineTestKit.ExpenseRef(id))).Status.Reason, StringComparison.Ordinal);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));

        // A smaller receipt is attached, and the note goes.
        kit.Files.Store(EngineTestKit.ExpenseRef(id), "a small receipt", "receipt.jpg", "image/jpeg");
        kit.Saved(Tempest.Core.Expenses.ProjectExpense.CanonicalKind, id);
        await kit.SettleAsync();

        Assert.Single(Assert.Single(kit.Bills).Attachments);
        var after = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.ExpenseRef(id));
        Assert.DoesNotContain(XeroAttachmentRefusal.NotePrefix, after.Status.Reason ?? string.Empty, StringComparison.Ordinal);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task M6_AQuotePdfTooLargeToAttach_IsFinishedAsANote_TheStatusChangeStillGoes()
    {
        using var kit = await EngineTestKit.CreateAsync();

        var id = kit.ExportQuote();
        kit.Files.Store(EngineTestKit.QuoteRef(id), new string('x', 12 * 1024 * 1024), "quote.pdf");
        await kit.SettleAsync();

        Assert.Empty(Assert.Single(kit.LiveQuotes).Attachments);
        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.QuoteRef(id));
        Assert.Equal(XeroSyncBadge.InXeroDraft, status.Status.Badge);
        Assert.Contains(XeroAttachmentRefusal.NotePrefix, status.Status.Reason, StringComparison.Ordinal);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));

        // Sent in TempestOS: the status change is not held behind the refused PDF.
        kit.SetQuoteStatus(id, Tempest.Core.Quotations.QuotationStatus.Sent);
        await kit.SettleAsync();
        Assert.Equal("SENT", Assert.Single(kit.LiveQuotes).Status);
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------------ M7

    [Fact]
    public async Task M7_UnitPricesAtThreeOrFourPlaces_TotalTheSameInXero_AndTheOrderIsStillProvablyOurs()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.Orders[id] = EngineTestKit.Order(id) with
        {
            Lines =
            [
                new XeroPurchaseOrderLine("Washers M6", 1000m, 0.125m, VatRate.Standard),
                new XeroPurchaseOrderLine("Bracket machining", 3m, 33.333m, VatRate.Standard),
            ],
        };
        kit.Files.Store(EngineTestKit.OrderRef(id), "PO sheet", "PO-2026-001.pdf");
        kit.Saved(Tempest.Core.PurchaseOrders.PurchaseOrder.CanonicalKind, id, WorkspaceChangeType.StatusChanged);
        await kit.SettleAsync();

        var order = Assert.Single(kit.LiveOrders);
        Assert.Equal(225m, order.Body["SubTotal"]!.GetValue<decimal>()); // 125.00 + 100.00, as TempestOS totals it
        Assert.Equal(0.125m, order.Body["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Equal(33.333m, order.Body["LineItems"]![1]!["UnitAmount"]!.GetValue<decimal>());
        Assert.All(kit.WritesTo("PurchaseOrders").Where(w => !w.Path.Contains("/Attachments/", StringComparison.Ordinal)), w => Assert.Equal("4", w.Query["unitdp"]));

        // The ownership value TempestOS logged for the create is the one Xero holds.
        var sent = Assert.Single(await kit.Creates.ListSentAsync(EngineTestKit.TenantId, EngineTestKit.OrderRef(id)));
        var held = await kit.Api.GetPurchaseOrderAsync(order.Id);
        Assert.Equal(sent.Value, XeroPurchasingOwnership.ValueOf(held.Value!));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task M7_AUnitPriceXeroWouldTotalDifferently_IsRefusedWithTheReason_NothingIsSent()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.Orders[id] = EngineTestKit.Order(id) with { Lines = [new XeroPurchaseOrderLine("Shims", 1000m, 0.33333m, VatRate.Standard)] };
        kit.Saved(Tempest.Core.PurchaseOrders.PurchaseOrder.CanonicalKind, id, WorkspaceChangeType.StatusChanged);
        await kit.SettleAsync();

        Assert.Empty(kit.LiveOrders);
        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.OrderRef(id));
        Assert.Equal(XeroSyncBadge.Failed, status.Status.Badge);
        Assert.Contains("more than 4 decimal places", status.Status.Reason, StringComparison.Ordinal);
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------------ m19

    [Fact]
    public async Task M19_ALongLineDescription_IsShortenedForXero_OnAQuoteAndAPurchaseOrder()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var longText = new StringBuilder().Insert(0, "Scope of work. ", 400).ToString(); // 6,000 characters

        var quoteId = kit.ExportQuote();
        kit.Quotes[quoteId] = kit.Quotes[quoteId] with { Lines = [.. kit.Quotes[quoteId].Lines.Select((l, i) => i == 0 ? l with { Description = longText } : l)] };
        var orderId = Guid.NewGuid();
        kit.Orders[orderId] = EngineTestKit.Order(orderId, reference: "PO-2026-002") with { Lines = [new XeroPurchaseOrderLine(longText, 1m, 10m, VatRate.Standard)] };
        kit.Saved(Tempest.Core.PurchaseOrders.PurchaseOrder.CanonicalKind, orderId, WorkspaceChangeType.StatusChanged);
        await kit.SettleAsync();

        foreach (var held in new[] { Assert.Single(kit.LiveQuotes), Assert.Single(kit.LiveOrders) })
        {
            var description = held.Body["LineItems"]![0]!["Description"]!.GetValue<string>();
            Assert.InRange(description.Length, XeroLineRules.MaximumDescriptionLength - 1, XeroLineRules.MaximumDescriptionLength); // a trailing space is trimmed
            Assert.EndsWith(XeroLineRules.ShortenedMarker, description, StringComparison.Ordinal);
        }

        Assert.NotNull(await kit.LinkAsync(EngineTestKit.QuoteRef(quoteId)));
        Assert.NotNull(await kit.LinkAsync(EngineTestKit.OrderRef(orderId)));
        kit.AssertNoViolations();
    }
}
