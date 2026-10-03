using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` review-board fixes M5, m15 and n5, end to end over the simulator:
/// <see cref="XeroDocumentLinkActions"/> — <em>Unlink from Xero</em> for a
/// quote, purchase order or bill whose Xero copy was deleted there (checked
/// with Xero first, audited, nothing sent), <em>Send again</em> afterwards (a
/// new draft under a new key, its PDF too), and <em>link by Xero number</em>
/// for a purchase order that reads <em>Can't tell</em>. Every test ends with no
/// simulator violation.
/// </summary>
public sealed class XeroDocumentLinkActionsTests
{
    private static XeroDocumentLinkActions Actions(EngineTestKit kit) =>
        new(kit.Parts, kit.Engine, kit.Store, kit.Api, kit.Audit, kit.Clock);

    [Fact]
    public async Task AQuoteDeletedInXero_IsUnlinkedOnlyWhenAsked_ThenSendAgainMakesANewDraft()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var quoteId = kit.ExportQuote();
        await kit.SettleAsync();
        var first = Assert.Single(kit.LiveQuotes);
        var document = EngineTestKit.QuoteRef(quoteId);
        var actions = Actions(kit);
        Assert.False(await actions.CanUnlinkAsync(document)); // live in Xero: nothing to unlink

        kit.Simulator.DeleteInXero("Quotes", first.Id);
        Assert.NotNull(await kit.Engine.ReadBackNowAsync());
        Assert.Equal("DELETED", (await kit.LinkAsync(document))!.LastKnownXeroStatus);
        Assert.True(await actions.CanUnlinkAsync(document));

        var mark = kit.Simulator.Requests.Count;
        var unlinked = await actions.UnlinkAsync(document);
        Assert.True(unlinked.Done, unlinked.Message);
        Assert.Contains("Send again", unlinked.Message, StringComparison.Ordinal);
        Assert.Null(await kit.LinkAsync(document));
        Assert.True(await actions.WasUnlinkedAsync(document));
        var audit = Assert.Single(kit.AuditRows(XeroDocumentLinkActions.AuditUnlinked));
        Assert.Equal(first.Id, audit!["xeroId"]);
        Assert.Equal("DELETED", audit["status"]);

        // Unlinking sends nothing, and nothing goes by itself afterwards.
        await kit.SettleAsync();
        Assert.DoesNotContain(kit.Simulator.Requests.Skip(mark), r => r.Method != HttpMethod.Get);
        Assert.Empty(kit.LiveQuotes);
        Assert.Equal(XeroSyncBadge.NotSent, (await kit.Engine.GetStatusAsync(document)).Badge);

        var again = await actions.SendAgainAsync(document);
        Assert.True(again.Done, again.Message);
        await kit.SettleAsync();

        var second = Assert.Single(kit.LiveQuotes);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("DRAFT", second.Status);
        Assert.Single(second.Attachments); // the PDF went to the new copy too
        Assert.Equal(second.Id, (await kit.LinkAsync(document))!.XeroId);
        Assert.False(await actions.WasUnlinkedAsync(document));
        Assert.Single(kit.AuditRows(XeroDocumentLinkActions.AuditSendAgain));

        var creates = kit.Simulator.Requests.Where(r => r.Method == HttpMethod.Put && r.Path == "Quotes").ToList();
        Assert.Equal(2, creates.Select(r => r.IdempotencyKey).Distinct().Count()); // a new key, never the deleted copy's
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Unlink_AsksXeroFirst_AndKeepsTheLinkOfACopyStillLiveThere()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var quoteId = kit.ExportQuote();
        await kit.SettleAsync();
        var document = EngineTestKit.QuoteRef(quoteId);
        var link = (await kit.LinkAsync(document))!;

        // A stale reading says deleted, but Xero still holds the quote.
        await kit.Links.SaveAsync(link with { LastKnownXeroStatus = "DELETED" });
        var actions = Actions(kit);
        Assert.True(await actions.CanUnlinkAsync(document));

        var refused = await actions.UnlinkAsync(document);
        Assert.False(refused.Done);
        Assert.Contains("stays linked", refused.Message, StringComparison.Ordinal);
        Assert.NotNull(await kit.LinkAsync(document));
        Assert.Empty(kit.AuditRows(XeroDocumentLinkActions.AuditUnlinked));
        Assert.False((await actions.SendAgainAsync(document)).Done);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task APurchaseOrderDeletedInXero_IsUnlinked_AndSentAgainAsOneNewDraft()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var orderId = kit.IssueOrder();
        await kit.SettleAsync();
        var first = Assert.Single(kit.LiveOrders);
        var document = EngineTestKit.OrderRef(orderId);

        kit.Simulator.DeleteInXero("PurchaseOrders", first.Id);
        await kit.Engine.ReadBackNowAsync();
        var actions = Actions(kit);

        Assert.True((await actions.UnlinkAsync(document)).Done);
        await kit.SettleAsync();
        Assert.Empty(kit.LiveOrders); // nothing re-sent by itself

        // The badge offers X5's Send again too (the create is recorded gone); either path sends one new draft.
        Assert.True((await kit.Engine.GetDocumentStatusAsync(document)).CanSendAgain);
        var again = await actions.SendAgainAsync(document);
        Assert.True(again.Done, again.Message);
        await kit.SettleAsync();

        var second = Assert.Single(kit.LiveOrders);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(second.Id, (await kit.LinkAsync(document))!.XeroId);
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(document)).Badge);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ABillVoidedInXero_IsUnlinked_AndSentAgainAsANewBill()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var expenseId = kit.RecordExpense();
        await kit.SettleAsync();
        var first = Assert.Single(kit.Bills);
        var document = EngineTestKit.ExpenseRef(expenseId);

        kit.Simulator.DeleteInXero("Invoices", first.Id);
        await kit.Engine.ReadBackNowAsync();
        var actions = Actions(kit);
        Assert.True(await actions.CanUnlinkAsync(document));
        Assert.True((await actions.UnlinkAsync(document)).Done);

        Assert.True((await actions.SendAgainAsync(document)).Done);
        await kit.SettleAsync();

        var live = kit.Bills.Where(b => b.Status is not ("DELETED" or "VOIDED")).ToList();
        var second = Assert.Single(live);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(second.Id, (await kit.LinkAsync(document))!.XeroId);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task CannotTell_LinkByXeroNumber_FindsTheRecord_LinksIt_AndCreatesNothing()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();
        var orderId = kit.IssueOrder("PO-2026-077");
        var document = EngineTestKit.OrderRef(orderId);

        using var process = new CancellationTokenSource();
        kit.Hop.CrashOnNewWrite = ("PurchaseOrders", process);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => kit.Engine.RunCycleAsync(process.Token));
        var created = Assert.Single(kit.LiveOrders);
        kit.Clock.Advance(TimeSpan.FromMinutes(10));
        kit.Simulator.ForgetIdempotencyKeys();
        await kit.Restart().StartAsync();
        await kit.SettleAsync();
        Assert.True((await kit.Engine.GetDocumentStatusAsync(document)).CannotTell);

        var actions = Actions(kit);
        Assert.Null((await actions.FindByNumberAsync(document, "PO-1999-000")).Match); // not there: nothing linked

        var lookup = await actions.FindByNumberAsync(document, "PO-2026-077");
        var match = Assert.IsType<XeroNumberMatch>(lookup.Match);
        Assert.Equal(created.Id, match.XeroId);
        Assert.Contains("PO-2026-077", match.Describe(), StringComparison.Ordinal);

        var mark = kit.Simulator.Requests.Count;
        var linked = await actions.LinkByNumberAsync(match);
        Assert.True(linked.Done, linked.Message);
        await kit.SettleAsync();

        Assert.Equal(created.Id, (await kit.LinkAsync(document))!.XeroId);
        Assert.Equal(created.Id, Assert.Single(kit.LiveOrders).Id); // never a second purchase order
        Assert.DoesNotContain(kit.Simulator.Requests.Skip(mark), r => r.Method == HttpMethod.Put && r.Path == "PurchaseOrders");
        var status = await kit.Engine.GetDocumentStatusAsync(document);
        Assert.False(status.CannotTell);
        Assert.NotEqual(XeroSyncBadge.Failed, status.Status.Badge);
        var audit = Assert.Single(kit.AuditRows(XeroDocumentLinkActions.AuditLinkedByNumber), d => d!["kind"] == nameof(XeroDocumentKind.PurchaseOrder));
        Assert.Equal("PO-2026-077", audit!["xeroNumber"]);
        Assert.Equal("person", audit["by"]);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task LinkByNumber_RefusesARecordAlreadyLinkedToAnotherDocument()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var otherId = kit.IssueOrder("PO-2026-010");
        await kit.SettleAsync();

        var actions = Actions(kit);
        var lookup = await actions.FindByNumberAsync(EngineTestKit.OrderRef(Guid.NewGuid()), "PO-2026-010");
        Assert.Null(lookup.Match);
        Assert.Contains("already linked", lookup.Reason, StringComparison.Ordinal);
        Assert.NotNull(await kit.LinkAsync(EngineTestKit.OrderRef(otherId)));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AWriteWaitingForAContactLink_IsTypedBlocked_NotAFailure()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();
        var expenseId = kit.RecordExpense(EngineTestKit.UnlinkedReference);
        await kit.Engine.RunCycleAsync();

        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.ExpenseRef(expenseId));
        Assert.Equal(XeroSyncBadge.Failed, status.Status.Badge);
        Assert.True(status.Blocked);
        Assert.Contains($"'{EngineTestKit.UnlinkedReference}' is not linked to a Xero contact", status.Status.Reason, StringComparison.Ordinal);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ForgetSent_SupersedesOnlyTheDocumentsFinishedEntries()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var quoteId = kit.ExportQuote();
        var otherId = kit.ExportQuote("P0012-Q-002");
        await kit.SettleAsync();

        var forgotten = await kit.Outbox.ForgetSentAsync(EngineTestKit.QuoteRef(quoteId));
        Assert.True(forgotten > 0);
        Assert.All(await kit.Outbox.ListForDocumentAsync(EngineTestKit.QuoteRef(quoteId)), e => Assert.Equal(XeroOutboxState.Superseded, e.State));
        Assert.Contains(await kit.Outbox.ListForDocumentAsync(EngineTestKit.QuoteRef(otherId)), e => e.State == XeroOutboxState.Succeeded);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ARetryLaterReason_ShowsTheTimeInLocalTime_NotUtc()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var quoteId = kit.ExportQuote();
        await kit.SettleAsync();

        // A status change (not a create) that Xero cannot take now: retried later, at a time shown in local time.
        kit.SetQuoteStatus(quoteId, Tempest.Core.Quotations.QuotationStatus.Sent);
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, "Quotes", Times: 1));
        await kit.Engine.RunCycleAsync();

        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.QuoteRef(quoteId));
        Assert.Equal(XeroSyncBadge.Queued, status.Status.Badge);
        Assert.Contains("trying again at", status.Status.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("UTC", status.Status.Reason, StringComparison.Ordinal);
        kit.AssertNoViolations();
    }
}
