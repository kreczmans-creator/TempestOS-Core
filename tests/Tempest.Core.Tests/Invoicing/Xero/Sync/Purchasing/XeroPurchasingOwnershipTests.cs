using System.Text;
using System.Text.Json.Nodes;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// `v0.24.0` X5: the one purchasing ownership rule (<see cref="XeroPurchasingOwnership"/>)
/// on every path, the same for a purchase order and a bill. A Xero record is
/// TempestOS's only when linked, or when its number, contact and amounts match
/// a create TempestOS logged (deleted copies counted as evidence); only that
/// is ever linked, changed or deleted. Anything else is left untouched,
/// reported once, and the entry ends NothingToDo when the source is cancelled
/// or deleted, Rejected (Retry) for a live push — never Failed for good.
/// </summary>
public sealed class XeroPurchasingOwnershipTests
{
    private const string OrderNumber = "PO-2026-001";

    private static string Steps(IEnumerable<PurchasingDrainStep> steps) =>
        string.Join(" | ", steps.Select(s => $"{s.Entry.Operation} {s.Result.Outcome} {s.Result.Reason}"));

    // ------------------------------------------------------------ the rule itself

    private static XeroPurchasingSentCreate Sent(string key, string? value) => new("NS-5", "c1", key, null, value);

    private sealed record Doc(string Id, string? Value, bool Live);

    private static XeroOwnershipJudgement<Doc> Judge(IReadOnlyList<Doc> docs, params XeroPurchasingSentCreate[] sent) =>
        XeroPurchasingOwnership.Judge(docs, sent, d => d.Value, d => d.Live);

    [Fact]
    public void Rule_ADeletedCopyCarryingWhatWasSent_IsEvidence_SoALiveCopyBesideItIsAmbiguous()
    {
        var judged = Judge([new Doc("ours", "v", Live: false), new Doc("hand", "v", Live: true)], Sent("k1", "v"));
        Assert.Equal(XeroOwnershipVerdict.Ambiguous, judged.Verdict);
        Assert.Equal(2, judged.Count);
    }

    [Fact]
    public void Rule_AsManyCopiesAsCreatesSent_IsOurs_TheLiveOne()
    {
        var judged = Judge([new Doc("first", "v", Live: false), new Doc("second", "v", Live: true)], Sent("k1", "v"), Sent("k2", "v"));
        Assert.Equal(XeroOwnershipVerdict.Ours, judged.Verdict);
        Assert.Equal("second", judged.Ours!.Id);
    }

    [Fact]
    public void Rule_OtherAmounts_OrNoRecordedContent_AreNeverOurs()
    {
        Assert.Equal(XeroOwnershipVerdict.NotOurs, Judge([new Doc("hand", "w", Live: true)], Sent("k1", "v")).Verdict);
        Assert.Equal(XeroOwnershipVerdict.NotOurs, Judge([new Doc("hand", "v", Live: true)], Sent("k1", null)).Verdict);
        Assert.Equal(XeroOwnershipVerdict.NotOurs, Judge([new Doc("hand", "v", Live: true)]).Verdict);
        Assert.Equal(XeroOwnershipVerdict.NothingLive, Judge([new Doc("ours", "v", Live: false)], Sent("k1", "v")).Verdict);
    }

    [Fact]
    public void Rule_ComparesValueOnly_NeverTheFreeTextABookkeeperMayEdit()
    {
        var contact = new XeroWireContactRef("c1");
        var sentOrder = new XeroWirePurchaseOrderWrite(
            OrderNumber, "P0012", contact, "2026-10-02", "2026-10-16", "GBP", "Exclusive",
            [new XeroWireLineItem("Steel plate", 10m, 50m, "310", "INPUT2"), new XeroWireLineItem("Freight", 1m, 75m, "310", "ZERORATEDINPUT")]);
        var editedInXero = new XeroWirePurchaseOrder(
            "id", OrderNumber, "Bookkeeper's note", "DRAFT", contact, "2026-10-09", null, "GBP",
            [new XeroWireLineItem("Plate, 10mm", 10m, 50m, LineAmount: 500m), new XeroWireLineItem("Carriage", 1m, 75m, LineAmount: 75m)]);
        Assert.Equal(XeroPurchasingOwnership.ValueOf(sentOrder), XeroPurchasingOwnership.ValueOf(editedInXero));
        Assert.NotEqual(XeroPurchasingOwnership.ValueOf(sentOrder), XeroPurchasingOwnership.ValueOf(editedInXero with { SubTotal = 580m }));

        var sentBill = new XeroWireBillWrite("NS-5", contact, "2026-10-01", "GBP", "Exclusive", [new XeroWireLineItem("Train", 1m, 100m, "493", "INPUT2", TaxAmount: 20m)]);
        var bill = new XeroWireBill("id", "ACCPAY", "NS-5", "DRAFT", contact, "2026-10-05", CurrencyCode: "GBP", SubTotal: 100m, TotalTax: 20m);
        Assert.Equal(XeroPurchasingOwnership.ValueOf(sentBill), XeroPurchasingOwnership.ValueOf(bill));
        Assert.NotEqual(XeroPurchasingOwnership.ValueOf(sentBill), XeroPurchasingOwnership.ValueOf(bill with { TotalTax = 0m }));
        Assert.Null(XeroPurchasingOwnership.ValueOf(bill with { SubTotal = null }));
    }

    // ------------------------------------------------------------ helpers

    private static async Task<(Guid Id, string XeroId)> LostOrderCreateAsync(PurchasingSyncTestKit kit)
    {
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        kit.Lost.LoseWrites = 1;
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        return (id, Assert.Single(kit.LiveOrders).Id);
    }

    private static async Task<(Guid Id, string XeroId)> LostBillCreateAsync(PurchasingSyncTestKit kit, string number = "NS-5")
    {
        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: number);
        await kit.PlanExpenseAsync(id);
        kit.Lost.LoseWrites = 1;
        Assert.Equal(XeroPushOutcome.RetryLater, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        return (id, Assert.Single(kit.LiveBills).Id);
    }

    /// <summary>An order keyed into Xero by hand under <see cref="OrderNumber"/>: one line, <paramref name="net"/> net.</summary>
    private static Task<string> HandOrderAsync(PurchasingSyncTestKit kit, decimal net, string reference = "Bookkeeper's own order", string? contactId = null)
    {
        var body = SimulatorTestKit.PurchaseOrder(contactId ?? kit.SupplierContactId, OrderNumber, line: SimulatorTestKit.Line("Aluminium bar", 1m, net, "INPUT2", "310"));
        body["PurchaseOrders"]![0]!["Reference"] = reference;
        return XeroPurchaseOrderSyncTests.PutByHandAsync(kit.Simulator, "PurchaseOrders", body);
    }

    /// <summary>A bill keyed into Xero by hand for the supplier: one line, <paramref name="net"/> net and <paramref name="vat"/> VAT.</summary>
    private static Task<string> HandBillAsync(PurchasingSyncTestKit kit, string number, decimal net, decimal vat)
    {
        var line = SimulatorTestKit.Line("Supplier invoice", 1m, net, "INPUT2", "310");
        line["TaxAmount"] = vat;
        return XeroPurchaseOrderSyncTests.PutByHandAsync(kit.Simulator, "Invoices", SimulatorTestKit.Invoice(kit.SupplierContactId, number, type: "ACCPAY", line: line));
    }

    /// <summary>The bookkeeper edits a record in Xero (<c>POST {resource}/{id}</c>), as Xero's own screens would.</summary>
    private static async Task EditByHandAsync(PurchasingSyncTestKit kit, string resource, string id, JsonObject fields)
    {
        fields[resource == "Invoices" ? "InvoiceID" : "PurchaseOrderID"] = id;
        using var client = kit.Simulator.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{resource}/{id}?summarizeErrors=true")
        {
            Content = new StringContent(new JsonObject { [resource] = new JsonArray { fields } }.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", kit.Simulator.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", kit.Simulator.Options.TenantId);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"by-hand:{Guid.NewGuid():N}");
        using var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await SimulatorTestKit.ReadAsync(response)).Status);
    }

    private static string Body(PurchasingSyncTestKit kit, string resource, string id) => kit.Simulator.Find(resource, id)!.Body.ToJsonString();

    /// <summary>Drains, retries whatever Failed (as the user's Retry would), and drains again.</summary>
    private static async Task<List<PurchasingDrainStep>> DrainRetryAsync(PurchasingSyncTestKit kit)
    {
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var steps = (await kit.DrainAsync()).ToList();
        foreach (var failed in await kit.Outbox.ListAsync([XeroOutboxState.Failed]))
            await kit.Outbox.RetryAsync(failed.Id);
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        steps.AddRange(await kit.DrainAsync());
        return steps;
    }

    private static void AssertNoDestroyAdvice(IEnumerable<PurchasingDrainStep> steps) =>
        Assert.All(steps, s => Assert.DoesNotMatch("(?i)(delete|rename|renumber) (that|the extra|the other)", s.Result.Reason ?? string.Empty));

    private static void Cancel(PurchasingSyncTestKit kit, Guid id) => kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);

    private static void Delete(PurchasingSyncTestKit kit, Guid id) => kit.FakeExpenses[id] = kit.FakeExpenses[id] with { IsDeleted = true };

    // ------------------------------------------------------------ verifier defect 1: cancel/delete over a record that is not ours

    [Fact]
    public async Task Po_OursDeletedInXero_AHandOrderUnderTheNumber_ThenCancelled_EndsNothingToDo_HandOrderUntouched()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        var hand = await HandOrderAsync(kit, net: 100m);
        var handBody = Body(kit, "PurchaseOrders", hand);

        Cancel(kit, id);
        await kit.PlanOrderAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Contains(steps, s => s.Result.Reason?.Contains($"numbered {OrderNumber} there is not TempestOS's", StringComparison.Ordinal) == true);
        AssertNoDestroyAdvice(steps);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(handBody, Body(kit, "PurchaseOrders", hand));
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_OursDeletedInXero_AHandBillUnderTheNumber_ThenDeleted_EndsNothingToDo_HandBillUntouched()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit);
        kit.Simulator.DeleteInXero("Invoices", ours);
        var hand = await HandBillAsync(kit, "NS-5", net: 240m, vat: 48m);
        var handBody = Body(kit, "Invoices", hand);

        Delete(kit, id);
        await kit.PlanExpenseAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Contains(steps, s => s.Result.Reason?.Contains("numbered NS-5 there is not TempestOS's", StringComparison.Ordinal) == true);
        AssertNoDestroyAdvice(steps);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(handBody, Body(kit, "Invoices", hand));
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_OursDeletedInXero_AHandOrderUnderTheNumber_LivePush_IsRejectedWithRetry_NeverTellingTheUserToDestroyIt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        var hand = await HandOrderAsync(kit, net: 100m);
        var handBody = Body(kit, "PurchaseOrders", hand);

        var steps = await DrainRetryAsync(kit);

        Assert.Equal(2, steps.Count);
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Rejected, s.Result.Outcome));
        Assert.All(steps, s => Assert.Contains("Retry", s.Result.Reason, StringComparison.Ordinal));
        AssertNoDestroyAdvice(steps);
        Assert.Equal(handBody, Body(kit, "PurchaseOrders", hand));
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_OursDeletedInXero_AHandBillUnderTheNumber_LivePush_IsRejectedWithRetry_NeverTellingTheUserToDestroyIt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit);
        kit.Simulator.DeleteInXero("Invoices", ours);
        var hand = await HandBillAsync(kit, "NS-5", net: 240m, vat: 48m);
        var handBody = Body(kit, "Invoices", hand);

        var steps = await DrainRetryAsync(kit);

        Assert.Equal(2, steps.Count);
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Rejected, s.Result.Outcome));
        Assert.All(steps, s => Assert.Contains("Retry", s.Result.Reason, StringComparison.Ordinal));
        AssertNoDestroyAdvice(steps);
        Assert.Equal(handBody, Body(kit, "Invoices", hand));
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ verifier defect 2: re-keyed by hand with our reference

    [Fact]
    public async Task Po_OursDeletedInXero_ReKeyedByHandWithOurReference_ThenCancelled_IsNeverLinkedOrDeleted()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        var hand = await HandOrderAsync(kit, net: 100m, reference: "P0012");
        var handBody = Body(kit, "PurchaseOrders", hand);

        Cancel(kit, id);
        await kit.PlanOrderAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", hand)!.Status);
        Assert.Equal(handBody, Body(kit, "PurchaseOrders", hand));
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_OursDeletedInXero_AnExactCopyReKeyedByHand_IsAmbiguous_PushRejected_CancelEndsNothingToDo_CopyUntouched()
    {
        // Same number, contact, reference and amounts as ours: deleted ours is evidence, so two records carry what one create sent.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        kit.Simulator.DeleteInXero("PurchaseOrders", ours);
        var hand = await HandOrderAsync(kit, net: 575m, reference: "P0012");
        var handBody = Body(kit, "PurchaseOrders", hand);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var push = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, push.Result.Outcome);
        Assert.Contains("cannot tell which", push.Result.Reason, StringComparison.Ordinal);
        AssertNoDestroyAdvice([push]);

        Cancel(kit, id);
        await kit.PlanOrderAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(handBody, Body(kit, "PurchaseOrders", hand));
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_OursDeletedInXero_AnExactCopyKeyedByHand_IsAmbiguous_PushRejected_DeleteEndsNothingToDo_CopyUntouched()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit);
        kit.Simulator.DeleteInXero("Invoices", ours);
        var hand = await HandBillAsync(kit, "NS-5", net: 100m, vat: 20m);
        var handBody = Body(kit, "Invoices", hand);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var push = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, push.Result.Outcome);
        Assert.Contains("cannot tell which", push.Result.Reason, StringComparison.Ordinal);
        AssertNoDestroyAdvice([push]);

        Delete(kit, id);
        await kit.PlanExpenseAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(handBody, Body(kit, "Invoices", hand));
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_AndAnExactCopyKeyedByHand_BothLive_IsAmbiguous_DeleteEndsNothingToDo_BothUntouched()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit);
        var hand = await HandBillAsync(kit, "NS-5", net: 100m, vat: 20m);
        var (ourBody, handBody) = (Body(kit, "Invoices", ours), Body(kit, "Invoices", hand));

        Delete(kit, id);
        await kit.PlanExpenseAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Contains(steps, s => s.Result.Reason?.Contains("cannot tell which", StringComparison.Ordinal) == true);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(ourBody, Body(kit, "Invoices", ours));
        Assert.Equal(handBody, Body(kit, "Invoices", hand));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_AndAHandBillWithOtherAmountsUnderTheNumber_LinksOurs_DeleteDeletesOursOnly()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit);
        var hand = await HandBillAsync(kit, "NS-5", net: 240m, vat: 48m);
        var handBody = Body(kit, "Invoices", hand);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal(ours, (await kit.ExpenseLinkAsync(id))!.XeroId);

        Delete(kit, id);
        await kit.PlanExpenseAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.Equal("DELETED", kit.Simulator.Find("Invoices", ours)!.Status);
        Assert.Equal(handBody, Body(kit, "Invoices", hand));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.True(steps.All(s => s.Result.Outcome is XeroPushOutcome.Succeeded or XeroPushOutcome.NothingToDo), Steps(steps));
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ verifier defect 3: two TempestOS copies under one number

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bill_OursDeletedInXero_ThenAmendedAndLostAgain_LinksTheLiveOne_AndADeleteLeavesNoneLive(bool amountsChanged)
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, first) = await LostBillCreateAsync(kit);
        kit.Simulator.DeleteInXero("Invoices", first);

        kit.FakeExpenses[id] = amountsChanged
            ? kit.FakeExpenses[id] with { NetAmount = 150m, VatAmount = 30m }
            : kit.FakeExpenses[id] with { Description = "Train to Leeds" };
        await kit.PlanExpenseAsync(id);
        kit.Lost.LoseWrites = 1;
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        await kit.DrainAsync();
        var second = Assert.Single(kit.LiveBills).Id;

        var pushed = await DrainRetryAsync(kit);
        Assert.True(pushed.All(s => s.Result.Outcome is XeroPushOutcome.Succeeded or XeroPushOutcome.NothingToDo), Steps(pushed));
        Assert.Equal(second, (await kit.ExpenseLinkAsync(id))!.XeroId);

        Delete(kit, id);
        await kit.PlanExpenseAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(kit.LiveBills.Count == 0, Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ edited in Xero after a lost create

    [Fact]
    public async Task Po_LostCreate_ThenItsAmountsChangedInXero_IsNoLongerProvablyOurs_PushRejected_CancelLeavesIt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        await EditByHandAsync(kit, "PurchaseOrders", ours, new JsonObject { ["LineItems"] = new JsonArray { SimulatorTestKit.Line("Steel plate 10 mm", 12m, 50m, "INPUT2", "310") } });
        var edited = Body(kit, "PurchaseOrders", ours);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        Cancel(kit, id);
        await kit.PlanOrderAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(edited, Body(kit, "PurchaseOrders", ours));
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenItsAmountsChangedInXero_IsNoLongerProvablyOurs_PushRejected_DeleteLeavesIt()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit);
        var line = SimulatorTestKit.Line("Train to Sheffield", 1m, 110m, "INPUT2", "493");
        line["TaxAmount"] = 22m;
        await EditByHandAsync(kit, "Invoices", ours, new JsonObject { ["LineItems"] = new JsonArray { line } });
        var edited = Body(kit, "Invoices", ours);

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        Delete(kit, id);
        await kit.PlanExpenseAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(edited, Body(kit, "Invoices", ours));
        Assert.Null(await kit.ExpenseLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_LostCreate_ThenItsFreeTextChangedInXero_IsStillOurs_LinkedAndDeletedByACancel()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var (id, ours) = await LostOrderCreateAsync(kit);
        await EditByHandAsync(kit, "PurchaseOrders", ours, new JsonObject { ["Reference"] = "Bookkeeper's note", ["DeliveryDate"] = "2026-10-30" });

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal(ours, (await kit.OrderLinkAsync(id))!.XeroId);

        Cancel(kit, id);
        await kit.PlanOrderAsync(id);
        await DrainRetryAsync(kit);

        Assert.Equal("DELETED", kit.Simulator.Find("PurchaseOrders", ours)!.Status);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_LostCreate_ThenItsFreeTextChangedInXero_IsStillOurs_LinkedAndDeletedWithTheExpense()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var (id, ours) = await LostBillCreateAsync(kit);
        var line = SimulatorTestKit.Line("Rail fare, Sheffield (per receipt)", 1m, 100m, "INPUT2", "493");
        line["TaxAmount"] = 20m;
        await EditByHandAsync(kit, "Invoices", ours, new JsonObject { ["LineItems"] = new JsonArray { line }, ["DueDate"] = "2026-11-30" });

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(XeroPushOutcome.Succeeded, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Equal(ours, (await kit.ExpenseLinkAsync(id))!.XeroId);

        Delete(kit, id);
        await kit.PlanExpenseAsync(id);
        await DrainRetryAsync(kit);

        Assert.Equal("DELETED", kit.Simulator.Find("Invoices", ours)!.Status);
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        kit.AssertNoViolations();
    }

    // ------------------------------------------------------------ never sent: a record keyed by hand under our number

    [Fact]
    public async Task Po_NeverSent_AHandOrderForAnotherContactUnderTheNumber_PushRejected_CancelEndsNothingToDo()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var other = kit.Simulator.SeedContact("Somebody Else Ltd");
        var hand = await HandOrderAsync(kit, net: 575m, reference: "P0012", contactId: other);
        var handBody = Body(kit, "PurchaseOrders", hand);

        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        Cancel(kit, id);
        await kit.PlanOrderAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(handBody, Body(kit, "PurchaseOrders", hand));
        Assert.Single(kit.WritesTo("PurchaseOrders"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Bill_NeverSent_AnExactCopyKeyedByHandUnderTheNumber_PushRejected_DeleteEndsNothingToDo()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(chooseGeneralContact: false);
        var hand = await HandBillAsync(kit, "NS-5", net: 100m, vat: 20m);
        var handBody = Body(kit, "Invoices", hand);

        var id = Guid.NewGuid();
        kit.FakeExpenses[id] = PurchasingSyncTestKit.Expense(id, supplier: PurchasingSyncTestKit.SupplierReference, supplierInvoiceNumber: "NS-5");
        await kit.PlanExpenseAsync(id);
        var push = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, push.Result.Outcome);
        AssertNoDestroyAdvice([push]);

        Delete(kit, id);
        await kit.PlanExpenseAsync(id);
        var steps = await DrainRetryAsync(kit);

        Assert.True(steps.All(s => s.Result.Outcome == XeroPushOutcome.NothingToDo), Steps(steps));
        Assert.Empty(await kit.Outbox.ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(handBody, Body(kit, "Invoices", hand));
        Assert.Single(kit.WritesTo("Invoices"));
        kit.AssertNoViolations();
    }
}
