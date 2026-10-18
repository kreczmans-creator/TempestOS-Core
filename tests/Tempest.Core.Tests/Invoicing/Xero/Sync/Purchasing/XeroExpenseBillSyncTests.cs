using Tempest.Core.BusinessGovernance;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// `v0.24.0` X5 — expense bills to Xero (D5, Q3, Q4, Q6; design §3, §4.4,
/// §6.4), end to end over the S1 simulator: a recorded expense becomes an
/// <c>ACCPAY</c> <c>DRAFT</c> bill with its receipt, the category's account
/// code and the recorded VAT, against its supplier or the "General
/// expenses" contact, numbered with the supplier's invoice number or
/// <c>EXP-{id}</c>; amended and deleted only while Xero holds a draft; an
/// expense from a purchase order's lines is never billed twice; a lost
/// response is matched by number and contact. Every test asserts the
/// simulator's violation log is empty.
/// </summary>
public sealed class XeroExpenseBillSyncTests
{
    [Fact]
    public async Task AnExpenseWithNoSupplier_BecomesAnAccpayDraft_AgainstTheGeneralExpensesContact_NumberedExpId_WithItsReceipt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        var receipt = kit.Files.Store(PurchasingSyncTestKit.ExpenseRef(id), "jpeg bytes", "train-ticket.jpg", "image/jpeg");

        var queued = await kit.PlanExpenseAsync(id);
        Assert.Equal([XeroOperation.PushExpenseBill, XeroOperation.UploadAttachment], queued.Select(e => e.Operation));
        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        var bill = Assert.Single(kit.LiveBills);
        Assert.Equal("DRAFT", bill.Status);
        Assert.Equal($"EXP-{id:N}", bill.Number);
        Assert.Equal(kit.GeneralContactId, bill.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Equal("2026-10-01", bill.Body["DateString"]!.GetValue<string>()[..10]);
        Assert.Null(bill.Body["Reference"]);

        var line = Assert.Single(bill.Body["LineItems"]!.AsArray())!;
        Assert.Equal("P0012 · Train to Sheffield", line["Description"]!.GetValue<string>());
        Assert.Equal(1m, line["Quantity"]!.GetValue<decimal>());
        Assert.Equal(100m, line["UnitAmount"]!.GetValue<decimal>());
        Assert.Equal(XeroAccountCodeMap.DefaultExpenseAccountCode(ExpenseCategory.Travel), line["AccountCode"]!.GetValue<string>());
        Assert.Equal("INPUT2", line["TaxType"]!.GetValue<string>());
        Assert.Equal(20m, line["TaxAmount"]!.GetValue<decimal>());
        Assert.Equal(120m, bill.Body["Total"]!.GetValue<decimal>());

        var attachment = Assert.Single(bill.Attachments);
        Assert.Equal("train-ticket.jpg", attachment.FileName);
        Assert.False(attachment.IncludeOnline);

        var link = await kit.ExpenseLinkAsync(id);
        Assert.Equal(bill.Id, link!.XeroId);
        Assert.Equal(XeroPurchasingMapper.LinkedByCreated, link.LinkedBy);
        Assert.Equal(receipt.Sha256, link.AttachmentContentHash);

        var create = kit.WritesTo("Invoices").First(w => w.Method == HttpMethod.Put);
        var element = create.JsonBody!["Invoices"]![0]!;
        Assert.Equal("ACCPAY", element["Type"]!.GetValue<string>());
        Assert.Equal("DRAFT", element["Status"]!.GetValue<string>());
        Assert.Null(element["SentToContact"]);
        Assert.False(string.IsNullOrEmpty(create.IdempotencyKey));

        Assert.Empty(await kit.PlanExpenseAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnExpenseWithASupplierAndItsInvoiceNumber_IsBilledAgainstTheSupplier_UnderTheirNumber()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(
            id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-4471", net: 400m, vat: 80m,
            category: ExpenseCategory.Materials, description: "Steel offcuts");

        await kit.PlanExpenseAsync(id);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        var bill = Assert.Single(kit.LiveBills);
        Assert.Equal("NS-4471", bill.Number);
        Assert.Equal(kit.SupplierContactId, bill.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Equal("429", bill.Body["LineItems"]![0]!["AccountCode"]!.GetValue<string>());
        Assert.True(kit.Simulator.Find("Contacts", kit.SupplierContactId)!.Body["IsSupplier"]!.GetValue<bool>());
        kit.AssertNoViolations();
    }

    [Theory]
    [InlineData(100, 0, "NONE")]
    [InlineData(100, 5, "RRINPUT")]
    [InlineData(100, 20, "INPUT2")]
    [InlineData(99.99, 19.99, "INPUT2")]
    public async Task TheInputTaxType_FollowsTheReceiptsVat_AndTheRecordedVatIsKept(decimal net, decimal vat, string taxType)
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, net: net, vat: vat);

        await kit.PlanExpenseAsync(id);
        await kit.DrainAsync();

        var line = Assert.Single(kit.LiveBills).Body["LineItems"]![0]!;
        Assert.Equal(taxType, line["TaxType"]!.GetValue<string>());
        Assert.Equal(vat, line["TaxAmount"]!.GetValue<decimal>());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task NoGeneralExpensesContactChosen_BlocksTheBill_UntilOneIsChosen()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        var entry = Assert.Single(await kit.PlanExpenseAsync(id));

        var blocked = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Blocked, blocked.Result.Outcome);
        Assert.Contains("General expenses", blocked.Result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.Requests);

        await kit.GeneralContact.SetAsync(PurchasingSyncTestKit.GeneralReference);
        Assert.True(await kit.Outbox.RetryAsync(entry.Id));
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Single(kit.LiveBills);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ASupplierTempestOsDoesNotKnow_BlocksTheBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id) with { SupplierOrganisationIdUnresolved = "GONE1" };
        await kit.PlanExpenseAsync(id);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Blocked, step.Result.Outcome);
        Assert.Contains("GONE1", step.Result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.LiveBills);
    }

    [Fact]
    public async Task AnAmendedExpense_UpdatesTheDraftBill_InPlace()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        await kit.PlanExpenseAsync(id);
        await kit.DrainAsync();
        var billId = Assert.Single(kit.LiveBills).Id;

        // Amended: dearer, and the supplier's own invoice number entered (Q4).
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, net: 150m, vat: 30m, supplierInvoiceNumber: "GWR-88812");
        Assert.Equal(XeroOperation.PushExpenseBill, Assert.Single(await kit.PlanExpenseAsync(id)).Operation);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        var bill = Assert.Single(kit.LiveBills);
        Assert.Equal(billId, bill.Id);
        Assert.Equal("DRAFT", bill.Status);
        Assert.Equal("GWR-88812", bill.Number);
        Assert.Equal(150m, bill.Body["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Equal(30m, bill.Body["LineItems"]![0]!["TaxAmount"]!.GetValue<decimal>());

        var update = kit.WritesTo("Invoices").Last();
        Assert.Equal(HttpMethod.Post, update.Method);
        // `v0.24.0` review m1: a content update pins DRAFT, so a bill approved in Xero meanwhile is refused, never changed.
        Assert.Equal("DRAFT", update.JsonBody!["Invoices"]![0]!["Status"]!.GetValue<string>());
        Assert.Equal("GWR-88812", (await kit.ExpenseLinkAsync(id))!.XeroNumber);
        Assert.Empty(await kit.PlanExpenseAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnExpenseAmendedAfterItsBillWasApprovedInXero_IsRefused_AndTheBillIsUntouched()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        await kit.PlanExpenseAsync(id);
        await kit.DrainAsync();
        var billId = Assert.Single(kit.LiveBills).Id;
        kit.Simulator.ApproveInXero(billId);
        var writes = kit.WritesTo("Invoices").Count;

        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, net: 150m, vat: 30m);
        await kit.PlanExpenseAsync(id);
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("approved in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Contains("change the bill in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(writes, kit.WritesTo("Invoices").Count);
        Assert.Equal(100m, kit.Simulator.Find("Invoices", billId)!.Body["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ADeletedExpense_DeletesItsDraftBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        await kit.PlanExpenseAsync(id);
        await kit.DrainAsync();
        var billId = Assert.Single(kit.LiveBills).Id;

        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, deleted: true);
        Assert.Equal(XeroOperation.DeleteExpenseBill, Assert.Single(await kit.PlanExpenseAsync(id)).Operation);
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        Assert.Equal("DELETED", kit.Simulator.Find("Invoices", billId)!.Status);
        var delete = kit.WritesTo("Invoices").Last().JsonBody!["Invoices"]![0]!.AsObject();
        Assert.Equal(["InvoiceID", "Status"], delete.Select(p => p.Key).Order());
        Assert.Empty(await kit.PlanExpenseAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnExpenseDeletedAfterItsBillWasApproved_IsRefused_NeverVoidedByTempestOs()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        await kit.PlanExpenseAsync(id);
        await kit.DrainAsync();
        var billId = Assert.Single(kit.LiveBills).Id;
        kit.Simulator.ApproveInXero(billId);

        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, deleted: true);
        await kit.PlanExpenseAsync(id);
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("approved in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Equal("AUTHORISED", kit.Simulator.Find("Invoices", billId)!.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task TwoExpenses_SharingTheSuppliersInvoiceNumber_GetABillEach_AndDeletingOneLeavesTheOther()
    {
        // One receipt split across two categories: both expenses carry the supplier's invoice number.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var steel = Guid.NewGuid();
        var delivery = Guid.NewGuid();
        kit.FakeExpenses[steel] = PurchasingSyncTestKit.Expense(
            steel, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-1", net: 100m, vat: 20m,
            category: ExpenseCategory.Materials, description: "Steel");
        kit.FakeExpenses[delivery] = PurchasingSyncTestKit.Expense(
            delivery, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-1", net: 30m, vat: 6m,
            category: ExpenseCategory.Other, description: "Delivery");

        await kit.PlanExpenseAsync(steel);
        await kit.DrainAsync();
        await kit.PlanExpenseAsync(delivery);
        Assert.All(await kit.DrainAsync(), s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        var steelLink = await kit.ExpenseLinkAsync(steel);
        var deliveryLink = await kit.ExpenseLinkAsync(delivery);
        Assert.NotEqual(steelLink!.XeroId, deliveryLink!.XeroId);
        Assert.Equal(2, kit.LiveBills.Count);

        var steelBill = kit.Simulator.Find("Invoices", steelLink.XeroId)!;
        Assert.Equal("P0012 · Steel", steelBill.Body["LineItems"]![0]!["Description"]!.GetValue<string>());
        Assert.Equal(100m, steelBill.Body["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());

        // Deleting the delivery expense deletes its own bill only.
        kit.FakeExpenses[delivery] = kit.FakeExpenses[delivery] with { IsDeleted = true };
        await kit.PlanExpenseAsync(delivery);
        await kit.DrainAsync();
        var left = Assert.Single(kit.LiveBills);
        Assert.Equal(steelLink.XeroId, left.Id);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ADraftBillTypedIntoXeroByHand_UnderTheSuppliersNumber_IsRefused_NeverTakenOverOrOverwritten()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var handBillId = await XeroPurchaseOrderSyncTests.PutByHandAsync(
            kit.Simulator, "Invoices", SimulatorTestKit.Invoice(kit.SupplierContactId, "NS-9", type: "ACCPAY"));
        var before = kit.Simulator.Find("Invoices", handBillId)!.Body.ToJsonString();

        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(
            id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-9", net: 30m, vat: 6m, description: "Delivery");
        await kit.PlanExpenseAsync(id);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("already used in Xero by another bill", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        Assert.Equal(before, kit.Simulator.Find("Invoices", handBillId)!.Body.ToJsonString());
        Assert.Single(kit.LiveBills);

        // Nor does deleting the expense touch it.
        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        await kit.DrainAsync();
        Assert.Equal("DRAFT", kit.Simulator.Find("Invoices", handBillId)!.Status);
        kit.AssertNoViolations();
    }

    /// <summary>A hand bill "NS-9" for the supplier, and an expense under the same number whose first push was Rejected for it.</summary>
    private static async Task<(PurchasingSyncTestKit Kit, Guid ExpenseId, string HandBillId, string HandBody, XeroOutboxEntry Rejected)> RejectedForAHandBillAsync()
    {
        var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var handBillId = await XeroPurchaseOrderSyncTests.PutByHandAsync(
            kit.Simulator, "Invoices", SimulatorTestKit.Invoice(kit.SupplierContactId, "NS-9", type: "ACCPAY"));
        var handBody = kit.Simulator.Find("Invoices", handBillId)!.Body.ToJsonString();

        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(
            id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-9", net: 30m, vat: 6m, description: "Delivery");
        await kit.PlanExpenseAsync(id);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        return (kit, id, handBillId, handBody, step.Entry);
    }

    [Fact]
    public async Task Bill_RejectedThenAmended_DoesNotTakeOverHandBill()
    {
        // A Rejected push sent no create: its Failed (then Superseded) entry is no proof one reached Xero.
        var rejected = await RejectedForAHandBillAsync();
        using var kit = rejected.Kit;
        var (id, handBillId, handBody) = (rejected.ExpenseId, rejected.HandBillId, rejected.HandBody);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { NetAmount = 40m, VatAmount = 8m };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var step = Assert.Single(await kit.DrainAsync(), s => s.Entry.Operation == XeroOperation.PushExpenseBill);

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("already used in Xero by another bill", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBillId)!.Body.ToJsonString());
        Assert.Single(kit.WritesTo("Invoices")); // The hand bill's own PUT only.
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_RejectedThenAmendedThenDeleted_HandBillSurvives()
    {
        var rejected = await RejectedForAHandBillAsync();
        using var kit = rejected.Kit;
        var (id, handBillId, handBody) = (rejected.ExpenseId, rejected.HandBillId, rejected.HandBody);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { NetAmount = 40m, VatAmount = 8m };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        var hand = kit.Simulator.Find("Invoices", handBillId)!;
        Assert.Equal("DRAFT", hand.Status);
        Assert.Equal(handBody, hand.Body.ToJsonString());
        Assert.Null(await kit.ExpenseLinkAsync(id));
        Assert.Single(kit.WritesTo("Invoices")); // The hand bill's own PUT only.
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_RejectedThenRetried_DoesNotTakeOverHandBill()
    {
        // Retry makes the entry's second attempt; it still never sent a create.
        var rejected = await RejectedForAHandBillAsync();
        using var kit = rejected.Kit;
        var (id, handBillId, handBody) = (rejected.ExpenseId, rejected.HandBillId, rejected.HandBody);

        Assert.True(await kit.Outbox.RetryAsync(rejected.Rejected.Id));
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(2, step.Entry.Attempts);
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBillId)!.Body.ToJsonString());
        Assert.Single(kit.WritesTo("Invoices")); // The hand bill's own PUT only.
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_BlockedForAnUnlinkedSupplier_ThenLinkedAndRetried_DoesNotTakeOverHandBill()
    {
        // A Blocked push sent nothing at all to Xero.
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var newcoContactId = kit.Simulator.SeedContact("Newco Fixings Ltd");
        var handBillId = await XeroPurchaseOrderSyncTests.PutByHandAsync(
            kit.Simulator, "Invoices", SimulatorTestKit.Invoice(newcoContactId, "NF-1", type: "ACCPAY"));
        var handBody = kit.Simulator.Find("Invoices", handBillId)!.Body.ToJsonString();

        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.UnlinkedReference, supplierInvoiceNumber: "NF-1");
        await kit.PlanExpenseAsync(id);
        var blocked = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Blocked, blocked.Result.Outcome);

        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync(PurchasingSyncTestKit.UnlinkedReference, newcoContactId)).Outcome);
        Assert.True(await kit.Outbox.RetryAsync(blocked.Entry.Id));
        var step = Assert.Single(await kit.DrainAsync());

        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Null(await kit.ExpenseLinkAsync(id));
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBillId)!.Body.ToJsonString());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateUnderTheSuppliersNumber_ThenTheExpenseAmended_StillMakesOneBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-5");
        await kit.PlanExpenseAsync(id);

        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        Assert.Single(kit.LiveBills);

        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-5", net: 110m, vat: 22m);
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        var bill = Assert.Single(kit.LiveBills);
        Assert.Equal(bill.Id, (await kit.ExpenseLinkAsync(id))!.XeroId);
        Assert.Equal(110m, bill.Body["LineItems"]![0]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Empty(await kit.PlanExpenseAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateResponse_IsRecoveredByReplayingItsKey_AndMakesOneBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        await kit.PlanExpenseAsync(id);

        kit.Lost.LoseWrites = 1;
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Single(kit.LiveBills);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var mark = kit.Simulator.Requests.Count;
        var second = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Succeeded, second.Result.Outcome);

        // Recovery re-sends the create exactly, under its own key (Xero replays its first answer), and reads the id back.
        var first = Assert.Single(kit.Simulator.Requests.Take(mark), r => r.Method == HttpMethod.Put && r.Path == "Invoices");
        var replay = Assert.Single(kit.Simulator.Requests.Skip(mark), r => r.Method != HttpMethod.Get);
        Assert.Equal(first.IdempotencyKey, replay.IdempotencyKey);
        Assert.Equal(first.JsonBody!.ToJsonString(), replay.JsonBody!.ToJsonString());
        Assert.Contains(kit.Simulator.Requests.Skip(mark), r => r.Method == HttpMethod.Get && r.Path == $"Invoices/{Assert.Single(kit.Bills).Id}");

        var bill = Assert.Single(kit.LiveBills);
        var link = await kit.ExpenseLinkAsync(id);
        Assert.Equal(bill.Id, link!.XeroId);
        Assert.Equal(XeroPurchasingMapper.LinkedByReconciled, link.LinkedBy);
        Assert.Equal(second.Entry.ContentHash, link.LastPushedContentHash);
        Assert.Empty(await kit.PlanExpenseAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateResponse_ThenTheSuppliersNumberEntered_StillMakesOneBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        await kit.PlanExpenseAsync(id);

        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();

        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplierInvoiceNumber: "GWR-1");
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        var bill = Assert.Single(kit.LiveBills);
        Assert.Equal("GWR-1", bill.Number);
        Assert.Equal(bill.Id, (await kit.ExpenseLinkAsync(id))!.XeroId);
        Assert.Empty(await kit.PlanExpenseAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateUnderTheSuppliersNumber_ThenTheNumberChanged_RewritesThatBill_NeverASecond()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-5");
        await kit.PlanExpenseAsync(id);

        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        Assert.Equal("NS-5", Assert.Single(kit.LiveBills).Number);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { SupplierInvoiceNumber = "NS-6" };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.All(await kit.DrainAsync(), s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        var bill = Assert.Single(kit.LiveBills);
        Assert.Equal("NS-6", bill.Number);
        Assert.Equal(bill.Id, (await kit.ExpenseLinkAsync(id))!.XeroId);
        Assert.Empty(await kit.PlanExpenseAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateToTheSupplier_ThenTheSupplierRemoved_MovesThatBillToGeneralExpenses_NeverASecond()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference);
        await kit.PlanExpenseAsync(id);

        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        Assert.Single(kit.LiveBills);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { SupplierOrganisationReference = null };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.All(await kit.DrainAsync(), s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        var bill = Assert.Single(kit.LiveBills);
        Assert.Equal($"EXP-{id:N}", bill.Number);
        Assert.Equal(kit.GeneralContactId, bill.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Equal(bill.Id, (await kit.ExpenseLinkAsync(id))!.XeroId);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateUnderTheSuppliersNumber_ThenTheNumberChangedAndTheExpenseDeleted_DeletesThatBill()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-5");
        await kit.PlanExpenseAsync(id);

        kit.Lost.LoseWrites = 1;
        await kit.DrainAsync();
        var billId = Assert.Single(kit.LiveBills).Id;

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { SupplierInvoiceNumber = "NS-6" };
        await kit.PlanExpenseAsync(id);
        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        await kit.PlanExpenseAsync(id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();

        Assert.Empty(kit.LiveBills);
        Assert.Equal("DELETED", kit.Simulator.Find("Invoices", billId)!.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task DeletingAnExpenseRejectedForAHandBill_IsNothingToDo_AndSaysTheBillIsNotTempestOs()
    {
        // The Failed push holds the expense's queue; once it is retried, neither
        // it nor the delete behind it asks for a new number and a Retry.
        var rejected = await RejectedForAHandBillAsync();
        using var kit = rejected.Kit;
        var (id, handBillId, handBody) = (rejected.ExpenseId, rejected.HandBillId, rejected.HandBody);

        kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };
        Assert.Equal(XeroOperation.DeleteExpenseBill, Assert.Single(await kit.PlanExpenseAsync(id)).Operation);
        Assert.True(await kit.Outbox.RetryAsync(rejected.Rejected.Id));
        var steps = await kit.DrainAsync();

        Assert.Equal([XeroOperation.PushExpenseBill, XeroOperation.DeleteExpenseBill], steps.Select(s => s.Entry.Operation));
        Assert.All(steps, s =>
        {
            Assert.Equal(XeroPushOutcome.NothingToDo, s.Result.Outcome);
            Assert.Contains("the bill numbered NS-9 there is not TempestOS's, and is left as it is", s.Result.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain("Retry", s.Result.Reason, StringComparison.Ordinal);
        });
        Assert.Null(await kit.ExpenseLinkAsync(id));
        Assert.Equal(handBody, kit.Simulator.Find("Invoices", handBillId)!.Body.ToJsonString());
        Assert.Single(kit.WritesTo("Invoices")); // The hand bill's own PUT only.
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnExpenseRecordedFromAPurchaseOrdersLines_IsNotBilled_AndSaysWhy()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var orderId = Guid.NewGuid();
        kit.FakeOrders[orderId] = PurchasingSyncTestKit.Order(orderId, PurchaseOrderStatus.Received);
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, sourcePurchaseOrderId: orderId, category: ExpenseCategory.Materials);
        kit.Files.Store(PurchasingSyncTestKit.ExpenseRef(id), "receipt", "r.pdf");

        Assert.Empty(await kit.PlanExpenseAsync(id));
        Assert.Equal(0, await kit.ExpensePlanner.ScanAsync());

        var why = await kit.ExpensePlanner.DescribeNotPushedAsync(id);
        Assert.Contains("PO-2026-001", why, StringComparison.Ordinal);
        Assert.Contains("Copy to bill", why, StringComparison.Ordinal);
        Assert.False((await kit.ExpensePlanner.SendToXeroAsync(id)).Queued);
        Assert.Null(await kit.ExpensePlanner.DescribeNotPushedAsync(Guid.NewGuid()));

        // A push queued before the expense recorded its order (a race) sends nothing.
        var entry = await kit.Outbox.EnqueueAsync(XeroOperation.PushExpenseBill, PurchasingSyncTestKit.ExpenseRef(id), XeroPurchasingMapper.ContentHash(kit.FakeExpenses[id]));
        Assert.Equal(XeroPushOutcome.NothingToDo, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal(entry.Id, Assert.Single(await kit.Outbox.ListForDocumentAsync(PurchasingSyncTestKit.ExpenseRef(id))).Id);

        Assert.Empty(kit.Requests);
    }

    [Fact]
    public async Task AnExpenseDeletedBeforeItsQueuedCreateWasSent_NeverReachesXero()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        await kit.PlanExpenseAsync(id);

        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, deleted: true);
        Assert.Equal(XeroOperation.DeleteExpenseBill, Assert.Single(await kit.PlanExpenseAsync(id)).Operation);

        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.NothingToDo, s.Result.Outcome));
        Assert.Empty(kit.Bills);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnExpenseDeletedWithNothingQueued_PlansNothing()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, deleted: true);

        Assert.Empty(await kit.PlanExpenseAsync(id));
    }

    [Fact]
    public async Task AnExpenseRecordedBeforeSyncBegan_GoesOnlyWhenSentToXero()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(options: new XeroPurchasingPlannerOptions { AutomaticFromUtc = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero) });
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, recordedAt: new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

        Assert.Empty(await kit.PlanExpenseAsync(id));
        var request = await kit.ExpensePlanner.SendToXeroAsync(id);
        Assert.True(request.Queued);
        await kit.DrainAsync();
        Assert.Single(kit.LiveBills);

        var later = Guid.NewGuid();
        kit.FakeExpenses[later] = PurchasingSyncTestKit.Expense(later, recordedAt: new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
        Assert.Single(await kit.PlanExpenseAsync(later));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task TheDefaultAutomaticStart_IsRecordedOnce_AndKept()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(options: new XeroPurchasingPlannerOptions());
        var first = await kit.State.AutomaticFromAsync();
        kit.Clock.Advance(TimeSpan.FromDays(3));

        Assert.Equal(first, await kit.State.AutomaticFromAsync());
        Assert.Equal(first, await new XeroPurchasingSyncState(kit.Store, kit.Secrets, timeProvider: kit.Clock).AutomaticFromAsync());
    }

    [Fact]
    public async Task ABillDeletedInXero_IsNotRecreated()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        await kit.PlanExpenseAsync(id);
        await kit.DrainAsync();
        kit.Simulator.DeleteInXero("Invoices", Assert.Single(kit.LiveBills).Id);

        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, net: 120m, vat: 24m);
        await kit.PlanExpenseAsync(id);
        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("deleted in Xero", step.Result.Reason, StringComparison.Ordinal);

        // Recorded: nothing more is planned for it, and no second bill is made.
        Assert.Empty(await kit.PlanExpenseAsync(id));
        Assert.Empty(kit.LiveBills);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task TheHandlers_RefuseAnotherKindOfDocument()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var entry = await kit.Outbox.EnqueueAsync(XeroOperation.PushPurchaseOrder, XeroDocumentRef.For(XeroDocumentKind.PurchaseOrder, Guid.NewGuid()), "hash");

        Assert.Equal(XeroPushOutcome.Rejected, (await kit.BillHandler.PushAsync(PurchasingSyncTestKit.TenantId, entry)).Outcome);
        Assert.Equal(XeroPushOutcome.Rejected, (await kit.BillAttachments.PushAsync(PurchasingSyncTestKit.TenantId, entry)).Outcome);
        Assert.Equal(XeroDocumentKind.ExpenseBill, kit.BillHandler.DocumentKind);
        Assert.Equal(XeroDocumentKind.ExpenseBill, kit.BillAttachments.DocumentKind);
        Assert.Equal(XeroDocumentKind.ExpenseBill, kit.ExpensePlanner.Kind);
        Assert.Equal(ProjectExpense.CanonicalKind, kit.ExpensePlanner.CanonicalKind);
    }

    [Fact]
    public async Task AReceiptAddedLater_IsAttachedThen_AndAnUploadBeforeTheBillExistsWaits()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id);
        kit.Files.Store(PurchasingSyncTestKit.ExpenseRef(id), "pdf", "receipt.pdf");
        await kit.PlanExpenseAsync(id);

        // The bill is Blocked (no general contact); its upload waits behind it.
        var steps = await kit.DrainAsync();
        Assert.Equal(XeroPushOutcome.Blocked, Assert.Single(steps).Result.Outcome);

        await kit.GeneralContact.SetAsync(PurchasingSyncTestKit.GeneralReference);
        var failed = Assert.Single(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        await kit.Outbox.RetryAsync(failed.Id);
        var after = await kit.DrainAsync();
        Assert.Equal([XeroOperation.PushExpenseBill, XeroOperation.UploadAttachment], after.Select(s => s.Entry.Operation));
        Assert.Single(Assert.Single(kit.LiveBills).Attachments);
        kit.AssertNoViolations();
    }

    [Fact]
    public void TheBillNumber_IsTheSuppliersNumber_OrExpId()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"EXP-{id:N}", XeroPurchasingMapper.BillNumber(PurchasingSyncTestKit.Expense(id)));
        Assert.Equal($"EXP-{id:N}", XeroPurchasingMapper.BillNumber(PurchasingSyncTestKit.Expense(id, supplierInvoiceNumber: "  ")));
        Assert.Equal("INV 7", XeroPurchasingMapper.BillNumber(PurchasingSyncTestKit.Expense(id, supplierInvoiceNumber: " INV 7 ")));
        Assert.Equal(VatRate.OutOfScope, XeroPurchasingMapper.InferVatRate(0m, 0m));
        Assert.Equal(VatRate.Standard, XeroPurchasingMapper.InferVatRate(0m, 1m));
        Assert.NotEqual(
            XeroPurchasingMapper.ContentHash(PurchasingSyncTestKit.Expense(id)),
            XeroPurchasingMapper.ContentHash(PurchasingSyncTestKit.Expense(id, supplierInvoiceNumber: "X")));
    }

    [Fact]
    public async Task ABillNumberWithAComma_IsLookedUpWithAWhereFilter()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var found = await kit.Api.FindBillsAsync("A,B", kit.GeneralContactId);

        Assert.Equal(ConnectorOutcome.Ok, found.Outcome);
        Assert.Empty(found.Value!);
        var request = kit.Simulator.Requests.Last();
        Assert.Equal("InvoiceNumber==\"A,B\"", request.Query["where"]);
        Assert.False(request.Query.ContainsKey("InvoiceNumbers"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task TheBillsApi_RefusesAMalformedBill_BeforeSending()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var bill = new Core.Invoicing.Xero.Api.XeroWireBillWrite(
            "EXP-1", new Core.Invoicing.Xero.Api.XeroWireContactRef(kit.GeneralContactId), "2026-10-01", "GBP", "Exclusive", []);

        Assert.Equal(ConnectorOutcome.Rejected, (await kit.Api.CreateBillAsync(bill, "k1")).Outcome);
        Assert.Equal(ConnectorOutcome.Rejected, (await kit.Api.CreateBillAsync(bill with { InvoiceID = "x" }, "k2")).Outcome);
        Assert.Equal(ConnectorOutcome.Rejected, (await kit.Api.CreateBillAsync(bill with { InvoiceNumber = new string('9', 256) }, "k3")).Outcome);
        Assert.Empty(kit.Requests);
    }
}
