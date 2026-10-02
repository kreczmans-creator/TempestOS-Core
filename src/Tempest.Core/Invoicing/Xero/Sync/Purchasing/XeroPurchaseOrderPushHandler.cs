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
/// link in the tenant, and only after looking its number up
/// (<c>GET PurchaseOrders/{PurchaseOrderNumber}</c>) and applying
/// <see cref="XeroPurchasingOwnership"/>'s one rule: an order provably this
/// order's own (a create whose answer was lost: its number, contact and
/// amounts match a create the log recorded) is linked (<c>"reconciled"</c>);
/// any other order under the number is left untouched and the push is
/// Rejected with the reason. Every write carries the entry's fixed
/// <c>Idempotency-Key</c>.
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
            body.Reference, XeroPurchasingOwnership.ValueOf(body)).ConfigureAwait(false);
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
            // The create may have landed with its answer lost: look before saying it never reached Xero.
            // An order no longer readable, or a supplier no longer linked, still leaves the
            // creates the log says were sent to look up.
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
    /// 3–4): looks Xero up by the order's number for its supplier's contact,
    /// then by every number and contact the <see cref="XeroPurchasingCreateLog"/>
    /// says a create for this order was sent with, and applies
    /// <see cref="XeroPurchasingOwnership"/>'s one rule — the same the bill
    /// handler applies. Only an order provably this order's own is linked
    /// (so a cancel deletes it); any other is left untouched and reported
    /// once: NothingToDo when the order is cancelled, gone or being deleted,
    /// Rejected (Retry) for a live push, never a second order under the number.
    /// </summary>
    /// <remarks>
    /// Xero keeps a live purchase order number unique, so any live order
    /// under the number that is not this order's own — another contact's,
    /// another TempestOS order's, or one keyed or changed in Xero — clashes
    /// with a create.
    /// </remarks>
    private async Task<(XeroPushResult? Result, XeroLink? Link)> ReconcileAsync(
        string tenantId, XeroOutboxEntry entry, XeroPurchaseOrderSnapshot? order, string? contactId, bool stale, CancellationToken cancellationToken)
    {
        var sent = await _creates.ListSentAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        var current = order is not null && contactId is not null ? (Number: order.Reference.Trim(), ContactId: contactId) : default((string Number, string ContactId)?);
        var pairs = XeroPurchasingOwnership.Pairs(current is { } now ? [now] : [], sent);
        var sourceGone = order is null || order.Status == PurchaseOrderStatus.Cancelled || entry.Operation == XeroOperation.DeletePurchaseOrder;

        // Only a push that would send a create (again) asks whether its own create was deleted in Xero.
        var resendKey = !sourceGone && !stale ? entry.IdempotencyKey : null;

        var byNumber = new Dictionary<string, IReadOnlyList<XeroWirePurchaseOrder>>(StringComparer.OrdinalIgnoreCase);
        var deletedComplete = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        IReadOnlySet<string>? linkedElsewhere = null;
        IReadOnlyList<XeroPurchasingSentCreate>? sentForOthers = null;
        (string Number, XeroOwnershipVerdict Verdict)? refusal = null;
        foreach (var (number, pairContactId) in pairs)
        {
            List<XeroWirePurchaseOrder> UnderPair(IEnumerable<XeroWirePurchaseOrder> orders) =>
                [.. orders.Where(o => string.Equals(o.Contact?.ContactID, pairContactId, StringComparison.OrdinalIgnoreCase)
                                      && !linkedElsewhere!.Contains(o.PurchaseOrderID!))];

            if (!byNumber.TryGetValue(number, out var all))
            {
                var found = await _api.FindPurchaseOrdersByNumberAsync(number, cancellationToken).ConfigureAwait(false);
                if (found.Outcome != ConnectorOutcome.Ok)
                    return (XeroPurchasingMapper.Failed(found), null);

                byNumber[number] = all = [.. found.Value!.Where(o => o.PurchaseOrderID is not null)];
            }

            var sentUnder = XeroPurchasingOwnership.SentUnder(sent, number, pairContactId);
            var entrySentHere = resendKey is not null && sentUnder.Any(s => string.Equals(s.IdempotencyKey, resendKey, StringComparison.Ordinal));
            if (!all.Any(IsLive) && !entrySentHere)
                continue;

            linkedElsewhere ??= await LinkedToOtherOrdersAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
            sentForOthers ??= await _creates.ListSentForOthersAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
            var sentForOthersUnder = XeroPurchasingOwnership.SentUnder(sentForOthers, number, pairContactId);
            XeroOwnershipJudgement<XeroWirePurchaseOrder> JudgeNow(bool complete) => XeroPurchasingOwnership.Judge(
                UnderPair(all), sentUnder, XeroPurchasingOwnership.ValueOf, IsLive, resendKey, sentForOthersUnder, complete);

            var judged = JudgeNow(deletedComplete.GetValueOrDefault(number, true));
            if ((judged.Verdict is XeroOwnershipVerdict.Ours || entrySentHere) && !deletedComplete.ContainsKey(number))
            {
                // Xero answers one order per number: before calling one ours, or resending a create
                // the log holds, read every deleted copy under it too (no date filter — a date is
                // free text a bookkeeper may edit), the evidence a live copy may be someone else's.
                var deleted = await _api.FindDeletedPurchaseOrdersAsync(number, cancellationToken).ConfigureAwait(false);
                if (deleted.Outcome != ConnectorOutcome.Ok)
                    return (XeroPurchasingMapper.Failed(deleted), null);

                deletedComplete[number] = deleted.Value!.Complete;
                byNumber[number] = all = [.. all, .. deleted.Value.Orders.Where(d => !all.Any(o => string.Equals(o.PurchaseOrderID, d.PurchaseOrderID, StringComparison.OrdinalIgnoreCase)))];
                judged = JudgeNow(deleted.Value.Complete);
            }

            // Xero keeps a live order number unique: a live order under it that is another
            // contact's, or another TempestOS order's, is in the way all the same.
            var verdict = judged.Verdict == XeroOwnershipVerdict.NothingLive && all.Any(IsLive) ? XeroOwnershipVerdict.NotOurs : judged.Verdict;
            switch (verdict)
            {
                case XeroOwnershipVerdict.Ambiguous:
                    return (XeroPurchasingOwnership.Ambiguous("purchase orders", number, judged.Count, "order", sourceGone), null);

                case XeroOwnershipVerdict.CannotTell:
                    return (XeroPurchasingOwnership.CannotTell("purchase orders", number, "order", sourceGone), null);

                case XeroOwnershipVerdict.Ours:
                {
                    var ours = judged.Ours!;

                    // This entry's own create landed with what it sent, to the contact the order still has.
                    var landed = !stale
                                 && entry.Operation == XeroOperation.PushPurchaseOrder
                                 && current is { } pair
                                 && string.Equals(number, pair.Number, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(pairContactId, pair.ContactId, StringComparison.OrdinalIgnoreCase)
                                 && XeroPurchasingOwnership.SentByThisEntry(entry, sentUnder, XeroPurchasingOwnership.ValueOf(ours));

                    var link = NewLink(tenantId, entry.Document, ours, landed ? entry.ContentHash : null, XeroPurchasingMapper.LinkedByReconciled);
                    await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
                    await XeroPurchasingAudit.RecordAsync(_audit, XeroPurchasingAudit.LinkReconciled, link, entry, cancellationToken).ConfigureAwait(false);
                    return (null, link);
                }

                case XeroOwnershipVerdict.NotOurs or XeroOwnershipVerdict.AnotherDocuments or XeroOwnershipVerdict.DeletedInXero:
                    // Left as it is: keep looking under the other pairs for this order's own.
                    // A live order in the way is reported before a deleted one.
                    if (refusal is null || (refusal.Value.Verdict == XeroOwnershipVerdict.DeletedInXero && verdict != XeroOwnershipVerdict.DeletedInXero))
                        refusal = (number, verdict);
                    break;
            }
        }

        return refusal switch
        {
            null => (null, null),
            { Verdict: XeroOwnershipVerdict.DeletedInXero } r => (XeroPurchasingOwnership.DeletedInXero(
                "Purchase order", "order", r.Number,
                "An issued order's lines are fixed, so if it is still wanted, key it in Xero by hand or check with whoever keeps the books."), null),
            { Verdict: XeroOwnershipVerdict.AnotherDocuments } r => (XeroPurchasingOwnership.AnotherDocuments("Purchase order", "order", r.Number, sourceGone), null),
            { } r => (XeroPurchasingOwnership.NotOurs(
                "Purchase order", "order", r.Number,
                "Check with whoever keeps the books which order that is, then Retry.",
                sourceGone), null),
        };
    }

    private static bool IsLive(XeroWirePurchaseOrder order) => XeroPurchasingMapper.Word(order.Status) != XeroPurchasingMapper.StatusDeleted;

    /// <summary>The Xero ids of the orders already linked to a TempestOS purchase order other than <paramref name="document"/> in the tenant.</summary>
    private async Task<IReadOnlySet<string>> LinkedToOtherOrdersAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken)
    {
        var links = await _links.ListAsync(tenantId, XeroDocumentKind.PurchaseOrder, cancellationToken).ConfigureAwait(false);
        return links
            .Where(l => l.Document != document)
            .Select(l => l.XeroId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

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
