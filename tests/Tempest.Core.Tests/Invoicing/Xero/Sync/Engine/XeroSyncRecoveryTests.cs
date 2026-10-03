using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` X6: a create whose answer was lost — TempestOS stopped
/// mid-request, or the answer dropped — is recovered before any other work
/// and while Xero still holds its <c>Idempotency-Key</c>, making exactly one
/// record; past the key's lifetime a purchase order's create is
/// <em>CannotTell</em> and nothing is sent. Every test ends with no simulator
/// violation.
/// </summary>
public sealed class XeroSyncRecoveryTests
{
    [Fact]
    public async Task ACrashMidCreate_IsRecoveredAtStartUp_WithinTheKeyLifetime_AsOneRecord()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync(); // settings read; nothing to send yet

        var orderId = kit.IssueOrder();
        using var process = new CancellationTokenSource();
        kit.Hop.CrashOnNewWrite = ("PurchaseOrders", process);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => kit.Engine.RunCycleAsync(process.Token));

        // Xero committed the purchase order; TempestOS never heard back.
        var created = Assert.Single(kit.LiveOrders);
        var push = Assert.Single(await kit.Outbox.ListForDocumentAsync(EngineTestKit.OrderRef(orderId)), e => e.Operation == XeroOperation.PushPurchaseOrder);
        Assert.Equal(XeroOutboxState.InFlight, push.State);
        Assert.Null(await kit.LinkAsync(EngineTestKit.OrderRef(orderId)));

        // Restarted two minutes later: the lost create is recovered first, by replaying its own key.
        kit.Clock.Advance(TimeSpan.FromMinutes(2));
        var mark = kit.Simulator.Requests.Count;
        var engine = kit.Restart();
        await engine.StartAsync();

        var afterRestart = kit.Simulator.Requests.Skip(mark).ToList();
        var replay = afterRestart.First(r => r.Method != HttpMethod.Get);
        Assert.Equal("PurchaseOrders", replay.Path);
        var crashed = kit.Simulator.Requests.Take(mark).Single(r => r.Method == HttpMethod.Put && r.Path == "PurchaseOrders");
        Assert.Equal(crashed.IdempotencyKey, replay.IdempotencyKey);

        var link = await kit.LinkAsync(EngineTestKit.OrderRef(orderId));
        Assert.NotNull(link);
        Assert.Equal(created.Id, link.XeroId);
        Assert.Equal(XeroOutboxState.Succeeded, (await kit.Outbox.ListForDocumentAsync(EngineTestKit.OrderRef(orderId))).Single(e => e.Id == push.Id).State);
        Assert.Contains(kit.AuditRows(XeroSyncService.AuditRecovering), _ => true);
        Assert.Contains(kit.AuditRows(XeroSyncService.AuditSucceeded), d => d!["entry"] == push.Id.ToString("D") && d["recovered"] == "true");

        await kit.SettleAsync();
        var order = Assert.Single(kit.LiveOrders);
        Assert.Equal(created.Id, order.Id);
        Assert.Single(order.Attachments);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ARestartBeyondTheKeyLifetime_SurfacesCannotTell_AndSendsNothing()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        var orderId = kit.IssueOrder();
        using var process = new CancellationTokenSource();
        kit.Hop.CrashOnNewWrite = ("PurchaseOrders", process);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => kit.Engine.RunCycleAsync(process.Token));
        var created = Assert.Single(kit.LiveOrders);

        // Down for ten minutes: Xero has forgotten the key.
        kit.Clock.Advance(TimeSpan.FromMinutes(10));
        kit.Simulator.ForgetIdempotencyKeys();
        var mark = kit.Simulator.Requests.Count;
        var engine = kit.Restart();
        await engine.StartAsync();
        await kit.SettleAsync();

        Assert.DoesNotContain(kit.Simulator.Requests.Skip(mark), r => r.Method != HttpMethod.Get && r.Path.StartsWith("PurchaseOrders", StringComparison.Ordinal));
        Assert.Equal(created.Id, Assert.Single(kit.LiveOrders).Id);
        Assert.Null(await kit.LinkAsync(EngineTestKit.OrderRef(orderId)));

        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.OrderRef(orderId));
        Assert.Equal(XeroSyncBadge.Failed, status.Status.Badge);
        Assert.Contains("cannot tell", status.Status.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(status.CannotTell); // Backlog U3: typed, for the badge.
        Assert.False(status.CanSendAgain);

        // A Retry still sends nothing: the create is never re-sent blindly.
        Assert.True(await kit.Engine.RetryAsync(status.Status.RetryableEntryId!.Value));
        await kit.SettleAsync();
        Assert.DoesNotContain(kit.Simulator.Requests.Skip(mark), r => r.Method != HttpMethod.Get && r.Path.StartsWith("PurchaseOrders", StringComparison.Ordinal));
        Assert.Single(kit.LiveOrders);
        Assert.True((await kit.Engine.GetDocumentStatusAsync(EngineTestKit.OrderRef(orderId))).CannotTell);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreate_IsRecoveredBeforeAnyOtherWork_EvenOlderWork()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        // An expense queued first, held back (its supplier is not linked yet).
        var expenseId = kit.RecordExpense(EngineTestKit.UnlinkedReference);
        await kit.Engine.RunCycleAsync();
        var held = Assert.Single(await kit.Outbox.ListForDocumentAsync(EngineTestKit.ExpenseRef(expenseId)), e => e.Operation == XeroOperation.PushExpenseBill);
        Assert.Equal(XeroOutboxState.Failed, held.State);

        // Then a purchase order whose create is cut off by a crash.
        var orderId = kit.IssueOrder();
        using var process = new CancellationTokenSource();
        kit.Hop.CrashOnNewWrite = ("PurchaseOrders", process);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => kit.Engine.RunCycleAsync(process.Token));

        // The supplier is linked and the expense retried by hand: it is now the oldest work queued.
        var newco = kit.Simulator.SeedContact("Newco Fixings Ltd");
        Assert.Equal(Tempest.Core.Invoicing.ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync(EngineTestKit.UnlinkedReference, newco)).Outcome);
        Assert.True(await kit.Outbox.RetryAsync(held.Id));

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var mark = kit.Simulator.Requests.Count;
        var engine = kit.Restart();
        await engine.StartAsync();

        // The recovery's requests come first; the older expense's only after.
        var afterRestart = kit.Simulator.Requests.Skip(mark).ToList();
        Assert.StartsWith("PurchaseOrders", afterRestart[0].Path, StringComparison.Ordinal);
        Assert.DoesNotContain(afterRestart, r => r.Path.StartsWith("Invoices", StringComparison.Ordinal));
        Assert.NotNull(await kit.LinkAsync(EngineTestKit.OrderRef(orderId)));

        await kit.SettleAsync();
        Assert.Single(kit.LiveOrders);
        Assert.Single(kit.Bills);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostAnswer_IsRetriedWithinSeconds_AndMakesOneRecord()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        var orderId = kit.IssueOrder();
        kit.Hop.LoseNewWrites = 1;
        var first = await kit.Engine.RunCycleAsync();
        Assert.Equal(1, first.Drain.Deferred);
        Assert.Single(kit.LiveOrders);

        var push = (await kit.Outbox.ListForDocumentAsync(EngineTestKit.OrderRef(orderId))).Single(e => e.Operation == XeroOperation.PushPurchaseOrder);
        Assert.Equal(XeroOutboxState.Unknown, push.State);
        Assert.Equal(kit.Clock.GetUtcNow() + XeroBackoff.RecoveryBaseDelay, push.NotBeforeUtc);
        Assert.Equal(push.NotBeforeUtc, await kit.Engine.NextWorkDueAtAsync());

        kit.Clock.Advance(XeroBackoff.RecoveryBaseDelay);
        await kit.SettleAsync();

        var order = Assert.Single(kit.LiveOrders);
        Assert.Equal(order.Id, (await kit.LinkAsync(EngineTestKit.OrderRef(orderId)))!.XeroId);
        Assert.Equal(2, kit.WritesTo("PurchaseOrders").Count(w => w.Method == HttpMethod.Put && w.Path == "PurchaseOrders")); // the create and its replay, one key
        Assert.Single(kit.WritesTo("PurchaseOrders").Where(w => w.Path == "PurchaseOrders").Select(w => w.IdempotencyKey).Distinct());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateWhoseRecordWasDeletedInXero_OffersSendAgain_WhichSendsANewDraft()
    {
        using var kit = await EngineTestKit.CreateAsync();
        await kit.Engine.RunCycleAsync();

        var orderId = kit.IssueOrder();
        kit.Hop.LoseNewWrites = 1;
        await kit.Engine.RunCycleAsync();
        kit.Simulator.DeleteInXero("PurchaseOrders", Assert.Single(kit.LiveOrders).Id);

        kit.Clock.Advance(TimeSpan.FromSeconds(10));
        await kit.SettleAsync();

        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.OrderRef(orderId));
        Assert.Equal(XeroSyncBadge.Failed, status.Status.Badge);
        Assert.True(status.CanRetry);
        Assert.True(status.CanSendAgain);
        Assert.False(status.CannotTell); // Deleted in Xero is not "can't tell".
        Assert.Empty(kit.LiveOrders);

        var again = await kit.Engine.SendAgainAsync(EngineTestKit.OrderRef(orderId));
        Assert.True(again.Queued, again.Reason);
        await kit.SettleAsync();

        var order = Assert.Single(kit.LiveOrders);
        Assert.Equal("DRAFT", order.Status);
        Assert.Equal(order.Id, (await kit.LinkAsync(EngineTestKit.OrderRef(orderId)))!.XeroId);
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetStatusAsync(EngineTestKit.OrderRef(orderId))).Badge);
        Assert.Contains(kit.Audit.Rows, r => r.Action == XeroPurchasingSendAgain.AuditAction);
        kit.AssertNoViolations();
    }
}
