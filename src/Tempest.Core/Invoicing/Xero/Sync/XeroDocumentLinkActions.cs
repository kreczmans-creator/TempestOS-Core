using System.Globalization;
using System.Text.Json;
using Tempest.Core.Audit;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>What one of the person's link actions did (<see cref="XeroDocumentLinkActions"/>), in the words shown to them.</summary>
/// <param name="Done">Whether the action happened.</param>
/// <param name="Message">What to show: the confirmation, or why nothing was done.</param>
public sealed record XeroLinkActionResult(bool Done, string Message);

/// <summary>
/// The one Xero record found under a number (<see cref="XeroDocumentLinkActions.FindByNumberAsync"/>),
/// shown to the person for confirmation before it is linked.
/// </summary>
/// <param name="Document">The TempestOS record it would be linked to.</param>
/// <param name="XeroId">Xero's id for it (<c>PurchaseOrderID</c>, or <c>InvoiceID</c> for a bill).</param>
/// <param name="Number">Its number in Xero.</param>
/// <param name="Status">Its status word in Xero, verbatim.</param>
/// <param name="ContactName">The supplier Xero holds it against, when Xero said.</param>
/// <param name="Total">Its total in Xero, when Xero said.</param>
/// <param name="CurrencyCode">Its currency in Xero, when Xero said.</param>
public sealed record XeroNumberMatch(
    XeroDocumentRef Document, string XeroId, string Number, string? Status, string? ContactName, decimal? Total, string? CurrencyCode)
{
    /// <summary>The match in one line, for the confirmation: number, supplier, total and status.</summary>
    public string Describe() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Number}{(string.IsNullOrWhiteSpace(ContactName) ? string.Empty : $" for {ContactName}")}"
            + $"{(Total is { } total ? $", {(string.IsNullOrWhiteSpace(CurrencyCode) ? string.Empty : CurrencyCode + " ")}{total:0.00}" : string.Empty)}"
            + $"{(string.IsNullOrWhiteSpace(Status) ? string.Empty : $", {Status} in Xero")}");
}

/// <summary>What looking a number up in Xero found: one record to confirm, or why there is none.</summary>
/// <param name="Match">The one live record found; <see langword="null"/> when there is none to link.</param>
/// <param name="Reason">Why there is nothing to link; <see langword="null"/> when <paramref name="Match"/> is set.</param>
public sealed record XeroNumberLookup(XeroNumberMatch? Match, string? Reason);

/// <summary>
/// The person's deliberate link actions on a quote, purchase order or bill
/// (`v0.24.0` review-board fixes M5 and m15, `ADR-0162`; design §5). An
/// invoice is never unlinked: its link is re-imported from the request's own
/// <c>ExternalId</c> at start-up, and a deleted or voided invoice is billed
/// again by raising a new invoice request.
/// <list type="bullet">
/// <item><b>Unlink from Xero</b> — for a record whose Xero copy was deleted
/// (or voided) there. Xero is asked first, so a record restored there is never
/// unlinked; the link is then removed, audited
/// (<see cref="AuditUnlinked"/>), and remembered as unlinked by the person.
/// Nothing is sent: a record deleted in Xero on purpose stays out of Xero.</item>
/// <item><b>Send again</b> — after an unlink, the person's explicit request
/// to send the record to Xero again as a new draft, under a new
/// <c>Idempotency-Key</c> (the deleted record's writes no longer count as
/// sent). Audited (<see cref="AuditSendAgain"/>).</item>
/// <item><b>I found it in Xero — link by Xero number</b> — for a purchase
/// order or bill whose create's answer was lost (X5 <em>Can't tell</em>): the
/// person looks the record up by its Xero number, confirms what was found, and
/// it is linked (audited, <see cref="AuditLinkedByNumber"/>); the failed write
/// is then retried against the linked record, so nothing is created twice.</item>
/// </list>
/// </summary>
/// <remarks>
/// Every refusal says why, and changes nothing. The only calls to Xero are
/// reads (<c>GET</c>): an unlink's check and a number lookup. A contact is
/// never unlinked here — that is <see cref="XeroContactLinker"/>'s.
/// </remarks>
public sealed class XeroDocumentLinkActions
{
    /// <summary>The persistence collection remembering each record the person unlinked, keyed as the link was (<see cref="PersistenceXeroLinkStore.KeyFor"/>).</summary>
    public const string Collection = "Xero.UnlinkedDocuments";

    /// <summary>The audit action written when the person unlinks a record from Xero (design §6.7).</summary>
    public const string AuditUnlinked = XeroContactLinker.AuditLinkUnlinked;

    /// <summary>The audit action written when the person links a record found in Xero by its number (design §6.7).</summary>
    public const string AuditLinkedByNumber = XeroContactLinker.AuditLinkLinked;

    /// <summary>The audit action written when the person sends an unlinked record to Xero again.</summary>
    public const string AuditSendAgain = "xero.link.send-again";

    /// <summary>The action's name, as every message that points to it says it.</summary>
    public const string UnlinkActionName = "Unlink from Xero";

    /// <summary>The <em>Can't tell</em> action's name.</summary>
    public const string LinkByNumberActionName = "I found it in Xero — link by Xero number";

    /// <summary>
    /// What to tell whoever keeps the books when a purchase order or bill reads
    /// <em>Can't tell</em>: what to look for in Xero, and what to do either way.
    /// </summary>
    public const string BookkeeperGuidance =
        "TempestOS sent this to Xero but never heard back, so it cannot tell whether Xero has it. "
        + "Ask whoever keeps the books to look in Xero for it (its number, supplier and total). "
        + "If it is there, choose \"" + LinkByNumberActionName + "\" and enter its Xero number. "
        + "If it is not, leave it: TempestOS never sends it again by itself, so it can never be in Xero twice.";

    /// <summary>Why a customer or supplier is refused by <see cref="UnlinkAsync"/> and <see cref="SendAgainAsync"/>.</summary>
    public const string ContactRefusal = "A customer or supplier is unlinked under Customers & suppliers, not here.";

    private const string Deleted = "DELETED";
    private const string Voided = "VOIDED";
    private const string LinkedByPerson = "linked";

    private readonly XeroSyncParts _parts;
    private readonly XeroSyncService _engine;
    private readonly IPersistenceStore _store;
    private readonly XeroAccountingApi? _api;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="XeroDocumentLinkActions"/> class.</summary>
    /// <param name="parts">The link store, outbox, create log and planners (X6).</param>
    /// <param name="engine">The engine: re-plans, retries (audited) and is woken after each action.</param>
    /// <param name="store">Where the unlinked records are remembered (<see cref="Collection"/>).</param>
    /// <param name="api">The typed client, for the read-only checks; <see langword="null"/> unlinks on the last reading alone and finds nothing by number.</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    public XeroDocumentLinkActions(
        XeroSyncParts parts, XeroSyncService engine, IPersistenceStore store, XeroAccountingApi? api = null, IAuditRecorder? audit = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(store);

        _parts = parts;
        _engine = engine;
        _store = store;
        _api = api;
        _audit = audit;
        _time = timeProvider ?? engine.Clock;
    }

    /// <summary>Whether Xero can be asked about a record by number (<see cref="FindByNumberAsync"/>).</summary>
    public bool CanLookUpInXero => _api is not null;

    /// <summary>
    /// Whether <em>Unlink from Xero</em> is offered for <paramref name="document"/>:
    /// it is a quote, purchase order or bill (never an invoice), linked in the connected
    /// organisation, and Xero last read it as deleted or voided. Local state
    /// only, never a network call.
    /// </summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<bool> CanUnlinkAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind is XeroDocumentKind.Contact or XeroDocumentKind.Invoice
            || await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not { } tenantId)
            return false;

        return await _parts.Links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false) is { } link
               && !PersistenceXeroLinkStore.IsFromNewerVersion(link)
               && IsGone(link.LastKnownXeroStatus);
    }

    /// <summary>
    /// Whether the person unlinked <paramref name="document"/> and has not yet
    /// sent it again (it is not linked). Local state only.
    /// </summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<bool> WasUnlinkedAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not { } tenantId)
            return false;

        return await _store.ReadAsync(Collection, PersistenceXeroLinkStore.KeyFor(tenantId, document), cancellationToken).ConfigureAwait(false) is not null
               && await _parts.Links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false) is null;
    }

    /// <summary>
    /// <em>Unlink from Xero</em>: removes <paramref name="document"/>'s link
    /// once Xero confirms its copy is deleted (or voided), audited. A
    /// purchase order's or bill's create is recorded as gone, so it is never
    /// recovered or re-sent by itself. Nothing is sent to Xero.
    /// </summary>
    /// <param name="document">The quote, purchase order or bill (an invoice is refused).</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    public async Task<XeroLinkActionResult> UnlinkAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind == XeroDocumentKind.Contact)
            return new XeroLinkActionResult(false, ContactRefusal);

        if (document.Kind == XeroDocumentKind.Invoice)
            return new XeroLinkActionResult(false, "An invoice stays linked to its Xero copy; nothing was unlinked. To bill this work again, raise a new invoice request.");

        if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not { } tenantId)
            return new XeroLinkActionResult(false, "No Xero organisation is connected; nothing was unlinked.");

        if (await _parts.Links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false) is not { } link)
            return new XeroLinkActionResult(false, "This record is not linked to Xero; there is nothing to unlink.");

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroLinkActionResult(false, $"The Xero link for this record was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        var name = $"{KindWord(document.Kind)} {link.XeroNumber ?? link.XeroId}";
        string goneStatus;
        if (_api is not null)
        {
            var (status, problem) = await ReadStatusAsync(document.Kind, link.XeroId, cancellationToken).ConfigureAwait(false);
            if (problem is not null)
                return new XeroLinkActionResult(false, $"Xero could not be asked whether {name} is still there ({problem}); nothing was unlinked. Try again when Xero is reachable.");

            if (!IsGone(status))
            {
                return new XeroLinkActionResult(
                    false,
                    $"Xero holds {name} as {status ?? "an unknown status"}: it was not deleted there (or was restored), so it stays linked. Nothing was unlinked.");
            }

            goneStatus = status!;
        }
        else if (IsGone(link.LastKnownXeroStatus))
        {
            goneStatus = link.LastKnownXeroStatus!.Trim().ToUpperInvariant();
        }
        else
        {
            return new XeroLinkActionResult(false, $"Xero last read {name} as {link.LastKnownXeroStatus ?? "not read yet"}; only a record deleted or voided in Xero is unlinked.");
        }

        // A purchase order's or bill's create is recorded gone, so ownership
        // never recovers the deleted record, and nothing re-sends it until the
        // person chooses Send again.
        if (_parts.Creates is { } creates && document.Kind is XeroDocumentKind.PurchaseOrder or XeroDocumentKind.ExpenseBill)
        {
            foreach (var sent in await creates.ListSentAsync(tenantId, document, cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(sent.XeroId, link.XeroId, StringComparison.OrdinalIgnoreCase) && sent.GoneStatus is null)
                    await creates.RecordGoneAsync(tenantId, document, sent.IdempotencyKey, link.XeroId, goneStatus, link.XeroNumber, cancellationToken).ConfigureAwait(false);
            }
        }

        // The mark is written before the link is removed, so there is never a
        // moment when the record is unlinked but not marked: the planner, the
        // push handler and Retry all read it.
        var now = _time.GetUtcNow();
        await _store.WriteAsync(
            Collection, PersistenceXeroLinkStore.KeyFor(tenantId, document),
            JsonSerializer.Serialize(new UnlinkedRecord(link.XeroId, link.XeroNumber, goneStatus, now)), cancellationToken).ConfigureAwait(false);
        await _parts.Links.UnlinkAsync(tenantId, document, cancellationToken).ConfigureAwait(false);

        // Writes already queued (or failed) for the record are set aside, so
        // neither the drain nor Retry all sends them; Send again re-plans.
        if (_parts.Outbox is PersistenceXeroOutbox outbox)
            await outbox.SetAsideOpenAsync(document, cancellationToken).ConfigureAwait(false);

        await AuditAsync(AuditUnlinked, document, link.XeroId, link.XeroNumber, new Dictionary<string, string>
        {
            ["status"] = goneStatus,
            ["by"] = "person",
            ["reason"] = $"{KindWord(document.Kind)} {goneStatus.ToLowerInvariant()} in Xero",
        }, cancellationToken).ConfigureAwait(false);

        _engine.Signal();
        return new XeroLinkActionResult(true, $"Unlinked {name} from Xero. Nothing was sent; choose Send again to send it to Xero as a new draft.");
    }

    /// <summary>
    /// <em>Send again</em> after an unlink: the record goes to Xero again as a
    /// new draft, under a new <c>Idempotency-Key</c>. Refused, with the
    /// reason, for a record the person did not unlink, one linked again, or an
    /// invoice (a new invoice request is raised instead).
    /// </summary>
    /// <param name="document">The quote, purchase order or bill.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    public async Task<XeroLinkActionResult> SendAgainAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind == XeroDocumentKind.Contact)
            return new XeroLinkActionResult(false, ContactRefusal);

        if (document.Kind == XeroDocumentKind.Invoice)
            return new XeroLinkActionResult(false, "An invoice is not sent again: raise a new invoice request to bill the work again.");

        if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not { } tenantId)
            return new XeroLinkActionResult(false, "No Xero organisation is connected; nothing was queued.");

        if (!await WasUnlinkedAsync(document, cancellationToken).ConfigureAwait(false))
            return new XeroLinkActionResult(false, $"Nothing was unlinked from Xero for this record, so there is nothing to send again; choose {UnlinkActionName} first.");

        if (_parts.Creates is { } creates && document.Kind is XeroDocumentKind.PurchaseOrder or XeroDocumentKind.ExpenseBill)
            await creates.ReleaseGoneAsync(tenantId, document, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        // The deleted record's writes no longer count as sent, so the push and
        // its PDF are queued afresh, each under a new key.
        if (_parts.Outbox is PersistenceXeroOutbox outbox)
            await outbox.ForgetSentAsync(document, cancellationToken).ConfigureAwait(false);

        // The mark is cleared first: Retry refuses an unlinked record's writes.
        await _store.DeleteAsync(Collection, PersistenceXeroLinkStore.KeyFor(tenantId, document), cancellationToken).ConfigureAwait(false);

        foreach (var failed in (await _parts.Outbox.ListForDocumentAsync(document, cancellationToken).ConfigureAwait(false)).Where(e => e.State == XeroOutboxState.Failed))
            await _engine.RetryAsync(failed.Id, cancellationToken).ConfigureAwait(false);

        await AuditAsync(AuditSendAgain, document, null, null, null, cancellationToken).ConfigureAwait(false);

        await _engine.PlanDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        _engine.Signal();

        var queued = (await _parts.Outbox.ListForDocumentAsync(document, cancellationToken).ConfigureAwait(false))
            .Any(e => e.State is XeroOutboxState.Pending or XeroOutboxState.InFlight or XeroOutboxState.Unknown);
        return queued
            ? new XeroLinkActionResult(true, "Queued to be sent to Xero again as a new draft.")
            : new XeroLinkActionResult(false, "TempestOS has nothing to send to Xero for this record now (for example it was cancelled); nothing was queued.");
    }

    /// <summary>
    /// Looks <paramref name="number"/> up in Xero for a purchase order or bill
    /// reading <em>Can't tell</em> — read only. Finds the one live record under
    /// that number that no other TempestOS record is linked to; anything else
    /// is a reason, and nothing is linked.
    /// </summary>
    /// <param name="document">The purchase order or bill.</param>
    /// <param name="number">The number the person found it under in Xero.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    public async Task<XeroNumberLookup> FindByNumberAsync(XeroDocumentRef document, string number, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind is not (XeroDocumentKind.PurchaseOrder or XeroDocumentKind.ExpenseBill))
            return new XeroNumberLookup(null, "Only a purchase order or bill is linked by its Xero number.");

        if (string.IsNullOrWhiteSpace(number))
            return new XeroNumberLookup(null, "Enter the number Xero shows for it.");

        if (_api is not { } api)
            return new XeroNumberLookup(null, "Xero cannot be asked from here.");

        if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not { } tenantId)
            return new XeroNumberLookup(null, "No Xero organisation is connected.");

        if (await _parts.Links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false) is not null)
            return new XeroNumberLookup(null, "This record is already linked to Xero.");

        var wanted = number.Trim();
        List<XeroNumberMatch> live;
        if (document.Kind == XeroDocumentKind.PurchaseOrder)
        {
            var found = await api.FindPurchaseOrdersByNumberAsync(wanted, cancellationToken).ConfigureAwait(false);
            if (found.Outcome != ConnectorOutcome.Ok)
                return new XeroNumberLookup(null, $"Xero could not be asked ({found.Reason ?? found.Outcome.ToString()}); nothing was linked.");

            live = [.. found.Value!
                .Where(o => !IsGone(o.Status) && !string.IsNullOrWhiteSpace(o.PurchaseOrderID))
                .Select(o => new XeroNumberMatch(document, o.PurchaseOrderID!, o.PurchaseOrderNumber ?? wanted, Word(o.Status), o.Contact?.Name, o.Total, o.CurrencyCode))];
        }
        else
        {
            if (wanted.Contains(',', StringComparison.Ordinal))
                return new XeroNumberLookup(null, "A bill number with a comma cannot be looked up; link it in Xero's own way instead.");

            var found = await api.FindInvoicesByNumberAsync(wanted, cancellationToken).ConfigureAwait(false);
            if (found.Outcome != ConnectorOutcome.Ok)
                return new XeroNumberLookup(null, $"Xero could not be asked ({found.Reason ?? found.Outcome.ToString()}); nothing was linked.");

            live = [.. found.Value!
                .Where(i => string.Equals(i.Type, XeroWire.InvoiceTypeBill, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(i.InvoiceNumber?.Trim(), wanted, StringComparison.OrdinalIgnoreCase)
                            && !IsGone(i.Status) && !string.IsNullOrWhiteSpace(i.InvoiceID))
                .Select(i => new XeroNumberMatch(document, i.InvoiceID!, i.InvoiceNumber ?? wanted, Word(i.Status), i.Contact?.Name, i.Total, i.CurrencyCode))];
        }

        var word = KindWord(document.Kind);
        if (live.Count == 0)
            return new XeroNumberLookup(null, $"Xero holds no {word} numbered {wanted} that is not deleted or voided; nothing was linked.");

        if (live.Count > 1)
            return new XeroNumberLookup(null, $"Xero holds {live.Count} {word}s numbered {wanted}; TempestOS cannot tell which is this one, so nothing was linked.");

        var match = live[0];
        var linkedElsewhere = (await _parts.Links.ListAsync(tenantId, document.Kind, cancellationToken).ConfigureAwait(false))
            .Any(l => string.Equals(l.XeroId, match.XeroId, StringComparison.OrdinalIgnoreCase) && l.Document != document);
        return linkedElsewhere
            ? new XeroNumberLookup(null, $"Xero's {word} {wanted} is already linked to another TempestOS record; nothing was linked.")
            : new XeroNumberLookup(match, null);
    }

    /// <summary>
    /// Links the record the person confirmed (<see cref="FindByNumberAsync"/>'s
    /// match) to <see cref="XeroNumberMatch.Document"/>, audited: Xero is read
    /// again first (still there, not deleted), the lost create is recorded as
    /// having made it, and the failed write is retried against the linked
    /// record — so nothing is created twice.
    /// </summary>
    /// <param name="match">The record the person confirmed.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    public async Task<XeroLinkActionResult> LinkByNumberAsync(XeroNumberMatch match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);

        var document = match.Document;
        if (document.Kind is not (XeroDocumentKind.PurchaseOrder or XeroDocumentKind.ExpenseBill))
            return new XeroLinkActionResult(false, "Only a purchase order or bill is linked by its Xero number.");

        if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not { } tenantId)
            return new XeroLinkActionResult(false, "No Xero organisation is connected; nothing was linked.");

        if (await _parts.Links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false) is not null)
            return new XeroLinkActionResult(false, "This record is already linked to Xero; nothing was changed.");

        var status = match.Status;
        if (_api is not null)
        {
            var (read, problem) = await ReadStatusAsync(document.Kind, match.XeroId, cancellationToken).ConfigureAwait(false);
            if (problem is not null)
                return new XeroLinkActionResult(false, $"Xero could not be asked about {match.Number} again ({problem}); nothing was linked.");

            status = read;
        }

        if (IsGone(status))
            return new XeroLinkActionResult(false, $"Xero now holds {match.Number} as {status}; nothing was linked.");

        var now = _time.GetUtcNow();
        await _parts.Links.SaveAsync(
            new XeroLink(XeroLink.CurrentSchemaVersion, tenantId, document, match.XeroId, match.Number, null, status, null, null, now, now, LinkedByPerson),
            cancellationToken).ConfigureAwait(false);

        // The create whose answer was lost made this record (the person says
        // so): from now on it is only read back by this id, never re-sent.
        if (_parts.Creates is { } creates
            && (await creates.ListSentAsync(tenantId, document, cancellationToken).ConfigureAwait(false)).LastOrDefault(s => s.XeroId is null && !s.IsTombstone) is { } lost)
        {
            await creates.RecordXeroIdAsync(tenantId, document, lost.IdempotencyKey, match.XeroId, cancellationToken).ConfigureAwait(false);
        }

        await AuditAsync(AuditLinkedByNumber, document, match.XeroId, match.Number, new Dictionary<string, string>
        {
            ["status"] = status ?? "unknown",
            ["by"] = "person",
            ["reason"] = "found in Xero by number after Can't tell",
        }, cancellationToken).ConfigureAwait(false);

        foreach (var failed in (await _parts.Outbox.ListForDocumentAsync(document, cancellationToken).ConfigureAwait(false)).Where(e => e.State == XeroOutboxState.Failed))
            await _engine.RetryAsync(failed.Id, cancellationToken).ConfigureAwait(false);

        _engine.Signal();
        return new XeroLinkActionResult(true, $"Linked to {KindWord(document.Kind)} {match.Number} in Xero.");
    }

    private async Task<(string? Status, string? Problem)> ReadStatusAsync(XeroDocumentKind kind, string xeroId, CancellationToken cancellationToken)
    {
        var api = _api!;
        (ConnectorOutcome Outcome, bool NotFound, string? Status, string? Reason) read = kind switch
        {
            XeroDocumentKind.Quote => From(await api.GetQuoteAsync(xeroId, cancellationToken).ConfigureAwait(false), q => q.Status),
            XeroDocumentKind.Invoice => From(await api.GetInvoiceAsync(xeroId, cancellationToken).ConfigureAwait(false), i => i.Status),
            XeroDocumentKind.PurchaseOrder => From(await api.GetPurchaseOrderAsync(xeroId, cancellationToken).ConfigureAwait(false), o => o.Status),
            XeroDocumentKind.ExpenseBill => From(await api.GetBillAsync(xeroId, cancellationToken).ConfigureAwait(false), b => b.Status),
            _ => (ConnectorOutcome.Rejected, false, null, "not a document"),
        };

        if (read.NotFound)
            return (Deleted, null);

        return read.Outcome == ConnectorOutcome.Ok
            ? (Word(read.Status), null)
            : (null, read.Reason ?? read.Outcome.ToString());
    }

    private static (ConnectorOutcome, bool, string?, string?) From<T>(XeroApiResult<T> result, Func<T, string?> status) =>
        (result.Outcome, result.NotFound, result.Outcome == ConnectorOutcome.Ok && result.Value is { } value ? status(value) : null, result.Reason);

    private async Task AuditAsync(
        string action, XeroDocumentRef document, string? xeroId, string? number, IReadOnlyDictionary<string, string>? extra, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;

        var detail = new Dictionary<string, string>
        {
            ["document"] = document.TempestKey,
            ["kind"] = document.Kind.ToString(),
        };
        if (xeroId is not null)
            detail["xeroId"] = xeroId;
        if (number is not null)
            detail["xeroNumber"] = number;
        foreach (var (key, value) in extra ?? new Dictionary<string, string>())
            detail[key] = value;

        await _audit.RecordAsync(action, detail, cancellationToken).ConfigureAwait(false);
    }

    private static string? Word(string? status) => string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();

    private static bool IsGone(string? status) =>
        Word(status) is Deleted or Voided;

    private static string KindWord(XeroDocumentKind kind) => kind switch
    {
        XeroDocumentKind.Quote => "quote",
        XeroDocumentKind.Invoice => "invoice",
        XeroDocumentKind.PurchaseOrder => "purchase order",
        XeroDocumentKind.ExpenseBill => "bill",
        _ => "record",
    };

    private sealed record UnlinkedRecord(string XeroId, string? XeroNumber, string Status, DateTimeOffset UnlinkedAtUtc);
}
