using System.Net;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;

/// <summary>
/// `v0.24.0` X3 (D2, Q1; design §4.1) end to end over the simulator: an
/// exported quotation becomes a Xero <c>DRAFT</c> with the same number,
/// lines, codes, expiry, contact and PDF; a new revision updates it while
/// <c>DRAFT</c>; Sent, Accepted and Declined follow; nothing is ever emailed,
/// and Xero is never asked for a transition it refuses.
/// </summary>
public sealed class XeroQuoteSyncTests
{
    [Fact]
    public async Task ExportedQuote_BecomesADraftXeroQuote_WithTheSameNumberLinesCodesExpiryContactAndPdf()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");

        var queued = await kit.PlanAsync(id);
        Assert.Equal([XeroOperation.PushQuote, XeroOperation.UploadAttachment], queued.Select(e => e.Operation));

        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        var quote = kit.OnlyQuote;
        Assert.Equal("DRAFT", quote.Status);
        Assert.Equal("P0012-Q-001", quote.Number);
        Assert.Equal("R1", quote.Body["Reference"]!.GetValue<string>());
        Assert.Equal("Bracket redesign", quote.Body["Title"]!.GetValue<string>());
        Assert.Equal("P0012 Bracket programme", quote.Body["Summary"]!.GetValue<string>());
        Assert.Equal(kit.ContactId, quote.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Equal(new DateOnly(2026, 11, 1), Tempest.Core.Invoicing.Xero.Api.XeroWire.ParseDate(quote.Body["ExpiryDate"]!.GetValue<string>()));
        Assert.Equal("GBP", quote.Body["CurrencyCode"]!.GetValue<string>());

        var lines = quote.Body["LineItems"]!.AsArray();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l =>
        {
            Assert.Equal("OUTPUT2", l!["TaxType"]!.GetValue<string>());
            Assert.Equal("200", l["AccountCode"]!.GetValue<string>());
        });
        Assert.Equal(12m, lines[0]!["Quantity"]!.GetValue<decimal>());
        Assert.Equal(95m, lines[0]!["UnitAmount"]!.GetValue<decimal>());

        var attachment = Assert.Single(quote.Attachments);
        Assert.Equal("P0012-Q-001.pdf", attachment.FileName);

        var link = await kit.LinkAsync(id);
        Assert.NotNull(link);
        Assert.Equal(quote.Id, link.XeroId);
        Assert.Equal("P0012-Q-001", link.XeroNumber);
        Assert.Equal("DRAFT", link.LastKnownXeroStatus);
        Assert.Equal(XeroQuoteMapper.LinkedByCreated, link.LinkedBy);
        Assert.Equal(kit.Files.Find(id)!.Sha256, link.AttachmentContentHash);
        Assert.Contains(kit.Audit.Rows, r => r.Action == XeroQuotePushHandler.AuditLinkCreated);

        // Nothing changed: planning again queues nothing and sends nothing.
        var writes = kit.QuoteWrites.Count;
        Assert.Empty(await kit.PlanAsync(id));
        Assert.Empty(await kit.DrainAsync());
        Assert.Equal(writes, kit.QuoteWrites.Count);
        kit.AssertNoViolations();
    }

    [Theory]
    [InlineData(QuotationStatus.Draft)]
    [InlineData(QuotationStatus.InReview)]
    public async Task ADraftOrInReviewQuotation_PlansNothing(QuotationStatus status)
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, status, revision: status == QuotationStatus.Draft ? 1 : 0);
        kit.Files.Store(id, "an old sheet");
        var requests = kit.Simulator.Requests.Count;

        Assert.Empty(await kit.PlanAsync(id));
        Assert.Empty(await kit.DrainAsync());
        Assert.Equal(requests, kit.Simulator.Requests.Count);
    }

    [Fact]
    public async Task AnApprovedQuotation_IsNotPushedUntilItIsExported()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);

        Assert.Empty(await kit.PlanAsync(id));

        kit.Files.Store(id, "R1 sheet");
        Assert.Equal(2, (await kit.PlanAsync(id)).Count);
    }

    [Fact]
    public async Task TheFullStateSequence_DraftRevisionSentAccepted_FollowsTempestOs_WithStatusOnlyUpdatesAndOnePdf()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();

        // Approved R1, exported.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();
        var quoteId = kit.OnlyQuote.Id;

        // Edited after approval: a draft again — nothing planned.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Draft);
        Assert.Empty(await kit.PlanAsync(id));

        // Approved R2 with a changed line, exported: content and PDF replaced while DRAFT.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, revision: 2, lines:
        [
            new XeroQuoteLine("Concept design", 16m, 95m, VatRate.Standard),
            new XeroQuoteLine("Drawing pack", 1m, 1_200m, VatRate.Standard),
            new XeroQuoteLine("Site visit", 1m, 250m, VatRate.Zero),
        ]);
        kit.Files.Store(id, "R2 sheet");
        Assert.Equal([XeroOperation.PushQuote, XeroOperation.UploadAttachment], (await kit.PlanAsync(id)).Select(e => e.Operation));
        Assert.All(await kit.DrainAsync(), s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        var revised = kit.OnlyQuote;
        Assert.Equal(quoteId, revised.Id);
        Assert.Equal("DRAFT", revised.Status);
        Assert.Equal("R2", revised.Body["Reference"]!.GetValue<string>());
        Assert.Equal(3, revised.Body["LineItems"]!.AsArray().Count);
        Assert.Equal("ZERORATEDOUTPUT", revised.Body["LineItems"]![2]!["TaxType"]!.GetValue<string>());
        var pdf = Assert.Single(revised.Attachments);
        Assert.Equal("R2 sheet".Length, pdf.Length);
        Assert.Contains(kit.QuoteWrites, r => r.Method == HttpMethod.Post && r.Path.EndsWith("/Attachments/P0012-Q-001.pdf", StringComparison.Ordinal));

        // Sent in TempestOS → SENT.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Sent, revision: 2, lines: kit.FakeQuotes[id].Lines);
        Assert.Equal([XeroOperation.SetQuoteStatus], (await kit.PlanAsync(id)).Select(e => e.Operation));
        await kit.DrainAsync();
        Assert.Equal("SENT", kit.OnlyQuote.Status);

        // Accepted in TempestOS → ACCEPTED.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Accepted, revision: 2, lines: kit.FakeQuotes[id].Lines);
        var accepted = await kit.PlanAsync(id);
        Assert.Equal("ACCEPTED", Assert.Single(accepted).Argument);
        await kit.DrainAsync();
        Assert.Equal("ACCEPTED", kit.OnlyQuote.Status);
        Assert.Equal("ACCEPTED", (await kit.LinkAsync(id))!.LastKnownXeroStatus);

        // Every status change carried no lines (status-only, §4.1), and nothing more is planned.
        var statusWrites = kit.QuoteWrites.Where(r => r.JsonBody?["Quotes"]?[0]?["Status"] is not null && r.Method == HttpMethod.Post).ToList();
        Assert.Equal(2, statusWrites.Count);
        Assert.All(statusWrites, r => Assert.Null(r.JsonBody!["Quotes"]![0]!["LineItems"]));
        Assert.Empty(await kit.PlanAsync(id));
        Assert.Single(kit.LiveQuotes);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task DeclinedInTempestOs_SetsDeclined()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Sent);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();
        Assert.Equal("SENT", kit.OnlyQuote.Status);

        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Declined);
        await kit.PlanAsync(id);
        await kit.DrainAsync();

        Assert.Equal("DECLINED", kit.OnlyQuote.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AQuotationAcceptedWhileOffline_IsCreatedThenWalkedDraftSentAccepted_InOrder()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Accepted);
        kit.Files.Store(id, "R1 sheet");

        var queued = await kit.PlanAsync(id);
        Assert.Equal(
            [XeroOperation.PushQuote, XeroOperation.UploadAttachment, XeroOperation.SetQuoteStatus, XeroOperation.SetQuoteStatus],
            queued.Select(e => e.Operation));
        Assert.Equal(["SENT", "ACCEPTED"], queued.Where(e => e.Operation == XeroOperation.SetQuoteStatus).Select(e => e.Argument!));

        var steps = await kit.DrainAsync();
        Assert.Equal(queued.Select(e => e.Id), steps.Select(s => s.Entry.Id));
        Assert.Equal("ACCEPTED", kit.OnlyQuote.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ANewRevision_OnceXeroHoldsTheQuoteAsSent_IsRefusedWithAClearReason_AndNothingIsWritten()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();
        var quoteId = kit.OnlyQuote.Id;

        // Someone marks it sent in Xero by hand; TempestOS then approves R2.
        await kit.SetStatusInXeroAsync(quoteId, "SENT");
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, revision: 2, lines: [new XeroQuoteLine("Concept design", 20m, 95m, VatRate.Standard)]);
        kit.Files.Store(id, "R2 sheet");
        await kit.PlanAsync(id);

        var writes = kit.QuoteWrites.Count;
        var steps = await kit.DrainAsync();

        var push = steps.Single(s => s.Entry.Operation == XeroOperation.PushQuote);
        Assert.Equal(XeroPushOutcome.Rejected, push.Result.Outcome);
        Assert.Contains("as SENT", push.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("only while it is DRAFT", push.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("R2", push.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(writes, kit.QuoteWrites.Count); // no content write, no upload (it waits behind the refusal)
        Assert.Equal("R1", kit.OnlyQuote.Body["Reference"]!.GetValue<string>());
        Assert.Equal("SENT", (await kit.LinkAsync(id))!.LastKnownXeroStatus);

        var failed = (await kit.Outbox.ListForDocumentAsync(QuoteSyncTestKit.Ref(id))).Single(e => e.Operation == XeroOperation.PushQuote && e.State == XeroOutboxState.Failed);
        Assert.Equal(push.Result.Reason, failed.LastError);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateResponse_IsReconciledByNumber_AndMakesExactlyOneQuote()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        kit.Lost.LoseWrites = 1;
        var first = await kit.DrainAsync();
        var lost = Assert.Single(first);
        Assert.Equal(XeroPushOutcome.RetryLater, lost.Result.Outcome);
        Assert.Single(kit.LiveQuotes); // Xero committed it
        Assert.Null(await kit.LinkAsync(id));

        kit.Clock.Advance(TimeSpan.FromSeconds(31));
        var second = await kit.DrainAsync();
        Assert.All(second, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        Assert.Single(kit.LiveQuotes);
        Assert.Single(kit.QuoteWrites, r => r.Method == HttpMethod.Put && r.Path == "Quotes");
        var link = await kit.LinkAsync(id);
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, link!.LinkedBy);
        Assert.NotNull(link.LastPushedContentHash);
        Assert.Contains(kit.Audit.Rows, r => r.Action == XeroQuotePushHandler.AuditLinkReconciled);
        Assert.Single(kit.OnlyQuote.Attachments);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ACrashMidCreate_LeavesTheEntryUnknown_AndTheNextDrainLinksTheQuoteXeroMade()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        // The create reaches Xero, then TempestOS stops before recording anything.
        var claimed = await kit.Outbox.ClaimNextDueAsync();
        kit.Lost.LoseWrites = 1;
        await kit.PushHandler.PushAsync(QuoteSyncTestKit.TenantId, claimed!);
        Assert.Equal(1, await kit.Outbox.RecoverInFlightAsync());
        Assert.Equal(XeroOutboxState.Unknown, (await kit.Outbox.FindAsync(claimed!.Id))!.State);

        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));
        Assert.Single(kit.LiveQuotes);
        Assert.Single(kit.QuoteWrites, r => r.Method == HttpMethod.Put && r.Path == "Quotes");
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, (await kit.LinkAsync(id))!.LinkedBy);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostStatusResponse_IsRecordedFromXero_WithoutASecondWrite()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();

        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Sent);
        await kit.PlanAsync(id);
        kit.Lost.LoseWrites = 1;
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal("SENT", kit.OnlyQuote.Status);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var retry = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Succeeded, retry.Result.Outcome);
        Assert.Equal("SENT", (await kit.LinkAsync(id))!.LastKnownXeroStatus);
        Assert.Single(kit.QuoteWrites, r => r.JsonBody?["Quotes"]?[0]?["Status"]?.GetValue<string>() == "SENT");
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task A429_PausesTheDrainForRetryAfter_ThenTheQuoteGoesThrough()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        kit.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, PathContains: "Quotes", RetryAfter: TimeSpan.FromSeconds(20)));
        var paused = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.RetryLater, paused.Result.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(20), paused.Result.RetryAfter);

        var waiting = (await kit.Outbox.ListForDocumentAsync(QuoteSyncTestKit.Ref(id)))[0];
        Assert.Equal(XeroOutboxState.Pending, waiting.State);
        Assert.Equal(kit.Clock.GetUtcNow() + TimeSpan.FromSeconds(20), waiting.NotBeforeUtc);

        kit.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Empty(await kit.DrainAsync());

        kit.Clock.Advance(TimeSpan.FromSeconds(11));
        Assert.All(await kit.DrainAsync(), s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));
        Assert.Equal("DRAFT", kit.OnlyQuote.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ARevokedToken_WaitsForReauthorisation_ThenResumes()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Sent);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        kit.Simulator.Inject(new XeroFault(XeroFaultKind.Unauthorised));
        var stopped = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Reauthorise, stopped.Result.Outcome);
        Assert.Contains(await kit.Outbox.ListForDocumentAsync(QuoteSyncTestKit.Ref(id)), e => e.State == XeroOutboxState.WaitingForAuthorisation);
        Assert.Empty(kit.LiveQuotes);
        Assert.Empty(await kit.DrainAsync()); // waits — nothing claimable

        Assert.Equal(1, await kit.Outbox.ResumeAfterAuthorisationAsync());
        Assert.Equal(3, (await kit.DrainAsync()).Count);
        Assert.Equal("SENT", kit.OnlyQuote.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task A503_IsRetriedAfterBackoff()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, PathContains: "Quotes"));
        var failed = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.RetryLater, failed.Result.Outcome);
        Assert.Null(failed.Result.RetryAfter);

        kit.Clock.Advance(TimeSpan.FromSeconds(31));
        await kit.DrainAsync();
        Assert.Equal("DRAFT", kit.OnlyQuote.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnUnlinkedClient_IsBlockedWithTheReason_UntilLinked()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, client: "OTHER1");
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        var blocked = (await kit.DrainAsync())[0];
        Assert.Equal(XeroPushOutcome.Blocked, blocked.Result.Outcome);
        Assert.Contains("'OTHER1' is not linked to a Xero contact", blocked.Result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.QuoteWrites);
        Assert.Empty(kit.LiveQuotes);

        // The client is now ACME1's contact; the engine's automatic retry (X6) sends it.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        var entries = await kit.PlanAsync(id);
        Assert.Equal(XeroOutboxState.Pending, entries[0].State);
        await kit.DrainAsync();
        Assert.Equal("DRAFT", kit.OnlyQuote.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AMissingSalesAccount_IsBlockedWithX1sReason()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        kit.Settings.Cached = QuoteSyncTestKit.UkDemoReading() with { Accounts = [] };
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        var blocked = (await kit.DrainAsync())[0];
        Assert.Equal(XeroPushOutcome.Blocked, blocked.Result.Outcome);
        Assert.Contains("Xero has no account 200", blocked.Result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.QuoteWrites);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ANumberAlreadyUsedInXeroByAnotherContact_IsRefused_NeverDuplicated()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var otherContact = kit.Simulator.SeedContact("Someone Else Ltd");
        {
            // Entered by hand in Xero, on the same simulator.
            using var client = kit.Simulator.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Put, "Quotes?summarizeErrors=true")
            {
                Content = new StringContent(SimulatorTestKit.Quote(otherContact, "P0012-Q-001").ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", kit.Simulator.Options.AccessToken);
            request.Headers.Add("xero-tenant-id", kit.Simulator.Options.TenantId);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("Idempotency-Key", "by-hand:quote");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        var refused = (await kit.DrainAsync())[0];
        Assert.Equal(XeroPushOutcome.Rejected, refused.Result.Outcome);
        Assert.Contains("already used in Xero", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Single(kit.LiveQuotes);
        Assert.Null(await kit.LinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AQuoteInvoicedInXero_IsNeverMovedByTempestOs_AndTheDriftIsReported()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Sent);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();
        var quoteId = kit.OnlyQuote.Id;

        // In Xero, by hand: accepted, then turned into an invoice.
        await kit.SetStatusInXeroAsync(quoteId, "ACCEPTED");
        kit.Simulator.ConvertQuoteToInvoiceInXero(quoteId);

        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Accepted);
        await kit.PlanAsync(id);
        var writes = kit.QuoteWrites.Count;
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("invoiced in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(writes, kit.QuoteWrites.Count);
        Assert.Equal("INVOICED", kit.OnlyQuote.Status);

        var link = await kit.LinkAsync(id);
        Assert.Equal("INVOICED", link!.LastKnownXeroStatus);
        Assert.Equal(
            "Invoiced in Xero — raising it from TempestOS too would bill twice.",
            XeroQuoteMapper.DriftNote(link.LastKnownXeroStatus, QuotationStatus.Accepted));
        Assert.Empty(await kit.Planner.PlanAsync(id, link));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ARevisionApprovedBeforeTheFirstPushIsSent_SupersedesIt_SoOnlyTheLatestIsCreated()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, revision: 2, lines: [new XeroQuoteLine("Concept design", 30m, 95m, VatRate.Standard)]);
        kit.Files.Store(id, "R2 sheet");
        await kit.PlanAsync(id);

        await kit.DrainAsync();
        Assert.Single(kit.QuoteWrites, r => r.Method == HttpMethod.Put && r.Path == "Quotes");
        Assert.Equal("R2", kit.OnlyQuote.Body["Reference"]!.GetValue<string>());
        Assert.Equal("R2 sheet".Length, Assert.Single(kit.OnlyQuote.Attachments).Length);
        Assert.Contains(await kit.Outbox.ListForDocumentAsync(QuoteSyncTestKit.Ref(id)), e => e.State == XeroOutboxState.Superseded);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnEntryForContentThatChangedSinceItWasQueued_SendsNothing()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        // Changed after queueing, not yet re-planned: the old key must not carry a new body.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, revision: 2);
        var step = (await kit.DrainAsync())[0];
        Assert.Equal(XeroPushOutcome.NothingToDo, step.Result.Outcome);
        Assert.Empty(kit.QuoteWrites);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ThePdf_IsUploadedOncePerContent_AndReplacedByNameWhenItChanges()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Sent);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();

        // The same bytes stored again: nothing to upload.
        kit.Files.Store(id, "R1 sheet", "P0012-Q-001-R1-quote.pdf");
        Assert.Empty(await kit.PlanAsync(id));

        kit.Files.Store(id, "R1 sheet, as sent");
        Assert.Equal(XeroOperation.UploadAttachment, Assert.Single(await kit.PlanAsync(id)).Operation);
        await kit.DrainAsync();

        var uploads = kit.QuoteWrites.Where(r => r.Path.Contains("/Attachments/", StringComparison.Ordinal)).ToList();
        Assert.Equal([HttpMethod.Put, HttpMethod.Post], uploads.Select(u => u.Method));
        var pdf = Assert.Single(kit.OnlyQuote.Attachments);
        Assert.Equal("R1 sheet, as sent".Length, pdf.Length);
        Assert.Equal(kit.Files.Find(id)!.Sha256, (await kit.LinkAsync(id))!.AttachmentContentHash);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task TheLiveOrganisation_RefusesEveryWrite_D7()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        kit.Settings.Cached = QuoteSyncTestKit.UkDemoReading() with
        {
            Organisation = QuoteSyncTestKit.UkDemoReading().Organisation with { IsDemoCompany = false },
        };
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);

        var refused = (await kit.DrainAsync())[0];
        Assert.Equal(XeroPushOutcome.Rejected, refused.Result.Outcome);
        Assert.Contains("TempestOS blocked the request", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.QuoteWrites);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task ARevisionRefusedBecauseXeroHoldsSent_DoesNotStrandTheLaterSentAndAccepted()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();
        await kit.SetStatusInXeroAsync(kit.OnlyQuote.Id, "SENT");

        // R2 approved and exported: refused (Xero holds SENT), with the reason.
        var r2 = QuoteSyncTestKit.Quote(id, revision: 2, lines: [new XeroQuoteLine("Concept design", 20m, 95m, VatRate.Standard)]);
        kit.FakeQuotes[id] = r2;
        kit.Files.Store(id, "R2 sheet");
        await kit.PlanAsync(id);
        Assert.Contains(await kit.DrainAsync(), s => s.Entry.Operation == XeroOperation.PushQuote && s.Result.Outcome == XeroPushOutcome.Rejected);

        // Sent, then Accepted, in TempestOS: the refusal no longer holds the queue.
        kit.FakeQuotes[id] = r2 with { Status = QuotationStatus.Sent };
        await kit.PlanAsync(id);
        var released = await kit.DrainAsync();
        Assert.Contains(released, s => s.Entry.Operation == XeroOperation.PushQuote && s.Result.Outcome == XeroPushOutcome.NothingToDo);

        kit.FakeQuotes[id] = r2 with { Status = QuotationStatus.Accepted };
        await kit.PlanAsync(id);
        await kit.DrainAsync();

        Assert.Equal("ACCEPTED", kit.OnlyQuote.Status);
        Assert.Equal("R1", kit.OnlyQuote.Body["Reference"]!.GetValue<string>()); // the content Xero refused is never forced in
        Assert.DoesNotContain(await kit.Outbox.ListForDocumentAsync(QuoteSyncTestKit.Ref(id)), e => e.State == XeroOutboxState.Failed);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task SendToXero_OnAnAcceptedQuotationKeyedIntoXeroByHand_LinksIt_WithoutARefusal()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions { AutomaticFromUtc = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) });
        var xeroId = await kit.CreateQuoteInXeroByHandAsync(kit.ContactId, "P0012-Q-001", "SENT", "ACCEPTED");
        var writes = kit.QuoteWrites.Count;

        // An older quotation (Q8), accepted in TempestOS; its Xero copy is already ACCEPTED.
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Accepted, issuedAt: new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));
        Assert.True((await kit.Planner.SendToXeroAsync(id)).Queued);

        var steps = await kit.DrainAsync();
        Assert.DoesNotContain(steps, s => s.Result.Outcome is XeroPushOutcome.Rejected or XeroPushOutcome.Blocked);
        var link = await kit.LinkAsync(id);
        Assert.Equal(xeroId, link!.XeroId);
        Assert.Equal(XeroQuoteMapper.LinkedByReconciled, link.LinkedBy);
        Assert.Equal("ACCEPTED", link.LastKnownXeroStatus);
        Assert.Equal(writes, kit.QuoteWrites.Count); // nothing written: the copy already matches
        Assert.Empty(await kit.PlanAsync(id));
        Assert.All(await kit.Outbox.ListForDocumentAsync(QuoteSyncTestKit.Ref(id)), e => Assert.Equal(XeroOutboxState.Succeeded, e.State));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALinkedNewRevision_IsPushedWhenExported_NotWhenApproved()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();

        // R2 approved: the R1 PDF is still the one held, so nothing goes yet (Xero keeps R1's lines with R1's PDF).
        var r2 = QuoteSyncTestKit.Quote(id, revision: 2, lines: [new XeroQuoteLine("Concept design", 20m, 95m, VatRate.Standard)]);
        kit.FakeQuotes[id] = r2;
        Assert.Empty(await kit.PlanAsync(id));

        // R2 exported: its content and its PDF go together.
        kit.Files.Store(id, "R2 sheet");
        Assert.Equal([XeroOperation.PushQuote, XeroOperation.UploadAttachment], (await kit.PlanAsync(id)).Select(e => e.Operation));
        await kit.DrainAsync();
        Assert.Equal("R2", kit.OnlyQuote.Body["Reference"]!.GetValue<string>());
        Assert.Equal("R2 sheet".Length, Assert.Single(kit.OnlyQuote.Attachments).Length);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALinkedNewRevision_SentWithoutAnExport_IsPushedBeforeItIsMarkedSent()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);
        kit.Files.Store(id, "R1 sheet");
        await kit.PlanAsync(id);
        await kit.DrainAsync();

        var r2 = QuoteSyncTestKit.Quote(id, revision: 2, lines: [new XeroQuoteLine("Concept design", 20m, 95m, VatRate.Standard)]);
        kit.FakeQuotes[id] = r2;
        Assert.Empty(await kit.PlanAsync(id));

        // "Approved Rn, then Export or Send": Send issues R2 too.
        kit.FakeQuotes[id] = r2 with { Status = QuotationStatus.Sent };
        Assert.Equal([XeroOperation.PushQuote, XeroOperation.SetQuoteStatus], (await kit.PlanAsync(id)).Select(e => e.Operation));
        Assert.All(await kit.DrainAsync(), s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));
        Assert.Equal("SENT", kit.OnlyQuote.Status);
        Assert.Equal("R2", kit.OnlyQuote.Body["Reference"]!.GetValue<string>());
        kit.AssertNoViolations();
    }
}
