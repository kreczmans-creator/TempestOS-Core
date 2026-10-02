using System.Text.Json.Nodes;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Quotations;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;

/// <summary>
/// `v0.24.0` X3 (verifier round 4): a write reaches Xero but its answer is
/// lost (or TempestOS stops before recording it), then someone changes the
/// quote in Xero by hand before the retry. Every quote-handler path for that
/// class: the retry records what Xero actually holds — never a revision it
/// does not hold, never "not sent" for a revision it does — and a PDF follows
/// its content; nothing is ever created twice and no key is reused with
/// another body.
/// </summary>
public sealed class XeroQuoteLostAnswerTests
{
    private static XeroQuoteSnapshot R2(Guid id) =>
        QuoteSyncTestKit.Quote(id, revision: 2, lines: [new XeroQuoteLine("Concept design", 20m, 95m, VatRate.Standard)]);

    /// <summary>R1 created and linked with its PDF; R2 approved and exported; R2's content update reaches Xero but its answer is lost.</summary>
    private static async Task<(QuoteSyncTestKit Kit, Guid Id, XeroQuoteSnapshot R2)> LostR2UpdateAsync()
    {
        var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();

        var r2 = R2(id);
        kit.FakeQuotes[id] = r2;
        kit.Files.Store(id, "R2 sheet!!");
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        var lost = Assert.Single(await kit.DrainAsync()); // the upload waits behind the push
        Assert.Equal(XeroOperation.PushQuote, lost.Entry.Operation);
        Assert.Equal(XeroPushOutcome.RetryLater, lost.Result.Outcome);
        Assert.Equal("R2", kit.OnlyQuote.Body["Reference"]!.GetValue<string>()); // Xero applied it
        Assert.Equal("R1", XeroQuoteMapper.RevisionOf((await kit.LinkAsync(id))!.LastPushedContentHash));
        return (kit, id, r2);
    }

    [Fact]
    public async Task ALostContentUpdate_ThenSentByHand_LinksTheRevisionXeroHolds_AndItsPdfFollows()
    {
        var (kit, id, r2) = await LostR2UpdateAsync();
        using var _ = kit;
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        var push = Assert.Single(steps, s => s.Entry.Operation == XeroOperation.PushQuote);
        Assert.Equal(XeroPushOutcome.Succeeded, push.Result.Outcome);
        var upload = Assert.Single(steps, s => s.Entry.Operation == XeroOperation.UploadAttachment);
        Assert.Equal(XeroPushOutcome.Succeeded, upload.Result.Outcome);

        var link = (await kit.LinkAsync(id))!;
        Assert.Equal(XeroQuoteMapper.ContentHash(r2), link.LastPushedContentHash);
        Assert.Equal("SENT", link.LastKnownXeroStatus);
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(r2, link));
        Assert.DoesNotContain("was not sent", XeroQuoteMapper.DriftNote(r2, link) ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("R2 sheet!!".Length, Assert.Single(kit.OnlyQuote.Attachments).Length); // R2's PDF beside R2's lines
        Assert.DoesNotContain(await kit.Outbox.ListForDocumentAsync(QuoteSyncTestKit.Ref(id)), e => e.State == XeroOutboxState.Failed);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ACrashAfterAContentUpdateReachedXero_ThenSentByHand_LinksTheRevisionXeroHolds_AndItsPdfFollows()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();

        var r2 = R2(id);
        kit.FakeQuotes[id] = r2;
        kit.Files.Store(id, "R2 sheet!!");
        await kit.PlanAsync(id);

        // R2's update reaches Xero, then TempestOS stops before recording anything (InFlight → Unknown).
        var claimed = await kit.Outbox.ClaimNextDueAsync();
        Assert.Equal(XeroOperation.PushQuote, claimed!.Operation);
        kit.Lost.LoseWrites = 1;
        await kit.PushHandler.PushAsync(QuoteSyncTestKit.TenantId, claimed);
        Assert.Equal(1, await kit.Outbox.RecoverInFlightAsync());
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");

        var steps = await kit.DrainAsync();
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(steps, s => s.Entry.Operation == XeroOperation.PushQuote).Result.Outcome);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(steps, s => s.Entry.Operation == XeroOperation.UploadAttachment).Result.Outcome);
        Assert.Equal(XeroQuoteMapper.ContentHash(r2), (await kit.LinkAsync(id))!.LastPushedContentHash);
        Assert.Equal("R2 sheet!!".Length, Assert.Single(kit.OnlyQuote.Attachments).Length);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ASupersededUpdateThatLanded_ThenSentByHand_ThenAChangeWithinTheRevision_IsNotCalledAnUnsentRevision()
    {
        var (kit, id, r2) = await LostR2UpdateAsync();
        using var _ = kit;
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");

        // A change within R2 (the title) queues a newer push, which supersedes the one whose update landed.
        var retitled = r2 with { Title = "Bracket redesign, issue 2" };
        kit.FakeQuotes[id] = retitled;
        await kit.Outbox.EnqueueAsync(XeroOperation.PushQuote, QuoteSyncTestKit.Ref(id), XeroQuoteMapper.ContentHash(retitled));

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        var push = Assert.Single(steps, s => s.Entry.Operation == XeroOperation.PushQuote);
        Assert.Equal(1, push.Entry.Attempts); // the newer entry: never sent before
        Assert.Equal(XeroPushOutcome.NothingToDo, push.Result.Outcome);
        Assert.Contains("with revision R2", push.Result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("was not sent", push.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(steps, s => s.Entry.Operation == XeroOperation.UploadAttachment).Result.Outcome);

        var link = (await kit.LinkAsync(id))!;
        Assert.Equal("R2", XeroQuoteMapper.RevisionOf(link.LastPushedContentHash));
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(retitled, link));
        Assert.Equal("Bracket redesign", kit.OnlyQuote.Body["Title"]!.GetValue<string>()); // Q1: nothing written past DRAFT
        Assert.Equal("R2 sheet!!".Length, Assert.Single(kit.OnlyQuote.Attachments).Length);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AStaleEntrysOwnUpdateThatLanded_ThenSentByHand_RecordsTheRevisionXeroHolds_WhenNoNewerPushFollows()
    {
        var (kit, id, r2) = await LostR2UpdateAsync();
        using var _ = kit;
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");

        // The quotation changes within R2 but no newer push is queued (nothing supersedes the entry).
        var retitled = r2 with { Title = "Bracket redesign, issue 2" };
        kit.FakeQuotes[id] = retitled;

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        Assert.Equal(XeroPushOutcome.NothingToDo, Assert.Single(steps, s => s.Entry.Operation == XeroOperation.PushQuote).Result.Outcome);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(steps, s => s.Entry.Operation == XeroOperation.UploadAttachment).Result.Outcome);
        var link = (await kit.LinkAsync(id))!;
        Assert.Equal("R2", XeroQuoteMapper.RevisionOf(link.LastPushedContentHash));
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(retitled, link));
        Assert.Equal("R2 sheet!!".Length, Assert.Single(kit.OnlyQuote.Attachments).Length);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostContentUpdate_ThenDeletedByHand_IsReportedAsDeleted_NotAsSucceededOrUnsent()
    {
        var (kit, id, _) = await LostR2UpdateAsync();
        using var __ = kit;
        kit.Simulator.DeleteInXero("Quotes", kit.OnlyQuote.Id);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var push = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroOperation.PushQuote, push.Entry.Operation);
        Assert.Equal(XeroPushOutcome.Rejected, push.Result.Outcome);
        Assert.Contains("was deleted in Xero", push.Result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("was not sent", push.Result.Reason, StringComparison.Ordinal);
        Assert.Equal("DELETED", (await kit.LinkAsync(id))!.LastKnownXeroStatus);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreate_ThenEditedByHandWhileDraft_IsBroughtUpToDate_WithoutReusingTheCreatesKey()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        var r1 = QuoteSyncTestKit.Quote(id);
        kit.FakeQuotes[id] = r1;
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        await kit.EditQuoteInXeroByHandAsync(kit.OnlyQuote.Id, q => q["Title"] = "Edited in Xero");

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        Assert.Equal("Bracket redesign", kit.OnlyQuote.Body["Title"]!.GetValue<string>()); // the DRAFT follows TempestOS
        Assert.Equal(XeroQuoteMapper.ContentHash(r1), (await kit.LinkAsync(id))!.LastPushedContentHash);
        Assert.Single(kit.QuoteWrites, r => r.Method == HttpMethod.Put && r.Path == "Quotes");
        var update = Assert.Single(kit.QuoteWrites, r => r.Method == HttpMethod.Post && r.Path == $"Quotes/{kit.OnlyQuote.Id}" && r.IdempotencyKey!.StartsWith(XeroIdempotencyKey.Prefix, StringComparison.Ordinal));
        Assert.NotEqual(steps[0].Entry.IdempotencyKey, update.IdempotencyKey);
        kit.AssertNoViolations(); // no Idempotency-Key reused with another body
    }

    [Fact]
    public async Task ACopyKeyedInByHand_AfterAnAttemptThatNeverReachedTheCreate_IsBroughtUpToDate_NotTakenForTheCreate()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        kit.Settings.Cached = QuoteSyncTestKit.UkDemoReading() with { Accounts = [] };
        var id = Guid.NewGuid();
        var r1 = QuoteSyncTestKit.Quote(id);
        kit.FakeQuotes[id] = r1;
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        var blocked = (await kit.DrainAsync())[0];
        Assert.Equal(XeroPushOutcome.Blocked, blocked.Result.Outcome); // never reached the create
        Assert.Empty(kit.QuoteWrites);

        // Someone keys the quote into Xero by hand: same number, same contact, Reference R1, other lines.
        await kit.CreateQuoteInXeroByHandAsync(kit.ContactId, "P0012-Q-001");
        Assert.Single(kit.OnlyQuote.Body["LineItems"]!.AsArray());

        kit.Settings.Cached = QuoteSyncTestKit.UkDemoReading();
        Assert.True(await kit.Outbox.RetryAsync(blocked.Entry.Id));
        var steps = await kit.DrainAsync();
        var push = Assert.Single(steps, s => s.Entry.Operation == XeroOperation.PushQuote);
        Assert.Equal(2, push.Entry.Attempts);
        Assert.Equal(XeroPushOutcome.Succeeded, push.Result.Outcome);

        Assert.Equal(2, kit.OnlyQuote.Body["LineItems"]!.AsArray().Count); // brought up to date, not left with the hand-keyed lines
        var link = (await kit.LinkAsync(id))!;
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, link.LinkedBy);
        Assert.Equal(XeroQuoteMapper.ContentHash(r1), link.LastPushedContentHash);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreate_ThenItsContactChangedByHand_IsLinked_NotRefusedAsSomeoneElses()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var other = kit.Simulator.SeedContact("Someone Else Ltd");
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        await kit.EditQuoteInXeroByHandAsync(kit.OnlyQuote.Id, q => q["Contact"] = new JsonObject { ["ContactID"] = other });

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        Assert.Single(kit.LiveQuotes);
        Assert.Equal(kit.ContactId, kit.OnlyQuote.Body["Contact"]!["ContactID"]!.GetValue<string>()); // the DRAFT follows TempestOS
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, (await kit.LinkAsync(id))!.LinkedBy);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnotherContactsQuoteWithTheNumber_IsStillRefused_EvenOnARetry()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var other = kit.Simulator.SeedContact("Someone Else Ltd");
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        // The first attempt fails before anything is sent; someone else's quote then takes the number.
        kit.Settings.Cached = QuoteSyncTestKit.UkDemoReading() with { Accounts = [] };
        var blocked = (await kit.DrainAsync())[0];
        await kit.CreateQuoteInXeroByHandAsync(other, "P0012-Q-001");
        kit.Settings.Cached = QuoteSyncTestKit.UkDemoReading();
        Assert.True(await kit.Outbox.RetryAsync(blocked.Entry.Id));

        var refused = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, refused.Result.Outcome);
        Assert.Contains("already used in Xero", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.LinkAsync(id));
        Assert.Single(kit.LiveQuotes);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreate_ThenRenumberedByHand_IsLinkedFromTheReplayedAnswer_NeverCreatedTwice()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        var quoteId = kit.OnlyQuote.Id;
        await kit.EditQuoteInXeroByHandAsync(quoteId, q => q["QuoteNumber"] = "P0012-Q-001-X");

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        Assert.Equal(quoteId, Assert.Single(kit.LiveQuotes).Id); // Xero replayed the create's answer (§6.4 item 2)
        Assert.Equal(quoteId, (await kit.LinkAsync(id))!.XeroId);
        kit.AssertNoViolations();
    }

    [Fact]
    public void HoldsContent_ComparesValuesNotText_AndSeesAnyChange()
    {
        var body = new XeroWireQuoteWrite(
            "P0012-Q-001", "R2", "Bracket redesign", "P0012", new XeroWireContactRef("c-1"), "2026-10-02", "2026-11-01", "Payment within 30 days.", "GBP",
            "Exclusive", [new XeroWireLineItem("Concept design", 12m, 95m, "200", "OUTPUT2")]);
        var held = new XeroWireQuote(
            "q-1", "p0012-q-001", "R2", "SENT", new XeroWireContactRef("C-1"), "/Date(1790899200000+0000)/", "2026-11-01T00:00:00", "Bracket redesign",
            "P0012", "Payment within 30 days.\r\n", "GBP", "EXCLUSIVE", [new XeroWireLineItem("Concept design", 12.0000m, 95.00m, "200", "output2", 228m, 1140m, "l-1")]);

        Assert.True(XeroQuoteMapper.HoldsContent(held, body));
        Assert.False(XeroQuoteMapper.HoldsContent(held with { Reference = "R1" }, body));
        Assert.False(XeroQuoteMapper.HoldsContent(held with { Title = "Edited" }, body));
        Assert.False(XeroQuoteMapper.HoldsContent(held with { LineItems = [held.LineItems![0] with { Quantity = 13m }] }, body));
        Assert.False(XeroQuoteMapper.HoldsContent(held with { LineItems = [] }, body));
        Assert.False(XeroQuoteMapper.HoldsContent(held with { Date = "2026-10-03" }, body));
        Assert.False(XeroQuoteMapper.HoldsContent(held with { Contact = new XeroWireContactRef("c-2") }, body));
        Assert.True(XeroQuoteMapper.HoldsContent(held with { Contact = new XeroWireContactRef("c-2") }, body, includeContact: false));
    }

    [Fact]
    public void TheContentUpdateKey_IsFixedPerEntry_NeverTheEntrysOwnKey_AndFitsXero()
    {
        var document = QuoteSyncTestKit.Ref(Guid.NewGuid());
        var hash = XeroQuoteMapper.ContentHash(QuoteSyncTestKit.Quote(Guid.Parse(document.TempestKey)));
        var entry = new XeroOutboxEntry(
            XeroOutboxEntry.CurrentSchemaVersion, Guid.NewGuid(), XeroOperation.PushQuote, document, null,
            XeroIdempotencyKey.Create(document, XeroOperation.PushQuote, hash), hash, XeroOutboxState.InFlight, 2,
            DateTimeOffset.UnixEpoch, null, null, null, "test");

        var key = XeroQuotePushHandler.ContentUpdateKey(entry);
        Assert.NotEqual(entry.IdempotencyKey, key);
        Assert.Equal(key, XeroQuotePushHandler.ContentUpdateKey(entry with { Attempts = 3 }));
        Assert.NotEqual(key, XeroQuotePushHandler.ContentUpdateKey(entry with { IdempotencyKey = XeroIdempotencyKey.Create(document, XeroOperation.PushQuote, hash, occurrence: 1) }));
        Assert.True(XeroIdempotencyKey.IsWellFormed(key));
        Assert.True(key.Length <= XeroOutboxEntry.MaximumIdempotencyKeyLength);
    }
}
