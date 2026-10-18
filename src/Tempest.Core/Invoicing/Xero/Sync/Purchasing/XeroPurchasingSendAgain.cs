using Tempest.Core.Audit;
using Tempest.Core.PurchaseOrders;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// The person's deliberate <em>Send again</em> on a purchase order or expense
/// whose Xero record — made by a create whose answer was lost, and found by
/// its id — was deleted (or a bill voided) in Xero (`v0.24.0` X5,
/// <see cref="XeroOwnershipVerdict.DeletedInXero"/>).
/// </summary>
/// <remarks>
/// <para>
/// Retry, a cancel or a delete never re-sends such a create: the create log
/// keeps a tombstone for it (<see cref="XeroPurchasingCreateLog.RecordGoneAsync"/>).
/// This is the only way past it, and only on the person's request: it releases
/// the tombstone (audited), so the deleted record is no longer this
/// document's, and queues the document's push again. That push sends the
/// document to Xero as a new <c>DRAFT</c> record under a new
/// <c>Idempotency-Key</c> (<see cref="XeroPurchasingOwnership.CreateKey"/>) —
/// never the deleted record's key, so Xero cannot replay its answer — after
/// the usual check for anything in the way under the number.
/// </para>
/// <para>
/// Refused, with the reason, when the document is linked, gone, cancelled or
/// deleted, or when nothing it sent is known to be deleted in Xero (a create
/// TempestOS cannot tell about is never released: it may still be live).
/// Never a network call.
/// </para>
/// </remarks>
public sealed class XeroPurchasingSendAgain
{
    /// <summary>The audit action written when the person chooses <em>Send again</em>.</summary>
    public const string AuditAction = "xero.purchasing.send-again";

    /// <summary>How a live purchase order's <see cref="XeroOwnershipVerdict.DeletedInXero"/> refusal ends: the way forward.</summary>
    public const string OrderAdvice =
        "If the order is still wanted in Xero, choose Send again on the order: TempestOS then sends it as a new purchase order (the deleted one stays deleted).";

    /// <summary>How a live expense's <see cref="XeroOwnershipVerdict.DeletedInXero"/> refusal ends: the way forward.</summary>
    public const string ExpenseAdvice =
        "If the expense is still wanted in Xero, choose Send again on the expense: TempestOS then sends it as a new bill (the deleted one stays as it is).";

    private readonly XeroPurchasingCreateLog _creates;
    private readonly IXeroOutbox _outbox;
    private readonly IXeroLinkStore _links;
    private readonly XeroPurchasingSyncState _state;
    private readonly IXeroPurchaseOrderSource _orders;
    private readonly IXeroExpenseSource _expenses;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="XeroPurchasingSendAgain"/> class.</summary>
    /// <param name="creates">The create log holding the tombstones.</param>
    /// <param name="outbox">The outbox the push is queued in.</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="state">The purchasing state (the connected tenant).</param>
    /// <param name="orders">Reads purchase orders.</param>
    /// <param name="expenses">Reads expenses.</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock the release is stamped with; <see langword="null"/> for the system clock.</param>
    public XeroPurchasingSendAgain(
        XeroPurchasingCreateLog creates, IXeroOutbox outbox, IXeroLinkStore links, XeroPurchasingSyncState state,
        IXeroPurchaseOrderSource orders, IXeroExpenseSource expenses, IAuditRecorder? audit = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(creates);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(expenses);

        _creates = creates;
        _outbox = outbox;
        _links = links;
        _state = state;
        _orders = orders;
        _expenses = expenses;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary><em>Send again</em> on the purchase order <paramref name="purchaseOrderId"/>.</summary>
    /// <param name="purchaseOrderId">The order.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<XeroPurchasingSendRequest> SendOrderAgainAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.FindAsync(purchaseOrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return new XeroPurchasingSendRequest(false, [], $"Purchase order {purchaseOrderId:D} does not exist.");
        if (!order.WasIssued)
            return new XeroPurchasingSendRequest(false, [], $"Purchase order {order.Reference} is not issued; it goes to Xero once it is.");
        if (order.Status == PurchaseOrderStatus.Cancelled)
            return new XeroPurchasingSendRequest(false, [], $"Purchase order {order.Reference} was cancelled; it is not sent to Xero again.");

        return await SendAgainAsync(
            XeroPurchaseOrderPlanner.Ref(purchaseOrderId), XeroOperation.PushPurchaseOrder, XeroPurchasingMapper.ContentHash(order),
            $"purchase order {order.Reference}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary><em>Send again</em> on the expense <paramref name="expenseId"/>.</summary>
    /// <param name="expenseId">The expense.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<XeroPurchasingSendRequest> SendExpenseAgainAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await _expenses.FindAsync(expenseId, cancellationToken).ConfigureAwait(false);
        if (expense is null || expense.IsDeleted)
            return new XeroPurchasingSendRequest(false, [], $"Expense {expenseId:D} does not exist.");
        if (expense.SourcePurchaseOrderId is not null)
            return new XeroPurchasingSendRequest(false, [], "Recorded from a purchase order's lines: billed from the purchase order in Xero, not as a separate bill (Q6).");

        return await SendAgainAsync(
            XeroExpenseBillPlanner.Ref(expenseId), XeroOperation.PushExpenseBill, XeroPurchasingMapper.ContentHash(expense),
            "this expense", cancellationToken).ConfigureAwait(false);
    }

    private async Task<XeroPurchasingSendRequest> SendAgainAsync(
        XeroDocumentRef document, XeroOperation push, string contentHash, string name, CancellationToken cancellationToken)
    {
        var tenantId = await _state.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return new XeroPurchasingSendRequest(false, [], "No Xero organisation is connected.");

        if (await _links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false) is not null)
            return new XeroPurchasingSendRequest(false, [], $"The Xero record for {name} is linked; there is nothing to send again.");

        var released = await _creates.ReleaseGoneAsync(tenantId, document, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (released == 0)
        {
            return new XeroPurchasingSendRequest(
                false, [], $"Nothing TempestOS sent to Xero for {name} is known to be deleted there, so there is nothing to send again; nothing is queued.");
        }

        if (_audit is not null)
        {
            await _audit.RecordAsync(AuditAction, new Dictionary<string, string>
            {
                ["document"] = document.TempestKey,
                ["kind"] = document.Kind.ToString(),
                ["released"] = released.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }, cancellationToken).ConfigureAwait(false);
        }

        // The push the deleted record refused is retried (it now sends under a new key);
        // queued afresh when there is none to retry.
        foreach (var failed in (await _outbox.ListForDocumentAsync(document, cancellationToken).ConfigureAwait(false))
                 .Where(e => e.Operation == push && e.State == XeroOutboxState.Failed))
        {
            await _outbox.RetryAsync(failed.Id, cancellationToken).ConfigureAwait(false);
        }

        var entry = await _outbox.EnqueueAsync(push, document, contentHash, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new XeroPurchasingSendRequest(true, [entry]);
    }
}
