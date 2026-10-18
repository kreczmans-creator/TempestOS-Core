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
    public async Task ACopyKeyedInByHand_AfterAnAttemptThatNeverReachedTheCreate_IsNeverTakenOrTouched_UntilItsValuesMatch_ThenRetry()
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
        var handKeyed = await kit.CreateQuoteInXeroByHandAsync(kit.ContactId, "P0012-Q-001");
        var handBody = kit.OnlyQuote.Body.ToJsonString();

        kit.Settings.Cached = QuoteSyncTestKit.UkDemoReading();
        Assert.True(await kit.Outbox.RetryAsync(blocked.Entry.Id));
        var refused = Assert.Single(await kit.DrainAsync()); // the upload waits behind the refusal
        Assert.Equal(XeroOperation.PushQuote, refused.Entry.Operation);
        Assert.Equal(XeroPushOutcome.Rejected, refused.Result.Outcome);
        Assert.Contains("already used in Xero", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("for this client whose lines, dates or currency match nothing TempestOS sent", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("then Retry", refused.Result.Reason, StringComparison.Ordinal);

        // Never taken, never touched: no link, no write of any kind, the copy as it was keyed.
        Assert.Null(await kit.LinkAsync(id));
        Assert.Empty(OwnWrites(kit));
        Assert.Equal(handBody, kit.OnlyQuote.Body.ToJsonString());
        Assert.Empty(kit.OnlyQuote.Attachments);

        // The actionable path: its lines brought in line with the quotation in Xero, then Retry → it is TempestOS's own.
        await kit.EditQuoteInXeroByHandAsync(handKeyed, q => q["LineItems"] = DefaultLines());
        Assert.True(await kit.Outbox.RetryAsync(refused.Entry.Id));
        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.True(s.Result.Outcome is XeroPushOutcome.Succeeded or XeroPushOutcome.NothingToDo, s.Result.Reason));

        var link = (await kit.LinkAsync(id))!;
        Assert.Equal(handKeyed, link.XeroId);
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, link.LinkedBy);
        Assert.Equal(XeroQuoteMapper.ContentHash(r1), link.LastPushedContentHash);
        Assert.Equal("P0012 Bracket programme", kit.OnlyQuote.Body["Summary"]!.GetValue<string>()); // a DRAFT of its own follows TempestOS
        Assert.Single(kit.OnlyQuote.Attachments);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreate_ThenItsContactChangedByHand_IsRefusedWithWhatToChange_AndLinkedOnceTheContactIsSetBack()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var other = kit.Simulator.SeedContact("Someone Else Ltd");
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        var quoteId = kit.OnlyQuote.Id;
        await kit.EditQuoteInXeroByHandAsync(quoteId, q => q["Contact"] = new JsonObject { ["ContactID"] = other });
        var writes = OwnWrites(kit).Count;

        // The ownership rule needs the contact: the quote is not touched, and the message says what to change.
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var refused = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, refused.Result.Outcome);
        Assert.Contains("for a different contact than this quotation's client", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("set its contact back", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.LinkAsync(id));
        Assert.Equal(writes, OwnWrites(kit).Count);
        Assert.Equal(other, kit.OnlyQuote.Body["Contact"]!["ContactID"]!.GetValue<string>());

        // Set back in Xero, then Retry: TempestOS's own create (its sent values), linked — never a second quote.
        await kit.EditQuoteInXeroByHandAsync(quoteId, q => q["Contact"] = new JsonObject { ["ContactID"] = kit.ContactId });
        Assert.True(await kit.Outbox.RetryAsync(refused.Entry.Id));
        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));
        Assert.Equal(quoteId, Assert.Single(kit.LiveQuotes).Id);
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, (await kit.LinkAsync(id))!.LinkedBy);
        Assert.Single(kit.OnlyQuote.Attachments);
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
    public void TheFingerprints_CompareValuesNotText_ValuesIgnoreFreeText_AndContentSeesEveryWrittenField()
    {
        var body = new XeroWireQuoteWrite(
            "P0012-Q-001", "R2", "Bracket redesign", "P0012", new XeroWireContactRef("c-1"), "2026-10-02", "2026-11-01", "Payment within 30 days.", "GBP",
            "Exclusive", [new XeroWireLineItem("Concept design", 12m, 95m, "200", "OUTPUT2")]);
        var held = new XeroWireQuote(
            "q-1", "p0012-q-001", "R2", "SENT", new XeroWireContactRef("C-1"), "/Date(1790899200000+0000)/", "2026-11-01T00:00:00", "Bracket redesign",
            "P0012", "Payment within 30 days.\r\n", "GBP", "EXCLUSIVE", [new XeroWireLineItem("Concept design", 12.0000m, 95.00m, "200", "output2", 228m, 1140m, "l-1")]);

        Assert.Equal(XeroQuoteMapper.ValueFingerprint(body), XeroQuoteMapper.ValueFingerprint(held));
        Assert.Equal(XeroQuoteMapper.ContentFingerprint(body), XeroQuoteMapper.ContentFingerprint(held));

        // Free text a bookkeeper may edit, and the contact: never in either fingerprint's ownership test of values.
        foreach (var edited in new[]
                 {
                     held with { Reference = "Rev 2 - ACME PO 4471" }, held with { Title = "Edited" }, held with { Summary = "x" }, held with { Terms = "y" },
                     held with { LineItems = [held.LineItems![0] with { Description = "Concept design (typo fixed)" }] },
                     held with { LineItems = [held.LineItems![0] with { AccountCode = "201" }] },
                 })
        {
            Assert.Equal(XeroQuoteMapper.ValueFingerprint(body), XeroQuoteMapper.ValueFingerprint(edited));
        }

        Assert.Equal(XeroQuoteMapper.ContentFingerprint(body), XeroQuoteMapper.ContentFingerprint(held with { Reference = "Rev 2 - ACME", Contact = new XeroWireContactRef("c-2") }));
        Assert.NotEqual(XeroQuoteMapper.ContentFingerprint(body), XeroQuoteMapper.ContentFingerprint(held with { Title = "Edited" }));
        Assert.NotEqual(XeroQuoteMapper.ContentFingerprint(body), XeroQuoteMapper.ContentFingerprint(held with { LineItems = [held.LineItems![0] with { Description = "x" }] }));

        // Every value-bearing field is seen.
        foreach (var edited in new[]
                 {
                     held with { LineItems = [held.LineItems![0] with { Quantity = 13m }] }, held with { LineItems = [held.LineItems![0] with { UnitAmount = 96m }] },
                     held with { LineItems = [held.LineItems![0] with { TaxType = "OUTPUT" }] }, held with { LineItems = [] },
                     held with { Date = "2026-10-03" }, held with { ExpiryDate = "2026-11-02" }, held with { CurrencyCode = "EUR" }, held with { LineAmountTypes = "Inclusive" },
                 })
        {
            Assert.NotEqual(XeroQuoteMapper.ValueFingerprint(body), XeroQuoteMapper.ValueFingerprint(edited));
        }
    }

    [Fact]
    public void TheOwnershipRule_NeedsTheNumberTheContactAndValuesTempestOsSentToThatContact()
    {
        var body = new XeroWireQuoteWrite(
            "P0012-Q-001", "R1", "Bracket redesign", "P0012", new XeroWireContactRef("c-1"), "2026-10-02", "2026-11-01", null, "GBP",
            "Exclusive", [new XeroWireLineItem("Concept design", 12m, 95m, "200", "OUTPUT2")]);
        var held = new XeroWireQuote(
            "q-1", "P0012-Q-001", "Rev 1 - PO 4471", "SENT", new XeroWireContactRef("c-1"), "2026-10-02", "2026-11-01", "Retitled by hand", "P0012", null, "GBP", "EXCLUSIVE",
            [new XeroWireLineItem("Concept design", 12m, 95m, "200", "OUTPUT2")]);
        var sent = new[] { XeroQuoteSentContent.Of(body, "R1.aaaa") };

        var own = XeroQuoteMapper.OwnSend(held, "P0012-Q-001", "c-1", sent);
        Assert.NotNull(own);
        Assert.Equal(XeroQuoteContentMatch.ValuesOnly, own.Value.Match); // its title was changed by hand; its Reference never counts
        Assert.Equal("R1.aaaa", own.Value.Send.ContentHash);

        Assert.Null(XeroQuoteMapper.OwnSend(held with { QuoteNumber = "P0012-Q-002" }, "P0012-Q-001", "c-1", sent));
        Assert.Null(XeroQuoteMapper.OwnSend(held with { Contact = new XeroWireContactRef("c-2") }, "P0012-Q-001", "c-1", sent));
        Assert.Null(XeroQuoteMapper.OwnSend(held with { LineItems = [held.LineItems![0] with { Quantity = 13m }] }, "P0012-Q-001", "c-1", sent));
        Assert.Null(XeroQuoteMapper.OwnSend(held, "P0012-Q-001", "c-1", []));
        Assert.Null(XeroQuoteMapper.OwnSend(held with { Contact = new XeroWireContactRef("c-2") }, "P0012-Q-001", "c-2", sent)); // sent to c-1, not c-2

        // A later send with the same values wins the tie; an exact match beats a values-only one.
        var later = XeroQuoteSentContent.Of(body with { Title = "Retitled by hand" }, "R1.bbbb");
        Assert.Equal((XeroQuoteContentMatch.Exact, later), XeroQuoteMapper.Identify(held, [later, .. sent]));
        Assert.Equal(XeroQuoteContentMatch.ValuesOnly, XeroQuoteMapper.Identify(held with { Title = "Other" }, [.. sent, later]).Match);
        Assert.Equal("R1.bbbb", XeroQuoteMapper.Identify(held with { Title = "Other" }, [.. sent, later]).Send!.ContentHash);
    }

    /// <summary>The default quotation's lines as Xero holds them (UK Demo codes).</summary>
    internal static JsonArray DefaultLines() =>
    [
        Tempest.Core.Tests.Invoicing.Xero.Simulator.SimulatorTestKit.Line("Concept design", 12m, 95m),
        Tempest.Core.Tests.Invoicing.Xero.Simulator.SimulatorTestKit.Line("Drawing pack", 1m, 1_200m),
    ];

    /// <summary>The quote writes TempestOS sent (its own keys), not the ones made by hand.</summary>
    internal static IReadOnlyList<Tempest.Core.Tests.Invoicing.Xero.Simulator.XeroSimulatedRequest> OwnWrites(QuoteSyncTestKit kit) =>
        [.. kit.QuoteWrites.Where(r => r.IdempotencyKey?.StartsWith(XeroIdempotencyKey.Prefix, StringComparison.Ordinal) == true)];

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
