using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Quotations;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;

/// <summary>`v0.24.0` X3 planner: Q8 (quotations from before Xero sync go only on an explicit <em>Send to Xero</em>), the status walk, and the pure mapping helpers.</summary>
public sealed class XeroQuotePlannerTests
{
    private static readonly DateTimeOffset SyncBegan = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AQuotationIssuedBeforeSyncBegan_IsLeftOut_UntilTheProductOwnerSendsItToXero()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions { AutomaticFromUtc = SyncBegan });
        var old = Guid.NewGuid();
        kit.FakeQuotes[old] = QuoteSyncTestKit.Quote(old, QuotationStatus.Accepted, issuedAt: SyncBegan.AddDays(-30));
        kit.Files.Store(old, "R1 sheet");

        Assert.Empty(await kit.PlanAsync(old));
        Assert.Equal(0, await kit.Planner.ScanAsync());

        var sent = await kit.Planner.SendToXeroAsync(old);
        Assert.True(sent.Queued);
        Assert.Equal(4, sent.Entries.Count);
        Assert.Contains(kit.Audit.Rows, r => r.Action == XeroQuotePlanner.AuditSendToXero && r.Detail!["number"] == "P0012-Q-001");

        await kit.DrainAsync();
        Assert.Equal("ACCEPTED", kit.OnlyQuote.Status);

        // Linked now: it stays in scope without asking again.
        Assert.Empty(await kit.PlanAsync(old));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task SendToXero_OnADraftQuotation_IsRefusedWithTheReason()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Draft, revision: 0);

        var refused = await kit.Planner.SendToXeroAsync(id);
        Assert.False(refused.Queued);
        Assert.Contains("Draft", refused.Reason, StringComparison.Ordinal);
        Assert.False((await kit.Planner.SendToXeroAsync(Guid.NewGuid())).Queued);
    }

    [Fact]
    public async Task SendToXero_CountsAsExported_ForAnApprovedQuotationWhosePdfIsNotHeld()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id);

        Assert.Empty(await kit.PlanAsync(id));
        var sent = await kit.Planner.SendToXeroAsync(id);
        Assert.Equal(XeroOperation.PushQuote, Assert.Single(sent.Entries).Operation);
    }

    [Fact]
    public async Task WithNoConfiguredStart_SyncBeginsTheFirstTimeThePlannerIsAsked_AndThatMomentIsKept()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions());
        var began = await kit.Planner.AutomaticFromAsync();
        Assert.Equal(kit.Clock.GetUtcNow(), began);

        kit.Clock.Advance(TimeSpan.FromDays(3));
        var restarted = new XeroQuotePlanner(kit.Quotes, kit.Links, kit.Outbox, kit.Store, kit.Secrets, kit.Files, timeProvider: kit.Clock);
        Assert.Equal(began, await restarted.AutomaticFromAsync());

        var before = Guid.NewGuid();
        kit.FakeQuotes[before] = QuoteSyncTestKit.Quote(before, QuotationStatus.Sent, reference: "Q-OLD", issuedAt: began.AddMinutes(-1));
        var after = Guid.NewGuid();
        kit.FakeQuotes[after] = QuoteSyncTestKit.Quote(after, QuotationStatus.Sent, reference: "Q-NEW", issuedAt: began.AddMinutes(1));
        var unknown = Guid.NewGuid();
        kit.FakeQuotes[unknown] = QuoteSyncTestKit.Quote(unknown, QuotationStatus.Sent, reference: "Q-UNK") with { IssuedAtUtc = null };

        Assert.Empty(await restarted.PlanAsync(before, null));
        Assert.NotEmpty(await restarted.PlanAsync(after, null));
        Assert.Empty(await restarted.PlanAsync(unknown, null));
    }

    [Fact]
    public async Task TheStatusWalk_PlansOnlyTheStepsXeroHasNotReached()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Accepted);
        var hash = XeroQuoteMapper.ContentHash(kit.FakeQuotes[id]);
        var link = new XeroLink(
            XeroLink.CurrentSchemaVersion, QuoteSyncTestKit.TenantId, QuoteSyncTestKit.Ref(id), "q-1", "P0012-Q-001", hash, "SENT",
            null, null, DateTimeOffset.UnixEpoch, null, XeroQuoteMapper.LinkedByCreated);

        var planned = await kit.Planner.PlanAsync(id, link);
        Assert.Equal("ACCEPTED", Assert.Single(planned).Argument);

        // Xero says ACCEPTED, TempestOS says Declined: never ACCEPTED → DECLINED (Xero refuses it); the badge notes it.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Declined);
        Assert.Empty(await kit.Planner.PlanAsync(id, link with { LastKnownXeroStatus = "ACCEPTED" }));
        Assert.Equal(
            "Xero shows this quote as ACCEPTED; TempestOS has it as Declined.",
            XeroQuoteMapper.DriftNote("ACCEPTED", QuotationStatus.Declined));

        // Deleted in Xero: nothing planned.
        Assert.Empty(await kit.Planner.PlanAsync(id, link with { LastKnownXeroStatus = "DELETED" }));
        Assert.Equal("Deleted in Xero.", XeroQuoteMapper.DriftNote("DELETED", QuotationStatus.Sent));
        Assert.Null(XeroQuoteMapper.DriftNote("SENT", QuotationStatus.Sent));
    }

    [Fact]
    public void TheStatusPath_IsDraftSentThenAcceptedOrDeclined()
    {
        Assert.Empty(XeroQuoteMapper.StatusPath(QuotationStatus.Approved));
        Assert.Equal([XeroQuoteWriteStatus.Sent], XeroQuoteMapper.StatusPath(QuotationStatus.Sent));
        Assert.Equal([XeroQuoteWriteStatus.Sent, XeroQuoteWriteStatus.Accepted], XeroQuoteMapper.StatusPath(QuotationStatus.Accepted));
        Assert.Equal([XeroQuoteWriteStatus.Sent, XeroQuoteWriteStatus.Declined], XeroQuoteMapper.StatusPath(QuotationStatus.Declined));
        Assert.Null(XeroQuoteMapper.Rank("INVOICED"));
        Assert.Equal("Q-2026-001.pdf", XeroQuoteMapper.AttachmentFileName("Q-2026/001"));
    }

    [Fact]
    public void TheContentHash_ChangesWithTheRevisionAndLines_AndFitsAnIdempotencyKey()
    {
        var id = Guid.NewGuid();
        var r1 = QuoteSyncTestKit.Quote(id);
        var hash = XeroQuoteMapper.ContentHash(r1);

        Assert.Equal(hash, XeroQuoteMapper.ContentHash(QuoteSyncTestKit.Quote(id)));
        Assert.NotEqual(hash, XeroQuoteMapper.ContentHash(QuoteSyncTestKit.Quote(id, revision: 2)));
        Assert.NotEqual(hash, XeroQuoteMapper.ContentHash(r1 with { Lines = [r1.Lines[0]] }));

        var key = XeroIdempotencyKey.Create(QuoteSyncTestKit.Ref(id), XeroOperation.PushQuote, hash);
        Assert.True(key.Length <= XeroOutboxEntry.MaximumIdempotencyKeyLength);
        Assert.EndsWith(hash, key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoConfiguredStart_AQuotationApprovedAfterStartUp_IsSyncedWhenExportedLater()
    {
        // Default options, as AddXeroQuotes registers the planner: the start is not set in advance.
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions());
        var id = Guid.NewGuid();

        // Approved a little after start-up; its approval commit is planned, but there is no PDF yet.
        kit.Clock.Advance(TimeSpan.FromMinutes(2));
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, issuedAt: kit.Clock.GetUtcNow());
        kit.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(await kit.PlanAsync(id));

        // Exported an hour later.
        kit.Clock.Advance(TimeSpan.FromHours(1));
        kit.Files.Store(id, "R1 sheet");
        Assert.Equal([XeroOperation.PushQuote, XeroOperation.UploadAttachment], (await kit.PlanAsync(id)).Select(e => e.Operation));
        await kit.DrainAsync();
        Assert.Equal("DRAFT", kit.OnlyQuote.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task WithNoConfiguredStart_AQuotationApprovedThenSent_IsSyncedOnItsFirstPlan()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions());
        var id = Guid.NewGuid();
        var approvedAt = kit.Clock.GetUtcNow().AddMinutes(1);

        // The approval is committed, and the first plan this run makes comes a moment later.
        kit.Clock.Advance(TimeSpan.FromMinutes(5));
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Sent, issuedAt: approvedAt);
        Assert.Equal([XeroOperation.PushQuote, XeroOperation.SetQuoteStatus], (await kit.PlanAsync(id)).Select(e => e.Operation));

        // The start is the moment sync started running, kept across restarts; an older quotation still waits for Send to Xero.
        var began = await kit.Planner.AutomaticFromAsync();
        Assert.True(began <= approvedAt);
        kit.Clock.Advance(TimeSpan.FromDays(1));
        var restarted = new XeroQuotePlanner(kit.Quotes, kit.Links, kit.Outbox, kit.Store, kit.Secrets, kit.Files, timeProvider: kit.Clock);
        Assert.Equal(began, await restarted.AutomaticFromAsync());
        var old = Guid.NewGuid();
        kit.FakeQuotes[old] = QuoteSyncTestKit.Quote(old, QuotationStatus.Sent, reference: "Q-OLD", issuedAt: began.AddMinutes(-1));
        Assert.Empty(await restarted.PlanAsync(old, null));
    }

    [Fact]
    public async Task WithNoConfiguredStart_TheStartUpScan_RecordsTheStart_BeforeAnyQuotationIsPlanned()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync(plannerOptions: new XeroQuotePlannerOptions());
        var startedAt = kit.Clock.GetUtcNow();
        Assert.Null(await kit.Store.ReadAsync(XeroQuotePlanner.StateCollection, XeroQuotePlanner.AutomaticFromKey));

        kit.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(0, await kit.Planner.ScanAsync());

        var recorded = await kit.Store.ReadAsync(XeroQuotePlanner.StateCollection, XeroQuotePlanner.AutomaticFromKey);
        Assert.Equal(startedAt, DateTimeOffset.Parse(recorded!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task OnceXeroIsKnownToHoldTheQuotePastDraft_NoContentPushIsPlanned()
    {
        using var kit = await QuoteSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        var r1 = QuoteSyncTestKit.Quote(id);
        var link = new XeroLink(
            XeroLink.CurrentSchemaVersion, QuoteSyncTestKit.TenantId, QuoteSyncTestKit.Ref(id), "q-1", "P0012-Q-001", XeroQuoteMapper.ContentHash(r1), "SENT",
            null, null, DateTimeOffset.UnixEpoch, null, XeroQuoteMapper.LinkedByCreated);

        // R2 approved and exported, but Xero already holds the quote as SENT (by hand): the badge shows the drift; nothing is queued that Xero would refuse.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, revision: 2);
        kit.Files.Store(id, "R2 sheet");
        Assert.DoesNotContain(await kit.Planner.PlanAsync(id, link), p => p.Operation == XeroOperation.PushQuote);
        Assert.Equal("Xero shows this quote as SENT; TempestOS has it as Approved.", XeroQuoteMapper.DriftNote("SENT", QuotationStatus.Approved));

        // Accepted in TempestOS with new content while Xero holds ACCEPTED: nothing at all.
        kit.FakeQuotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Accepted, revision: 2);
        Assert.Empty(await kit.Planner.PlanAsync(id, link with { LastKnownXeroStatus = "ACCEPTED", AttachmentContentHash = kit.Files.Find(id)!.Sha256 }));
    }

    [Fact]
    public void TheRevisionNotSentNote_NamesTheRevision_OnlyWhenXeroHoldsOlderContentPastDraft()
    {
        var id = Guid.NewGuid();
        var r1 = QuoteSyncTestKit.Quote(id);
        var r2 = QuoteSyncTestKit.Quote(id, revision: 2);
        var link = new XeroLink(
            XeroLink.CurrentSchemaVersion, QuoteSyncTestKit.TenantId, QuoteSyncTestKit.Ref(id), "q-1", "P0012-Q-001", XeroQuoteMapper.ContentHash(r1), "SENT",
            null, null, DateTimeOffset.UnixEpoch, null, XeroQuoteMapper.LinkedByCreated);

        Assert.True(XeroQuoteMapper.IsRevisionNotSent(r2, link));
        Assert.Equal(
            "Xero holds quote P0012-Q-001 as SENT, and Xero changes a quote's content only while it is DRAFT, so revision R2 was not sent "
            + "(Q1: the Xero copy follows a new revision only until the quote is sent). Change it in Xero by hand, or unlink it and issue a new quotation.",
            XeroQuoteMapper.DriftNote(r2, link));

        // The same content, still DRAFT, never pushed (a reconciled link) or off the walk: the plain drift notes.
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(r1, link));
        Assert.Equal("Xero shows this quote as SENT; TempestOS has it as Approved.", XeroQuoteMapper.DriftNote(r1, link));
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(r2, link with { LastKnownXeroStatus = "DRAFT" }));
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(r2, link with { LastPushedContentHash = null }));
        Assert.Null(XeroQuoteMapper.DriftNote(r2 with { Status = QuotationStatus.Sent }, link with { LastPushedContentHash = null }));
        Assert.Equal("Deleted in Xero.", XeroQuoteMapper.DriftNote(r2, link with { LastKnownXeroStatus = "DELETED" }));
        Assert.StartsWith("Invoiced in Xero", XeroQuoteMapper.DriftNote(r2, link with { LastKnownXeroStatus = "INVOICED" }), StringComparison.Ordinal);
    }

    [Fact]
    public void TheRevisionNotSentNote_IgnoresAClientOrProjectRenamedOnTheSameRevision()
    {
        var id = Guid.NewGuid();
        var r1 = QuoteSyncTestKit.Quote(id);
        var link = new XeroLink(
            XeroLink.CurrentSchemaVersion, QuoteSyncTestKit.TenantId, QuoteSyncTestKit.Ref(id), "q-1", "P0012-Q-001", XeroQuoteMapper.ContentHash(r1), "SENT",
            null, null, DateTimeOffset.UnixEpoch, null, XeroQuoteMapper.LinkedByCreated);

        // The customer's code, the project's name or the title changed — the same revision, so nothing was "not sent".
        var renamed = r1 with { ClientOrganisationReference = "ACME-2", ProjectSummary = "P0012 Renamed", Title = "Bracket redesign (v2)" };
        Assert.NotEqual(XeroQuoteMapper.ContentHash(r1), XeroQuoteMapper.ContentHash(renamed)); // a DRAFT copy is still brought up to date
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(renamed with { Status = QuotationStatus.Sent }, link));
        Assert.Null(XeroQuoteMapper.DriftNote(renamed with { Status = QuotationStatus.Sent }, link));
        Assert.True(XeroQuoteMapper.IsRevisionNotSent(QuoteSyncTestKit.Quote(id, revision: 2), link));
    }

    [Fact]
    public void AReconciledLink_RecordsTheRevisionXeroNames_SoAnotherRevisionIsNotSent()
    {
        var id = Guid.NewGuid();
        var link = new XeroLink(
            XeroLink.CurrentSchemaVersion, QuoteSyncTestKit.TenantId, QuoteSyncTestKit.Ref(id), "q-1", "P0012-Q-001", XeroQuoteMapper.ReconciledHash(" r1 "), "SENT",
            null, null, DateTimeOffset.UnixEpoch, null, XeroQuoteMapper.LinkedByReconciled);

        Assert.Equal("R1", XeroQuoteMapper.RevisionOf(link.LastPushedContentHash));
        Assert.NotEqual(XeroQuoteMapper.ContentHash(QuoteSyncTestKit.Quote(id)), link.LastPushedContentHash);
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(QuoteSyncTestKit.Quote(id), link));
        Assert.True(XeroQuoteMapper.IsRevisionNotSent(QuoteSyncTestKit.Quote(id, revision: 2), link));
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(QuoteSyncTestKit.Quote(id, revision: 2), link with { LastKnownXeroStatus = "DRAFT" }));

        // A reference naming no revision, or none at all (keyed in by hand): unknown, not flagged.
        Assert.Equal(XeroQuoteMapper.UnknownRevision, XeroQuoteMapper.RevisionToken("PO 4471"));
        Assert.Equal(XeroQuoteMapper.UnknownRevision, XeroQuoteMapper.RevisionToken("R01"));
        Assert.Equal("R0", XeroQuoteMapper.RevisionToken(null));
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(QuoteSyncTestKit.Quote(id, revision: 2), link with { LastPushedContentHash = XeroQuoteMapper.ReconciledHash("PO 4471") }));
        Assert.False(XeroQuoteMapper.IsRevisionNotSent(QuoteSyncTestKit.Quote(id, revision: 2), link with { LastPushedContentHash = XeroQuoteMapper.ReconciledHash(null) }));
    }
}
