using Tempest.Core.Events;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` X6: the engine under a 429 storm, an outage, a lost grant and a
/// missing precondition — pausing everything when Xero says so, backing off
/// per entry otherwise, never hammering, and finishing with one record per
/// document. Every test ends with no simulator violation.
/// </summary>
public sealed class XeroSyncResilienceTests
{
    [Fact]
    public async Task A429Storm_PausesEverything_ForExactlyRetryAfter_ThenCompletesWithOneRecordEach()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        var orders = new[] { kit.IssueOrder("PO-2026-001"), kit.IssueOrder("PO-2026-002"), kit.IssueOrder("PO-2026-003") };
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, "PurchaseOrders", Times: 3, RetryAfter: TimeSpan.FromSeconds(20)));

        var storms = 0;
        for (var i = 0; i < 20; i++)
        {
            var report = await kit.Engine.RunCycleAsync();
            if (report.Drain.ResumeNotBeforeUtc is not { } resume)
            {
                if (report.Drain.Attempted == 0 && report.Planned == 0)
                    break;
                continue;
            }

            storms++;
            Assert.True(resume >= kit.Clock.GetUtcNow() + TimeSpan.FromSeconds(20), $"Resumes at {resume:O}, before Retry-After.");

            // Paused: nothing at all is sent until Retry-After has passed.
            var before = kit.Simulator.Requests.Count;
            kit.Clock.Advance(TimeSpan.FromSeconds(10));
            var paused = await kit.Engine.RunCycleAsync();
            Assert.Equal(0, paused.Drain.Attempted);
            Assert.Equal(before, kit.Simulator.Requests.Count);

            kit.Clock.Advance(resume - kit.Clock.GetUtcNow());
        }

        Assert.True(storms >= 1);
        Assert.Equal(3, kit.LiveOrders.Count);
        Assert.Equal(["PO-2026-001", "PO-2026-002", "PO-2026-003"], kit.LiveOrders.Select(o => o.Number).Order());
        Assert.All(kit.LiveOrders, o => Assert.Single(o.Attachments));
        foreach (var id in orders)
            Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.OrderRef(id))).Badge);
        Assert.NotEmpty(kit.AuditRows(XeroSyncService.AuditRateLimited));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnOutage_BacksOffPerEntry_StopsADrainAfterThreeFailures_ThenRecovers_OneRecordEach()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        var quotes = Enumerable.Range(1, 4).Select(n => kit.ExportQuote($"P0012-Q-00{n}")).ToList();
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, Times: 6));

        var first = await kit.Engine.RunCycleAsync();
        Assert.Equal(3, first.Drain.Attempted); // three failures in a row stop the drain: Xero looks down
        Assert.Equal(3, first.Drain.Deferred);

        // Nothing is due yet: a cycle straight after sends nothing.
        var before = kit.Simulator.Requests.Count;
        var again = await kit.Engine.RunCycleAsync();
        Assert.Equal(1, again.Drain.Attempted); // only the fourth quote, never tried, is due
        Assert.Equal(before + 1, kit.Simulator.Requests.Count);

        for (var i = 0; i < 30 && kit.LiveQuotes.Count < 4; i++)
        {
            if (await kit.Engine.NextWorkDueAtAsync() is { } due && due > kit.Clock.GetUtcNow())
                kit.Clock.Advance(due - kit.Clock.GetUtcNow());
            await kit.Engine.RunCycleAsync();
        }

        await kit.SettleAsync();
        Assert.Equal(4, kit.LiveQuotes.Count);
        Assert.All(kit.LiveQuotes, q => Assert.Single(q.Attachments));
        Assert.Equal(4, kit.WritesTo("Quotes").Count(w => w.Path == "Quotes"));
        foreach (var id in quotes)
            Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.QuoteRef(id))).Badge);
        Assert.NotEmpty(kit.AuditRows(XeroSyncService.AuditDeferred));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ATransientFailureOnAnUpdate_BacksOffExponentiallyWithJitter()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var id = kit.ExportQuote();
        await kit.SettleAsync();

        kit.SetQuoteStatus(id, QuotationStatus.Sent);
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, "Quotes", Times: 2));
        await kit.Engine.RunCycleAsync();

        var entry = (await kit.Outbox.ListForDocumentAsync(EngineTestKit.QuoteRef(id))).Single(e => e.Operation == XeroOperation.SetQuoteStatus);
        Assert.Equal(XeroOutboxState.Pending, entry.State);
        Assert.Equal(kit.Clock.GetUtcNow() + TimeSpan.FromSeconds(30), entry.NotBeforeUtc); // jitter 0.5: the nominal 30 s

        kit.Clock.Advance(TimeSpan.FromSeconds(30));
        await kit.Engine.RunCycleAsync();
        entry = (await kit.Outbox.ListForDocumentAsync(EngineTestKit.QuoteRef(id))).Single(e => e.Operation == XeroOperation.SetQuoteStatus);
        Assert.Equal(kit.Clock.GetUtcNow() + TimeSpan.FromSeconds(60), entry.NotBeforeUtc);

        kit.Clock.Advance(TimeSpan.FromSeconds(60));
        await kit.SettleAsync();
        Assert.Equal("SENT", Assert.Single(kit.LiveQuotes).Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostGrant_PausesEverything_UntilXeroIsReauthorised_ThenResumes()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        var first = kit.IssueOrder("PO-2026-001");
        var second = kit.IssueOrder("PO-2026-002");
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.Unauthorised, "PurchaseOrders"));

        var report = await kit.Engine.RunCycleAsync();
        Assert.True(report.Drain.PausedForAuthorisation);
        Assert.Equal(1, report.Drain.Attempted);
        Assert.Empty(kit.LiveOrders);

        var firstStatus = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.OrderRef(first));
        Assert.Equal(XeroSyncBadge.NeedsReauthorisation, firstStatus.Status.Badge);
        Assert.Equal("Waiting for authorisation", firstStatus.Label);
        var secondStatus = await kit.Engine.GetStatusAsync(EngineTestKit.OrderRef(second));
        Assert.Equal(XeroSyncBadge.Queued, secondStatus.Badge);
        Assert.Contains("re-authorising", secondStatus.Reason, StringComparison.Ordinal);
        Assert.Single(kit.AuditRows(XeroSyncService.AuditReauthorisationRequired));

        // The grant still reads as usable and nothing changed: everything keeps waiting (no retry loop).
        var before = kit.Simulator.Requests.Count;
        kit.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True((await kit.Engine.RunCycleAsync()).Drain.PausedForAuthorisation);
        Assert.Equal(before, kit.Simulator.Requests.Count);

        // The token expires, then the Product Owner re-authorises: the engine sees it and resumes.
        kit.Connection.Status = ConnectorAuthorisation.Expired;
        Assert.True((await kit.Engine.RunCycleAsync()).Drain.PausedForAuthorisation);
        kit.Connection.Status = ConnectorAuthorisation.Authorised;
        await kit.SettleAsync();

        Assert.Equal(2, kit.LiveOrders.Count);
        Assert.NotEmpty(kit.AuditRows(XeroSyncService.AuditResumed));
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.OrderRef(first))).Badge);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task NotifyAuthorised_ResumesAtOnce_AndReadsTheSettingsOnConnect()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        kit.IssueOrder();
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.Unauthorised, "PurchaseOrders"));
        await kit.Engine.RunCycleAsync();
        Assert.Single(await kit.Outbox.ListAsync([XeroOutboxState.WaitingForAuthorisation]));

        var settingsReads = kit.Requests.Count(r => r.Path == "Organisation");
        Assert.Equal(1, await kit.Engine.NotifyAuthorisedAsync());
        Assert.Equal(settingsReads + 1, kit.Requests.Count(r => r.Path == "Organisation"));

        await kit.SettleAsync();
        Assert.Single(kit.LiveOrders);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ABlockedWrite_IsRetriedByItself_OnceItsSupplierIsLinked()
    {
        using var kit = await EngineTestKit.CreateAsync();

        var expenseId = kit.RecordExpense(EngineTestKit.UnlinkedReference);
        await kit.SettleAsync();

        var blocked = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.ExpenseRef(expenseId));
        Assert.Equal(XeroSyncBadge.Failed, blocked.Status.Badge);
        Assert.True(blocked.CanRetry);
        Assert.False(string.IsNullOrWhiteSpace(blocked.Status.Reason));
        Assert.Empty(kit.Bills);
        Assert.Single(kit.AuditRows(XeroSyncService.AuditFailed));

        // Nothing changed: the periodic retry blocks again, without a second audit row.
        kit.Clock.Advance(kit.Options.BlockedRetryInterval);
        await kit.Engine.RunCycleAsync();
        Assert.Single(kit.AuditRows(XeroSyncService.AuditFailed));

        var newco = kit.Simulator.SeedContact("Newco Fixings Ltd");
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync(EngineTestKit.UnlinkedReference, newco)).Outcome);
        kit.Clock.Advance(kit.Options.BlockedRetryInterval);
        await kit.SettleAsync();

        var bill = Assert.Single(kit.Bills);
        Assert.Equal(newco, bill.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.ExpenseRef(expenseId))).Badge);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ARefusedWrite_IsFailedWithXerosReason_AndARetryAfterTheFixSendsIt()
    {
        using var kit = await EngineTestKit.CreateAsync();

        // A quote keyed into Xero by hand already holds the number.
        await kit.Engine.RunCycleAsync();
        var mark = kit.Simulator.Requests.Count;
        using (var raw = kit.Simulator.CreateClient())
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, "Quotes?summarizeErrors=true")
            {
                Content = new StringContent(SimulatorTestKit.Quote(kit.SupplierContactId, "P0012-Q-001").ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", kit.Simulator.Options.AccessToken);
            request.Headers.Add("xero-tenant-id", kit.Simulator.Options.TenantId);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("Idempotency-Key", "by-hand:1");
            using var response = await raw.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode);
        }

        var id = kit.ExportQuote();
        await kit.SettleAsync();

        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.QuoteRef(id));
        Assert.Equal(XeroSyncBadge.Failed, status.Status.Badge);
        Assert.Contains("P0012-Q-001", status.Status.Reason, StringComparison.Ordinal);
        Assert.True(status.CanRetry);
        Assert.Single(kit.LiveQuotes);

        // Fixed in Xero (the hand-keyed quote deleted), then Retry.
        kit.Simulator.DeleteInXero("Quotes", Assert.Single(kit.LiveQuotes).Id);
        Assert.True(await kit.Engine.RetryAsync(status.Status.RetryableEntryId!.Value));
        Assert.Single(kit.AuditRows(XeroSyncService.AuditRetried));
        await kit.SettleAsync();

        Assert.Equal("P0012-Q-001", Assert.Single(kit.LiveQuotes).Number);
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.QuoteRef(id))).Badge);
        Assert.True(kit.Simulator.Requests.Count > mark);
        kit.AssertNoViolations();
    }
}
