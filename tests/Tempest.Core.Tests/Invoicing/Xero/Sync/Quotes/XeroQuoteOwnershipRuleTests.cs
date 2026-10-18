using System.Text.Json.Nodes;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Quotations;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;

/// <summary>
/// `v0.24.0` X3 (verifier round 5): the one ownership rule on
/// <see cref="XeroQuoteMapper"/> — a Xero quote is TempestOS's own only when
/// linked, or its number and contact match and its values equal content
/// TempestOS is known to have sent (the current entry's, or a recorded
/// earlier send) — run over the rows of the quote handler's path table not
/// covered elsewhere. <c>Reference</c> and other free text never decide it;
/// what is not TempestOS's own is never touched and gets one actionable
/// message; a past-<c>DRAFT</c> note always says what Xero holds.
/// </summary>
public sealed class XeroQuoteOwnershipRuleTests
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
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal("R2", kit.OnlyQuote.Body["Reference"]!.GetValue<string>()); // Xero applied it
        return (kit, id, r2);
    }

    // Verifier round 5, defect 1: a hand edit of Reference never decides what Xero holds.
    [Fact]
    public async Task ALostR2Update_ThenReferenceEditedByHand_ThenSent_RecordsR2_AndR2sPdfFollows()
    {
        var (kit, id, r2) = await LostR2UpdateAsync();
        using var _ = kit;
        await kit.EditQuoteInXeroByHandAsync(kit.OnlyQuote.Id, q => q["Reference"] = "Rev 2 - ACME PO 4471");
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        var push = Assert.Single(steps, s => s.Entry.Operation == XeroOperation.PushQuote);
        Assert.Equal(XeroPushOutcome.Succeeded, push.Result.Outcome);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(steps, s => s.Entry.Operation == XeroOperation.UploadAttachment).Result.Outcome);
        var link = (await kit.LinkAsync(id))!;
        Assert.Equal(XeroQuoteMapper.ContentHash(r2), link.LastPushedContentHash);
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(r2, link));
        Assert.Equal("R2 sheet!!".Length, Assert.Single(kit.OnlyQuote.Attachments).Length);
        Assert.Equal("Rev 2 - ACME PO 4471", kit.OnlyQuote.Body["Reference"]!.GetValue<string>()); // the bookkeeper's edit is kept
        Assert.DoesNotContain(await kit.Outbox.ListForDocumentAsync(QuoteSyncTestKit.Ref(id)), e => e.State == XeroOutboxState.Failed);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostR2Update_ThenTitleEditedByHand_ThenSent_RecordsR2_SaysTheTextWasChangedByHand_AndR2sPdfFollows()
    {
        var (kit, id, r2) = await LostR2UpdateAsync();
        using var _ = kit;
        await kit.EditQuoteInXeroByHandAsync(kit.OnlyQuote.Id, q => q["Title"] = "Bracket redesign (ACME)");
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        var push = Assert.Single(steps, s => s.Entry.Operation == XeroOperation.PushQuote);
        Assert.Equal(XeroPushOutcome.NothingToDo, push.Result.Outcome);
        Assert.Contains("with this revision's lines, dates and currency", push.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("changed in Xero by hand", push.Result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("was not sent", push.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(steps, s => s.Entry.Operation == XeroOperation.UploadAttachment).Result.Outcome);
        Assert.Equal(XeroQuoteMapper.ContentHash(r2), (await kit.LinkAsync(id))!.LastPushedContentHash);
        Assert.Equal("R2 sheet!!".Length, Assert.Single(kit.OnlyQuote.Attachments).Length);
        Assert.Equal("Bracket redesign (ACME)", kit.OnlyQuote.Body["Title"]!.GetValue<string>()); // Q1: nothing written past DRAFT
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostR2Update_ThenALineEditedByHand_ThenSent_SaysItsValuesWereChangedByHand_NotALaterChangeInTempestOs()
    {
        var (kit, id, r2) = await LostR2UpdateAsync();
        using var _ = kit;
        await kit.EditQuoteInXeroByHandAsync(kit.OnlyQuote.Id, q => q["LineItems"]![0]!["Quantity"] = 21m);
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");
        var contentWrites = XeroQuoteLostAnswerTests.OwnWrites(kit).Count(w => w.Path == $"Quotes/{kit.OnlyQuote.Id}");

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var push = Assert.Single(await kit.DrainAsync(), s => s.Entry.Operation == XeroOperation.PushQuote);

        Assert.Equal(XeroPushOutcome.NothingToDo, push.Result.Outcome);
        Assert.Contains("changed in Xero by hand and match nothing TempestOS sent", push.Result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("later change within that revision", push.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(21m, kit.OnlyQuote.Body["LineItems"]![0]!["Quantity"]!.GetValue<decimal>()); // never overwritten
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(r2, (await kit.LinkAsync(id))!));
        Assert.Equal(contentWrites, XeroQuoteLostAnswerTests.OwnWrites(kit).Count(w => w.Path == $"Quotes/{kit.OnlyQuote.Id}")); // no content written
        kit.AssertNoViolations();
    }

    // Verifier round 5, defect 3: a quote just found by its number past DRAFT never gets the "later change within that revision" note.
    [Fact]
    public async Task ALostCreate_ThenTitleEditedAndSentByHand_IsLinked_WithANoteAboutTheHandEdit_NotALaterChangeWithinTheRevision()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        var r1 = QuoteSyncTestKit.Quote(id);
        kit.FakeQuotes[id] = r1;
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        await kit.EditQuoteInXeroByHandAsync(kit.OnlyQuote.Id, q => q["Title"] = "Bracket redesign (ACME)");
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        var push = Assert.Single(steps, s => s.Entry.Operation == XeroOperation.PushQuote);
        Assert.Equal(XeroPushOutcome.NothingToDo, push.Result.Outcome);
        Assert.DoesNotContain("later change within that revision", push.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("changed in Xero by hand", push.Result.Reason, StringComparison.Ordinal);
        var link = (await kit.LinkAsync(id))!;
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, link.LinkedBy);
        Assert.Equal(XeroQuoteMapper.ContentHash(r1), link.LastPushedContentHash);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(steps, s => s.Entry.Operation == XeroOperation.UploadAttachment).Result.Outcome);
        Assert.Single(kit.LiveQuotes);
        kit.AssertNoViolations();
    }

    // Verifier round 5, defect 2: a superseded entry's create counts, through the send recorded before it was made.
    [Fact]
    public async Task ALostCreate_ThenAChangeWithinR1_ThenContactChangedByHand_IsRefusedWithWhatToChange_ThenRetryLinksAndUpdatesIt()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var other = kit.Simulator.SeedContact("Someone Else Ltd");
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync(); // R1's create lands; its answer is lost
        var quoteId = kit.OnlyQuote.Id;

        // Changed within R1 (new values): a newer entry supersedes the one whose create landed.
        var changed = QuoteSyncTestKit.Quote(id, lines: [new XeroQuoteLine("Concept design", 14m, 95m, VatRate.Standard), new XeroQuoteLine("Drawing pack", 1m, 1_200m, VatRate.Standard)]);
        kit.FakeQuotes[id] = changed;
        await kit.PlanAsync(id);
        await kit.EditQuoteInXeroByHandAsync(quoteId, q => q["Contact"] = new JsonObject { ["ContactID"] = other });

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var refused = Assert.Single(await kit.DrainAsync(), s => s.Result.Outcome == XeroPushOutcome.Rejected);
        Assert.Equal(XeroQuoteMapper.ContentHash(changed), refused.Entry.ContentHash);
        Assert.Contains("for a different contact than this quotation's client", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("set its contact back", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.LinkAsync(id));

        // The contact set back in Xero, then Retry: Xero holds the superseded entry's values — TempestOS's own — so it is linked and brought up to date.
        await kit.EditQuoteInXeroByHandAsync(quoteId, q => q["Contact"] = new JsonObject { ["ContactID"] = kit.ContactId });
        Assert.True(await kit.Outbox.RetryAsync(refused.Entry.Id));
        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        Assert.Equal(quoteId, Assert.Single(kit.LiveQuotes).Id);
        Assert.Equal(14m, kit.OnlyQuote.Body["LineItems"]![0]!["Quantity"]!.GetValue<decimal>());
        var link = (await kit.LinkAsync(id))!;
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, link.LinkedBy);
        Assert.Equal(XeroQuoteMapper.ContentHash(changed), link.LastPushedContentHash);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ASupersededEntrysSend_IsRecordedBeforeTheWrite_SoItsQuoteCountsAsTempestOsOwn()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        var r1 = QuoteSyncTestKit.Quote(id);
        kit.FakeQuotes[id] = r1;
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();

        var stored = await kit.Store.ReadAsync(XeroQuotePushHandler.SentCollection, id.ToString("D"));
        var sent = Assert.Single(System.Text.Json.JsonSerializer.Deserialize<List<XeroQuoteSentContent>>(stored!)!);
        Assert.Equal(XeroQuoteMapper.ContentHash(r1), sent.ContentHash);
        Assert.Equal(kit.ContactId, sent.ContactId);
    }

    [Fact]
    public async Task AStaleEntry_FindingAQuoteThatIsNotTempestOsOwn_TouchesNothing_AndLeavesItToTheNewerEntry()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var other = kit.Simulator.SeedContact("Someone Else Ltd");
        await kit.CreateQuoteInXeroByHandAsync(other, "P0012-Q-001");
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        kit.FakeQuotes[id] = R2(id); // changed after queueing, not yet re-planned
        var step = (await kit.DrainAsync())[0];
        Assert.Equal(XeroPushOutcome.NothingToDo, step.Result.Outcome);
        Assert.Contains("newer write", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.LinkAsync(id));
        Assert.Empty(XeroQuoteLostAnswerTests.OwnWrites(kit));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AQuoteThatIsNotTempestOsOwn_IsNeverMovedOrGivenAPdf_EvenWithStatusChangesQueued()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var xeroId = await kit.CreateQuoteInXeroByHandAsync(kit.ContactId, "P0012-Q-001");
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Accepted);
        kit.Files.Store(id, "R1 sheet");
        var queued = await kit.PlanAsync(id);
        Assert.Contains(queued, e => e.Operation == XeroOperation.SetQuoteStatus);

        var steps = await kit.DrainAsync();
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(steps).Result.Outcome);

        // Called directly too: with no link, a status change and an upload never reach it.
        var status = queued.First(e => e.Operation == XeroOperation.SetQuoteStatus);
        Assert.Equal(XeroPushOutcome.Blocked, (await kit.PushHandler.PushAsync(QuoteSyncTestKit.TenantId, status)).Outcome);
        var upload = queued.First(e => e.Operation == XeroOperation.UploadAttachment);
        Assert.Equal(XeroPushOutcome.Blocked, (await kit.AttachmentHandler.PushAsync(QuoteSyncTestKit.TenantId, upload)).Outcome);

        Assert.Equal("DRAFT", kit.Simulator.Find("Quotes", xeroId)!.Status);
        Assert.Empty(kit.OnlyQuote.Attachments);
        Assert.Empty(XeroQuoteLostAnswerTests.OwnWrites(kit));
        kit.AssertNoViolations();
    }
}
