using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.PurchaseOrders;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// Sends the purchase-order writes the <see cref="XeroPurchaseOrderPlanner"/>
/// queued (`v0.24.0` X5, D5, Q2; design §3, §4.3, §6.4):
/// <see cref="XeroOperation.PushPurchaseOrder"/> — create the Xero purchase
/// order as <c>DRAFT</c> with the same number, the supplier's
/// <c>ContactID</c>, the project code and the lines — and
/// <see cref="XeroOperation.DeletePurchaseOrder"/> — delete it because the
/// TempestOS order was cancelled.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never two orders.</b> A create is issued only when the order has no
/// link in the tenant, and only after applying
/// <see cref="XeroPurchasingOwnership"/>'s one rule: every create the log
/// recorded for the order (its answer lost) is re-sent under its own
/// <c>Idempotency-Key</c> and the answered id read back — a live order so
/// found is linked (<c>"reconciled"</c>), one deleted in Xero is never linked
/// or resent — and only then is the number looked up
/// (<c>GET PurchaseOrders/{PurchaseOrderNumber}</c>): any order under it is
/// left untouched and the push is Rejected with the reason. Every write
/// carries the entry's fixed <c>Idempotency-Key</c>.
/// </para>
/// <para>
/// <b>Delete reads first</b>: an order already deleted in Xero is simply
/// recorded; one Xero holds as <c>BILLED</c> (its <em>Copy to bill</em> was
/// used) is refused with the reason, never asked of Xero. A cancelled order
/// whose create's answer was lost is looked up by number before anything is
/// said about it.
/// </para>
/// <para>
/// <b>Blocked</b> when the supplier is not linked to a Xero contact (X2), or a
/// line's input tax type or the materials account is missing from Xero (X1).
/// Never throws for anything Xero or the network did (`ADR-0151`); never
/// emails (D4).
/// </para>
/// </remarks>
public sealed class XeroPurchaseOrderPushHandler : IXeroPushHandler
{
    private readonly XeroAccountingApi _api;
    private readonly IXeroLinkStore _links;
    private readonly XeroPurchasingCreateLog _creates;
    private readonly IXeroPurchaseOrderSource _orders;
    private readonly XeroContactLinker _contacts;
    private readonly XeroTaxTypeResolver _taxTypes;
    private readonly XeroAccountCodeMap _accounts;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="XeroPurchaseOrderPushHandler"/> class.</summary>
    /// <param name="api">The typed Xero client (its <see cref="HttpClient"/> holds the safety handler).</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="creates">The create log: written just before each create is sent, and read before an order found in Xero without our reference is called this order's own.</param>
    /// <param name="orders">Reads purchase orders.</param>
    /// <param name="contacts">The X2 linker: the supplier's <c>ContactID</c>, or why the push is Blocked.</param>
    /// <param name="taxTypes">The X1 tax-type resolver (input side).</param>
    /// <param name="accounts">The X1 account-code map (the materials expense account).</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock links are stamped with; <see langword="null"/> for the system clock.</param>
    public XeroPurchaseOrderPushHandler(
        XeroAccountingApi api,
        IXeroLinkStore links,
        XeroPurchasingCreateLog creates,
        IXeroPurchaseOrderSource orders,
        XeroContactLinker contacts,
        XeroTaxTypeResolver taxTypes,
        XeroAccountCodeMap accounts,
        IAuditRecorder? audit = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(creates);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(taxTypes);
        ArgumentNullException.ThrowIfNull(accounts);

        _api = api;
        _links = links;
        _creates = creates;
        _orders = orders;
        _contacts = contacts;
        _taxTypes = taxTypes;
        _accounts = accounts;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The kind of document this handler sends.</summary>
    public XeroDocumentKind DocumentKind => XeroDocumentKind.PurchaseOrder;

    /// <inheritdoc />
    public IReadOnlyCollection<XeroOperation> Operations { get; } = [XeroOperation.PushPurchaseOrder, XeroOperation.DeletePurchaseOrder];

    /// <inheritdoc />
    public async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Document.Kind != XeroDocumentKind.PurchaseOrder || !Guid.TryParse(entry.Document.TempestKey, out var orderId))
            return new XeroPushResult(XeroPushOutcome.Rejected, $"The purchase order handler sends purchase orders only; {entry.Document.Kind} {entry.Document.TempestKey} is not one.");

        return entry.Operation switch
        {
            XeroOperation.PushPurchaseOrder => await PushAsync(tenantId, entry, orderId, cancellationToken).ConfigureAwait(false),
            XeroOperation.DeletePurchaseOrder => await DeleteAsync(tenantId, entry, orderId, cancellationToken).ConfigureAwait(false),
            _ => new XeroPushResult(XeroPushOutcome.Rejected, $"The purchase order handler does not send {entry.Operation}."),
        };
    }

    private async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.FindAsync(orderId, cancellationToken).ConfigureAwait(false);
        // Never Failed: a Failed push holds the order's queue, and the delete
        // queued behind it must still run to remove an order a lost create made.
        if (order is null)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Purchase order {orderId:D} no longer exists in TempestOS; nothing is pushed.");

        var link = await _links.FindAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        if (link is not null)
        {
            return PersistenceXeroLinkStore.IsFromNewerVersion(link)
                ? new XeroPushResult(XeroPushOutcome.Blocked, $"The Xero link for purchase order {order.Reference} was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.")
                : new XeroPushResult(XeroPushOutcome.NothingToDo, Link: link); // Lines are fixed once issued: an existing copy is never re-sent.
        }

        if (!order.WasIssued)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Purchase order {order.Reference} is not issued; it goes to Xero once it is.");

        var contact = await ResolveSupplierAsync(tenantId, order, cancellationToken).ConfigureAwait(false);
        if (!contact.IsLinked)
        {
            // A cancelled order needs no contact: Blocked would hold the delete
            // queued behind this push, which finds any order a create made.
            return order.Status == PurchaseOrderStatus.Cancelled
                ? new XeroPushResult(XeroPushOutcome.NothingToDo, $"Purchase order {order.Reference} was cancelled; nothing is pushed.")
                : new XeroPushResult(XeroPushOutcome.Blocked, contact.BlockedReason);
        }

        var stale = !string.Equals(XeroPurchasingMapper.ContentHash(order), entry.ContentHash, StringComparison.Ordinal);

        var reconciled = await ReconcileAsync(tenantId, entry, order, contact.ContactId!, stale, cancellationToken).ConfigureAwait(false);
        if (reconciled.Result is { } answered)
            return answered;
        if (reconciled.Link is { } found)
            return new XeroPushResult(XeroPushOutcome.Succeeded, Link: found);

        if (order.Status == PurchaseOrderStatus.Cancelled)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Purchase order {order.Reference} was cancelled, and no purchase order TempestOS sent for it is live in Xero; nothing is sent.");

        if (stale)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, "The purchase order changed after this write was queued; the newer write creates the Xero purchase order.");

        var account = await _accounts.ResolveExpenseAsync(XeroPurchasingMapper.PurchaseOrderLineCategory, cancellationToken).ConfigureAwait(false);
        var taxTypes = new Dictionary<VatRate, XeroCodeResolution>();
        foreach (var rate in order.Lines.Select(l => l.VatRate).Distinct())
            taxTypes[rate] = await _taxTypes.ResolveAsync(rate, VatTaxDirection.Purchases, cancellationToken).ConfigureAwait(false);

        var body = XeroPurchasingMapper.BuildPurchaseOrder(order, contact.ToContactRef(), rate => taxTypes[rate], account, out var blocked);
        if (body is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, blocked);

        // Recorded before it goes, so a lost answer (or a crash mid-request)
        // still leaves proof this order's create may be in Xero.
        await _creates.RecordSendingAsync(
            tenantId, entry.Document, body.PurchaseOrderNumber, contact.ContactId!, entry.IdempotencyKey, cancellationToken,
            body.Reference, XeroPurchasingOwnership.ValueOf(body), XeroPurchasingSentCreate.Serialise(body)).ConfigureAwait(false);
        var created = await _api.CreatePurchaseOrderAsync(body, entry.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        await _creates.RecordAnswerAsync(tenantId, entry.Document, entry.IdempotencyKey, created.Outcome, cancellationToken).ConfigureAwait(false);
        if (created.Outcome != ConnectorOutcome.Ok)
            return XeroPurchasingMapper.Failed(created);

        var createdLink = NewLink(tenantId, entry.Document, created.Value!, entry.ContentHash, XeroPurchasingMapper.LinkedByCreated);
        await _links.SaveAsync(createdLink, cancellationToken).ConfigureAwait(false);
        await XeroPurchasingAudit.RecordAsync(_audit, XeroPurchasingAudit.LinkCreated, createdLink, entry, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: createdLink);
    }

    private async Task<XeroPushResult> DeleteAsync(string tenantId, XeroOutboxEntry entry, Guid orderId, CancellationToken cancellationToken)
    {
        var link = await _links.FindAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            // The create may have landed with its answer lost: recover it before saying it never reached Xero.
            // An order no longer readable, or a supplier no longer linked, still leaves the
            // creates the log says were sent to recover by their keys.
            var order = await _orders.FindAsync(orderId, cancellationToken).ConfigureAwait(false);
            string? contactId = null;
            if (order is not null)
            {
                var contact = await ResolveSupplierAsync(tenantId, order, cancellationToken).ConfigureAwait(false);
                contactId = contact.IsLinked ? contact.ContactId : null;
            }

            var reconciled = await ReconcileAsync(tenantId, entry, order, contactId, stale: true, cancellationToken).ConfigureAwait(false);
            if (reconciled.Result is { } answered)
                return answered;
            if (reconciled.Link is null)
            {
                return new XeroPushResult(
                    XeroPushOutcome.NothingToDo,
                    order is null
                        ? "No purchase order TempestOS sent for this order is live in Xero; there is nothing to delete."
                        : $"No purchase order TempestOS sent for {order.Reference} is live in Xero; there is nothing to delete.");
            }

            link = reconciled.Link;
        }

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The Xero link for this purchase order was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        var number = link.XeroNumber ?? link.XeroId;
        var read = await _api.GetPurchaseOrderAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
        {
            if (!read.NotFound)
                return XeroPurchasingMapper.Failed(read);

            var gone = await RecordStatusAsync(link, XeroPurchasingMapper.StatusDeleted, cancellationToken).ConfigureAwait(false);
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Purchase order {number} is no longer in Xero.", Link: gone);
        }

        var status = XeroPurchasingMapper.Word(read.Value!.Status);
        link = await RecordStatusAsync(link, status, cancellationToken).ConfigureAwait(false);

        if (status == XeroPurchasingMapper.StatusDeleted)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, Link: link);

        if (status == XeroPurchasingMapper.StatusBilled)
        {
            return new XeroPushResult(
                XeroPushOutcome.Rejected,
                $"Purchase order {number} was billed in Xero (Copy to bill), and Xero does not delete a billed purchase order; "
                + "it was cancelled in TempestOS only. Deal with the bill in Xero.",
                Link: link);
        }

        var deleted = await _api.DeletePurchaseOrderAsync(link.XeroId, entry.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (deleted.Outcome != ConnectorOutcome.Ok)
            return XeroPurchasingMapper.Failed(deleted);

        link = await RecordStatusAsync(link, XeroPurchasingMapper.Word(deleted.Value!.Status) ?? XeroPurchasingMapper.StatusDeleted, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
    }

    /// <summary>
    /// Before a first create, and before a delete with no link (§6.4 items
    /// 3–4), applies <see cref="XeroPurchasingOwnership"/>'s one rule — the
    /// same the bill handler applies. First every create the
    /// <see cref="XeroPurchasingCreateLog"/> recorded for this order is
    /// recovered by identity: its body re-sent under its own
    /// <c>Idempotency-Key</c> (Xero replays its first answer, the order's id),
    /// and that id read back. Only a live order recovered so is linked (so a
    /// cancel deletes it); one no longer live is never linked or resent; a
    /// replay Xero refuses is cannot-tell. Only then is the order's number
    /// looked up, for an order in the way of a create — never to call one
    /// ours. Anything else is left untouched and reported once: NothingToDo
    /// when the order is cancelled, gone or being deleted, Rejected (Retry)
    /// for a live push, never a second order under the number.
    /// </summary>
    /// <remarks>
    /// Xero keeps a live purchase order number unique, so any live order
    /// under the number that is not this order's own — another contact's,
    /// another TempestOS order's, or one keyed in Xero — clashes with a
    /// create.
    /// </remarks>
    private async Task<(XeroPushResult? Result, XeroLink? Link)> ReconcileAsync(
        string tenantId, XeroOutboxEntry entry, XeroPurchaseOrderSnapshot? order, string? contactId, bool stale, CancellationToken cancellationToken)
    {
        var sent = await _creates.ListSentAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        var sourceGone = order is null || order.Status == PurchaseOrderStatus.Cancelled || entry.Operation == XeroOperation.DeletePurchaseOrder;

        // Only a push that would send a create (again) has a create of its own to recover.
        var resendKey = !sourceGone && !stale ? entry.IdempotencyKey : null;

        var recovered = new List<XeroRecoveredCreate<XeroWirePurchaseOrder>>();
        foreach (var create in XeroPurchasingOwnership.ToRecover(sent))
        {
            var (failed, one) = await RecoverAsync(create, cancellationToken).ConfigureAwait(false);
            if (failed is not null)
                return (failed, null);
            recovered.Add(one!);
        }

        var judged = XeroPurchasingOwnership.Judge(recovered, resendKey, sourceGone, [], XeroPurchasingOwnership.ValueOf);
        var number = order?.Reference.Trim();
        if (judged.Verdict == XeroOwnershipVerdict.NothingLive && !string.IsNullOrEmpty(number))
        {
            // Nothing of this order's is live: is anything in the way of a create under its number?
            var found = await _api.FindPurchaseOrdersByNumberAsync(number, cancellationToken).ConfigureAwait(false);
            if (found.Outcome != ConnectorOutcome.Ok)
                return (XeroPurchasingMapper.Failed(found), null);

            List<XeroWirePurchaseOrder> inTheWay = [.. found.Value!.Where(o => o.PurchaseOrderID is not null && IsLive(o))];
            if (inTheWay.Count > 0)
            {
                var sentForOthers = contactId is null
                    ? []
                    : XeroPurchasingOwnership.SentUnder(
                        await _creates.ListSentForOthersAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false), number, contactId);
                judged = XeroPurchasingOwnership.Judge(recovered, resendKey, sourceGone, inTheWay, XeroPurchasingOwnership.ValueOf, sentForOthers);
            }
        }

        switch (judged.Verdict)
        {
            case XeroOwnershipVerdict.Ours:
            {
                var ours = judged.Ours!;
                var create = judged.From!.Create;

                // This entry's own create, to the number and contact the order still has: its content is what was sent.
                var landed = resendKey is not null
                             && string.Equals(create.IdempotencyKey, resendKey, StringComparison.Ordinal)
                             && string.Equals(create.Number, number, StringComparison.OrdinalIgnoreCase)
                             && string.Equals(create.ContactId, contactId, StringComparison.OrdinalIgnoreCase);

                var link = NewLink(tenantId, entry.Document, ours, landed ? entry.ContentHash : null, XeroPurchasingMapper.LinkedByReconciled);
                await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
                await XeroPurchasingAudit.RecordAsync(_audit, XeroPurchasingAudit.LinkReconciled, link, entry, cancellationToken).ConfigureAwait(false);
                return (null, link);
            }

            case XeroOwnershipVerdict.CannotTell:
                return (XeroPurchasingOwnership.CannotTell("Purchase order", "order", judged.From!.Create.Number, judged.From.Problem, sourceGone), null);

            case XeroOwnershipVerdict.DeletedInXero:
                return (XeroPurchasingOwnership.DeletedInXero(
                    "Purchase order", "order", judged.Ours?.PurchaseOrderNumber?.Trim() is { Length: > 0 } n ? n : judged.From!.Create.Number, judged.Ours?.Status,
                    "An issued order's lines are fixed, so if it is still wanted, key it in Xero by hand or check with whoever keeps the books.",
                    sourceGone), null);

            case XeroOwnershipVerdict.AnotherDocuments:
                return (XeroPurchasingOwnership.AnotherDocuments("Purchase order", "order", number!, sourceGone), null);

            case XeroOwnershipVerdict.NotOurs:
                return (XeroPurchasingOwnership.NotOurs(
                    "Purchase order", "order", number!,
                    "Check with whoever keeps the books which order that is, then Retry.",
                    sourceGone), null);

            default:
                return (null, null);
        }
    }

    /// <summary>
    /// Recovers one logged create by identity: re-sends its body under its
    /// own <c>Idempotency-Key</c> — Xero replays its first answer, the
    /// order's id (or, had the create never reached Xero, makes it now) — and
    /// reads that id back. A refused replay is <see cref="XeroRecovery.Unrecoverable"/>
    /// (never struck off the log: the create may still be in Xero).
    /// </summary>
    private async Task<(XeroPushResult? Failed, XeroRecoveredCreate<XeroWirePurchaseOrder>? Recovered)> RecoverAsync(
        XeroPurchasingSentCreate create, CancellationToken cancellationToken)
    {
        if (create.BodyAs<XeroWirePurchaseOrderWrite>() is not { } body)
            return (null, new(create, XeroRecovery.Unrecoverable, Problem: "TempestOS holds no copy of what it sent"));

        var replay = await _api.CreatePurchaseOrderAsync(body, create.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (replay.Outcome == ConnectorOutcome.Rejected)
            return (null, new(create, XeroRecovery.Unrecoverable, Problem: XeroPurchasingMapper.Problem(replay)));
        if (replay.Outcome != ConnectorOutcome.Ok)
            return (XeroPurchasingMapper.Failed(replay), null);

        var read = await _api.GetPurchaseOrderAsync(replay.Value!.PurchaseOrderID!, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
        {
            return read.NotFound
                ? (null, new(create, XeroRecovery.Gone, replay.Value with { Status = XeroPurchasingMapper.StatusDeleted }))
                : (XeroPurchasingMapper.Failed(read), null);
        }

        return (null, new(create, IsLive(read.Value!) ? XeroRecovery.Live : XeroRecovery.Gone, read.Value));
    }

    private static bool IsLive(XeroWirePurchaseOrder order) => XeroPurchasingMapper.Word(order.Status) != XeroPurchasingMapper.StatusDeleted;

    private async Task<XeroContactResolution> ResolveSupplierAsync(string tenantId, XeroPurchaseOrderSnapshot order, CancellationToken cancellationToken)
    {
        if (order.SupplierOrganisationReference is null)
        {
            return new XeroContactResolution(
                null, $"Purchase order {order.Reference} names no supplier TempestOS knows, so it has no Xero contact; choose its supplier under Customers & suppliers.", null);
        }

        return await _contacts.ResolveForPushAsync(tenantId, order.SupplierOrganisationReference, cancellationToken).ConfigureAwait(false);
    }

    private async Task<XeroLink> RecordStatusAsync(XeroLink link, string? status, CancellationToken cancellationToken)
    {
        var updated = link with { LastKnownXeroStatus = status ?? link.LastKnownXeroStatus, LastReadAtUtc = _time.GetUtcNow() };
        await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private XeroLink NewLink(string tenantId, XeroDocumentRef document, XeroWirePurchaseOrder order, string? contentHash, string linkedBy)
    {
        var now = _time.GetUtcNow();
        return new XeroLink(
            XeroLink.CurrentSchemaVersion, tenantId, document, order.PurchaseOrderID!, order.PurchaseOrderNumber,
            contentHash, XeroPurchasingMapper.Word(order.Status) ?? XeroPurchasingMapper.StatusDraft,
            AttachmentFileName: null, AttachmentContentHash: null, LinkedAtUtc: now, LastReadAtUtc: now, LinkedBy: linkedBy);
    }
}

/// <summary>The audit rows the purchasing handlers write (§6.7).</summary>
internal static class XeroPurchasingAudit
{
    /// <summary>TempestOS created the Xero record and linked it.</summary>
    public const string LinkCreated = "xero.link.created";

    /// <summary>A Xero record was found by its number after an uncertain answer (or entered by hand) and linked, instead of created again.</summary>
    public const string LinkReconciled = "xero.link.reconciled";

    /// <summary>Writes one row naming the document, operation, Xero id and number, idempotency key and attempt — never a token.</summary>
    public static async Task RecordAsync(IAuditRecorder? audit, string action, XeroLink link, XeroOutboxEntry entry, CancellationToken cancellationToken)
    {
        if (audit is null)
            return;

        await audit.RecordAsync(action, new Dictionary<string, string>
        {
            ["document"] = link.Document.TempestKey,
            ["kind"] = link.Document.Kind.ToString(),
            ["operation"] = entry.Operation.ToString(),
            ["xeroId"] = link.XeroId,
            ["xeroNumber"] = link.XeroNumber ?? string.Empty,
            ["idempotencyKey"] = entry.IdempotencyKey,
            ["attempt"] = entry.Attempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, cancellationToken).ConfigureAwait(false);
    }
}
