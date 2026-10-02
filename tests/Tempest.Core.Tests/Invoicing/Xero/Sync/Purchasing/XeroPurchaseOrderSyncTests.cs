using System.Text;
using System.Text.Json.Nodes;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// `v0.24.0` X5 — purchase orders to Xero (D5, Q2; design §3, §4.3, §6.4),
/// end to end over the S1 simulator: an issued TempestOS order becomes a
/// Xero <c>DRAFT</c> purchase order with the same number, supplier contact,
/// lines and PDF; a cancelled one is deleted (refused once billed in Xero);
/// a lost response never makes a second order. Every test asserts the
/// simulator's violation log is empty.
/// </summary>
public sealed class XeroPurchaseOrderSyncTests
{
    [Fact]
    public async Task AnIssuedOrder_BecomesADraftXeroPurchaseOrder_WithTheSameNumberSupplierLinesAndPdf()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        var pdf = kit.Files.Store(PurchasingSyncTestKit.OrderRef(id), "PO-2026-001 sheet", "issued.pdf");

        var queued = await kit.PlanOrderAsync(id);
        Assert.Equal([XeroOperation.PushPurchaseOrder, XeroOperation.UploadAttachment], queued.Select(e => e.Operation));

        var steps = await kit.DrainAsync();
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.Succeeded, s.Result.Outcome));

        var order = Assert.Single(kit.LiveOrders);
        Assert.Equal("DRAFT", order.Status);
        Assert.Equal("PO-2026-001", order.Number);
        Assert.Equal("P0012", order.Body["Reference"]!.GetValue<string>());
        Assert.Equal(kit.SupplierContactId, order.Body["Contact"]!["ContactID"]!.GetValue<string>());
        Assert.Equal("GBP", order.Body["CurrencyCode"]!.GetValue<string>());
        Assert.Equal("2026-10-02", order.Body["DateString"]!.GetValue<string>()[..10]);
        Assert.Equal("2026-10-16", order.Body["DeliveryDateString"]!.GetValue<string>()[..10]);

        var lines = order.Body["LineItems"]!.AsArray();
        Assert.Equal(2, lines.Count);
        Assert.Equal("Steel plate 10 mm", lines[0]!["Description"]!.GetValue<string>());
        Assert.Equal(10m, lines[0]!["Quantity"]!.GetValue<decimal>());
        Assert.Equal(50m, lines[0]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Equal("INPUT2", lines[0]!["TaxType"]!.GetValue<string>());
        Assert.Equal(XeroAccountCodeMap.DefaultExpenseAccountCode(ExpenseCategory.Materials), lines[0]!["AccountCode"]!.GetValue<string>());
        Assert.Equal("ZERORATEDINPUT", lines[1]!["TaxType"]!.GetValue<string>());

        var attachment = Assert.Single(order.Attachments);
        Assert.Equal("PO-2026-001.pdf", attachment.FileName);
        Assert.Equal(pdf.Content.Length, attachment.Length);

        var link = await kit.OrderLinkAsync(id);
        Assert.NotNull(link);
        Assert.Equal(order.Id, link.XeroId);
        Assert.Equal("PO-2026-001", link.XeroNumber);
        Assert.Equal(XeroPurchasingMapper.LinkedByCreated, link.LinkedBy);
        Assert.Equal("DRAFT", link.LastKnownXeroStatus);
        Assert.Equal(pdf.Sha256, link.AttachmentContentHash);

        // Every write carried an Idempotency-Key; the create never carried a non-DRAFT status or SentToContact.
        Assert.All(kit.WritesTo("PurchaseOrders"), w => Assert.False(string.IsNullOrEmpty(w.IdempotencyKey)));
        var create = kit.WritesTo("PurchaseOrders").First(w => w.Method == HttpMethod.Put && w.Path == "PurchaseOrders");
        Assert.Equal("DRAFT", create.JsonBody!["PurchaseOrders"]![0]!["Status"]!.GetValue<string>());
        Assert.Null(create.JsonBody["PurchaseOrders"]![0]!["SentToContact"]);

        // Nothing more to do: the order and its PDF match.
        Assert.Empty(await kit.PlanOrderAsync(id));
        Assert.Contains(kit.Audit.Rows, r => r.Action == "xero.link.created");
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ADraftOrder_PlansNothing()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Draft);

        Assert.Empty(await kit.PlanOrderAsync(id));
        Assert.Empty(await kit.PlanOrderAsync(Guid.NewGuid()));
        Assert.Empty(kit.Requests);
    }

    [Fact]
    public async Task ReceivedAndClosed_ChangeNothingInXero_AndTheLinesAreNeverResent()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        await kit.DrainAsync();
        var mark = kit.Simulator.Requests.Count;

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Received);
        Assert.Empty(await kit.PlanOrderAsync(id));
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Closed);
        Assert.Empty(await kit.PlanOrderAsync(id));

        Assert.Equal(mark, kit.Simulator.Requests.Count);
        Assert.Equal("DRAFT", Assert.Single(kit.LiveOrders).Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ACancelledOrder_IsDeletedInXero_Once()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        await kit.DrainAsync();
        var xeroId = Assert.Single(kit.LiveOrders).Id;

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        var queued = await kit.PlanOrderAsync(id);
        Assert.Equal(XeroOperation.DeletePurchaseOrder, Assert.Single(queued).Operation);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Succeeded, step.Result.Outcome);
        Assert.Equal("DELETED", kit.Simulator.Find("PurchaseOrders", xeroId)!.Status);
        Assert.Equal("DELETED", (await kit.OrderLinkAsync(id))!.LastKnownXeroStatus);

        // The delete carried only the id and DELETED — never a line.
        var delete = kit.WritesTo("PurchaseOrders").Last();
        var element = delete.JsonBody!["PurchaseOrders"]![0]!.AsObject();
        Assert.Equal(["PurchaseOrderID", "Status"], element.Select(p => p.Key).Order());
        Assert.Equal("DELETED", element["Status"]!.GetValue<string>());

        Assert.Empty(await kit.PlanOrderAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ACancelledOrder_BilledInXero_IsRefusedWithTheReason_AndXeroIsNeverAsked()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        await kit.DrainAsync();
        var xeroId = Assert.Single(kit.LiveOrders).Id;

        // The Product Owner approves it and uses "Copy to bill" in Xero (Q6).
        kit.Simulator.ApproveInXero(xeroId);
        kit.Simulator.BillPurchaseOrderInXero(xeroId);

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        var writes = kit.WritesTo("PurchaseOrders").Count;

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("billed in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Equal(writes, kit.WritesTo("PurchaseOrders").Count);
        Assert.Equal("BILLED", kit.Simulator.Find("PurchaseOrders", xeroId)!.Status);
        Assert.Equal("BILLED", (await kit.OrderLinkAsync(id))!.LastKnownXeroStatus);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnOrderAlreadyDeletedInXero_IsRecorded_NotDeletedAgain()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        await kit.DrainAsync();
        var xeroId = Assert.Single(kit.LiveOrders).Id;
        kit.Simulator.DeleteInXero("PurchaseOrders", xeroId);

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        var writes = kit.WritesTo("PurchaseOrders").Count;

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.NothingToDo, step.Result.Outcome);
        Assert.Equal(writes, kit.WritesTo("PurchaseOrders").Count);
        Assert.Empty(await kit.PlanOrderAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ALostCreateResponse_IsReconciledByNumber_AndMakesOneOrder()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);

        kit.Lost.LoseWrites = 1;
        var first = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.RetryLater, first.Result.Outcome);
        Assert.Single(kit.LiveOrders);
        Assert.Null(await kit.OrderLinkAsync(id));

        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Succeeded, second.Result.Outcome);
        Assert.Equal(2, second.Entry.Attempts);

        var order = Assert.Single(kit.LiveOrders);
        var link = await kit.OrderLinkAsync(id);
        Assert.Equal(order.Id, link!.XeroId);
        Assert.Equal(XeroPurchasingMapper.LinkedByReconciled, link.LinkedBy);
        Assert.Equal(second.Entry.ContentHash, link.LastPushedContentHash);
        Assert.Single(kit.WritesTo("PurchaseOrders"), w => w.Method == HttpMethod.Put);
        Assert.Contains(kit.Audit.Rows, r => r.Action == "xero.link.reconciled");
        Assert.Empty(await kit.PlanOrderAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ANumberAlreadyUsedInXeroForAnotherContact_IsRefused_NeverDuplicated()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var otherContact = kit.Simulator.SeedContact("Somebody Else Ltd");
        await PutByHandAsync(kit.Simulator, "PurchaseOrders", SimulatorTestKit.PurchaseOrder(otherContact, "PO-2026-001"));

        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("already used in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Single(kit.LiveOrders);
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnOrderInXero_WithTheNumberSupplierAndOurProjectReference_IsLinked_NotDuplicated()
    {
        // §6.4 item 4: "found with our reference → link". The simulator's order carries Reference "P0012", the project code.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        await PutByHandAsync(kit.Simulator, "PurchaseOrders", SimulatorTestKit.PurchaseOrder(kit.SupplierContactId, "PO-2026-001"));

        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Succeeded, step.Result.Outcome);
        Assert.Single(kit.LiveOrders);
        Assert.Equal(XeroPurchasingMapper.LinkedByReconciled, (await kit.OrderLinkAsync(id))!.LinkedBy);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnOrderEnteredByHandInXero_ForTheSameSupplier_WithoutOurReference_IsRefused_AndNeverDeletedByACancel()
    {
        // §6.4 item 4: "found otherwise → Failed". Xero numbers its own orders PO-…, so a clash is likely.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var handEntered = SimulatorTestKit.PurchaseOrder(kit.SupplierContactId, "PO-2026-001");
        handEntered["PurchaseOrders"]![0]!["Reference"] = "Bookkeeper's own order";
        var handId = await PutByHandAsync(kit.Simulator, "PurchaseOrders", handEntered);

        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Rejected, step.Result.Outcome);
        Assert.Contains("PO-2026-001 is already used in Xero", step.Result.Reason, StringComparison.Ordinal);
        Assert.Null(await kit.OrderLinkAsync(id));
        Assert.Equal(handId, Assert.Single(kit.LiveOrders).Id);

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        await kit.DrainAsync();
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", handId)!.Status);
        Assert.Null(await kit.OrderLinkAsync(id));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_RejectedThenRetried_DoesNotTakeOverHandPo()
    {
        // Retry makes a second attempt of an entry that never sent a create: no proof the hand order is ours.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var handEntered = SimulatorTestKit.PurchaseOrder(kit.SupplierContactId, "PO-2026-001");
        handEntered["PurchaseOrders"]![0]!["Reference"] = "Bookkeeper's own order";
        var handId = await PutByHandAsync(kit.Simulator, "PurchaseOrders", handEntered);

        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        var entry = Assert.Single(await kit.PlanOrderAsync(id));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        Assert.True(await kit.Outbox.RetryAsync(entry.Id));
        var retried = Assert.Single(await kit.DrainAsync());
        Assert.Equal(2, retried.Entry.Attempts);
        Assert.Equal(XeroPushOutcome.Rejected, retried.Result.Outcome);
        Assert.Null(await kit.OrderLinkAsync(id));

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        await kit.PlanOrderAsync(id);
        await kit.DrainAsync();
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", handId)!.Status);
        Assert.Single(kit.WritesTo("PurchaseOrders")); // The hand order's own PUT only.
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task Po_BlockedForAnUnlinkedSupplier_ThenLinkedAndRetried_DoesNotTakeOverHandPo()
    {
        // A Blocked push sent nothing to Xero; its retry is the entry's second attempt.
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var newcoContactId = kit.Simulator.SeedContact("Newco Fixings Ltd");
        var handEntered = SimulatorTestKit.PurchaseOrder(newcoContactId, "PO-2026-001");
        handEntered["PurchaseOrders"]![0]!["Reference"] = "Bookkeeper's own order";
        var handId = await PutByHandAsync(kit.Simulator, "PurchaseOrders", handEntered);

        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, supplier: PurchasingSyncTestKit.UnlinkedReference);
        var entry = Assert.Single(await kit.PlanOrderAsync(id));
        Assert.Equal(XeroPushOutcome.Blocked, Assert.Single(await kit.DrainAsync()).Result.Outcome);

        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync(PurchasingSyncTestKit.UnlinkedReference, newcoContactId)).Outcome);
        Assert.True(await kit.Outbox.RetryAsync(entry.Id));
        Assert.Equal(XeroPushOutcome.Rejected, Assert.Single(await kit.DrainAsync()).Result.Outcome);
        Assert.Null(await kit.OrderLinkAsync(id));

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled, supplier: PurchasingSyncTestKit.UnlinkedReference);
        await kit.PlanOrderAsync(id);
        await kit.DrainAsync();
        Assert.Equal("DRAFT", kit.Simulator.Find("PurchaseOrders", handId)!.Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnOrderIssuedOnTheDaySyncBegan_IsAutomatic_DayGranularity()
    {
        // An order holds an issue date, not an instant: the cut-off compares dates, inclusive (planner remarks).
        using var kit = await PurchasingSyncTestKit.CreateAsync(options: new XeroPurchasingPlannerOptions { AutomaticFromUtc = new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero) });
        var sameDay = Guid.NewGuid();
        kit.FakeOrders[sameDay] = PurchasingSyncTestKit.Order(sameDay, issued: new DateOnly(2026, 10, 5));
        var dayBefore = Guid.NewGuid();
        kit.FakeOrders[dayBefore] = PurchasingSyncTestKit.Order(dayBefore, reference: "PO-2026-002", issued: new DateOnly(2026, 10, 4));

        Assert.Single(await kit.PlanOrderAsync(sameDay));
        Assert.Empty(await kit.PlanOrderAsync(dayBefore));
    }

    [Fact]
    public async Task AnUnlinkedSupplier_IsBlocked_ThenSentOnRetry_OnceLinked()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, supplier: PurchasingSyncTestKit.UnlinkedReference);
        var entry = Assert.Single(await kit.PlanOrderAsync(id));

        var blocked = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Blocked, blocked.Result.Outcome);
        Assert.Contains("not linked to a Xero contact", blocked.Result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.LiveOrders);

        var contactId = kit.Simulator.SeedContact("Newco Fixings Ltd");
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync(PurchasingSyncTestKit.UnlinkedReference, contactId)).Outcome);
        Assert.True(await kit.Outbox.RetryAsync(entry.Id));

        var sent = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Succeeded, sent.Result.Outcome);
        Assert.Equal(contactId, Assert.Single(kit.LiveOrders).Body["Contact"]!["ContactID"]!.GetValue<string>());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnOrderWithNoKnownSupplier_IsBlockedWithTheReason()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, supplier: null);
        await kit.PlanOrderAsync(id);

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Blocked, step.Result.Outcome);
        Assert.Contains("names no supplier", step.Result.Reason, StringComparison.Ordinal);
        Assert.Empty(kit.Requests);
    }

    [Fact]
    public async Task AnAccountXeroLacks_BlocksThePush_BeforeAnythingIsSent()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        XeroAccountCodeMap.EnsureDefinitions(kit.SettingsProvider);
        await kit.SettingsProvider.SetValueAsync(XeroAccountCodeMap.ExpenseSettingKey(ExpenseCategory.Materials), "999");
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);
        var mark = kit.Simulator.Requests.Count;

        var step = Assert.Single(await kit.DrainAsync());
        Assert.Equal(XeroPushOutcome.Blocked, step.Result.Outcome);
        Assert.Contains("999", step.Result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(kit.Simulator.Requests.Skip(mark), r => r.Method != HttpMethod.Get);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnOrderCancelledBeforeItsQueuedCreateWasSent_NeverReachesXero()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        await kit.PlanOrderAsync(id);

        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled);
        var queued = await kit.PlanOrderAsync(id);
        Assert.Equal(XeroOperation.DeletePurchaseOrder, Assert.Single(queued).Operation);

        var steps = await kit.DrainAsync();
        Assert.Equal(2, steps.Count);
        Assert.All(steps, s => Assert.Equal(XeroPushOutcome.NothingToDo, s.Result.Outcome));
        Assert.Empty(kit.Simulator.All("PurchaseOrders"));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AnOrderCancelledBeforeIssue_PlansNothing()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Cancelled) with { IssuedDate = null };

        Assert.Empty(await kit.PlanOrderAsync(id));
    }

    [Fact]
    public async Task AnOrderIssuedBeforeSyncBegan_GoesOnlyWhenSentToXero()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync(options: new XeroPurchasingPlannerOptions { AutomaticFromUtc = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero) });
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, issued: new DateOnly(2026, 10, 2));

        Assert.Empty(await kit.PlanOrderAsync(id));
        Assert.Equal(0, await kit.OrderPlanner.ScanAsync());

        var request = await kit.OrderPlanner.SendToXeroAsync(id);
        Assert.True(request.Queued);
        Assert.Equal(XeroOperation.PushPurchaseOrder, Assert.Single(request.Entries).Operation);
        Assert.Contains(kit.Audit.Rows, r => r.Action == XeroPurchasingSyncState.AuditSendToXero);

        await kit.DrainAsync();
        Assert.Single(kit.LiveOrders);

        var later = Guid.NewGuid();
        kit.FakeOrders[later] = PurchasingSyncTestKit.Order(later, reference: "PO-2026-002", issued: new DateOnly(2026, 10, 5));
        Assert.Single(await kit.PlanOrderAsync(later));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task SendToXero_RefusesADraftOrAMissingOrder()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id, PurchaseOrderStatus.Draft);

        Assert.False((await kit.OrderPlanner.SendToXeroAsync(id)).Queued);
        Assert.False((await kit.OrderPlanner.SendToXeroAsync(Guid.NewGuid())).Queued);
    }

    [Fact]
    public async Task TheHandlers_RefuseAnotherKindOfDocument()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var entry = await kit.Outbox.EnqueueAsync(XeroOperation.PushQuote, XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.NewGuid()), "hash");

        Assert.Equal(XeroPushOutcome.Rejected, (await kit.OrderHandler.PushAsync(PurchasingSyncTestKit.TenantId, entry)).Outcome);
        Assert.Equal(XeroPushOutcome.Rejected, (await kit.OrderAttachments.PushAsync(PurchasingSyncTestKit.TenantId, entry)).Outcome);
        Assert.Equal(XeroDocumentKind.PurchaseOrder, kit.OrderHandler.DocumentKind);
        Assert.Equal(XeroDocumentKind.PurchaseOrder, kit.OrderAttachments.DocumentKind);
        Assert.Equal(XeroDocumentKind.PurchaseOrder, kit.OrderPlanner.Kind);
        Assert.Equal(PurchaseOrder.CanonicalKind, kit.OrderPlanner.CanonicalKind);
    }

    [Fact]
    public async Task ANewPdf_ReplacesTheAttachmentByName()
    {
        using var kit = await PurchasingSyncTestKit.CreateAsync();
        var id = Guid.NewGuid();
        kit.FakeOrders[id] = PurchasingSyncTestKit.Order(id);
        kit.Files.Store(PurchasingSyncTestKit.OrderRef(id), "first", "a.pdf");
        await kit.PlanOrderAsync(id);
        await kit.DrainAsync();

        kit.Files.Store(PurchasingSyncTestKit.OrderRef(id), "second, re-exported", "a.pdf");
        Assert.Equal(XeroOperation.UploadAttachment, Assert.Single(await kit.PlanOrderAsync(id)).Operation);
        await kit.DrainAsync();

        var attachment = Assert.Single(Assert.Single(kit.LiveOrders).Attachments);
        Assert.Equal("PO-2026-001.pdf", attachment.FileName);
        Assert.Equal(Encoding.UTF8.GetByteCount("second, re-exported"), attachment.Length);
        Assert.Equal(HttpMethod.Post, kit.WritesTo("PurchaseOrders").Last().Method);
        kit.AssertNoViolations();
    }

    /// <summary>Creates a document in Xero by hand, outside TempestOS's pipeline.</summary>
    internal static async Task<string> PutByHandAsync(XeroApiSimulator simulator, string resource, JsonObject body)
    {
        using var client = simulator.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{resource}?summarizeErrors=true")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", simulator.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", simulator.Options.TenantId);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"by-hand:{Guid.NewGuid():N}");
        using var response = await client.SendAsync(request);
        var reply = await SimulatorTestKit.ReadAsync(response);
        Assert.Equal(System.Net.HttpStatusCode.OK, reply.Status);
        return reply.First(resource)[resource == "Invoices" ? "InvoiceID" : "PurchaseOrderID"]!.GetValue<string>();
    }
}
