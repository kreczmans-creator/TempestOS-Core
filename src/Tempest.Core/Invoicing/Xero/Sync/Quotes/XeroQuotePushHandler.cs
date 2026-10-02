using System.Globalization;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Quotations;

namespace Tempest.Core.Invoicing.Xero.Sync.Quotes;

/// <summary>
/// Sends the quote writes the <see cref="XeroQuotePlanner"/> queued
/// (`v0.24.0` X3, D2, Q1; design §3, §4.1, §6.4):
/// <see cref="XeroOperation.PushQuote"/> — create the Xero quote as
/// <c>DRAFT</c> with the same <c>QuoteNumber</c>, or replace its content
/// while Xero still holds it as <c>DRAFT</c> — and
/// <see cref="XeroOperation.SetQuoteStatus"/> — <c>SENT</c>, then
/// <c>ACCEPTED</c> or <c>DECLINED</c>, following TempestOS.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never two quotes.</b> A create is issued only when the quotation has no
/// link in the tenant, and only after looking the number up
/// (<c>GET Quotes?QuoteNumber=</c>): a quote TempestOS made (same number and
/// contact) is linked (<c>"reconciled"</c>) instead — the lost-response case
/// — and a quote someone else made with the number Fails with the reason.
/// Every write carries the entry's fixed <c>Idempotency-Key</c>; an entry
/// whose quotation changed since it was queued sends nothing (a body other
/// than the one first sent under its key would be refused by Xero), and the
/// planner's newer entry carries the new content.
/// </para>
/// <para>
/// <b>Read before write.</b> Each update reads the Xero quote first: content
/// is replaced only while it is <c>DRAFT</c> (Q1 — once <c>SENT</c> a new
/// approved revision is refused with the reason, never sent; when there is
/// nothing left to push — TempestOS has sent or answered the quotation, or
/// the quote was just found by its number already past <c>DRAFT</c> — the
/// link is recorded and the answer is NothingToDo with the drift note, so
/// the status changes queued behind it still run); a status is moved only
/// along <c>DRAFT → SENT → ACCEPTED | DECLINED</c>, so Xero is never asked
/// for a transition it refuses; a status already reached (a lost response)
/// is simply recorded. A status-only update carries the quote's number,
/// contact and date as Xero holds them and never its lines.
/// </para>
/// <para>
/// <b>A write that landed with its answer lost, then a change by hand.</b>
/// Before any decision the handler records what Xero actually holds, never
/// what an attempt count suggests: exactly this entry's content
/// (<see cref="XeroQuoteMapper.HoldsContent"/>) records its hash — Succeeded,
/// so its PDF follows — and otherwise a copy past <c>DRAFT</c> records the
/// revision its <c>Reference</c> names, so a revision Xero holds is never
/// called unsent; a deleted copy is reported deleted. A content update
/// carries its own key (<see cref="ContentUpdateKey"/>), never the one an
/// earlier attempt may have sent the create under.
/// </para>
/// <para>
/// <b>Blocked</b> when the client is not linked to a Xero contact (X2) or a
/// line's tax type or the sales account is missing from Xero (X1). Never
/// throws for anything Xero or the network did (`ADR-0151`); never emails
/// (D4). Every request goes through the Xero <see cref="HttpClient"/>'s
/// <see cref="XeroWriteSafetyHandler"/>.
/// </para>
/// </remarks>
public sealed class XeroQuotePushHandler : IXeroPushHandler
{
    /// <summary>Audit action (§6.7): TempestOS created the Xero quote and linked it.</summary>
    public const string AuditLinkCreated = "xero.link.created";

    /// <summary>Audit action: a Xero quote was found by its number after an uncertain answer and linked, instead of created again.</summary>
    public const string AuditLinkReconciled = "xero.link.reconciled";

    private readonly XeroAccountingApi _api;
    private readonly IXeroLinkStore _links;
    private readonly IXeroQuoteSource _quotes;
    private readonly XeroContactLinker _contacts;
    private readonly XeroTaxTypeResolver _taxTypes;
    private readonly XeroAccountCodeMap _accounts;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="XeroQuotePushHandler"/> class.</summary>
    /// <param name="api">The typed Xero client (its <see cref="HttpClient"/> holds the safety handler).</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="quotes">Reads quotations.</param>
    /// <param name="contacts">The X2 linker: the client's <c>ContactID</c>, or why the push is Blocked.</param>
    /// <param name="taxTypes">The X1 tax-type resolver (output side).</param>
    /// <param name="accounts">The X1 account-code map (the sales account).</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock links are stamped with; <see langword="null"/> for the system clock.</param>
    public XeroQuotePushHandler(
        XeroAccountingApi api,
        IXeroLinkStore links,
        IXeroQuoteSource quotes,
        XeroContactLinker contacts,
        XeroTaxTypeResolver taxTypes,
        XeroAccountCodeMap accounts,
        IAuditRecorder? audit = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(taxTypes);
        ArgumentNullException.ThrowIfNull(accounts);

        _api = api;
        _links = links;
        _quotes = quotes;
        _contacts = contacts;
        _taxTypes = taxTypes;
        _accounts = accounts;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<XeroOperation> Operations { get; } = [XeroOperation.PushQuote, XeroOperation.SetQuoteStatus];

    /// <inheritdoc />
    public async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Document.Kind != XeroDocumentKind.Quote || !Guid.TryParse(entry.Document.TempestKey, out var quotationId))
            return new XeroPushResult(XeroPushOutcome.Rejected, $"The quote handler sends quotations only; {entry.Document.Kind} {entry.Document.TempestKey} is not one.");

        return entry.Operation switch
        {
            XeroOperation.PushQuote => await PushQuoteAsync(tenantId, entry, quotationId, cancellationToken).ConfigureAwait(false),
            XeroOperation.SetQuoteStatus => await SetStatusAsync(tenantId, entry, cancellationToken).ConfigureAwait(false),
            _ => new XeroPushResult(XeroPushOutcome.Rejected, $"The quote handler does not send {entry.Operation}."),
        };
    }

    private async Task<XeroPushResult> PushQuoteAsync(string tenantId, XeroOutboxEntry entry, Guid quotationId, CancellationToken cancellationToken)
    {
        var quote = await _quotes.FindAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return new XeroPushResult(XeroPushOutcome.Rejected, $"Quotation {quotationId:D} no longer exists in TempestOS.");

        var link = await _links.FindAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The Xero link for quotation {quote.Reference} was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        if (link is not null && string.Equals(link.LastPushedContentHash, entry.ContentHash, StringComparison.Ordinal))
            return new XeroPushResult(XeroPushOutcome.NothingToDo, Link: link);

        // A body other than the one this entry's key was first sent with
        // would be refused by Xero (S7): a changed quotation is the newer
        // entry's to push. This one still reconciles a create it may have made.
        var stale = !string.Equals(XeroQuoteMapper.ContentHash(quote), entry.ContentHash, StringComparison.Ordinal);

        var contact = await _contacts.ResolveForPushAsync(tenantId, quote.ClientOrganisationReference, cancellationToken).ConfigureAwait(false);
        if (!contact.IsLinked)
        {
            return new XeroPushResult(XeroPushOutcome.Blocked, quote.ClientOrganisationReference is null
                ? $"Quotation {quote.Reference}'s client is not a customer TempestOS knows, so it has no Xero contact."
                : contact.BlockedReason);
        }

        // This entry's content as Xero would receive it — also the evidence
        // that an earlier attempt's write landed (XeroQuoteMapper.HoldsContent).
        // Not built for a stale entry: its content is no longer TempestOS's.
        var (body, blocked) = stale ? (null, null) : await BuildAsync(quote, contact.ToContactRef(), cancellationToken).ConfigureAwait(false);

        var reconciledNow = false;
        if (link is null)
        {
            var reconciled = await ReconcileByNumberAsync(tenantId, entry, quote, contact.ContactId!, body, stale, cancellationToken).ConfigureAwait(false);
            if (reconciled.Result is { } answered)
                return answered;

            link = reconciled.Link;
            reconciledNow = link is not null;
            if (link is null)
            {
                if (stale)
                    return new XeroPushResult(XeroPushOutcome.NothingToDo, "The quotation changed after this write was queued; the newer write creates the Xero quote.");

                if (body is null)
                    return new XeroPushResult(XeroPushOutcome.Blocked, blocked);

                var created = await _api.CreateQuoteAsync(body, entry.IdempotencyKey, cancellationToken).ConfigureAwait(false);
                if (created.Outcome != ConnectorOutcome.Ok)
                    return Failed(created);

                var createdLink = NewLink(tenantId, entry.Document, created.Value!, entry.ContentHash, XeroQuoteMapper.LinkedByCreated);
                await _links.SaveAsync(createdLink, cancellationToken).ConfigureAwait(false);
                await AuditAsync(AuditLinkCreated, createdLink, entry, cancellationToken).ConfigureAwait(false);
                return new XeroPushResult(XeroPushOutcome.Succeeded, Link: createdLink);
            }

            if (string.Equals(link.LastPushedContentHash, entry.ContentHash, StringComparison.Ordinal))
                return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
        }

        if (stale)
        {
            // Sent before (Attempts > 1): this entry's own update may have
            // landed with its answer lost, and the quote then been moved past
            // DRAFT by hand — record the revision Xero now holds, so its PDF
            // follows and the badge does not call it unsent; a newer entry
            // may never come to do it (a quotation already Sent in TempestOS).
            if (entry.Attempts > 1 && !reconciledNow)
            {
                var held = await _api.GetQuoteAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
                if (held.Outcome != ConnectorOutcome.Ok)
                    return await FailedReadAsync(held, link, quote.Reference, cancellationToken).ConfigureAwait(false);

                link = await RecordStatusAsync(link, held.Value!, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Word(held.Value!.Status), XeroQuoteStatusWords.Draft, StringComparison.Ordinal))
                    link = await RecordHeldRevisionAsync(link, held.Value!, cancellationToken).ConfigureAwait(false);
            }

            return new XeroPushResult(XeroPushOutcome.NothingToDo, "The quotation changed after this write was queued; the newer write updates the Xero quote.", Link: link);
        }

        // Linked: replace the content only while Xero holds the quote as DRAFT (Q1).
        var read = await _api.GetQuoteAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
            return await FailedReadAsync(read, link, quote.Reference, cancellationToken).ConfigureAwait(false);

        link = await RecordStatusAsync(link, read.Value!, cancellationToken).ConfigureAwait(false);
        var status = Word(read.Value!.Status);
        var number = read.Value.QuoteNumber ?? quote.Reference;
        if (string.Equals(status, XeroQuoteStatusWords.Deleted, StringComparison.Ordinal))
            return new XeroPushResult(XeroPushOutcome.Rejected, DeletedNote(number), Link: link);

        if (!string.Equals(status, XeroQuoteStatusWords.Draft, StringComparison.Ordinal))
        {
            // Xero already holds exactly this content: an earlier attempt's
            // update landed and its answer was lost, then the quote was moved
            // past DRAFT by hand. Record it, so the PDF follows its content.
            if (body is not null && XeroQuoteMapper.HoldsContent(read.Value, body))
            {
                link = link with { LastPushedContentHash = entry.ContentHash };
                await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
                return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
            }

            // Otherwise the revision Xero's Reference names is what it holds
            // (another entry's update that landed unseen — one this entry
            // superseded — or a copy brought up to date by hand).
            link = await RecordHeldRevisionAsync(link, read.Value, cancellationToken).ConfigureAwait(false);
            if (string.Equals(XeroQuoteMapper.RevisionOf(link.LastPushedContentHash), XeroQuoteMapper.RevisionOf(entry.ContentHash), StringComparison.Ordinal))
            {
                // This revision reached Xero; only a change within it (a
                // renamed client or project) did not. Not a revision unsent.
                return new XeroPushResult(
                    XeroPushOutcome.NothingToDo,
                    $"Xero already holds quote {number} as {status} with revision {quote.RevisionLabel ?? "(unnumbered)"}; its content is not changed once past DRAFT (Q1), so this later change within that revision is not written to Xero.",
                    Link: link);
            }

            // Nothing left to push: TempestOS has sent (or answered) the
            // quotation, so its content is fixed, or the Xero quote was just
            // found by its number (keyed in by hand, or Send to Xero on an
            // older quote) already past DRAFT. The link is recorded and the
            // status changes behind this entry still run; the badge shows
            // any drift. A refusal here would hold the document's queue for
            // good — nothing would ever supersede it.
            if (quote.Status != QuotationStatus.Approved || reconciledNow)
            {
                return new XeroPushResult(
                    XeroPushOutcome.NothingToDo,
                    XeroQuoteMapper.DriftNote(quote, link)
                    ?? $"Xero already holds quote {number} as {status}; its content is not changed once past DRAFT (Q1).",
                    Link: link);
            }

            return new XeroPushResult(
                XeroPushOutcome.Rejected,
                XeroQuoteMapper.RevisionNotSentNote(number, status ?? "(unknown)", quote.RevisionLabel),
                Link: link);
        }

        if (body is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, blocked);

        // Its own key, never the entry's: an earlier attempt of this entry
        // may have sent the create under that key (its answer lost), and a
        // different body under a used key is refused by Xero (S7).
        var updated = await _api.UpdateQuoteContentAsync(link.XeroId, body, ContentUpdateKey(entry), cancellationToken).ConfigureAwait(false);
        if (updated.Outcome != ConnectorOutcome.Ok)
            return Failed(updated);

        var now = _time.GetUtcNow();
        link = link with
        {
            XeroNumber = updated.Value!.QuoteNumber ?? link.XeroNumber,
            LastPushedContentHash = entry.ContentHash,
            LastKnownXeroStatus = Word(updated.Value.Status) ?? link.LastKnownXeroStatus,
            LastReadAtUtc = now,
        };
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
    }

    /// <summary>
    /// The <c>Idempotency-Key</c> of a <see cref="XeroOperation.PushQuote"/>
    /// entry's content update: derived from the entry's own key, so it is
    /// fixed across the entry's attempts (a lost answer is replayed) yet
    /// never the key an earlier attempt may have sent the create under.
    /// </summary>
    /// <param name="entry">The entry.</param>
    internal static string ContentUpdateKey(XeroOutboxEntry entry) =>
        XeroIdempotencyKey.Create(entry.Document, entry.Operation, entry.IdempotencyKey, argument: "content-update");

    /// <summary>
    /// Before a first create (§6.4 items 3–4): looks the number up. A live
    /// quote with the number and this client's contact is TempestOS's own
    /// (a create whose answer was lost) and is linked; a live quote with the
    /// number and another contact is someone else's — Rejected, never a
    /// silent duplicate — unless this entry was sent before and that quote
    /// carries exactly its content but for the contact (its own create,
    /// whose contact was then changed in Xero by hand). Deleted quotes are
    /// ignored (Xero frees their number).
    /// </summary>
    private async Task<(XeroPushResult? Result, XeroLink? Link)> ReconcileByNumberAsync(
        string tenantId, XeroOutboxEntry entry, XeroQuoteSnapshot quote, string contactId, XeroWireQuoteWrite? body, bool stale,
        CancellationToken cancellationToken)
    {
        var found = await _api.FindQuotesByNumberAsync(quote.Reference, cancellationToken).ConfigureAwait(false);
        if (found.Outcome != ConnectorOutcome.Ok)
            return (Failed(found), null);

        var live = found.Value!
            .Where(q => !string.Equals(Word(q.Status), XeroQuoteStatusWords.Deleted, StringComparison.Ordinal)
                        && string.Equals(q.QuoteNumber?.Trim(), quote.Reference.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (live.Count == 0)
            return (null, null);

        var ours = live.FirstOrDefault(q => string.Equals(q.Contact?.ContactID, contactId, StringComparison.OrdinalIgnoreCase))
                   ?? (entry.Attempts > 1 && body is not null
                       ? live.FirstOrDefault(q => XeroQuoteMapper.HoldsContent(q, body, includeContact: false))
                       : null);
        if (ours is null)
        {
            var other = live[0];
            return (new XeroPushResult(
                XeroPushOutcome.Rejected,
                $"Quote number {quote.Reference} is already used in Xero by another quote ({Word(other.Status) ?? "unknown status"}, for a different contact); "
                + "TempestOS never makes a second quote with the same number. Rename or delete that quote in Xero, then Retry."), null);
        }

        // What the link records Xero as holding. Exactly this entry's content
        // (whoever wrote it — its own create whose answer was lost, or an
        // identical copy): its hash, so it is not pushed again. Otherwise
        // (changed by hand since, keyed in by hand, Send to Xero on an older
        // quote, or a stale entry whose content is no longer TempestOS's to
        // compare): only the revision Xero's Reference names, so a DRAFT copy
        // is brought up to date and a copy past DRAFT showing another
        // revision is never given this revision's PDF
        // (XeroQuoteMapper.IsRevisionNotSent). Whether an earlier attempt
        // reached the create is never assumed from the attempt count.
        var landed = !stale && body is not null && XeroQuoteMapper.HoldsContent(ours, body);

        var link = NewLink(
            tenantId, entry.Document, ours, landed ? entry.ContentHash : XeroQuoteMapper.ReconciledHash(ours.Reference), XeroQuoteMapper.LinkedByReconciled);
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        await AuditAsync(AuditLinkReconciled, link, entry, cancellationToken).ConfigureAwait(false);
        return (null, link);
    }

    private async Task<XeroPushResult> SetStatusAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken)
    {
        var target = XeroQuoteMapper.ParseWriteStatus(entry.Argument);
        if (target is null or XeroQuoteWriteStatus.Draft)
            return new XeroPushResult(XeroPushOutcome.Rejected, $"'{entry.Argument}' is not a status TempestOS moves a Xero quote to (SENT, ACCEPTED or DECLINED).");

        var link = await _links.FindAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        if (link is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, "The quotation is not in Xero yet; its status follows once the Xero quote exists.");

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The Xero link for this quotation was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        var read = await _api.GetQuoteAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
            return await FailedReadAsync(read, link, link.XeroNumber ?? link.XeroId, cancellationToken).ConfigureAwait(false);

        var held = read.Value!;
        link = await RecordStatusAsync(link, held, cancellationToken).ConfigureAwait(false);

        var targetWord = XeroQuoteMapper.StatusWord(target.Value);
        var current = Word(held.Status);
        var number = held.QuoteNumber ?? link.XeroNumber ?? link.XeroId;

        // Already there: the earlier attempt's answer was lost after Xero applied it.
        if (string.Equals(current, targetWord, StringComparison.Ordinal))
            return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);

        var currentRank = XeroQuoteMapper.Rank(current);
        var targetRank = XeroQuoteMapper.Rank(targetWord)!.Value;
        if (currentRank is null)
        {
            return new XeroPushResult(
                XeroPushOutcome.Rejected,
                (string.Equals(current, XeroQuoteStatusWords.Invoiced, StringComparison.Ordinal)
                    ? $"Quote {number} was invoiced in Xero — raising it from TempestOS too would bill twice. "
                    : $"Xero holds quote {number} as {current ?? "an unknown status"}. ")
                + $"TempestOS never moves a quote out of {current ?? "an unknown status"}, so it was not set to {targetWord}.",
                Link: link);
        }

        if (currentRank.Value > targetRank)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Xero already holds quote {number} as {current}, past {targetWord}.", Link: link);

        if (currentRank.Value == targetRank)
        {
            return new XeroPushResult(
                XeroPushOutcome.Rejected,
                $"Xero holds quote {number} as {current}; TempestOS has it as {targetWord}. Xero does not move a quote from {current} to {targetWord}; change it in Xero.",
                Link: link);
        }

        if (currentRank.Value != targetRank - 1)
        {
            return new XeroPushResult(
                XeroPushOutcome.Rejected,
                $"Xero holds quote {number} as {current}; it reaches {targetWord} only through SENT, which has not been set yet.",
                Link: link);
        }

        var contactId = held.Contact?.ContactID;
        var date = XeroWire.ParseDate(held.Date);
        if (string.IsNullOrWhiteSpace(contactId) || date is null)
            return new XeroPushResult(XeroPushOutcome.Rejected, $"Xero answered quote {number} without its contact or date; its status was not changed.", Link: link);

        var update = new XeroWireQuoteStatusUpdate(link.XeroId, held.QuoteNumber, new XeroWireContactRef(contactId), XeroWire.FormatDate(date.Value), target.Value);
        var moved = await _api.SetQuoteStatusAsync(update, entry.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (moved.Outcome != ConnectorOutcome.Ok)
            return Failed(moved);

        link = link with
        {
            LastKnownXeroStatus = Word(moved.Value!.Status) ?? targetWord,
            LastReadAtUtc = _time.GetUtcNow(),
        };
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
    }

    private async Task<(XeroWireQuoteWrite? Body, string? BlockedReason)> BuildAsync(XeroQuoteSnapshot quote, XeroWireContactRef contact, CancellationToken cancellationToken)
    {
        var account = await _accounts.ResolveSalesAsync(cancellationToken).ConfigureAwait(false);
        var taxTypes = new Dictionary<VatRate, XeroCodeResolution>();
        foreach (var rate in quote.Lines.Select(l => l.VatRate).Distinct())
            taxTypes[rate] = await _taxTypes.ResolveAsync(rate, VatTaxDirection.Sales, cancellationToken).ConfigureAwait(false);

        var body = XeroQuoteMapper.Build(quote, contact, rate => taxTypes[rate], account, out var blocked);
        return (body, blocked);
    }

    private async Task<XeroLink> RecordStatusAsync(XeroLink link, XeroWireQuote held, CancellationToken cancellationToken)
    {
        var status = Word(held.Status);
        var updated = link with
        {
            LastKnownXeroStatus = status ?? link.LastKnownXeroStatus,
            XeroNumber = held.QuoteNumber ?? link.XeroNumber,
            LastReadAtUtc = _time.GetUtcNow(),
        };
        await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private async Task<XeroPushResult> FailedReadAsync<T>(XeroApiResult<T> read, XeroLink link, string number, CancellationToken cancellationToken)
    {
        if (!read.NotFound)
            return Failed(read);

        var gone = link with { LastKnownXeroStatus = XeroQuoteStatusWords.Deleted, LastReadAtUtc = _time.GetUtcNow() };
        await _links.SaveAsync(gone, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Rejected, DeletedNote(number), Link: gone);
    }

    private static string DeletedNote(string number) => $"Quote {number} was deleted in Xero; unlink it to send the quotation again.";

    /// <summary>
    /// For a quote Xero holds past <c>DRAFT</c>: when its <c>Reference</c>
    /// names a revision (<see cref="XeroQuoteMapper.RevisionToken"/>) other
    /// than the one the link records, records that revision
    /// (<see cref="XeroQuoteMapper.ReconciledHash"/>, no content claimed) —
    /// a write whose answer was lost landed, or the copy was brought up to
    /// date by hand — so the PDF follows the content Xero holds and the
    /// badge does not call a revision Xero holds unsent. A reference that
    /// names no revision changes nothing.
    /// </summary>
    private async Task<XeroLink> RecordHeldRevisionAsync(XeroLink link, XeroWireQuote held, CancellationToken cancellationToken)
    {
        var heldHash = XeroQuoteMapper.ReconciledHash(held.Reference);
        var heldRevision = XeroQuoteMapper.RevisionOf(heldHash);
        if (string.Equals(heldRevision, XeroQuoteMapper.UnknownRevision, StringComparison.Ordinal)
            || string.Equals(heldRevision, XeroQuoteMapper.RevisionOf(link.LastPushedContentHash), StringComparison.Ordinal))
        {
            return link;
        }

        var updated = link with { LastPushedContentHash = heldHash };
        await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private XeroLink NewLink(string tenantId, XeroDocumentRef document, XeroWireQuote quote, string? contentHash, string linkedBy)
    {
        var now = _time.GetUtcNow();
        return new XeroLink(
            XeroLink.CurrentSchemaVersion, tenantId, document, quote.QuoteID!, quote.QuoteNumber, contentHash, Word(quote.Status) ?? XeroQuoteStatusWords.Draft,
            AttachmentFileName: null, AttachmentContentHash: null, LinkedAtUtc: now, LastReadAtUtc: now, LinkedBy: linkedBy);
    }

    private async Task AuditAsync(string action, XeroLink link, XeroOutboxEntry entry, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;

        await _audit.RecordAsync(action, new Dictionary<string, string>
        {
            ["document"] = link.Document.TempestKey,
            ["kind"] = link.Document.Kind.ToString(),
            ["operation"] = entry.Operation.ToString(),
            ["xeroId"] = link.XeroId,
            ["xeroNumber"] = link.XeroNumber ?? string.Empty,
            ["idempotencyKey"] = entry.IdempotencyKey,
            ["attempt"] = entry.Attempts.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Xero's status word, trimmed and upper-cased; <see langword="null"/> when absent.</summary>
    internal static string? Word(string? status) => string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();

    /// <summary>Maps a failed call to the push outcome the engine acts on (§6.3, §6.6).</summary>
    /// <typeparam name="T">The call's answer type.</typeparam>
    /// <param name="result">A result whose outcome is not Ok.</param>
    internal static XeroPushResult Failed<T>(XeroApiResult<T> result) => result.Outcome switch
    {
        ConnectorOutcome.Rejected => new XeroPushResult(XeroPushOutcome.Rejected, result.Reason),
        ConnectorOutcome.Reauthorise => new XeroPushResult(XeroPushOutcome.Reauthorise, result.Reason),
        ConnectorOutcome.Unavailable => new XeroPushResult(XeroPushOutcome.RetryLater, result.Reason, result.RetryAfter),
        _ => new XeroPushResult(XeroPushOutcome.Unknown, result.Reason),
    };
}

/// <summary>
/// Uploads a quotation's issued PDF to its Xero quote (`v0.24.0` X3,
/// <see cref="XeroOperation.UploadAttachment"/> for
/// <see cref="XeroDocumentKind.Quote"/> documents; design §3): under the
/// quotation's own reference (<c>{Reference}.pdf</c>), so a new revision's
/// PDF replaces the old one by name (<c>POST …/Attachments/{FileName}</c>);
/// never the same PDF twice (<see cref="XeroLink.AttachmentContentHash"/>).
/// </summary>
/// <remarks>
/// The engine (X6) dispatches <see cref="XeroOperation.UploadAttachment"/>
/// by document kind: X4 and X5 upload their own documents' files. Blocked
/// until the Xero quote exists and its PDF is held.
/// </remarks>
public sealed class XeroQuoteAttachmentHandler : IXeroPushHandler
{
    private readonly XeroAccountingApi _api;
    private readonly IXeroLinkStore _links;
    private readonly IXeroQuoteSource _quotes;
    private readonly IXeroDocumentFileSource? _files;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="XeroQuoteAttachmentHandler"/> class.</summary>
    /// <param name="api">The typed Xero client.</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="quotes">Reads quotations (for the file name).</param>
    /// <param name="files">The issued PDFs (X6); <see langword="null"/> while none is registered — every upload is then Blocked.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    public XeroQuoteAttachmentHandler(
        XeroAccountingApi api, IXeroLinkStore links, IXeroQuoteSource quotes, IXeroDocumentFileSource? files = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(quotes);

        _api = api;
        _links = links;
        _quotes = quotes;
        _files = files;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The kind of document whose files this handler uploads.</summary>
    public XeroDocumentKind DocumentKind => XeroDocumentKind.Quote;

    /// <inheritdoc />
    public IReadOnlyCollection<XeroOperation> Operations { get; } = [XeroOperation.UploadAttachment];

    /// <inheritdoc />
    public async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Operation != XeroOperation.UploadAttachment || entry.Document.Kind != XeroDocumentKind.Quote || !Guid.TryParse(entry.Document.TempestKey, out var quotationId))
            return new XeroPushResult(XeroPushOutcome.Rejected, $"The quote attachment handler uploads quotation PDFs only; not {entry.Operation} for {entry.Document.Kind} {entry.Document.TempestKey}.");

        var link = await _links.FindAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        if (link is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, "The quotation is not in Xero yet; its PDF is attached once the Xero quote exists.");

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The Xero link for this quotation was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        var file = _files is null ? null : await _files.FindAsync(entry.Document, cancellationToken).ConfigureAwait(false);
        if (file is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, "The quotation's PDF is not held yet; export or send it in TempestOS and it is attached.");

        if (string.Equals(link.AttachmentContentHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            return new XeroPushResult(XeroPushOutcome.NothingToDo, Link: link);

        // The PDF follows its content (Q1): when Xero holds the quote past
        // DRAFT with an older revision's lines, this revision's PDF is not
        // put beside them — an upload queued before the content push was
        // refused would otherwise run once that refusal is released.
        var current = await _quotes.FindAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (current is not null && XeroQuoteMapper.IsRevisionNotSent(current, link))
            return new XeroPushResult(XeroPushOutcome.NothingToDo, XeroQuoteMapper.DriftNote(current, link), Link: link);

        // A different PDF than this entry was queued for: the newer entry uploads it under its own key.
        if (!string.Equals(file.Sha256, entry.ContentHash, StringComparison.OrdinalIgnoreCase))
            return new XeroPushResult(XeroPushOutcome.NothingToDo, "A newer PDF replaced the one this upload was queued for; the newer upload sends it.", Link: link);

        var name = entry.Argument;
        if (string.IsNullOrWhiteSpace(name))
            name = XeroQuoteMapper.AttachmentFileName(current?.Reference ?? link.XeroNumber ?? quotationId.ToString("D"));

        // Replace by name once a file of that name was uploaded (no attachment delete in Xero, §3).
        var replace = string.Equals(link.AttachmentFileName, name, StringComparison.OrdinalIgnoreCase);
        var uploaded = await _api.UploadAttachmentAsync(
            XeroAttachableResource.Quotes, link.XeroId, file with { FileName = name }, entry.IdempotencyKey, replace, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (uploaded.Outcome != ConnectorOutcome.Ok)
        {
            if (uploaded.NotFound)
                return new XeroPushResult(XeroPushOutcome.Rejected, $"Quote {link.XeroNumber ?? link.XeroId} was deleted in Xero; its PDF was not attached.", Link: link);

            return XeroQuotePushHandler.Failed(uploaded);
        }

        link = link with { AttachmentFileName = name, AttachmentContentHash = file.Sha256, LastReadAtUtc = _time.GetUtcNow() };
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
    }
}
