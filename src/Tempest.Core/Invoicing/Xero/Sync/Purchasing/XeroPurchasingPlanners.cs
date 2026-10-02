using Tempest.Core.Expenses;
using Tempest.Core.PurchaseOrders;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// Decides, from a TempestOS purchase order's current state and its Xero
/// link, which Xero writes are still needed (`v0.24.0` X5, D5, Q2; design
/// §4.3, §6.2) — desired state, not events — and queues them in the
/// outbox. Never a network call.
/// </summary>
/// <remarks>
/// <para>
/// <b>In scope</b> once issued (Issued, Received, Closed, or Cancelled after
/// issue) and — for an order issued before Xero sync began in this workspace
/// (<see cref="XeroPurchasingPlannerOptions.AutomaticFromUtc"/>) — only after
/// its explicit <see cref="SendToXeroAsync"/>, or once linked. A draft plans
/// nothing.
/// </para>
/// <para>
/// <b>What it plans</b>, in order (the outbox keeps per-document order):
/// <see cref="XeroOperation.PushPurchaseOrder"/> while the order has no Xero
/// copy (Q2: a <c>DRAFT</c> with the same number; a TempestOS order's lines
/// are fixed once issued, so a linked copy is never re-sent);
/// <see cref="XeroOperation.UploadAttachment"/> when the issued PDF differs
/// from the one last uploaded; and, once cancelled,
/// <see cref="XeroOperation.DeletePurchaseOrder"/> — for a linked copy Xero
/// does not already hold as deleted, or for an order whose create was queued
/// (its answer may have been lost, so the delete looks it up first).
/// Received and Closed change nothing in Xero (billing is the order's own
/// <em>Copy to bill</em> there). A copy deleted in Xero plans nothing more.
/// </para>
/// <para>
/// <b>Automatic from when.</b> An order goes automatically when it was issued
/// on or after the <em>date</em> automatic sync began (the order holds an
/// issue date, not an instant: day granularity, inclusive); an expense
/// compares its recording instant with the instant sync began. Anything
/// earlier goes only when sent to Xero by hand.
/// </para>
/// </remarks>
public sealed class XeroPurchaseOrderPlanner : IXeroSyncPlanner
{
    private readonly IXeroPurchaseOrderSource _orders;
    private readonly IXeroLinkStore _links;
    private readonly IXeroOutbox _outbox;
    private readonly XeroPurchasingSyncState _state;
    private readonly IXeroDocumentFileSource? _files;

    /// <summary>Initialises a new instance of the <see cref="XeroPurchaseOrderPlanner"/> class.</summary>
    /// <param name="orders">Reads purchase orders.</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="outbox">The outbox (B2) planned writes are queued in.</param>
    /// <param name="state">The purchasing planners' own state (automatic sync, opt-ins, tenant).</param>
    /// <param name="files">The issued PDFs (X6); <see langword="null"/> while none is registered — then nothing is uploaded.</param>
    public XeroPurchaseOrderPlanner(
        IXeroPurchaseOrderSource orders, IXeroLinkStore links, IXeroOutbox outbox, XeroPurchasingSyncState state, IXeroDocumentFileSource? files = null)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(state);

        _orders = orders;
        _links = links;
        _outbox = outbox;
        _state = state;
        _files = files;
    }

    /// <inheritdoc />
    public XeroDocumentKind Kind => XeroDocumentKind.PurchaseOrder;

    /// <inheritdoc />
    public string CanonicalKind => PurchaseOrder.CanonicalKind;

    /// <summary>The ref of the purchase order <paramref name="purchaseOrderId"/>.</summary>
    /// <param name="purchaseOrderId">The order.</param>
    public static XeroDocumentRef Ref(Guid purchaseOrderId) => XeroDocumentRef.For(XeroDocumentKind.PurchaseOrder, purchaseOrderId);

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroPlannedOperation>> PlanAsync(Guid objectId, XeroLink? link, CancellationToken cancellationToken = default)
    {
        var order = await _orders.FindAsync(objectId, cancellationToken).ConfigureAwait(false);
        if (order is null || !order.WasIssued)
            return [];

        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return [];

        var document = Ref(objectId);
        var deletedInXero = link is not null && XeroPurchasingMapper.Word(link.LastKnownXeroStatus) == XeroPurchasingMapper.StatusDeleted;

        if (order.Status == PurchaseOrderStatus.Cancelled)
        {
            if (link is not null)
                return deletedInXero ? [] : [new XeroPlannedOperation(XeroOperation.DeletePurchaseOrder, XeroPurchasingMapper.DeleteHash)];

            return await XeroPurchasingPlanning.CreateWasQueuedAsync(_outbox, document, XeroOperation.PushPurchaseOrder, cancellationToken).ConfigureAwait(false)
                ? [new XeroPlannedOperation(XeroOperation.DeletePurchaseOrder, XeroPurchasingMapper.DeleteHash)]
                : [];
        }

        if (deletedInXero)
            return [];

        if (link is null)
        {
            // Day granularity, inclusive — deliberately unlike an expense's
            // instant comparison: a TempestOS order carries only the date it
            // was issued (no time), so an order issued on the day automatic
            // sync began counts as issued after it, even if issued earlier
            // that day. The cost is at most a few DRAFT orders from that one
            // day reaching Xero (deletable there); the alternative — excluding
            // the whole first day — would silently miss orders issued after
            // sync began. Pinned by AnOrderIssuedOnTheDaySyncBegan_IsAutomatic.
            var automatic = order.IssuedDate is { } issued
                            && issued >= DateOnly.FromDateTime((await _state.AutomaticFromAsync(cancellationToken).ConfigureAwait(false)).UtcDateTime);
            if (!automatic && !await _state.IsOptedInAsync(document, cancellationToken).ConfigureAwait(false))
                return [];
        }

        var planned = new List<XeroPlannedOperation>();
        if (link is null)
            planned.Add(new XeroPlannedOperation(XeroOperation.PushPurchaseOrder, XeroPurchasingMapper.ContentHash(order)));

        var file = _files is null ? null : await _files.FindAsync(document, cancellationToken).ConfigureAwait(false);
        if (file is not null && !string.Equals(link?.AttachmentContentHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            planned.Add(new XeroPlannedOperation(XeroOperation.UploadAttachment, file.Sha256, XeroPurchasingMapper.PurchaseOrderAttachmentFileName(order.Reference)));

        return planned;
    }

    /// <summary>Plans <paramref name="purchaseOrderId"/> against its link in the connected Xero organisation and queues every planned write, in order. No network call.</summary>
    /// <param name="purchaseOrderId">The order.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The outbox entries for the planned writes, in order; empty when Xero already matches or the order is not in scope.</returns>
    public Task<IReadOnlyList<XeroOutboxEntry>> PlanAndEnqueueAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default) =>
        XeroPurchasingPlanning.PlanAndEnqueueAsync(this, _state, _links, _outbox, Ref(purchaseOrderId), purchaseOrderId, cancellationToken);

    /// <summary>The start-up and Refresh scan (§6.2): <see cref="PlanAndEnqueueAsync"/> for every purchase order.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>How many outbox entries the scan produced or found.</returns>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var id in await _orders.ListIdsAsync(cancellationToken).ConfigureAwait(false))
            count += (await PlanAndEnqueueAsync(id, cancellationToken).ConfigureAwait(false)).Count;
        return count;
    }

    /// <summary>The Product Owner's <em>Send to Xero</em> on a purchase order issued before Xero sync began: records the request (audited) and queues its writes.</summary>
    /// <param name="purchaseOrderId">The order.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<XeroPurchasingSendRequest> SendToXeroAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.FindAsync(purchaseOrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return new XeroPurchasingSendRequest(false, [], $"Purchase order {purchaseOrderId:D} does not exist.");

        if (!order.WasIssued || order.Status == PurchaseOrderStatus.Cancelled)
            return new XeroPurchasingSendRequest(false, [], $"Purchase order {order.Reference} is {order.Status}; only an issued order goes to Xero.");

        await _state.OptInAsync(Ref(purchaseOrderId), order.Reference, cancellationToken).ConfigureAwait(false);
        return new XeroPurchasingSendRequest(true, await PlanAndEnqueueAsync(purchaseOrderId, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>
/// Decides, from a TempestOS expense's current state and its Xero link,
/// which Xero writes its draft bill still needs (`v0.24.0` X5, D5, Q3, Q4,
/// Q6; design §4.4, §6.2) and queues them in the outbox. Never a network
/// call.
/// </summary>
/// <remarks>
/// <para>
/// <b>In scope</b> once recorded — for an expense recorded before Xero sync
/// began (<see cref="XeroPurchasingPlannerOptions.AutomaticFromUtc"/>), only
/// after its explicit <see cref="SendToXeroAsync"/>, or once linked.
/// <b>Q6:</b> an expense recorded from a received purchase order's lines
/// (<see cref="ProjectExpense.SourcePurchaseOrderId"/>) is never pushed as a
/// separate bill — the order's own <em>Copy to bill</em> in Xero is the bill —
/// and <see cref="DescribeNotPushedAsync"/> says so for its badge.
/// </para>
/// <para>
/// <b>What it plans</b>, in order: <see cref="XeroOperation.PushExpenseBill"/>
/// while the content differs from what was last pushed (create the
/// <c>ACCPAY</c> <c>DRAFT</c>, or update it — the handler refuses with the
/// reason once Xero holds it as anything but a draft);
/// <see cref="XeroOperation.UploadAttachment"/> when the receipt differs from
/// the one last uploaded; and, once deleted in TempestOS,
/// <see cref="XeroOperation.DeleteExpenseBill"/> — for a linked bill Xero does
/// not already hold as deleted or voided, or for an expense whose create was
/// queued (its answer may have been lost). A bill deleted or voided in Xero
/// plans nothing more.
/// </para>
/// </remarks>
public sealed class XeroExpenseBillPlanner : IXeroSyncPlanner
{
    private readonly IXeroExpenseSource _expenses;
    private readonly IXeroLinkStore _links;
    private readonly IXeroOutbox _outbox;
    private readonly XeroPurchasingSyncState _state;
    private readonly IXeroDocumentFileSource? _files;
    private readonly IXeroPurchaseOrderSource? _orders;

    /// <summary>Initialises a new instance of the <see cref="XeroExpenseBillPlanner"/> class.</summary>
    /// <param name="expenses">Reads expenses.</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="outbox">The outbox (B2) planned writes are queued in.</param>
    /// <param name="state">The purchasing planners' own state (automatic sync, opt-ins, tenant).</param>
    /// <param name="files">The receipts (X6); <see langword="null"/> while none is registered — then nothing is uploaded.</param>
    /// <param name="orders">Reads purchase orders, for the Q6 note's order number; <see langword="null"/> names the order by id.</param>
    public XeroExpenseBillPlanner(
        IXeroExpenseSource expenses, IXeroLinkStore links, IXeroOutbox outbox, XeroPurchasingSyncState state,
        IXeroDocumentFileSource? files = null, IXeroPurchaseOrderSource? orders = null)
    {
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(state);

        _expenses = expenses;
        _links = links;
        _outbox = outbox;
        _state = state;
        _files = files;
        _orders = orders;
    }

    /// <inheritdoc />
    public XeroDocumentKind Kind => XeroDocumentKind.ExpenseBill;

    /// <inheritdoc />
    public string CanonicalKind => ProjectExpense.CanonicalKind;

    /// <summary>The ref of the expense <paramref name="expenseId"/>.</summary>
    /// <param name="expenseId">The expense.</param>
    public static XeroDocumentRef Ref(Guid expenseId) => XeroDocumentRef.For(XeroDocumentKind.ExpenseBill, expenseId);

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroPlannedOperation>> PlanAsync(Guid objectId, XeroLink? link, CancellationToken cancellationToken = default)
    {
        var expense = await _expenses.FindAsync(objectId, cancellationToken).ConfigureAwait(false);
        if (expense is null)
            return [];

        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return [];

        // Q6: recorded from a received purchase order's lines — the order's
        // own "Copy to bill" in Xero is the bill.
        if (link is null && expense.SourcePurchaseOrderId is not null)
            return [];

        var document = Ref(objectId);
        var status = XeroPurchasingMapper.Word(link?.LastKnownXeroStatus);
        var goneInXero = status is XeroPurchasingMapper.StatusDeleted or XeroPurchasingMapper.StatusVoided;

        if (expense.IsDeleted)
        {
            if (link is not null)
                return goneInXero ? [] : [new XeroPlannedOperation(XeroOperation.DeleteExpenseBill, XeroPurchasingMapper.DeleteHash)];

            return await XeroPurchasingPlanning.CreateWasQueuedAsync(_outbox, document, XeroOperation.PushExpenseBill, cancellationToken).ConfigureAwait(false)
                ? [new XeroPlannedOperation(XeroOperation.DeleteExpenseBill, XeroPurchasingMapper.DeleteHash)]
                : [];
        }

        if (goneInXero)
            return [];

        if (link is null)
        {
            var automatic = expense.RecordedAtUtc is { } recorded
                            && recorded >= await _state.AutomaticFromAsync(cancellationToken).ConfigureAwait(false);
            if (!automatic && !await _state.IsOptedInAsync(document, cancellationToken).ConfigureAwait(false))
                return [];
        }

        var planned = new List<XeroPlannedOperation>();
        var contentHash = XeroPurchasingMapper.ContentHash(expense);
        if (!string.Equals(link?.LastPushedContentHash, contentHash, StringComparison.Ordinal))
            planned.Add(new XeroPlannedOperation(XeroOperation.PushExpenseBill, contentHash));

        var file = _files is null ? null : await _files.FindAsync(document, cancellationToken).ConfigureAwait(false);
        if (file is not null && !string.Equals(link?.AttachmentContentHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            planned.Add(new XeroPlannedOperation(XeroOperation.UploadAttachment, file.Sha256, XeroPurchasingMapper.ReceiptFileName(file.FileName)));

        return planned;
    }

    /// <summary>
    /// Why <paramref name="expenseId"/> is deliberately not pushed as a bill
    /// (Q6), for its badge; <see langword="null"/> when it is synced as usual.
    /// No network call.
    /// </summary>
    /// <param name="expenseId">The expense.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<string?> DescribeNotPushedAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await _expenses.FindAsync(expenseId, cancellationToken).ConfigureAwait(false);
        if (expense?.SourcePurchaseOrderId is not { } orderId)
            return null;

        var tenantId = await _state.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is not null && await _links.FindAsync(tenantId, Ref(expenseId), cancellationToken).ConfigureAwait(false) is not null)
            return null;

        var order = _orders is null ? null : await _orders.FindAsync(orderId, cancellationToken).ConfigureAwait(false);
        var name = order?.Reference ?? orderId.ToString("D");
        return $"Recorded from purchase order {name}: not sent as a separate bill — bill it from the purchase order in Xero (Copy to bill), so the cost is in the accounts once.";
    }

    /// <summary>Plans <paramref name="expenseId"/> against its link in the connected Xero organisation and queues every planned write, in order. No network call.</summary>
    /// <param name="expenseId">The expense.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The outbox entries for the planned writes, in order; empty when Xero already matches or the expense is not in scope.</returns>
    public Task<IReadOnlyList<XeroOutboxEntry>> PlanAndEnqueueAsync(Guid expenseId, CancellationToken cancellationToken = default) =>
        XeroPurchasingPlanning.PlanAndEnqueueAsync(this, _state, _links, _outbox, Ref(expenseId), expenseId, cancellationToken);

    /// <summary>The start-up and Refresh scan (§6.2): <see cref="PlanAndEnqueueAsync"/> for every expense.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>How many outbox entries the scan produced or found.</returns>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var id in await _expenses.ListIdsAsync(cancellationToken).ConfigureAwait(false))
            count += (await PlanAndEnqueueAsync(id, cancellationToken).ConfigureAwait(false)).Count;
        return count;
    }

    /// <summary>The Product Owner's <em>Send to Xero</em> on an expense recorded before Xero sync began: records the request (audited) and queues its writes. Refused for an expense recorded from a purchase order (Q6) and for a deleted one.</summary>
    /// <param name="expenseId">The expense.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<XeroPurchasingSendRequest> SendToXeroAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await _expenses.FindAsync(expenseId, cancellationToken).ConfigureAwait(false);
        if (expense is null || expense.IsDeleted)
            return new XeroPurchasingSendRequest(false, [], $"Expense {expenseId:D} does not exist.");

        if (await DescribeNotPushedAsync(expenseId, cancellationToken).ConfigureAwait(false) is { } reason)
            return new XeroPurchasingSendRequest(false, [], reason);

        await _state.OptInAsync(Ref(expenseId), XeroPurchasingMapper.BillNumber(expense), cancellationToken).ConfigureAwait(false);
        return new XeroPurchasingSendRequest(true, await PlanAndEnqueueAsync(expenseId, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>What the two purchasing planners share.</summary>
internal static class XeroPurchasingPlanning
{
    /// <summary>Whether a create (<paramref name="createOperation"/>) for <paramref name="document"/> was ever queued and not superseded — so its answer may have been lost, and a delete must look the record up.</summary>
    public static async Task<bool> CreateWasQueuedAsync(IXeroOutbox outbox, XeroDocumentRef document, XeroOperation createOperation, CancellationToken cancellationToken)
    {
        var entries = await outbox.ListForDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        return entries.Any(e => e.Operation == createOperation && e.State != XeroOutboxState.Superseded);
    }

    /// <summary>Plans <paramref name="objectId"/> with <paramref name="planner"/> against its link in the connected tenant and queues each planned write, in order.</summary>
    public static async Task<IReadOnlyList<XeroOutboxEntry>> PlanAndEnqueueAsync(
        IXeroSyncPlanner planner, XeroPurchasingSyncState state, IXeroLinkStore links, IXeroOutbox outbox, XeroDocumentRef document, Guid objectId,
        CancellationToken cancellationToken)
    {
        var tenantId = await state.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var link = tenantId is null ? null : await links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false);

        var planned = await planner.PlanAsync(objectId, link, cancellationToken).ConfigureAwait(false);
        var entries = new List<XeroOutboxEntry>(planned.Count);
        foreach (var operation in planned)
            entries.Add(await outbox.EnqueueAsync(operation.Operation, document, operation.ContentHash, operation.Argument, cancellationToken).ConfigureAwait(false));

        return entries;
    }
}
