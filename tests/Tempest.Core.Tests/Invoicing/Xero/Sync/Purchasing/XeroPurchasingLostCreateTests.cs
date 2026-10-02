using System.Text;
using System.Text.Json.Nodes;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// `v0.24.0` X5: a create that landed in Xero with its answer lost, then a
/// number, supplier or contact changed, or someone keyed a document into Xero
/// by hand, then the TempestOS source edited, deleted or gone. TempestOS's own
/// record is always found by its id — the create re-sent under its own
/// <c>Idempotency-Key</c>, the answered id read back — and linked (so a delete
/// deletes it); one no longer live in Xero is never linked or resent; and a
/// record someone else keyed is never linked, changed or deleted.
/// </summary>
public sealed class XeroPurchasingLostCreateTests
{
    private static string Steps(IEnumerable<PurchasingDrainStep> steps) =>
        string.Join(" | ", steps.Select(s => $"{s.Entry.Operation} {s.Result.Outcome} {s.Result.Reason}"));

    /// <summary>A lost create of an expense's bill under the supplier's number <paramref name="number"/>; returns the expense and its bill's id.</summary>
    private static async Task<(Guid ExpenseId, string BillId)> LostBillCreateAsync(PurchasingSyncTestKit kit, string number)
    {
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: number);
        await kit.PlanExpenseAsync(id);
        kit.Lost.LoseWrites = 1;
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        return (id, Assert.Single(kit.LiveBills).Id);
    }

    private static Task<string> HandBillAsync(PurchasingSyncTestKit kit, string number) =>
        XeroPurchaseOrderSyncTests.PutByHandAsync(kit.Simulator, "Invoices", SimulatorTestKit.Invoice(kit.SupplierContactId, number, type: "ACCPAY"));

    /// <summary>A lost create of an issued order "PO-2026-001"; returns the order and its Xero id.</summary>
    private static async Task<(Guid OrderId, string XeroId)> LostOrderCreateAsync(PurchasingSyncTestKit kit)
    {
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        kit.Lost.LoseWrites = 1;
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        return (id, Assert.Single(kit.LiveOrders).Id);
    }

    // ---- Bills ----

    [Fact]
    public async Task Bill_LostCreate_ThenNumberChangedOntoAHandBill_ThenExpenseDeleted_DeletesOurBill_LeavesTheHandBill()
    {
        // Verifier defect 1: the hand bill under the current number must not hide our bill under the logged one.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ourBill) = await LostBillCreateAsync(kit, "NS-5");
        var handBill = await HandBillAsync(kit, "NS-6");
        var handBody = kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString();

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { SupplierInvoiceNumber = "NS-6" };
        await kit.PlanExpenseAsync(id);
        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();
        foreach (var failed in await kit.Outbox.ListAsync([XeroOutboxState.Failed]))
            await kit.Outbox.RetryAsync(failed.Id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        steps = [.. steps, .. await kit.DrainAsync()];

        Assert.True(kit.Simulator.Find("Invoices", ourBill)!.Status == "DELETED", Steps(steps));
        Assert.DoesNotContain(steps, s => s.Result.Reason?.Contains("is not TempestOS's", StringComparison.Ordinal) == true);
        Assert.Equal("DRAFT", kit.Simulator.Find("Invoices", handBill)!.Status);
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString());
        Assert.Equal(ourBill, (await kit.ExpenseLinkAsync(id))!.XeroId);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenNumberChangedOntoAHandBill_LinksOurBill_RefusesTheNumber_ThenANewNumberRewritesIt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ourBill) = await LostBillCreateAsync(kit, "NS-5");
        var handBill = await HandBillAsync(kit, "NS-6");
        var handBody = kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString();

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { SupplierInvoiceNumber = "NS-6" };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        var refused = Assert.Single(steps, s => s.Result.Outcome == XeroPushOutcome.Rejected);
        Assert.Contains("NS-6 is already used in Xero by another bill", refused.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(ourBill, refused.Result.Link?.XeroId);
        Assert.Equal(ourBill, (await kit.ExpenseLinkAsync(id))!.XeroId); // Linked: never orphaned, never created twice.
        Assert.Equal("NS-5", kit.Simulator.Find("Invoices", ourBill)!.Number);
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString());

        // A Retry under the same number is refused again: our bill is never renumbered onto the hand bill's number.
        Assert.True(await kit.Outbox.RetryAsync(refused.Entry.Id));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal("NS-5", kit.Simulator.Find("Invoices", ourBill)!.Number);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { SupplierInvoiceNumber = "NS-7" };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        Assert.Equal("NS-7", kit.Simulator.Find("Invoices", ourBill)!.Number);
        Assert.Equal(2, kit.LiveBills.Count);
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_Linked_ThenNumberChangedOntoAHandBill_IsRefused_NeverRenumberedOntoIt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-5");
        await kit.PlanExpenseAsync(id);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        var ourBill = Assert.Single(kit.LiveBills).Id;
        var handBill = await HandBillAsync(kit, "NS-6");
        var handBody = kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString();

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { SupplierInvoiceNumber = "NS-6" };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("NS-6 is already used in Xero by another bill", step.Result.Reason, StringComparison.Ordinal);
        Assert.Equal("NS-5", kit.Simulator.Find("Invoices", ourBill)!.Number);
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_Linked_ThenNumberChangedToOneAnotherExpensesBillUses_IsAllowed()
    {
        // A receipt split across expenses: both bills carry one supplier invoice number.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var steel = Guid.NewGuid();
        var delivery = Guid.NewGuid();
        kit.FakeExpenses[steel] = PurchasingSyncTestKit.Expense(steel, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-1");
        kit.FakeExpenses[delivery] = PurchasingSyncTestKit.Expense(delivery, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-X", net: 30m, vat: 6m);
        await kit.PlanExpenseAsync(steel);
        await kit.PlanExpenseAsync(delivery);
        await kit.DrainAsync();

        kit.FakeExpenses[delivery] = kit.FakeExpenses[delivery] with { SupplierInvoiceNumber = "NS-1" };
        await kit.PlanExpenseAsync(delivery);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.All(await kit.DrainAsync(), s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));
        Assert.All(kit.LiveBills, b => Assert.Equal("NS-1", b.Number));
        Assert.Equal(2, kit.LiveBills.Count);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenAHandBillUnderTheSameNumber_OursIsKnownByItsId_TheHandBillIsNeverTouched()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ourBill) = await LostBillCreateAsync(kit, "NS-5");
        var handBill = await HandBillAsync(kit, "NS-5");
        var handBody = kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString();

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Succeeded, step.Result.Outcome);
        Assert.Equal(ourBill, (await kit.ExpenseLinkAsync(id))!.XeroId);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        Assert.Equal("DELETED", kit.Simulator.Find("Invoices", ourBill)!.Status);
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString());
        Assert.Equal(handBill, Assert.Single(kit.LiveBills).Id);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenOursDeletedInXeroAndAnotherKeyedByHandUnderTheNumber_IsRefused_NeverLinkedOrDeleted()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ourBill) = await LostBillCreateAsync(kit, "NS-5");
        kit.Simulator.DeleteInXero("Invoices", ourBill);
        var handBill = await HandBillAsync(kit, "NS-5");
        var handBody = kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString();

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("was deleted in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.ExpenseLinkAsync(id));

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        Assert.True(await kit.Outbox.RetryAsync(step.Entry.Id));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBill)!.Body.ToJsonString());
        Assert.Equal("DRAFT", kit.Simulator.Find("Invoices", handBill)!.Status);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenTheExpenseDeletedAndNoLongerReadable_StillDeletesOurBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ourBill) = await LostBillCreateAsync(kit, "NS-5");

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        Assert.Contains(await kit.PlanExpenseAsync(id), e => e.Operation == XeroOperation.DeleteExpenseBill);
        kit.FakeExpenses.Remove(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();
        foreach (var failed in await kit.Outbox.ListAsync([XeroOutboxState.Failed]))
            await kit.Outbox.RetryAsync(failed.Id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        steps = [.. steps, .. await kit.DrainAsync()];

        Assert.True(kit.Simulator.Find("Invoices", ourBill)!.Status == "DELETED", Steps(steps));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenTheSuppliersContactUnlinked_ThenDeleted_StillDeletesOurBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ourBill) = await LostBillCreateAsync(kit, "NS-5");
        Assert.True(await kit.Linker.UnlinkAsync(PurchasingSyncTestKit.SupplierReference));

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        Assert.True(kit.Simulator.Find("Invoices", ourBill)!.Status == "DELETED", Steps(steps));
        kit.AssertNoViolations();
    }

    // ---- Purchase orders ----

    [Fact]
    public async Task Po_LostCreate_ThenTheSupplierRelinkedToAnotherContact_ThenCancelled_DeletesOurOrder()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ourOrder) = await LostOrderCreateAsync(kit);
        var otherContact = kit.Simulator.SeedContact("Steel Supplies (new account) Ltd");
        Assert.True(await kit.Linker.UnlinkAsync(PurchasingSyncTestKit.SupplierReference));
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync(PurchasingSyncTestKit.SupplierReference, otherContact)).Outcome);

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        Assert.True(kit.Simulator.Find("PurchaseOrders", ourOrder)!.Status == "DELETED", Steps(steps));
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_ThenTheSupplierUnlinked_ThenCancelled_DeletesOurOrder()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ourOrder) = await LostOrderCreateAsync(kit);
        Assert.True(await kit.Linker.UnlinkAsync(PurchasingSyncTestKit.SupplierReference));

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();

        Assert.True(kit.Simulator.Find("PurchaseOrders", ourOrder)!.Status == "DELETED", Steps(steps));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_ThenCancelledAndNoLongerReadable_StillDeletesOurOrder()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ourOrder) = await LostOrderCreateAsync(kit);

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        Assert.Contains(await kit.PlanOrderAsync(id), e => e.Operation == XeroOperation.DeletePurchaseOrder);
        kit.FakeOrders.Remove(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = await kit.DrainAsync();
        foreach (var failed in await kit.Outbox.ListAsync([XeroOutboxState.Failed]))
            await kit.Outbox.RetryAsync(failed.Id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        steps = [.. steps, .. await kit.DrainAsync()];

        Assert.True(kit.Simulator.Find("PurchaseOrders", ourOrder)!.Status == "DELETED", Steps(steps));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_ThenOursDeletedInXeroAndAnotherKeyedByHandUnderTheNumber_IsRefused_AndTheHandOrderIsNeverLinkedOrDeleted()
    {
        // Xero keeps live purchase order numbers unique, so a second order under the number comes after ours was deleted.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ourOrder) = await LostOrderCreateAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ourOrder);
        var handEntered = SimulatorTestKit.PurchaseOrder(kit.SupplierContactId, "PO-2026-001");
        handEntered["PurchaseOrders"]![0]!["Reference"] = "Bookkeeper's own order";
        var handOrder = await XeroPurchaseOrderSyncTests.PutByHandAsync(kit.Simulator, "PurchaseOrders", handEntered);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("was deleted in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.OrderLinkAsync(id));

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        Assert.True(await kit.Outbox.RetryAsync(step.Entry.Id));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", handOrder)!.Status);
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    // ---- Verifier round 6 ----

    private static Task<string> HandBillAsync(PurchasingSyncTestKit kit, string number, decimal net, decimal vat)
    {
        var line = SimulatorTestKit.Line("Supplier invoice", 1m, net, "INPUT2", "310");
        line["TaxAmount"] = vat;
        return XeroPurchaseOrderSyncTests.PutByHandAsync(kit.Simulator, "Invoices", SimulatorTestKit.Invoice(kit.SupplierContactId, number, type: "ACCPAY", line: line));
    }

    /// <summary>Drains, retries whatever ended Failed, and drains again.</summary>
    private static async Task<List<PurchasingDrainStep>> DrainAndRetryAsync(PurchasingSyncTestKit kit)
    {
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = (await kit.DrainAsync()).ToList();
        foreach (var failed in await kit.Outbox.ListAsync([XeroOutboxState.Failed]))
            await kit.Outbox.RetryAsync(failed.Id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        steps.AddRange(await kit.DrainAsync());
        return steps;
    }

    [Fact]
    public async Task Po_LostCreate_ThenOursDeletedInXero_IsNeverResentAndLinkedAsCreated_ThenTheCancelEndsNothingToDo()
    {
        // A resend under the same Idempotency-Key would replay Xero's answer about the deleted order,
        // and TempestOS would link it as "created" and show the order as synced while it is deleted there.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ourOrder) = await LostOrderCreateAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ourOrder);

        kit.Clock.Advance(TimeSpan.FromMinutes(2)); // Within Xero's key lifetime (XeroPurchasingOwnership.IdempotencyKeyLifetime).
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("was deleted in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("does not send it again", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(step.Result.Link);
        Assert.Null(await kit.OrderLinkAsync(id));
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        Assert.Empty(kit.LiveOrders);

        // Retry says the same, and never links the deleted order.
        Assert.True(await kit.Outbox.RetryAsync(step.Entry.Id));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Null(await kit.OrderLinkAsync(id));

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        var steps = await DrainAndRetryAsync(kit);

        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.NothingToDo, s.Result.Outcome));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Empty(kit.LiveOrders);
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenOursDeletedInXero_IsNeverResentAndLinkedAsCreated_ThenTheDeleteEndsNothingToDo()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ourBill) = await LostBillCreateAsync(kit, "NS-5");
        kit.Simulator.DeleteInXero("Invoices", ourBill);

        kit.Clock.Advance(TimeSpan.FromMinutes(2)); // Within Xero's key lifetime (XeroPurchasingOwnership.IdempotencyKeyLifetime).
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("was deleted in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        Assert.Single(kit.Bills);
        Assert.Empty(kit.LiveBills);

        // Amending the expense sends it as a new bill (a new entry, a new key): never the deleted one.
        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { NetAmount = 110m, VatAmount = 22m };
        await kit.PlanExpenseAsync(id);
        await DrainAndRetryAsync(kit);
        var link = await kit.ExpenseLinkAsync(id);
        Assert.NotNull(link);
        Assert.NotEqual(ourBill, link.XeroId);
        Assert.Equal("DRAFT", kit.Simulator.Find("Invoices", link.XeroId)!.Status);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        await DrainAndRetryAsync(kit);
        Assert.Empty(kit.LiveBills);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_ThenOurDateEditedAndDeletedInXero_ThenAnExactCopyKeyed_TheCancelLeavesTheCopy()
    {
        // The order date is free text a bookkeeper may edit: ours is known by its id (the key's replay),
        // never by its date or amounts, so the exact copy beside it is never called ours.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ourOrder) = await LostOrderCreateAsync(kit);
        kit.Simulator.SetDateInXero("PurchaseOrders", ourOrder, new DateOnly(2026, 10, 9));
        kit.Simulator.DeleteInXero("PurchaseOrders", ourOrder);
        var copy = SimulatorTestKit.PurchaseOrder(
            kit.SupplierContactId, "PO-2026-001", line: SimulatorTestKit.Line("Steel plate and freight", 1m, 575m, "INPUT2", "310"));
        var handOrder = await XeroPurchaseOrderSyncTests.PutByHandAsync(kit.Simulator, "PurchaseOrders", copy);
        var handBody = kit.Simulator.Find("PurchaseOrders", handOrder)!.Body.ToJsonString();

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        var steps = await DrainAndRetryAsync(kit);

        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.NothingToDo, s.Result.Outcome));
        Assert.Contains(steps, s => s.Result.Reason?.Contains("was already deleted in Xero", StringComparison.Ordinal) == true);
        Assert.Equal(handBody, kit.Simulator.Find("PurchaseOrders", handOrder)!.Body.ToJsonString());
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_TwoSentAmounts_TheOlderDeleted_TheNewerOursAndLive_TheDeleteRemovesOurs_AndLeavesTheHandBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, first) = await LostBillCreateAsync(kit, "NS-5");
        kit.Simulator.DeleteInXero("Invoices", first);

        // Amended to 150/30 (a new entry, a new key); that create's answer is lost too.
        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { NetAmount = 150m, VatAmount = 30m };
        await kit.PlanExpenseAsync(id);
        kit.LostNew.LoseNewWrites = 1; // The new create's answer, not the replay of the first.
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();
        var second = Assert.Single(kit.LiveBills).Id;

        // Someone keys a bill with the first amounts under the number.
        var hand = await HandBillAsync(kit, "NS-5", 100m, 20m);
        var handBody = kit.Simulator.Find("Invoices", hand)!.Body.ToJsonString();

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        var steps = await DrainAndRetryAsync(kit);

        Assert.True(kit.Simulator.Find("Invoices", second)!.Status == "DELETED", Steps(steps));
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", hand)!.Body.ToJsonString());
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_AnotherExpensesUnlinkedBillUnderTheNumber_IsNeverCalledKeyedByHand_AndBothLinkOnRetry()
    {
        // Two expenses carry one supplier invoice number. A's create is lost, so its bill is not linked yet.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (a, aBill) = await LostBillCreateAsync(kit, "NS-5");
        var b = Guid.NewGuid();
        kit.FakeExpenses[b] = PurchasingSyncTestKit.Expense(b, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-5");
        await kit.PlanExpenseAsync(b);

        var first = Assert.Single(await kit.DrainAsync(), s => s.Entry.Document == PurchasingSyncTestKit.ExpenseRef(b));
        Assert.Equal(XeroPushOutcome.Rejected, first.Result.Outcome);
        Assert.Contains("TempestOS sent for another expense", first.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("Retry", first.Result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("did not send", first.Result.Reason, StringComparison.Ordinal);

        await DrainAndRetryAsync(kit);
        Assert.Equal(aBill, (await kit.ExpenseLinkAsync(a))!.XeroId);
        var bLink = await kit.ExpenseLinkAsync(b);
        Assert.NotNull(bLink);
        Assert.NotEqual(aBill, bLink.XeroId);
        Assert.Equal(2, kit.LiveBills.Count);
        kit.AssertNoViolations();
    }

    // ---- Verifier round 7: identity by key replay and read-back, never by matching ----

    /// <summary>The bookkeeper edits a record in Xero (<c>POST {resource}/{id}</c>), as Xero's own screens would.</summary>
    private static async Task EditInXeroAsync(PurchasingSyncTestKit kit, string resource, string id, JsonObject fields)
    {
        using var client = kit.Simulator.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{resource}/{id}")
        {
            Content = new StringContent(new JsonObject { [resource] = new JsonArray { fields } }.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", kit.Simulator.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", kit.Simulator.Options.TenantId);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"by-hand:{Guid.NewGuid():N}");
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertDeletedInXeroRejectedAsync(PurchasingSyncTestKit kit, string resource, string ours, Func<Task<XeroLink?>> link, string act = "deleted")
    {
        kit.Clock.Advance(TimeSpan.FromMinutes(2)); // Within Xero's key lifetime (XeroPurchasingOwnership.IdempotencyKeyLifetime).
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains($"was {act} in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("does not send it again", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(step.Result.Link);
        Assert.Null(await link());
        Assert.Single(kit.Simulator.All(resource));
        Assert.NotEqual("DRAFT", kit.Simulator.Find(resource, ours)!.Status);

        // Retry says the same, and never links it.
        Assert.True(await kit.Outbox.RetryAsync(step.Entry.Id));
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Null(await link());
        Assert.Single(kit.Simulator.All(resource));
    }

    [Fact]
    public async Task Po_LostCreate_ThenItsAmountsEditedThenDeletedInXero_IsNeverLinked_PushRejected_CancelNothingToDo()
    {
        // Probe A: the deleted order no longer carries what was sent; its id (the key's replay) still says it is ours.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        await EditInXeroAsync(kit, "PurchaseOrders", ours, new JsonObject
        {
            ["LineItems"] = new JsonArray { SimulatorTestKit.Line("Steel plate 10 mm", 10m, 55m, "INPUT2", "310") },
        });
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);

        await AssertDeletedInXeroRejectedAsync(kit, "PurchaseOrders", ours, () => kit.OrderLinkAsync(id));

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        var steps = await DrainAndRetryAsync(kit);
        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenItsAmountsEditedThenDeletedInXero_IsNeverLinked_PushRejected_DeleteNothingToDo()
    {
        // Probe B.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit, "NS-5");
        await EditInXeroAsync(kit, "Invoices", ours, new JsonObject { ["LineItems"] = new JsonArray { SimulatorTestKit.Line("Train fare", 1m, 90m, "INPUT2", "493") } });
        kit.Simulator.DeleteInXero("Invoices", ours);

        await AssertDeletedInXeroRejectedAsync(kit, "Invoices", ours, () => kit.ExpenseLinkAsync(id));

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        var steps = await DrainAndRetryAsync(kit);
        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenRenumberedThenDeletedInXero_IsNeverLinked_PushRejected()
    {
        // Probe C: nothing under NS-5 any more; the key's replay still answers ours, and its read-back says deleted.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit, "NS-5");
        await EditInXeroAsync(kit, "Invoices", ours, new JsonObject { ["InvoiceNumber"] = "NS-5X" });
        kit.Simulator.DeleteInXero("Invoices", ours);

        await AssertDeletedInXeroRejectedAsync(kit, "Invoices", ours, () => kit.ExpenseLinkAsync(id));
        Assert.Empty(kit.LiveBills);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenApprovedThenVoidedInXero_IsNeverLinked_PushRejected()
    {
        // Probe D.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit, "NS-5");
        kit.Simulator.ApproveInXero(ours);
        kit.Simulator.VoidInXero(ours);

        await AssertDeletedInXeroRejectedAsync(kit, "Invoices", ours, () => kit.ExpenseLinkAsync(id), act: "voided");
        Assert.Equal("VOIDED", kit.Simulator.Find("Invoices", ours)!.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_OursRenumberedThenDeleted_AnExactCopyKeyedUnderTheNumber_TheCancelLeavesTheCopy()
    {
        // Probe E (verifier item 2): the copy matches on number, contact and amounts; it is never ours, because
        // ours is known only by its id, and that one is deleted.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        await EditInXeroAsync(kit, "PurchaseOrders", ours, new JsonObject { ["PurchaseOrderNumber"] = "PO-2026-001-OLD" });
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        var copy = SimulatorTestKit.PurchaseOrder(kit.SupplierContactId, "PO-2026-001");
        copy["PurchaseOrders"]![0]!["Reference"] = "P0012";
        var lines = kit.Simulator.Find("PurchaseOrders", ours)!.Body["LineItems"]!.DeepClone().AsArray();
        foreach (var line in lines)
            line!.AsObject().Remove("LineItemID");
        copy["PurchaseOrders"]![0]!["LineItems"] = lines;
        var hand = await XeroPurchaseOrderSyncTests.PutByHandAsync(kit.Simulator, "PurchaseOrders", copy);
        var handBody = kit.Simulator.Find("PurchaseOrders", hand)!.Body.ToJsonString();

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        var steps = await DrainAndRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", hand)!.Status);
        Assert.Equal(handBody, kit.Simulator.Find("PurchaseOrders", hand)!.Body.ToJsonString());
        Assert.Null(await kit.OrderLinkAsync(id));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_OursRenumberedThenDeleted_AnExactCopyKeyedUnderTheNumber_ALivePushIsRejected_NeverLinksTheCopy()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        await EditInXeroAsync(kit, "PurchaseOrders", ours, new JsonObject { ["PurchaseOrderNumber"] = "PO-2026-001-OLD" });
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        var copy = SimulatorTestKit.PurchaseOrder(kit.SupplierContactId, "PO-2026-001", line: SimulatorTestKit.Line("Steel plate and freight", 1m, 575m, "INPUT2", "310"));
        var hand = await XeroPurchaseOrderSyncTests.PutByHandAsync(kit.Simulator, "PurchaseOrders", copy);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("PO-2026-001-OLD", step.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("was deleted in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.OrderLinkAsync(id));
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", hand)!.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_OursRenumberedThenDeleted_AnExactCopyKeyedUnderTheNumber_TheDeleteLeavesTheCopy()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit, "NS-5");
        await EditInXeroAsync(kit, "Invoices", ours, new JsonObject { ["InvoiceNumber"] = "NS-5X" });
        kit.Simulator.DeleteInXero("Invoices", ours);
        var hand = await HandBillAsync(kit, "NS-5", 100m, 20m);
        var handBody = kit.Simulator.Find("Invoices", hand)!.Body.ToJsonString();

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        var steps = await DrainAndRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Equal("DRAFT", kit.Simulator.Find("Invoices", hand)!.Status);
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", hand)!.Body.ToJsonString());
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_ThenRenumberedInXero_IsStillOursByItsId_TheCancelDeletesIt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        await EditInXeroAsync(kit, "PurchaseOrders", ours, new JsonObject { ["PurchaseOrderNumber"] = "PO-2026-001-B" });

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        var steps = await DrainAndRetryAsync(kit);

        Assert.True(kit.Simulator.Find("PurchaseOrders", ours)!.Status == "DELETED", Steps(steps));
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreate_IsRecoveredByReplayingItsOwnKey_WithTheSameBody_AndMakesNoSecondRecord()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        var creates = kit.WritesTo("PurchaseOrders").Where(w => w.Method == HttpMethod.Put).ToList();
        Assert.Equal(2, creates.Count);
        Assert.Equal(creates[0].IdempotencyKey, creates[1].IdempotencyKey);
        Assert.Equal(creates[0].JsonBody!.ToJsonString(), creates[1].JsonBody!.ToJsonString());
        Assert.Contains(kit.Simulator.Requests, r => r.Method == HttpMethod.Get && r.Path == $"PurchaseOrders/{ours}");
        Assert.Equal(ours, (await kit.OrderLinkAsync(id))!.XeroId);
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_ThenTheKeyExpired_XeroSaysTheNumberIsUsed_IsCannotTell_NothingLinkedSentOrDeleted()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        var body = kit.Simulator.Find("PurchaseOrders", ours)!.Body.ToJsonString();
        kit.Simulator.ForgetIdempotencyKeys();

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("cannot tell", step.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("must be unique", step.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("nothing is sent", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.OrderLinkAsync(id));

        // A cancel is refused the same way: it never deletes what it cannot tell is ours.
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        var steps = await DrainAndRetryAsync(kit);
        Assert.True(steps.Count > 0 && steps.All(s => s.Result.Outcome == XeroPushOutcome.Rejected), Steps(steps));
        Assert.All(steps, s => Assert.Contains("nothing is deleted", s.Result.Reason, StringComparison.Ordinal));

        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", ours)!.Status);
        Assert.Equal(body, kit.Simulator.Find("PurchaseOrders", ours)!.Body.ToJsonString());
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        Assert.Null(await kit.OrderLinkAsync(id));
        Assert.All(kit.Simulator.Violations, v => Assert.Equal(XeroSimulatorRules.Duplicate, v.Rule)); // Xero's own refusals of the replay only.
        Assert.DoesNotContain(kit.WritesTo("PurchaseOrders"), w => w.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Bill_ALoggedCreateWithNoCopyOfItsBody_IsCannotTell_NothingSentOrDeleted()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var hand = await HandBillAsync(kit, "NS-5");
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-5");
        var entry = Assert.Single(await kit.PlanExpenseAsync(id));
        await kit.Creates.RecordSendingAsync(PurchasingSyncTestKit.TenantId, entry.Document, "NS-5", kit.SupplierContactId, entry.IdempotencyKey);
        var writes = kit.WritesTo("Invoices").Count;

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("cannot tell", step.Result.Reason, StringComparison.Ordinal);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        var steps = await DrainAndRetryAsync(kit);
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Rejected, s.Result.Outcome));

        Assert.Equal(writes, kit.WritesTo("Invoices").Count);
        Assert.Equal("DRAFT", kit.Simulator.Find("Invoices", hand)!.Status);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }
}
