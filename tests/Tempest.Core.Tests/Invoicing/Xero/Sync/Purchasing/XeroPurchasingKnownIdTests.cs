using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// `v0.24.0` X5, verifier round 8: a lost create's Xero id is recorded as soon
/// as any answer reveals it, and from then on it is only read back by that id
/// — never re-sent; a create with no known id is re-sent under its key only
/// while Xero still holds the key (<see cref="XeroPurchasingOwnership.IdempotencyKeyLifetime"/>),
/// else cannot-tell; a record found deleted is kept as a tombstone that only
/// the person's deliberate <em>Send again</em> releases. Probes F–I are the
/// first four tests.
/// </summary>
public sealed class XeroPurchasingKnownIdTests
{
    private static string Steps(IEnumerable<PurchasingDrainStep> steps) =>
        string.Join(" | ", steps.Select(s => $"{s.Entry.Operation} {s.Result.Outcome} {s.Result.Reason}"));

    private static int Puts(PurchasingSyncTestKit kit, string resource) => kit.WritesTo(resource).Count(w => w.Method == HttpMethod.Put);

    private static async Task<(Guid ExpenseId, string BillId)> LostBillAsync(PurchasingSyncTestKit kit, string number)
    {
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: number);
        await kit.PlanExpenseAsync(id);
        kit.Lost.LoseWrites = 1;
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        return (id, Assert.Single(kit.LiveBills).Id);
    }

    private static async Task<(Guid OrderId, string XeroId)> LostOrderAsync(PurchasingSyncTestKit kit)
    {
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        kit.Lost.LoseWrites = 1;
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        return (id, Assert.Single(kit.LiveOrders).Id);
    }

    private static async Task RetryFailedAsync(PurchasingSyncTestKit kit)
    {
        foreach (var failed in await kit.Outbox.ListAsync([XeroOutboxState.Failed]))
            await kit.Outbox.RetryAsync(failed.Id);
    }

    // ---- Probes F–I: deleted in Xero, then the key expires ----

    [Fact]
    public async Task F_Po_DeletedInXero_ThenKeyExpired_Retry_StaysRejected_NoSecondOrder_NoLink()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        var puts = Puts(kit, "PurchaseOrders");

        kit.Simulator.ForgetIdempotencyKeys();
        Assert.True(await kit.Outbox.RetryAsync(step.Entry.Id));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        Assert.True(steps.Count > 0 && steps.All(s => s.Result.Outcome == XeroPushOutcome.Rejected), Steps(steps));
        Assert.All(steps, s => Assert.Contains("was deleted in Xero", s.Result.Reason, StringComparison.Ordinal));
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        Assert.Empty(kit.LiveOrders);
        Assert.Null(await kit.OrderLinkAsync(id));
        Assert.Equal(puts, Puts(kit, "PurchaseOrders")); // The tombstone is never re-sent.
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task G_Po_DeletedInXero_ThenKeyExpired_Cancel_EndsNothingToDo_CreatesAndDeletesNothing()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        var puts = Puts(kit, "PurchaseOrders");

        kit.Simulator.ForgetIdempotencyKeys();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        await RetryFailedAsync(kit);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        Assert.True(steps.Count > 0 && steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        Assert.Equal(puts, Puts(kit, "PurchaseOrders"));
        Assert.DoesNotContain(kit.WritesTo("PurchaseOrders"), w => w.Method == HttpMethod.Post);
        Assert.Null(await kit.OrderLinkAsync(id));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task H_Bill_DeletedInXero_ThenKeyExpired_Retry_StaysRejected_NoSecondBill_NoLink()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillAsync(kit, "NS-5");
        kit.Simulator.DeleteInXero("Invoices", ours);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        var puts = Puts(kit, "Invoices");

        kit.Simulator.ForgetIdempotencyKeys();
        Assert.True(await kit.Outbox.RetryAsync(step.Entry.Id));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        Assert.True(steps.Count > 0 && steps.All(s => s.Result.Outcome == XeroPushOutcome.Rejected), Steps(steps));
        Assert.Single(kit.Bills);
        Assert.Empty(kit.LiveBills);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        Assert.Equal(puts, Puts(kit, "Invoices"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task I_Bill_DeletedInXero_ThenKeyExpired_ExpenseDeleted_EndsNothingToDo_CreatesAndDeletesNothing()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillAsync(kit, "NS-5");
        kit.Simulator.DeleteInXero("Invoices", ours);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        var puts = Puts(kit, "Invoices");
        var writes = kit.WritesTo("Invoices").Count;

        kit.Simulator.ForgetIdempotencyKeys();
        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        await RetryFailedAsync(kit);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        Assert.True(steps.Count > 0 && steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Single(kit.Bills);
        Assert.Equal(puts, Puts(kit, "Invoices"));
        Assert.Equal(writes, kit.WritesTo("Invoices").Count);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    // ---- The id is recorded as soon as an answer reveals it ----

    [Fact]
    public async Task Po_TheIdAReplayAnswers_IsRecorded_ThenAfterTheKeyExpires_ItIsReadBackById_AndLinked_NeverResent()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);

        // The replay answers the id, but reading it back fails: the push waits.
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, $"PurchaseOrders/{ours}"));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        var logged = Assert.Single(await kit.Creates.ListSentAsync(PurchasingSyncTestKit.TenantId, PurchasingSyncTestKit.OrderRef(id)));
        Assert.Equal(ours, logged.XeroId);
        var puts = Puts(kit, "PurchaseOrders");

        // Long after Xero forgot the key, the order is read back by its id only.
        kit.Simulator.ForgetIdempotencyKeys();
        kit.Clock.Advance(TimeSpan.FromHours(2));
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Succeeded, step.Result.Outcome);
        Assert.Equal(ours, (await kit.OrderLinkAsync(id))!.XeroId);
        Assert.Equal(puts, Puts(kit, "PurchaseOrders"));
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_TheIdAReplayAnswers_IsRecorded_ThenAfterTheKeyExpires_ItIsReadBackById_NoSecondBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillAsync(kit, "NS-5");

        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, $"Invoices/{ours}"));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal(ours, Assert.Single(await kit.Creates.ListSentAsync(PurchasingSyncTestKit.TenantId, PurchasingSyncTestKit.ExpenseRef(id))).XeroId);
        var puts = Puts(kit, "Invoices");

        kit.Simulator.ForgetIdempotencyKeys();
        kit.Clock.Advance(TimeSpan.FromHours(2));
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Succeeded, step.Result.Outcome);
        Assert.Equal(ours, (await kit.ExpenseLinkAsync(id))!.XeroId);
        Assert.Equal(puts, Puts(kit, "Invoices"));
        Assert.Single(kit.Bills);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ACreateXeroAnswered_IsLoggedWithItsId()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        var logged = Assert.Single(await kit.Creates.ListSentAsync(PurchasingSyncTestKit.TenantId, PurchasingSyncTestKit.OrderRef(id)));
        Assert.Equal((await kit.OrderLinkAsync(id))!.XeroId, logged.XeroId);
        Assert.Equal(kit.Clock.GetUtcNow(), logged.SentAtUtc);
    }

    [Fact]
    public async Task AGoneRecord_IsKeptAsATombstone_AndNeverAskedOfXeroAgain()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);

        var logged = Assert.Single(await kit.Creates.ListSentAsync(PurchasingSyncTestKit.TenantId, PurchasingSyncTestKit.OrderRef(id)));
        Assert.Equal(ours, logged.XeroId);
        Assert.Equal("DELETED", logged.GoneStatus);
        Assert.True(logged.IsTombstone);

        var requests = kit.Simulator.Requests.Count;
        Assert.True(await kit.Outbox.RetryAsync(step.Entry.Id));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var again = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, again.Result.Outcome);
        Assert.Contains("Send again", again.Result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(kit.Simulator.Requests.Skip(requests), r => r.Path.Contains(ours, StringComparison.Ordinal) || r.Method == HttpMethod.Put);
    }

    // ---- No id known: the key is relied on only while Xero holds it ----

    [Fact]
    public async Task Po_LostCreate_RecoveredJustInsideTheKeyLifetime_IsReplayedAndLinked()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);
        kit.Clock.Advance(XeroPurchasingOwnership.IdempotencyKeyLifetime - TimeSpan.FromSeconds(1));

        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal(ours, (await kit.OrderLinkAsync(id))!.XeroId);
    }

    [Fact]
    public async Task Po_LostCreate_NotRecoveredWithinTheKeyLifetime_IsCannotTell_NothingSentLinkedOrDeleted()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);
        var puts = Puts(kit, "PurchaseOrders");
        kit.Clock.Advance(XeroPurchasingOwnership.IdempotencyKeyLifetime);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("cannot tell", step.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("longer ago than Xero keeps its Idempotency-Key", step.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(puts, Puts(kit, "PurchaseOrders"));
        Assert.Null(await kit.OrderLinkAsync(id));

        // Nor does a cancel delete what it cannot tell is ours.
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        await RetryFailedAsync(kit);
        var steps = await kit.DrainAsync();
        Assert.True(steps.Count > 0 && steps.All(s => s.Result.Outcome == XeroPushOutcome.Rejected), Steps(steps));
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", ours)!.Status);
        Assert.Equal(puts, Puts(kit, "PurchaseOrders"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_AfterXeroForgotTheKey_IsCannotTell_NeverASecondBill()
    {
        // The residual risk recorded in round 7: a bill's number is not unique in Xero, so
        // re-sending after the key expired made a second bill. It is never re-sent now.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, _) = await LostBillAsync(kit, "NS-5");
        kit.Simulator.ForgetIdempotencyKeys();
        kit.Clock.Advance(TimeSpan.FromMinutes(30));
        var puts = Puts(kit, "Invoices");

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("cannot tell", step.Result.Reason, StringComparison.Ordinal);
        Assert.Single(kit.Bills);
        Assert.Equal(puts, Puts(kit, "Invoices"));
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    // ---- Send again: the person's way past a record deleted in Xero ----

    [Fact]
    public async Task Po_SendAgain_AfterDeletedInXero_SendsANewOrderUnderANewKey_AndLinksIt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains(XeroPurchasingSendAgain.OrderAdvice, step.Result.Reason, StringComparison.Ordinal);
        kit.Simulator.ForgetIdempotencyKeys();
        kit.Clock.Advance(TimeSpan.FromDays(2));
        var firstKey = kit.WritesTo("PurchaseOrders").First(w => w.Method == HttpMethod.Put).IdempotencyKey;

        var request = await kit.SendAgain.SendOrderAgainAsync(id);
        Assert.True(request.Queued, request.Reason);
        Assert.Contains(kit.Audit.Rows, r => r.Action == XeroPurchasingSendAgain.AuditAction);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var sent = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Succeeded, sent.Result.Outcome);
        var link = (await kit.OrderLinkAsync(id))!;
        Assert.NotEqual(ours, link.XeroId);
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", link.XeroId)!.Status);
        Assert.Equal("DELETED", kit.Simulator.Find("PurchaseOrders", ours)!.Status);
        var lastPut = kit.WritesTo("PurchaseOrders").Last(w => w.Method == HttpMethod.Put);
        Assert.NotEqual(firstKey, lastPut.IdempotencyKey);
        Assert.StartsWith("tos:", lastPut.IdempotencyKey, StringComparison.Ordinal);
        Assert.Single(kit.LiveOrders);

        // A later cancel deletes the new order only.
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        Assert.All(await kit.DrainAsync(), s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));
        Assert.Empty(kit.LiveOrders);
        Assert.Equal(2, kit.Simulator.All("PurchaseOrders").Count());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_SendAgain_AfterDeletedInXero_SendsANewBill_OnlyOnce()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillAsync(kit, "NS-5");
        kit.Simulator.DeleteInXero("Invoices", ours);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains(XeroPurchasingSendAgain.ExpenseAdvice, step.Result.Reason, StringComparison.Ordinal);

        Assert.True((await kit.SendAgain.SendExpenseAgainAsync(id)).Queued);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        var link = (await kit.ExpenseLinkAsync(id))!;
        Assert.NotEqual(ours, link.XeroId);
        Assert.Equal(2, kit.Bills.Count);
        Assert.Single(kit.LiveBills);

        // Once linked there is nothing to send again.
        var refused = await kit.SendAgain.SendExpenseAgainAsync(id);
        Assert.False(refused.Queued);
        Assert.Contains("linked", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(2, kit.Bills.Count);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task SendAgain_IsRefused_WhenNothingIsKnownDeletedInXero_SoACannotTellIsNeverReleased()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);
        kit.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome); // CannotTell.

        var request = await kit.SendAgain.SendOrderAgainAsync(id);
        Assert.False(request.Queued);
        Assert.Contains("nothing to send again", request.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(kit.Audit.Rows, r => r.Action == XeroPurchasingSendAgain.AuditAction);
        Assert.Single(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", ours)!.Status);
    }

    [Fact]
    public async Task SendAgain_IsRefused_ForACancelledOrder()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);

        var request = await kit.SendAgain.SendOrderAgainAsync(id);
        Assert.False(request.Queued);
        Assert.True(Assert.Single(await kit.Creates.ListSentAsync(PurchasingSyncTestKit.TenantId, PurchasingSyncTestKit.OrderRef(id))).IsTombstone);
    }

    // ---- The rule itself ----

    [Fact]
    public void RecoveryFor_ReadsById_OnceKnown_ReplaysOnlyInsideTheKeyLifetime()
    {
        var at = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        var create = new XeroPurchasingSentCreate("PO-1", "c1", "k1", Body: "{}", SentAtUtc: at);

        Assert.Equal(XeroPurchasingOwnership.RecoveryStep.Replay, XeroPurchasingOwnership.RecoveryFor(create, at.AddMinutes(4)));
        Assert.Equal(XeroPurchasingOwnership.RecoveryStep.KeyExpired, XeroPurchasingOwnership.RecoveryFor(create, at + XeroPurchasingOwnership.IdempotencyKeyLifetime));
        Assert.Equal(XeroPurchasingOwnership.RecoveryStep.KeyExpired, XeroPurchasingOwnership.RecoveryFor(create, at.AddMinutes(-1))); // A clock stepped back proves nothing.
        Assert.Equal(XeroPurchasingOwnership.RecoveryStep.KeyExpired, XeroPurchasingOwnership.RecoveryFor(create with { SentAtUtc = null }, at));
        Assert.Equal(XeroPurchasingOwnership.RecoveryStep.NoBody, XeroPurchasingOwnership.RecoveryFor(create with { Body = null }, at));
        Assert.Equal(XeroPurchasingOwnership.RecoveryStep.ReadById, XeroPurchasingOwnership.RecoveryFor(create with { XeroId = "x" }, at.AddDays(9)));
        Assert.Equal(XeroPurchasingOwnership.RecoveryStep.Tombstoned, XeroPurchasingOwnership.RecoveryFor(create with { XeroId = "x", GoneStatus = "DELETED" }, at));
        Assert.True(XeroPurchasingOwnership.IdempotencyKeyLifetime < TimeSpan.FromMinutes(6)); // Xero keeps a key 6 minutes from the first call.
    }

    [Fact]
    public void CreateKey_IsTheEntrysOwn_UntilItsTombstoneIsReleased_ThenANewDeterministicKeyPerRelease()
    {
        var at = DateTimeOffset.UnixEpoch;
        var own = XeroPurchasingOwnership.CreateKey("tos:k", []);
        Assert.Equal("tos:k", own);

        var gone = new XeroPurchasingSentCreate("PO-1", "c1", "tos:k", XeroId: "x", GoneStatus: "DELETED");
        Assert.Equal("tos:k", XeroPurchasingOwnership.CreateKey("tos:k", [gone])); // Not released: no new key.

        var first = XeroPurchasingOwnership.CreateKey("tos:k", [gone with { ReleasedAtUtc = at }]);
        Assert.NotEqual("tos:k", first);
        Assert.Equal(first, XeroPurchasingOwnership.CreateKey("tos:k", [gone with { ReleasedAtUtc = at }]));
        Assert.True(first.Length <= XeroOutboxEntry.MaximumIdempotencyKeyLength);

        var second = XeroPurchasingOwnership.CreateKey("tos:k", [gone with { ReleasedAtUtc = at }, gone with { IdempotencyKey = first, ReleasedAtUtc = at }]);
        Assert.NotEqual(first, second);
        Assert.NotEqual("tos:k", second);
    }

    [Fact]
    public void ToRecover_PassesOverAReleasedTombstone()
    {
        var gone = new XeroPurchasingSentCreate("PO-1", "c1", "k1", XeroId: "x", GoneStatus: "DELETED", ReleasedAtUtc: DateTimeOffset.UnixEpoch);
        var live = new XeroPurchasingSentCreate("PO-1", "c1", "k2");
        Assert.Equal([live], XeroPurchasingOwnership.ToRecover([gone, live]));
    }
}
