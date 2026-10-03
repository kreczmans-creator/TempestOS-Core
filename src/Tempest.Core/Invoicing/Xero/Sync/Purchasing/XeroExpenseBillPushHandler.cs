using Tempest.Core.Audit;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// Sends the expense-bill writes the <see cref="XeroExpenseBillPlanner"/>
/// queued (`v0.24.0` X5, D5, Q3, Q4, Q6; design §3, §4.4, §6.4):
/// <see cref="XeroOperation.PushExpenseBill"/> — create the expense's
/// <c>ACCPAY</c> bill as <c>DRAFT</c>, or replace its content while Xero still
/// holds it as a draft — and <see cref="XeroOperation.DeleteExpenseBill"/> —
/// delete the draft because the expense was deleted.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bill.</b> Numbered with the supplier's invoice number when
/// recorded, else <c>EXP-{expense id}</c> (Q4); against the expense's supplier,
/// or the configured "General expenses" contact when it names none (Q3,
/// <see cref="XeroGeneralExpensesContact"/>); one line with the recorded net,
/// the category's account code (X1), the input tax type and the recorded VAT
/// as <c>TaxAmount</c>. An expense recorded from a purchase order's lines is
/// never billed here (Q6).
/// </para>
/// <para>
/// <b>Never two bills.</b> Before a first create — and so before any resend
/// after a lost response — the handler applies
/// <see cref="XeroPurchasingOwnership"/>'s one rule: every create the
/// <see cref="XeroPurchasingCreateLog"/> recorded for the expense is read
/// back by the bill's id — recorded as soon as any answer revealed it, or,
/// while none is known and Xero still holds the key, by re-sending the create
/// under its own <c>Idempotency-Key</c> (<see cref="XeroPurchasingRecovery"/>)
/// — and a live bill so found is linked (<c>"reconciled"</c>) instead of
/// creating another; one deleted or voided in Xero is never linked or resent
/// (until the person's <em>Send again</em>). Only
/// then is Xero looked up by <c>InvoiceNumber</c> + <c>ContactID</c>, for a
/// bill in the way of a create, which is left untouched.
/// </para>
/// <para>
/// <b>Read before write.</b> An update or delete reads the bill first: it is
/// changed only while Xero holds it as <c>DRAFT</c>; otherwise the push is
/// refused with the reason ("approved in Xero; change it there") and nothing
/// is asked of Xero. Never throws for anything Xero or the network did
/// (`ADR-0151`); never approves, never emails (D3, D4).
/// </para>
/// </remarks>
public sealed class XeroExpenseBillPushHandler : IXeroPushHandler
{
    private readonly XeroAccountingApi _api;
    private readonly IXeroLinkStore _links;
    private readonly XeroPurchasingCreateLog _creates;
    private readonly IXeroExpenseSource _expenses;
    private readonly XeroContactLinker _contacts;
    private readonly XeroGeneralExpensesContact _generalContact;
    private readonly XeroTaxTypeResolver _taxTypes;
    private readonly XeroAccountCodeMap _accounts;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;
    private readonly XeroPurchasingRecovery.Calls<XeroWireBillWrite, XeroWireBill> _recovery;

    /// <summary>Initialises a new instance of the <see cref="XeroExpenseBillPushHandler"/> class.</summary>
    /// <param name="api">The typed Xero client (its <see cref="HttpClient"/> holds the safety handler).</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="creates">The create log: written just before each create is sent, and read before a bill found in Xero under the supplier's number is called the expense's own.</param>
    /// <param name="expenses">Reads expenses.</param>
    /// <param name="contacts">The X2 linker: the supplier's <c>ContactID</c>, or why the push is Blocked.</param>
    /// <param name="generalContact">The configured "General expenses" contact (Q3).</param>
    /// <param name="taxTypes">The X1 tax-type resolver (input side).</param>
    /// <param name="accounts">The X1 account-code map (per expense category).</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock links are stamped with; <see langword="null"/> for the system clock.</param>
    public XeroExpenseBillPushHandler(
        XeroAccountingApi api,
        IXeroLinkStore links,
        XeroPurchasingCreateLog creates,
        IXeroExpenseSource expenses,
        XeroContactLinker contacts,
        XeroGeneralExpensesContact generalContact,
        XeroTaxTypeResolver taxTypes,
        XeroAccountCodeMap accounts,
        IAuditRecorder? audit = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(creates);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(generalContact);
        ArgumentNullException.ThrowIfNull(taxTypes);
        ArgumentNullException.ThrowIfNull(accounts);

        _api = api;
        _links = links;
        _creates = creates;
        _expenses = expenses;
        _contacts = contacts;
        _generalContact = generalContact;
        _taxTypes = taxTypes;
        _accounts = accounts;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
        _recovery = new(
            (body, key, ct) => _api.CreateBillAsync(body, key, ct), (id, ct) => _api.GetBillAsync(id, ct),
            b => b.InvoiceID, b => b.InvoiceNumber, b => b.Status, IsLive);
    }

    /// <summary>The kind of document this handler sends.</summary>
    public XeroDocumentKind DocumentKind => XeroDocumentKind.ExpenseBill;

    /// <inheritdoc />
    public IReadOnlyCollection<XeroOperation> Operations { get; } = [XeroOperation.PushExpenseBill, XeroOperation.DeleteExpenseBill];

    /// <inheritdoc />
    public async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Document.Kind != XeroDocumentKind.ExpenseBill || !Guid.TryParse(entry.Document.TempestKey, out var expenseId))
            return new XeroPushResult(XeroPushOutcome.Rejected, $"The expense bill handler sends expenses only; {entry.Document.Kind} {entry.Document.TempestKey} is not one.");

        var expense = await _expenses.FindAsync(expenseId, cancellationToken).ConfigureAwait(false);

        return entry.Operation switch
        {
            XeroOperation.PushExpenseBill => await PushAsync(tenantId, entry, expenseId, expense, cancellationToken).ConfigureAwait(false),
            XeroOperation.DeleteExpenseBill => await DeleteAsync(tenantId, entry, expense, cancellationToken).ConfigureAwait(false),
            _ => new XeroPushResult(XeroPushOutcome.Rejected, $"The expense bill handler does not send {entry.Operation}."),
        };
    }

    private async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, Guid expenseId, XeroExpenseSnapshot? expense, CancellationToken cancellationToken)
    {
        // Never Failed: a Failed push holds the expense's queue, and the delete
        // queued behind it must still run to remove a bill a lost create made.
        if (expense is null)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Expense {expenseId:D} no longer exists in TempestOS; nothing is pushed.");

        var link = await _links.FindAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The Xero link for this expense was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        if (link is not null && string.Equals(link.LastPushedContentHash, entry.ContentHash, StringComparison.Ordinal))
            return new XeroPushResult(XeroPushOutcome.NothingToDo, Link: link);

        if (link is null && expense.SourcePurchaseOrderId is not null)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, "Recorded from a purchase order's lines: billed from the purchase order in Xero, not as a separate bill (Q6).");

        // A body other than the one this entry's key was first sent with would
        // be refused by Xero (S7): a changed expense is the newer entry's to push.
        var stale = !string.Equals(XeroPurchasingMapper.ContentHash(expense), entry.ContentHash, StringComparison.Ordinal);

        var contact = await ResolveContactAsync(tenantId, expense, cancellationToken).ConfigureAwait(false);
        if (!contact.IsLinked)
        {
            // A deleted expense needs no contact: Blocked would hold the delete
            // queued behind this push, which finds any bill a create made.
            return expense.IsDeleted
                ? new XeroPushResult(XeroPushOutcome.NothingToDo, "The expense was deleted; nothing is pushed.", Link: link)
                : new XeroPushResult(XeroPushOutcome.Blocked, contact.BlockedReason);
        }

        if (link is null)
        {
            var reconciled = await ReconcileAsync(tenantId, entry, expense, contact.ContactId!, stale, cancellationToken).ConfigureAwait(false);
            if (reconciled.Result is { } answered)
                return answered;

            link = reconciled.Link;
            if (link is null)
            {
                if (expense.IsDeleted)
                    return new XeroPushResult(XeroPushOutcome.NothingToDo, "The expense was deleted, and no bill TempestOS sent for it is live in Xero; nothing is sent.");

                if (stale)
                    return new XeroPushResult(XeroPushOutcome.NothingToDo, "The expense changed after this write was queued; the newer write creates the Xero bill.");

                var (createBody, blocked) = await BuildAsync(expense, contact.ToContactRef(), cancellationToken).ConfigureAwait(false);
                if (createBody is null)
                    return new XeroPushResult(XeroPushOutcome.Blocked, blocked);

                // Recorded before it goes, so a lost answer (or a crash mid-request)
                // still leaves proof this expense's create may be in Xero.
                // Its key is the entry's own, or a new one after the person's Send again.
                var createKey = reconciled.CreateKey;
                await _creates.RecordSendingAsync(
                    tenantId, entry.Document, createBody.InvoiceNumber, contact.ContactId!, createKey, cancellationToken,
                    value: XeroPurchasingOwnership.ValueOf(createBody), body: XeroPurchasingSentCreate.Serialise(createBody), sentAtUtc: _time.GetUtcNow()).ConfigureAwait(false);
                var created = await _api.CreateBillAsync(createBody, createKey, cancellationToken).ConfigureAwait(false);
                await _creates.RecordAnswerAsync(tenantId, entry.Document, createKey, created.Outcome, cancellationToken, created.Value?.InvoiceID).ConfigureAwait(false);
                if (created.Outcome != ConnectorOutcome.Ok)
                    return XeroPurchasingMapper.Failed(created);

                var createdLink = NewLink(tenantId, entry.Document, created.Value!, entry.ContentHash, XeroPurchasingMapper.LinkedByCreated);
                await _links.SaveAsync(createdLink, cancellationToken).ConfigureAwait(false);
                await XeroPurchasingAudit.RecordAsync(_audit, XeroPurchasingAudit.LinkCreated, createdLink, entry, cancellationToken).ConfigureAwait(false);
                return new XeroPushResult(XeroPushOutcome.Succeeded, Link: createdLink);
            }

            if (string.Equals(link.LastPushedContentHash, entry.ContentHash, StringComparison.Ordinal))
                return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
        }

        if (expense.IsDeleted)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, "The expense was deleted; its bill is deleted, not updated.", Link: link);

        if (stale)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, "The expense changed after this write was queued; the newer write updates the Xero bill.", Link: link);

        // Linked: replace the content only while Xero holds the bill as DRAFT.
        var read = await _api.GetBillAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
            return await FailedReadAsync(read, link, cancellationToken).ConfigureAwait(false);

        var status = XeroPurchasingMapper.Word(read.Value!.Status);
        link = await RecordStatusAsync(link, status, read.Value.InvoiceNumber, cancellationToken).ConfigureAwait(false);
        if (status is XeroPurchasingMapper.StatusDeleted or XeroPurchasingMapper.StatusVoided)
        {
            return new XeroPushResult(
                XeroPushOutcome.Rejected,
                $"Bill {link.XeroNumber ?? link.XeroId} was {status.ToLowerInvariant()} in Xero; TempestOS does not bill the expense again. To send the expense as a new bill, choose {XeroDocumentLinkActions.UnlinkActionName} on its Xero badge, then Send again.",
                Link: link);
        }

        if (status != XeroPurchasingMapper.StatusDraft)
            return NotADraft(link, status, "changed");

        var (body, reason) = await BuildAsync(expense, contact.ToContactRef(), cancellationToken).ConfigureAwait(false);
        if (body is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, reason);

        // Moving the bill to another number or contact: never onto a bill someone
        // else keyed into Xero under that pair (the same check a first create makes).
        if (!string.Equals(read.Value.InvoiceNumber?.Trim(), body.InvoiceNumber, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(read.Value.Contact?.ContactID, contact.ContactId, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await ForeignBillUnderAsync(tenantId, entry.Document, body.InvoiceNumber, contact.ContactId!, link.XeroId, cancellationToken).ConfigureAwait(false);
            if (clash.Failed is { } failedLookup)
                return failedLookup;
            if (clash.Found)
            {
                return new XeroPushResult(
                    XeroPushOutcome.Rejected,
                    $"Bill number {body.InvoiceNumber} is already used in Xero by another bill for this supplier, which TempestOS did not create; "
                    + $"the expense's own bill {link.XeroNumber ?? link.XeroId} is linked and left as it is. "
                    + "TempestOS never makes a second bill under a number someone else used. Change the supplier invoice number on the expense, then Retry.",
                    Link: link);
            }
        }

        var updated = await _api.UpdateBillAsync(link.XeroId, body, entry.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (updated.Outcome != ConnectorOutcome.Ok)
            return XeroPurchasingMapper.Failed(updated);

        link = link with
        {
            XeroNumber = updated.Value!.InvoiceNumber ?? link.XeroNumber,
            LastPushedContentHash = entry.ContentHash,
            LastKnownXeroStatus = XeroPurchasingMapper.Word(updated.Value.Status) ?? link.LastKnownXeroStatus,
            LastReadAtUtc = _time.GetUtcNow(),
        };
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
    }

    private async Task<XeroPushResult> DeleteAsync(string tenantId, XeroOutboxEntry entry, XeroExpenseSnapshot? expense, CancellationToken cancellationToken)
    {
        var link = await _links.FindAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            // The create may have landed with its answer lost: recover it before saying it never reached Xero.
            // An expense no longer readable, or an unresolved contact, still leaves the
            // creates the log says were sent to recover by their keys.
            string? contactId = null;
            if (expense is not null)
            {
                var contact = await ResolveContactAsync(tenantId, expense, cancellationToken).ConfigureAwait(false);
                contactId = contact.IsLinked ? contact.ContactId : null;
            }

            var reconciled = await ReconcileAsync(tenantId, entry, expense, contactId, stale: true, cancellationToken).ConfigureAwait(false);
            if (reconciled.Result is { } answered)
                return answered;
            if (reconciled.Link is null)
                return new XeroPushResult(XeroPushOutcome.NothingToDo, "No bill TempestOS sent for this expense is live in Xero; there is nothing to delete.");

            link = reconciled.Link;
        }

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The Xero link for this expense was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        var read = await _api.GetBillAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
        {
            if (!read.NotFound)
                return XeroPurchasingMapper.Failed(read);

            var gone = await RecordStatusAsync(link, XeroPurchasingMapper.StatusDeleted, null, cancellationToken).ConfigureAwait(false);
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Bill {link.XeroNumber ?? link.XeroId} is no longer in Xero.", Link: gone);
        }

        var status = XeroPurchasingMapper.Word(read.Value!.Status);
        link = await RecordStatusAsync(link, status, read.Value.InvoiceNumber, cancellationToken).ConfigureAwait(false);

        if (status is XeroPurchasingMapper.StatusDeleted or XeroPurchasingMapper.StatusVoided)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, Link: link);

        if (status != XeroPurchasingMapper.StatusDraft)
            return NotADraft(link, status, "deleted");

        var deleted = await _api.DeleteBillAsync(link.XeroId, entry.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (deleted.Outcome != ConnectorOutcome.Ok)
            return XeroPurchasingMapper.Failed(deleted);

        link = await RecordStatusAsync(link, XeroPurchasingMapper.Word(deleted.Value!.Status) ?? XeroPurchasingMapper.StatusDeleted, null, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
    }

    /// <summary>
    /// Before a first create, and before a delete with no link (§6.4 item 3),
    /// applies <see cref="XeroPurchasingOwnership"/>'s one rule — the same the
    /// purchase order handler applies. First every create the
    /// <see cref="XeroPurchasingCreateLog"/> recorded for this expense is
    /// recovered by identity (<see cref="XeroPurchasingRecovery"/>): read back
    /// by its id once known; while none is known and Xero still holds its key,
    /// its body re-sent under its own <c>Idempotency-Key</c> (Xero replays its
    /// first answer, the bill's id); otherwise cannot-tell — whatever number,
    /// contact or amounts the bill carries now. Only a live bill recovered so
    /// is linked; one deleted or voided in Xero is a tombstone, never linked or
    /// resent; a replay Xero refuses is cannot-tell. Only then is the expense's current number looked up for
    /// its contact, for a bill in the way of a create — never to call one
    /// ours.
    /// </summary>
    /// <remarks>
    /// A linked bill is updated while Xero holds it as a draft — or, should
    /// the current number be one another bill uses, is Rejected then, with the
    /// bill linked: <see cref="ForeignBillUnderAsync"/>. A bill linked to
    /// another TempestOS expense is that expense's (two expenses may carry one
    /// supplier invoice number) and is not in the way. Any other live bill is
    /// left untouched and reported once: NothingToDo when the expense is
    /// deleted or gone, Rejected (Retry) for a live push. A bill recovered
    /// from an earlier entry's create is linked with no pushed content, so the
    /// push that follows rewrites it to the current number, contact and
    /// amounts while Xero still holds it as a draft.
    /// </remarks>
    private async Task<(XeroPushResult? Result, XeroLink? Link, string CreateKey)> ReconcileAsync(
        string tenantId, XeroOutboxEntry entry, XeroExpenseSnapshot? expense, string? contactId, bool stale, CancellationToken cancellationToken)
    {
        var currentNumber = expense is null ? null : XeroPurchasingMapper.BillNumber(expense);
        var sent = await _creates.ListSentAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        var sourceGone = entry.Operation == XeroOperation.DeleteExpenseBill || expense is null || expense.IsDeleted;

        // The key this entry's create goes under: its own, or a new one after the person's Send again.
        var createKey = XeroPurchasingOwnership.CreateKey(entry.IdempotencyKey, sent);

        // Only a push that would send a create (again) has a create of its own to recover.
        var resendKey = !sourceGone && !stale ? createKey : null;

        var now = _time.GetUtcNow();
        var recovered = new List<XeroRecoveredCreate<XeroWireBill>>();
        foreach (var create in XeroPurchasingOwnership.ToRecover(sent))
        {
            var (failed, one) = await XeroPurchasingRecovery.RecoverAsync(_creates, tenantId, entry.Document, create, now, _recovery, cancellationToken).ConfigureAwait(false);
            if (failed is not null)
                return (failed, null, createKey);
            recovered.Add(one!);
        }

        var judged = XeroPurchasingOwnership.Judge(recovered, resendKey, sourceGone, [], XeroPurchasingOwnership.ValueOf);
        if (judged.Verdict == XeroOwnershipVerdict.NothingLive && currentNumber is not null && contactId is not null)
        {
            // Nothing of this expense's is live: is anything in the way of a create under its number and contact?
            var found = await _api.FindBillsAsync(currentNumber, contactId, cancellationToken).ConfigureAwait(false);
            if (found.Outcome != ConnectorOutcome.Ok)
                return (XeroPurchasingMapper.Failed(found), null, createKey);

            var live = found.Value!.Where(b => b.InvoiceID is not null && IsLive(b)).ToList();
            if (live.Count > 0)
            {
                var linkedElsewhere = await LinkedToOtherExpensesAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
                List<XeroWireBill> inTheWay = [.. live.Where(b => !linkedElsewhere.Contains(b.InvoiceID!))];
                var sentForOthers = XeroPurchasingOwnership.SentUnder(
                    await _creates.ListSentForOthersAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false), currentNumber, contactId);
                judged = XeroPurchasingOwnership.Judge(recovered, resendKey, sourceGone, inTheWay, XeroPurchasingOwnership.ValueOf, sentForOthers);
            }
        }

        switch (judged.Verdict)
        {
            case XeroOwnershipVerdict.Ours:
            {
                var ours = judged.Ours!;
                var create = judged.From!.Create;

                // This entry's own create, under the number and contact the expense still has: its content is what was sent.
                var landed = resendKey is not null
                             && string.Equals(create.IdempotencyKey, resendKey, StringComparison.Ordinal)
                             && string.Equals(create.Number, currentNumber, StringComparison.OrdinalIgnoreCase)
                             && string.Equals(create.ContactId, contactId, StringComparison.OrdinalIgnoreCase);

                var link = NewLink(tenantId, entry.Document, ours, landed ? entry.ContentHash : null, XeroPurchasingMapper.LinkedByReconciled);
                await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
                await XeroPurchasingAudit.RecordAsync(_audit, XeroPurchasingAudit.LinkReconciled, link, entry, cancellationToken).ConfigureAwait(false);
                return (null, link, createKey);
            }

            case XeroOwnershipVerdict.CannotTell:
                return (XeroPurchasingOwnership.CannotTell("Bill", "expense", judged.From!.Create.Number, judged.From.Problem, sourceGone), null, createKey);

            case XeroOwnershipVerdict.DeletedInXero:
                return (XeroPurchasingOwnership.DeletedInXero(
                    "Bill", "expense",
                    judged.Ours?.InvoiceNumber?.Trim() is { Length: > 0 } n ? n : judged.From!.Create.XeroNumber ?? judged.From.Create.Number,
                    judged.Ours?.Status ?? judged.From!.Create.GoneStatus,
                    XeroPurchasingSendAgain.ExpenseAdvice,
                    sourceGone), null, createKey);

            case XeroOwnershipVerdict.AnotherDocuments:
                return (XeroPurchasingOwnership.AnotherDocuments("Bill", "expense", currentNumber!, sourceGone), null, createKey);

            case XeroOwnershipVerdict.NotOurs:
                return (XeroPurchasingOwnership.NotOurs(
                    "Bill", "expense", currentNumber!,
                    "Change the supplier invoice number on the expense if it was mistyped, or check with whoever keeps the books which bill that is; then Retry.",
                    sourceGone), null, createKey);

            default:
                return (null, null, createKey);
        }
    }

    /// <summary>
    /// Whether Xero holds, under <paramref name="number"/> to
    /// <paramref name="contactId"/>, a live bill other than
    /// <paramref name="ownBillId"/> that no TempestOS expense is linked to —
    /// one someone keyed into Xero by hand, which a linked bill is never
    /// renumbered onto.
    /// </summary>
    private async Task<(bool Found, XeroPushResult? Failed)> ForeignBillUnderAsync(
        string tenantId, XeroDocumentRef document, string number, string contactId, string ownBillId, CancellationToken cancellationToken)
    {
        var found = await _api.FindBillsAsync(number, contactId, cancellationToken).ConfigureAwait(false);
        if (found.Outcome != ConnectorOutcome.Ok)
            return (false, XeroPurchasingMapper.Failed(found));

        var others = found.Value!
            .Where(b => b.InvoiceID is not null && IsLive(b) && !string.Equals(b.InvoiceID, ownBillId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (others.Count == 0)
            return (false, null);

        var linkedElsewhere = await LinkedToOtherExpensesAsync(tenantId, document, cancellationToken).ConfigureAwait(false);
        return (others.Any(b => !linkedElsewhere.Contains(b.InvoiceID!)), null);
    }

    private static bool IsLive(XeroWireBill bill) =>
        XeroPurchasingMapper.Word(bill.Status) is not (XeroPurchasingMapper.StatusDeleted or XeroPurchasingMapper.StatusVoided);

    /// <summary>The Xero ids of the bills already linked to an expense other than <paramref name="document"/> in the tenant.</summary>
    private async Task<IReadOnlySet<string>> LinkedToOtherExpensesAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken)
    {
        var links = await _links.ListAsync(tenantId, XeroDocumentKind.ExpenseBill, cancellationToken).ConfigureAwait(false);
        return links
            .Where(l => l.Document != document)
            .Select(l => l.XeroId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The bill's contact: the expense's supplier, or the "General expenses" contact when it names none (Q3) — or why the bill is Blocked.</summary>
    private async Task<XeroContactResolution> ResolveContactAsync(string tenantId, XeroExpenseSnapshot expense, CancellationToken cancellationToken)
    {
        if (expense.SupplierOrganisationReference is { } supplier)
            return await _contacts.ResolveForPushAsync(tenantId, supplier, cancellationToken).ConfigureAwait(false);

        if (expense.SupplierOrganisationIdUnresolved is { } unknown)
        {
            return new XeroContactResolution(
                null, $"The expense's supplier '{unknown}' is not a customer or supplier TempestOS knows; choose another supplier on the expense.", null);
        }

        var general = await _generalContact.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (general is null)
        {
            return new XeroContactResolution(
                null, "The expense names no supplier, and no \"General expenses\" contact is chosen for such bills; choose one in Settings (Xero), or a supplier on the expense.", null);
        }

        return await _contacts.ResolveForPushAsync(tenantId, general, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(XeroWireBillWrite? Body, string? BlockedReason)> BuildAsync(XeroExpenseSnapshot expense, XeroWireContactRef contact, CancellationToken cancellationToken)
    {
        var account = await _accounts.ResolveExpenseAsync(expense.Category, cancellationToken).ConfigureAwait(false);
        var rate = XeroPurchasingMapper.InferVatRate(expense.NetAmount, expense.VatAmount);
        var taxType = await _taxTypes.ResolveAsync(rate, VatTaxDirection.Purchases, cancellationToken).ConfigureAwait(false);

        var body = XeroPurchasingMapper.BuildBill(expense, contact, taxType, account, out var blocked);
        return (body, blocked);
    }

    private static XeroPushResult NotADraft(XeroLink link, string? status, string act) => new(
        XeroPushOutcome.Rejected,
        $"Xero holds bill {link.XeroNumber ?? link.XeroId} as {status ?? "an unknown status"}, not as a draft — it was approved in Xero, so TempestOS no longer changes it. "
        + $"The expense was {act} in TempestOS only; change the bill in Xero.",
        Link: link);

    private async Task<XeroPushResult> FailedReadAsync<T>(XeroApiResult<T> read, XeroLink link, CancellationToken cancellationToken)
    {
        if (!read.NotFound)
            return XeroPurchasingMapper.Failed(read);

        var gone = await RecordStatusAsync(link, XeroPurchasingMapper.StatusDeleted, null, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Rejected, $"Bill {link.XeroNumber ?? link.XeroId} was deleted in Xero. To send the expense again, choose {XeroDocumentLinkActions.UnlinkActionName} on its Xero badge, then Send again.", Link: gone);
    }

    private async Task<XeroLink> RecordStatusAsync(XeroLink link, string? status, string? number, CancellationToken cancellationToken)
    {
        var updated = link with
        {
            LastKnownXeroStatus = status ?? link.LastKnownXeroStatus,
            XeroNumber = number ?? link.XeroNumber,
            LastReadAtUtc = _time.GetUtcNow(),
        };
        await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private XeroLink NewLink(string tenantId, XeroDocumentRef document, XeroWireBill bill, string? contentHash, string linkedBy)
    {
        var now = _time.GetUtcNow();
        return new XeroLink(
            XeroLink.CurrentSchemaVersion, tenantId, document, bill.InvoiceID!, bill.InvoiceNumber,
            contentHash, XeroPurchasingMapper.Word(bill.Status) ?? XeroPurchasingMapper.StatusDraft,
            AttachmentFileName: null, AttachmentContentHash: null, LinkedAtUtc: now, LastReadAtUtc: now, LinkedBy: linkedBy);
    }
}
