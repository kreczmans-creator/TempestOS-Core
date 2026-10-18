using Tempest.Core.Audit;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` X6 pacing and isolation: the background loop waits its whole
/// sync interval when there is nothing it can do (no organisation connected,
/// writes waiting for re-authorisation) instead of waking every second; a 429
/// pauses everything — persisted and audited — whether or not it carried a
/// <c>Retry-After</c> and whichever handler met it; recovery at start-up
/// comes before the settings refresh even when a re-authorisation is seen
/// then; an entry inconclusive three times gives up; a failing audit recorder
/// never aborts a read-back pass. Also pins the outbox's state-filtered claim
/// the engine's recovery-first ordering relies on.
/// </summary>
public sealed class XeroSyncPacingTests
{
    private const string PausedUntilKey = "paused-until";

    // ------------------------------------------------------------ loop pacing

    [Fact]
    public async Task NotConnected_NothingIsDue_AndTheHostedLoopWaitsItsSyncInterval()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Secrets.RemoveAsync(XeroContactLinker.TenantIdSecretKey);
        kit.IssueOrder();
        await kit.Engine.RunCycleAsync();
        kit.Clock.Advance(kit.Options.ReadBackInterval + TimeSpan.FromMinutes(1));
        await kit.Engine.RunCycleAsync();

        // A queued write and a read-back past due, but neither can run with no organisation.
        Assert.Null(await kit.Engine.NextWorkDueAtAsync());

        var cycles = await CountLoopCyclesAsync(kit.Engine, TimeSpan.FromSeconds(4.5));
        Assert.True(cycles <= 2, $"{cycles} loop cycles in 4.5 s while not connected (sync interval {kit.Options.SyncInterval}).");
        Assert.Empty(kit.Requests);
    }

    [Fact]
    public async Task WaitingForAuthorisation_NothingIsDue_AndTheGrantIsNotReadEverySecond()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();
        kit.IssueOrder();
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.Unauthorised, "PurchaseOrders"));
        Assert.True((await kit.Engine.RunCycleAsync()).Drain.PausedForAuthorisation);
        kit.Clock.Advance(kit.Options.ReadBackInterval + TimeSpan.FromMinutes(1));
        await kit.Engine.RunCycleAsync();

        Assert.Null(await kit.Engine.NextWorkDueAtAsync());

        var reads = kit.Connection.Reads;
        var cycles = await CountLoopCyclesAsync(kit.Engine, TimeSpan.FromSeconds(4.5));
        Assert.True(cycles <= 2, $"{cycles} loop cycles in 4.5 s while waiting for authorisation.");
        Assert.True(kit.Connection.Reads - reads <= 2, $"the grant was read {kit.Connection.Reads - reads} times in 4.5 s (each read may refresh the token at Xero).");
    }

    [Fact]
    public async Task AReadBackPastDue_IsHeldBackToTheEndOfA429Pause()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();
        kit.Clock.Advance(kit.Options.ReadBackInterval + TimeSpan.FromMinutes(1));
        kit.IssueOrder();
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, "PurchaseOrders", RetryAfter: TimeSpan.FromSeconds(40)));
        var report = await kit.Engine.RunCycleAsync();
        var until = Assert.NotNull(report.Drain.ResumeNotBeforeUtc);
        Assert.Null(report.ReadBack);

        var due = await kit.Engine.NextWorkDueAtAsync();
        Assert.True(due >= until, $"due {due:O} before the pause ends at {until:O}");
    }

    // ------------------------------------------------------------ 429 pauses all

    [Fact]
    public async Task A429WithNoRetryAfter_PausesEverything_For60Seconds_PersistedAndAudited()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();
        kit.IssueOrder("PO-2026-001");
        kit.IssueOrder("PO-2026-002");
        kit.Hop.StripRetryAfter = true;
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, "PurchaseOrders"));

        var start = kit.Clock.GetUtcNow();
        var report = await kit.Engine.RunCycleAsync();

        var expected = start + XeroBackoff.DefaultRateLimitPause;
        Assert.Equal(1, report.Drain.Attempted);
        Assert.Equal(expected, report.Drain.ResumeNotBeforeUtc);
        Assert.Equal(expected, await ReadPausedUntilAsync(kit.Store));
        var row = Assert.Single(kit.AuditRows(XeroSyncService.AuditRateLimited));
        Assert.Equal(expected.ToString("O", System.Globalization.CultureInfo.InvariantCulture), row!["pausedUntilUtc"]);
        Assert.Empty(kit.AuditRows(XeroSyncService.AuditDeferred));

        // TempestOS restarts with no memory of the limiter's pause: the persisted one holds.
        var restarted = new XeroSyncService(
            kit.Parts, kit.Outbox, kit.Store, kit.Reader, kit.Connection, rateLimiter: null, kit.ReadBack, observer: null, importer: null, kit.Audit,
            timeProvider: kit.Clock, options: kit.Options);
        kit.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(0, (await restarted.RunCycleAsync()).Drain.Attempted);

        kit.Clock.Advance(TimeSpan.FromSeconds(31));
        kit.Hop.StripRetryAfter = false;
        for (var i = 0; i < 10; i++)
            await restarted.RunCycleAsync();

        Assert.Equal(2, kit.LiveOrders.Count);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task A429OnAnInvoiceWrite_PausesEverything_ThoughTheInvoiceHandlerPassesNoRetryAfter()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var handler = new InvoiceCreateThroughX4Mapping(kit.Api);
        var parts = new XeroSyncParts(kit.Links, kit.Outbox, kit.Secrets, [], [(XeroDocumentKind.Invoice, (IXeroPushHandler)handler)]);
        var engine = new XeroSyncService(
            parts, kit.Outbox, kit.Store, rateLimiter: kit.RateLimiter, audit: kit.Audit, timeProvider: kit.Clock, options: kit.Options);
        var first = XeroDocumentRef.For(XeroDocumentKind.Invoice, Guid.NewGuid());
        var second = XeroDocumentRef.For(XeroDocumentKind.Invoice, Guid.NewGuid());
        await kit.Outbox.EnqueueAsync(XeroOperation.PushInvoiceDraft, first, "h1", null);
        await kit.Outbox.EnqueueAsync(XeroOperation.PushInvoiceDraft, second, "h2", null);
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, "Invoices", RetryAfter: TimeSpan.FromSeconds(40)));

        var start = kit.Clock.GetUtcNow();
        var report = await engine.RunCycleAsync();

        // Xero's Retry-After + 1 s, as the limiter saw it — not a transport backoff of this entry alone.
        var expected = start + TimeSpan.FromSeconds(41);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, report.Drain.Attempted);
        Assert.Equal(expected, report.Drain.ResumeNotBeforeUtc);
        Assert.Equal(expected, await ReadPausedUntilAsync(kit.Store));
        Assert.Single(kit.AuditRows(XeroSyncService.AuditRateLimited));
        Assert.Empty(kit.AuditRows(XeroSyncService.AuditDeferred));
        var entry = Assert.Single(await kit.Outbox.ListForDocumentAsync(first));
        Assert.Equal(expected, entry.NotBeforeUtc);
        Assert.Equal(XeroOutboxState.Pending, Assert.Single(await kit.Outbox.ListForDocumentAsync(second)).State);

        // Restarted without the limiter's memory: still paused until Xero said.
        var restarted = new XeroSyncService(parts, kit.Outbox, kit.Store, timeProvider: kit.Clock, options: kit.Options);
        kit.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(0, (await restarted.RunCycleAsync()).Drain.Attempted);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task A5xxWhileTheMinuteIsNearlySpent_IsBackedOffAsA5xx_NotAuditedAsA429()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();
        kit.IssueOrder("PO-2026-001");
        kit.Hop.MinuteRemaining = 1;
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, "PurchaseOrders"));

        var start = kit.Clock.GetUtcNow();
        var report = await kit.Engine.RunCycleAsync();

        // The limiter's own pause to the end of the minute still holds every call (§6.5) ...
        Assert.Equal(1, report.Drain.Attempted);
        Assert.NotNull(kit.RateLimiter.PausedUntilUtc);
        Assert.Equal(kit.RateLimiter.PausedUntilUtc, report.Drain.ResumeNotBeforeUtc);

        // ... but the 503 is a 503: deferred with its own backoff, no 429 pause saved or audited.
        Assert.Empty(kit.AuditRows(XeroSyncService.AuditRateLimited));
        Assert.Single(kit.AuditRows(XeroSyncService.AuditDeferred));
        Assert.Null(await ReadPausedUntilAsync(kit.Store));
        var entry = Assert.Single(await kit.Outbox.ListAsync([XeroOutboxState.Pending, XeroOutboxState.Unknown]), e => e.LastError is not null);
        Assert.NotNull(entry.NotBeforeUtc);
        Assert.NotEqual(kit.RateLimiter.PausedUntilUtc, entry.NotBeforeUtc);
        Assert.Contains("503", entry.LastError, StringComparison.Ordinal);
        Assert.True(entry.NotBeforeUtc > start);
    }

    [Fact]
    public async Task A429OnAnInvoiceWrite_WhileTheMinuteIsNearlySpent_StillPausesEverythingForRetryAfter()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var handler = new InvoiceCreateThroughX4Mapping(kit.Api);
        var parts = new XeroSyncParts(kit.Links, kit.Outbox, kit.Secrets, [], [(XeroDocumentKind.Invoice, (IXeroPushHandler)handler)]);
        var engine = new XeroSyncService(
            parts, kit.Outbox, kit.Store, rateLimiter: kit.RateLimiter, audit: kit.Audit, timeProvider: kit.Clock, options: kit.Options);
        await kit.Outbox.EnqueueAsync(XeroOperation.PushInvoiceDraft, XeroDocumentRef.For(XeroDocumentKind.Invoice, Guid.NewGuid()), "h1", null);
        kit.Hop.MinuteRemaining = 0;
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, "Invoices", RetryAfter: TimeSpan.FromSeconds(90)));

        var start = kit.Clock.GetUtcNow();
        var report = await engine.RunCycleAsync();

        // Longer than the nearly-spent-minute rule can pause: Xero's Retry-After + 1 s, pause all.
        var expected = start + TimeSpan.FromSeconds(91);
        Assert.Equal(expected, report.Drain.ResumeNotBeforeUtc);
        Assert.Equal(expected, await ReadPausedUntilAsync(kit.Store));
        Assert.Single(kit.AuditRows(XeroSyncService.AuditRateLimited));
        Assert.Empty(kit.AuditRows(XeroSyncService.AuditDeferred));
    }

    // Backlog X6-1: a 429 whose Retry-After (+1 s) fits inside the nearly-spent-minute rule's pause was taken for
    // that rule alone, so it was backed off as a 5xx and its pause was not persisted across a restart.
    [Fact]
    public async Task A429OnAnInvoiceWrite_WithAShortRetryAfter_WhileTheMinuteIsNearlySpent_IsAPersistedPause()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var handler = new InvoiceCreateThroughX4Mapping(kit.Api);
        var parts = new XeroSyncParts(kit.Links, kit.Outbox, kit.Secrets, [], [(XeroDocumentKind.Invoice, (IXeroPushHandler)handler)]);
        var engine = new XeroSyncService(
            parts, kit.Outbox, kit.Store, rateLimiter: kit.RateLimiter, audit: kit.Audit, timeProvider: kit.Clock, options: kit.Options);
        await kit.Outbox.EnqueueAsync(XeroOperation.PushInvoiceDraft, XeroDocumentRef.For(XeroDocumentKind.Invoice, Guid.NewGuid()), "h1", null);
        kit.Hop.MinuteRemaining = 0;
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, "Invoices", RetryAfter: TimeSpan.FromSeconds(30)));

        var start = kit.Clock.GetUtcNow();
        var report = await engine.RunCycleAsync();

        Assert.True(kit.RateLimiter.PauseCameFromTooManyRequests);
        var expected = start + TimeSpan.FromSeconds(31);
        Assert.Equal(expected, report.Drain.ResumeNotBeforeUtc);
        Assert.Equal(expected, await ReadPausedUntilAsync(kit.Store));
        Assert.Single(kit.AuditRows(XeroSyncService.AuditRateLimited));
        Assert.Empty(kit.AuditRows(XeroSyncService.AuditDeferred));

        // Restarted without the limiter's memory: still paused until Xero said.
        var restarted = new XeroSyncService(parts, kit.Outbox, kit.Store, timeProvider: kit.Clock, options: kit.Options);
        kit.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(0, (await restarted.RunCycleAsync()).Drain.Attempted);
        Assert.Equal(1, handler.Calls);
        Assert.Empty(kit.Simulator.Violations);
    }

    // ------------------------------------------------------------ recovery first

    [Fact]
    public async Task AtStartUp_LostAnswersAreRecoveredBeforeTheSettingsRefresh_EvenWhenARe_AuthorisationIsSeen()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var handler = new SnapshotHandler(kit);
        var parts = new XeroSyncParts(kit.Links, kit.Outbox, kit.Secrets, [], [(XeroDocumentKind.Quote, (IXeroPushHandler)handler)]);

        // A write paused for re-authorisation, and one cut off mid-request (its answer lost).
        var waiting = await kit.Outbox.EnqueueAsync(XeroOperation.PushQuote, EngineTestKit.QuoteRef(Guid.NewGuid()), "w", null);
        Assert.Equal(waiting.Id, (await kit.Outbox.ClaimNextDueAsync())!.Id);
        await kit.Outbox.RecordOutcomeAsync(waiting.Id, XeroOutboxState.WaitingForAuthorisation, "401");
        var lost = await kit.Outbox.EnqueueAsync(XeroOperation.PushQuote, EngineTestKit.QuoteRef(Guid.NewGuid()), "u", null);
        Assert.Equal(lost.Id, (await kit.Outbox.ClaimNextDueAsync())!.Id);

        // The grant now reads as authorised: the new process resumes the waiting write.
        var engine = new XeroSyncService(parts, kit.Outbox, kit.Store, kit.Reader, kit.Connection, timeProvider: kit.Clock, options: kit.Options);
        await engine.StartAsync();

        var firstCall = Assert.Single(handler.Calls);
        Assert.Equal(lost.Id, firstCall.EntryId);
        Assert.DoesNotContain(firstCall.RequestsBefore, p => p is "Organisation" or "TaxRates" or "Accounts");
        Assert.Equal(XeroOutboxState.Pending, (await kit.Outbox.FindAsync(waiting.Id))!.State);

        // The settings are read once recovery is done, before the session's first write.
        await engine.RunCycleAsync();
        Assert.Equal(2, handler.Calls.Count);
        Assert.Contains("Organisation", handler.Calls[1].RequestsBefore);
    }

    // ------------------------------------------------------------ inconclusive

    [Fact]
    public async Task AnEntryInconclusiveThreeTimes_IsFailed_CheckXeroThenRetryOrUnlink_WithRetryOffered()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var handler = new AlwaysUnknownHandler();
        var parts = new XeroSyncParts(kit.Links, kit.Outbox, kit.Secrets, [], [(XeroDocumentKind.Quote, (IXeroPushHandler)handler)]);
        var engine = new XeroSyncService(parts, kit.Outbox, kit.Store, audit: kit.Audit, timeProvider: kit.Clock, options: kit.Options);
        var document = EngineTestKit.QuoteRef(Guid.NewGuid());
        await kit.Outbox.EnqueueAsync(XeroOperation.PushQuote, document, "h1", null);

        for (var i = 0; i < 10; i++)
        {
            await engine.RunCycleAsync();
            kit.Clock.Advance(TimeSpan.FromMinutes(2));
        }

        Assert.Equal(kit.Options.MaximumUnknownAnswers, handler.Calls);
        var entry = Assert.Single(await kit.Outbox.ListForDocumentAsync(document));
        Assert.Equal(XeroOutboxState.Failed, entry.State);
        Assert.StartsWith(XeroSyncService.InconclusiveReason, entry.LastError, StringComparison.Ordinal);
        Assert.Contains("garbled", entry.LastError, StringComparison.Ordinal);
        Assert.Single(kit.AuditRows(XeroSyncService.AuditFailed));

        var badge = await engine.GetDocumentStatusAsync(document);
        Assert.Equal(XeroSyncBadge.Failed, badge.Status.Badge);
        Assert.True(badge.CanRetry);
        Assert.Equal(entry.Id, badge.Status.RetryableEntryId);

        // Retry starts the count again.
        Assert.True(await engine.RetryAsync(entry.Id));
        await engine.RunCycleAsync();
        Assert.Equal(kit.Options.MaximumUnknownAnswers + 1, handler.Calls);
        Assert.Equal(XeroOutboxState.Unknown, Assert.Single(await kit.Outbox.ListForDocumentAsync(document)).State);
    }

    // ------------------------------------------------------------ read-back audit

    [Fact]
    public async Task AFailingAuditRecorder_DoesNotAbortTheReadBackPass()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var first = kit.IssueOrder("PO-2026-001");
        var second = kit.IssueOrder("PO-2026-002");
        await kit.SettleAsync();
        foreach (var order in kit.LiveOrders)
            kit.Simulator.ApproveInXero(order.Id);

        var readBack = new XeroReadBack(kit.Api, kit.Links, null, kit.RateLimiter, new ThrowingAuditRecorder(), kit.Clock);
        var report = await readBack.ReadAsync(EngineTestKit.TenantId, 25);

        Assert.False(report.Stopped);
        Assert.Equal(2, report.Changed.Count);
        Assert.Equal("AUTHORISED", (await kit.LinkAsync(EngineTestKit.OrderRef(first)))!.LastKnownXeroStatus);
        Assert.Equal("AUTHORISED", (await kit.LinkAsync(EngineTestKit.OrderRef(second)))!.LastKnownXeroStatus);
    }

    // ------------------------------------------------------------ helpers

    private static async Task<int> CountLoopCyclesAsync(XeroSyncService engine, TimeSpan window)
    {
        var service = new XeroSyncHostedService(engine);
        await service.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(window);
            return service.CompletedCycles;
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<DateTimeOffset?> ReadPausedUntilAsync(Tempest.Core.Persistence.IPersistenceStore store)
    {
        var text = await store.ReadAsync(XeroSyncService.StateCollection, PausedUntilKey);
        return text is null ? null : DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    /// <summary>An invoice create that goes to Xero and maps the answer exactly as X4's handlers do (<see cref="XeroInvoiceDrafts.ToPushResult"/>: no <c>Retry-After</c>).</summary>
    private sealed class InvoiceCreateThroughX4Mapping(XeroAccountingApi api) : IXeroPushHandler
    {
        public int Calls { get; private set; }

        public IReadOnlyCollection<XeroOperation> Operations => [XeroOperation.PushInvoiceDraft];

        public async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            Calls++;
            var found = await api.FindInvoicesByNumberAsync("ACME1-BRIDG1-0001", cancellationToken);
            return found.Outcome == ConnectorOutcome.Ok
                ? new XeroPushResult(XeroPushOutcome.NothingToDo)
                : XeroInvoiceDrafts.ToPushResult(found.Outcome, found.Reason);
        }
    }

    /// <summary>Answers NothingToDo, noting which entry it was given and what the simulator had been asked before.</summary>
    private sealed class SnapshotHandler(EngineTestKit kit) : IXeroPushHandler
    {
        public List<(Guid EntryId, IReadOnlyList<string> RequestsBefore)> Calls { get; } = [];

        public IReadOnlyCollection<XeroOperation> Operations => [XeroOperation.PushQuote];

        public Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            Calls.Add((entry.Id, [.. kit.Requests.Select(r => r.Path)]));
            return Task.FromResult(new XeroPushResult(XeroPushOutcome.NothingToDo));
        }
    }

    private sealed class AlwaysUnknownHandler : IXeroPushHandler
    {
        public int Calls { get; private set; }

        public IReadOnlyCollection<XeroOperation> Operations => [XeroOperation.PushQuote];

        public Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new XeroPushResult(XeroPushOutcome.Unknown, "garbled"));
        }
    }

    private sealed class ThrowingAuditRecorder : IAuditRecorder
    {
        public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated: the audit store is unavailable.");
    }
}
