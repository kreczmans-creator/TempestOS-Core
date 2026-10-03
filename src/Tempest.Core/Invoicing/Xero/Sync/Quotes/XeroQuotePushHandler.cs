using System.Globalization;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Persistence;
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
/// <b>One ownership rule</b> (the comment block on <see cref="XeroQuoteMapper"/>):
/// a Xero quote is TempestOS's own only when its link names it, or its
/// number and contact match and its values (lines, dates, currency) equal
/// content TempestOS is known to have sent for the document — the current
/// entry's, or any earlier send recorded before writing
/// (<see cref="SentCollection"/>). Only such a quote is ever updated, moved,
/// given a PDF or linked; anything else is never touched, and the push
/// answers one actionable message (Rejected: fix it in Xero, then Retry).
/// </para>
/// <para>
/// <b>Never two quotes.</b> A create is issued only when the quotation has no
/// link in the tenant, and only after looking the number up
/// (<c>GET Quotes?QuoteNumber=</c>): a quote that is TempestOS's own (the
/// lost-response case) is linked (<c>"reconciled"</c>) instead; any other
/// quote with the number is refused with the reason. Every write carries the
/// entry's fixed <c>Idempotency-Key</c>; an entry whose quotation changed
/// since it was queued sends nothing (a body other than the one first sent
/// under its key would be refused by Xero), and the planner's newer entry
/// carries the new content.
/// </para>
/// <para>
/// <b>Read before write.</b> Each update reads the Xero quote first: content
/// is replaced only while it is <c>DRAFT</c> (Q1). Past <c>DRAFT</c> the link
/// records which send Xero holds (<see cref="XeroQuoteMapper.Identify"/> —
/// never what an attempt count or a hand-editable <c>Reference</c>
/// suggests): exactly this entry's content is Succeeded, so its PDF follows;
/// an earlier send of the same revision, or values TempestOS sent under text
/// changed by hand, is NothingToDo with a note saying which; a new approved
/// revision Xero never received is refused with the reason, never sent; and
/// when TempestOS has sent or answered the quotation (its content is fixed)
/// or the quote was just found by its number, the answer is NothingToDo with
/// the drift note, so the status changes queued behind it still run. A
/// status is moved only along <c>DRAFT → SENT → ACCEPTED | DECLINED</c>; a
/// status already reached (a lost response) is simply recorded. A
/// status-only update carries the quote's number, contact and date as Xero
/// holds them and never its lines. A content update carries its own key
/// (<see cref="ContentUpdateKey"/>), never the one an earlier attempt may
/// have sent the create under.
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

    /// <summary>
    /// The <see cref="IPersistenceStore"/> collection, owned by this handler,
    /// holding what TempestOS sent for each quotation
    /// (<see cref="XeroQuoteSentContent"/>, keyed by the quotation id): written
    /// before each create or content update, so the ownership rule can count
    /// a write whose answer was lost even after its entry was superseded.
    /// </summary>
    public const string SentCollection = "Xero.QuoteSync.Sent";

    /// <summary>How many sends <see cref="SentCollection"/> keeps per quotation (the oldest are dropped).</summary>
    public const int MaximumSentRecords = 32;

    private readonly XeroAccountingApi _api;
    private readonly IXeroLinkStore _links;
    private readonly IXeroQuoteSource _quotes;
    private readonly IPersistenceStore _sent;
    private readonly XeroContactLinker _contacts;
    private readonly XeroTaxTypeResolver _taxTypes;
    private readonly XeroAccountCodeMap _accounts;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="XeroQuotePushHandler"/> class.</summary>
    /// <param name="api">The typed Xero client (its <see cref="HttpClient"/> holds the safety handler).</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="quotes">Reads quotations.</param>
    /// <param name="sent">Where the handler records what it sent (<see cref="SentCollection"/>).</param>
    /// <param name="contacts">The X2 linker: the client's <c>ContactID</c>, or why the push is Blocked.</param>
    /// <param name="taxTypes">The X1 tax-type resolver (output side).</param>
    /// <param name="accounts">The X1 account-code map (the sales account).</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock links are stamped with; <see langword="null"/> for the system clock.</param>
    public XeroQuotePushHandler(
        XeroAccountingApi api,
        IXeroLinkStore links,
        IXeroQuoteSource quotes,
        IPersistenceStore sent,
        XeroContactLinker contacts,
        XeroTaxTypeResolver taxTypes,
        XeroAccountCodeMap accounts,
        IAuditRecorder? audit = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(sent);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(taxTypes);
        ArgumentNullException.ThrowIfNull(accounts);

        _api = api;
        _links = links;
        _quotes = quotes;
        _sent = sent;
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

        // This entry's content as Xero would receive it. Not built for a
        // stale entry: its content is no longer TempestOS's.
        var (body, blocked) = stale ? (null, null) : await BuildAsync(quote, contact.ToContactRef(), cancellationToken).ConfigureAwait(false);

        // The ownership rule's evidence (XeroQuoteMapper): what TempestOS is
        // known to have sent for this quotation, oldest first, with this
        // entry's own content last so it wins a tie.
        var sent = await ReadSentAsync(quotationId, cancellationToken).ConfigureAwait(false);
        var own = body is null ? null : XeroQuoteSentContent.Of(body, entry.ContentHash);
        IReadOnlyList<XeroQuoteSentContent> known = own is null ? sent : [.. sent, own];

        var reconciledNow = false;
        if (link is null)
        {
            var reconciled = await ReconcileByNumberAsync(tenantId, entry, quote, contact.ContactId!, known, stale, cancellationToken).ConfigureAwait(false);
            if (reconciled.Result is { } answered)
                return answered;

            link = reconciled.Link;
            if (link is null)
            {
                if (stale)
                    return new XeroPushResult(XeroPushOutcome.NothingToDo, "The quotation changed after this write was queued; the newer write creates the Xero quote.");

                if (body is null)
                    return new XeroPushResult(XeroPushOutcome.Blocked, blocked);

                await RecordSentAsync(quotationId, sent, own!, cancellationToken).ConfigureAwait(false);
                var created = await _api.CreateQuoteAsync(body, entry.IdempotencyKey, cancellationToken).ConfigureAwait(false);
                if (created.Outcome != ConnectorOutcome.Ok)
                    return Failed(created);

                var createdLink = NewLink(tenantId, entry.Document, created.Value!, entry.ContentHash, XeroQuoteMapper.LinkedByCreated);
                await _links.SaveAsync(createdLink, cancellationToken).ConfigureAwait(false);
                await AuditAsync(AuditLinkCreated, createdLink, entry, cancellationToken).ConfigureAwait(false);
                return new XeroPushResult(XeroPushOutcome.Succeeded, Link: createdLink);
            }

            // Exactly this entry's content: its own create (or update) whose answer was lost.
            reconciledNow = true;
            if (reconciled.Match == XeroQuoteContentMatch.Exact && string.Equals(link.LastPushedContentHash, entry.ContentHash, StringComparison.Ordinal))
                return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
        }

        if (stale)
        {
            // Sent before (Attempts > 1): this entry's own update may have
            // landed with its answer lost, and the quote then been moved past
            // DRAFT by hand — record which send Xero holds, so its PDF
            // follows and the badge does not call it unsent; a newer entry
            // may never come to do it (a quotation already Sent in TempestOS).
            if (entry.Attempts > 1 && !reconciledNow)
            {
                var held = await _api.GetQuoteAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
                if (held.Outcome != ConnectorOutcome.Ok)
                    return await FailedReadAsync(held, link, quote.Reference, cancellationToken).ConfigureAwait(false);

                link = await RecordStatusAsync(link, held.Value!, cancellationToken).ConfigureAwait(false);
                var heldStatus = Word(held.Value!.Status);
                if (!string.Equals(heldStatus, XeroQuoteStatusWords.Draft, StringComparison.Ordinal) && !string.Equals(heldStatus, XeroQuoteStatusWords.Deleted, StringComparison.Ordinal))
                    (link, _) = await RecordHeldContentAsync(link, held.Value!, known, cancellationToken).ConfigureAwait(false);
            }

            return new XeroPushResult(XeroPushOutcome.NothingToDo, "The quotation changed after this write was queued; the newer write updates the Xero quote.", Link: link);
        }

        // Linked (ours by its link): replace the content only while Xero holds the quote as DRAFT (Q1).
        var read = await _api.GetQuoteAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
            return await FailedReadAsync(read, link, quote.Reference, cancellationToken).ConfigureAwait(false);

        link = await RecordStatusAsync(link, read.Value!, cancellationToken).ConfigureAwait(false);
        var status = Word(read.Value!.Status);
        var number = read.Value.QuoteNumber ?? quote.Reference;
        if (string.Equals(status, XeroQuoteStatusWords.Deleted, StringComparison.Ordinal))
            return new XeroPushResult(XeroPushOutcome.Rejected, DeletedNote(number), Link: link);

        if (!string.Equals(status, XeroQuoteStatusWords.Draft, StringComparison.Ordinal))
            return await PastDraftAsync(entry, quote, link, read.Value, known, reconciledNow, number, status ?? "(unknown)", cancellationToken).ConfigureAwait(false);

        if (body is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, blocked);

        // Its own key, never the entry's: an earlier attempt of this entry
        // may have sent the create under that key (its answer lost), and a
        // different body under a used key is refused by Xero (S7).
        await RecordSentAsync(quotationId, sent, own!, cancellationToken).ConfigureAwait(false);
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
    /// A linked quote Xero holds past <c>DRAFT</c> (Q1: its content is not
    /// changed). Records which send it holds (<see cref="RecordHeldContentAsync"/>),
    /// then answers by what that is — the note always says what Xero holds:
    /// this entry's content exactly → Succeeded (its PDF follows); this
    /// entry's values under text changed by hand → NothingToDo; another send
    /// of the same revision → NothingToDo ("this later change within that
    /// revision is not written"), or the by-hand note when its text or values
    /// were changed in Xero; another revision while TempestOS has sent or
    /// answered the quotation, or the quote was just found by its number →
    /// NothingToDo with the drift note (a refusal would hold the queue with
    /// nothing to supersede it); otherwise the new approved revision was not
    /// sent → Rejected, with the reason.
    /// </summary>
    private async Task<XeroPushResult> PastDraftAsync(
        XeroOutboxEntry entry, XeroQuoteSnapshot quote, XeroLink link, XeroWireQuote held, IReadOnlyList<XeroQuoteSentContent> known, bool reconciledNow,
        string number, string status, CancellationToken cancellationToken)
    {
        (link, var match) = await RecordHeldContentAsync(link, held, known, cancellationToken).ConfigureAwait(false);

        if (match != XeroQuoteContentMatch.None && string.Equals(link.LastPushedContentHash, entry.ContentHash, StringComparison.Ordinal))
        {
            return match == XeroQuoteContentMatch.Exact
                ? new XeroPushResult(XeroPushOutcome.Succeeded, Link: link)
                : new XeroPushResult(
                    XeroPushOutcome.NothingToDo,
                    $"Xero holds quote {number} as {status} with this revision's lines, dates and currency, but its text (title, summary, terms or a line's description or account) was changed in Xero by hand; "
                    + "TempestOS does not change a quote's content once past DRAFT (Q1).",
                    Link: link);
        }

        var revision = quote.RevisionLabel ?? "(unnumbered)";
        if (string.Equals(XeroQuoteMapper.RevisionOf(link.LastPushedContentHash), XeroQuoteMapper.RevisionOf(entry.ContentHash), StringComparison.Ordinal))
        {
            return new XeroPushResult(XeroPushOutcome.NothingToDo, match switch
            {
                XeroQuoteContentMatch.Exact =>
                    $"Xero already holds quote {number} as {status} with revision {revision}; its content is not changed once past DRAFT (Q1), so this later change within that revision is not written to Xero.",
                XeroQuoteContentMatch.ValuesOnly =>
                    $"Xero holds quote {number} as {status} with revision {revision}'s lines, dates and currency, and text changed in Xero by hand; its content is not changed once past DRAFT (Q1), so this later change within that revision is not written to Xero.",
                _ => HandEditedNote(number, status),
            }, Link: link);
        }

        if (quote.Status != QuotationStatus.Approved || reconciledNow)
        {
            return new XeroPushResult(
                XeroPushOutcome.NothingToDo,
                XeroQuoteMapper.DriftNote(quote, link)
                ?? (match == XeroQuoteContentMatch.None ? HandEditedNote(number, status) : $"Xero already holds quote {number} as {status}; its content is not changed once past DRAFT (Q1)."),
                Link: link);
        }

        return new XeroPushResult(XeroPushOutcome.Rejected, XeroQuoteMapper.RevisionNotSentNote(number, status, quote.RevisionLabel), Link: link);
    }

    private static string HandEditedNote(string number, string status) =>
        $"Xero holds quote {number} as {status}, but its lines, dates or currency were changed in Xero by hand and match nothing TempestOS sent; "
        + "TempestOS does not change a quote's content once past DRAFT (Q1). Check the quote in Xero.";

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
    /// Before a first create (§6.4 items 3–4): looks the number up, and
    /// applies the ownership rule (<see cref="XeroQuoteMapper.OwnSend"/>) to
    /// every live quote with it. TempestOS's own quote (a create or update
    /// whose answer was lost) is linked, recording the send it holds; any
    /// other is never touched — Rejected with what to change in Xero before
    /// a Retry, or NothingToDo for a stale entry (its newer entry asks
    /// again). Deleted quotes are ignored (Xero frees their number).
    /// </summary>
    private async Task<(XeroPushResult? Result, XeroLink? Link, XeroQuoteContentMatch Match)> ReconcileByNumberAsync(
        string tenantId, XeroOutboxEntry entry, XeroQuoteSnapshot quote, string contactId, IReadOnlyList<XeroQuoteSentContent> known, bool stale,
        CancellationToken cancellationToken)
    {
        var found = await _api.FindQuotesByNumberAsync(quote.Reference, cancellationToken).ConfigureAwait(false);
        if (found.Outcome != ConnectorOutcome.Ok)
            return (Failed(found), null, XeroQuoteContentMatch.None);

        var live = found.Value!
            .Where(q => !string.Equals(Word(q.Status), XeroQuoteStatusWords.Deleted, StringComparison.Ordinal)
                        && string.Equals(q.QuoteNumber?.Trim(), quote.Reference.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (live.Count == 0)
            return (null, null, XeroQuoteContentMatch.None);

        foreach (var candidate in live)
        {
            if (XeroQuoteMapper.OwnSend(candidate, quote.Reference, contactId, known) is not { } own)
                continue;

            var link = NewLink(tenantId, entry.Document, candidate, own.Send.ContentHash, XeroQuoteMapper.LinkedByReconciled);
            await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
            await AuditAsync(AuditLinkReconciled, link, entry, cancellationToken).ConfigureAwait(false);
            return (null, link, own.Match);
        }

        if (stale)
            return (new XeroPushResult(XeroPushOutcome.NothingToDo, "The quotation changed after this write was queued; the newer write sends it to Xero."), null, XeroQuoteContentMatch.None);

        return (new XeroPushResult(XeroPushOutcome.Rejected, NotOursNote(quote.Reference, live, contactId)), null, XeroQuoteContentMatch.None);
    }

    /// <summary>The one message for a quote with the number that is not TempestOS's own: what Xero holds, and what to change there before a Retry.</summary>
    private static string NotOursNote(string number, IReadOnlyList<XeroWireQuote> live, string contactId)
    {
        const string never = "TempestOS never makes a second quote with the same number and never changes a quote it cannot confirm it sent, so nothing was written.";
        if (live.FirstOrDefault(q => string.Equals(q.Contact?.ContactID?.Trim(), contactId.Trim(), StringComparison.OrdinalIgnoreCase)) is { } same)
        {
            return $"Quote number {number} is already used in Xero by a quote ({Word(same.Status) ?? "unknown status"}) for this client whose lines, dates or currency match nothing TempestOS sent for this quotation "
                   + $"(it was keyed in, or changed, in Xero by hand). {never} In Xero, delete or renumber that quote, or make its lines, dates and currency match this quotation; then Retry.";
        }

        return $"Quote number {number} is already used in Xero by a quote ({Word(live[0].Status) ?? "unknown status"}) for a different contact than this quotation's client. {never} "
               + "If it is this quotation, set its contact back to the client's linked Xero contact; otherwise delete or renumber it in Xero. Then Retry.";
    }

    /// <summary>What TempestOS recorded sending for <paramref name="quotationId"/>, oldest first; empty when nothing (or nothing readable) is recorded.</summary>
    private async Task<IReadOnlyList<XeroQuoteSentContent>> ReadSentAsync(Guid quotationId, CancellationToken cancellationToken)
    {
        var stored = await _sent.ReadAsync(SentCollection, quotationId.ToString("D"), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(stored))
            return [];

        try
        {
            var records = System.Text.Json.JsonSerializer.Deserialize<List<XeroQuoteSentContent>>(stored);
            return records is null
                ? []
                : [.. records.Where(r => r is not null && !string.IsNullOrEmpty(r.ValueFingerprint) && !string.IsNullOrEmpty(r.ContentFingerprint) && !string.IsNullOrEmpty(r.ContentHash))];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>Records, before the write is sent, that TempestOS is sending <paramref name="send"/> — so the write counts as TempestOS's even if its answer is lost and its entry superseded.</summary>
    private async Task RecordSentAsync(Guid quotationId, IReadOnlyList<XeroQuoteSentContent> sent, XeroQuoteSentContent send, CancellationToken cancellationToken)
    {
        if (sent.Count > 0 && sent[^1] == send)
            return;

        List<XeroQuoteSentContent> records = [.. sent.Where(s => s != send), send];
        if (records.Count > MaximumSentRecords)
            records.RemoveRange(0, records.Count - MaximumSentRecords);

        await _sent.WriteAsync(SentCollection, quotationId.ToString("D"), System.Text.Json.JsonSerializer.Serialize(records), cancellationToken).ConfigureAwait(false);
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
    /// For a linked quote Xero holds past <c>DRAFT</c>: records in the link
    /// which send it holds (<see cref="XeroQuoteMapper.Identify"/> over
    /// <paramref name="known"/>) — a write whose answer was lost landed, or
    /// was superseded — so the PDF follows the content Xero holds and the
    /// badge does not call a revision Xero holds unsent. When its values
    /// match nothing TempestOS sent (changed in Xero by hand), the revision
    /// its <c>Reference</c> names is recorded instead
    /// (<see cref="XeroQuoteMapper.ReconciledHash"/>, no content claimed) —
    /// for the badge only: ownership never rests on <c>Reference</c>; a
    /// reference that names no revision changes nothing.
    /// </summary>
    private async Task<(XeroLink Link, XeroQuoteContentMatch Match)> RecordHeldContentAsync(
        XeroLink link, XeroWireQuote held, IReadOnlyList<XeroQuoteSentContent> known, CancellationToken cancellationToken)
    {
        var (match, send) = XeroQuoteMapper.Identify(held, known);
        string? hash;
        if (send is not null)
        {
            hash = send.ContentHash;
        }
        else
        {
            hash = XeroQuoteMapper.ReconciledHash(held.Reference);
            var heldRevision = XeroQuoteMapper.RevisionOf(hash);
            if (string.Equals(heldRevision, XeroQuoteMapper.UnknownRevision, StringComparison.Ordinal)
                || string.Equals(heldRevision, XeroQuoteMapper.RevisionOf(link.LastPushedContentHash), StringComparison.Ordinal))
            {
                hash = link.LastPushedContentHash;
            }
        }

        if (string.Equals(hash, link.LastPushedContentHash, StringComparison.Ordinal))
            return (link, match);

        var updated = link with { LastPushedContentHash = hash };
        await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return (updated, match);
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

        // M6: a file that cannot be attached never holds the quote's queue.
        if (XeroAttachmentRefusal.TooLarge(file) is { } tooLarge)
            return await XeroAttachmentRefusal.FinishAsync(_links, link, tooLarge, cancellationToken).ConfigureAwait(false);

        // Replace by name once a file of that name was uploaded (no attachment delete in Xero, §3).
        var replace = string.Equals(link.AttachmentFileName, name, StringComparison.OrdinalIgnoreCase);
        var uploaded = await _api.UploadAttachmentAsync(
            XeroAttachableResource.Quotes, link.XeroId, file with { FileName = name }, entry.IdempotencyKey, replace, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (uploaded.Outcome != ConnectorOutcome.Ok)
        {
            if (uploaded.NotFound)
                return new XeroPushResult(XeroPushOutcome.Rejected, $"Quote {link.XeroNumber ?? link.XeroId} was deleted in Xero; its PDF was not attached.", Link: link);

            if (XeroAttachmentRefusal.IsFileRefusal(uploaded.Outcome, uploaded.NotFound, uploaded.Reason))
            {
                var why = uploaded.ValidationErrors is { Count: > 0 } errors ? string.Join("; ", errors) : uploaded.Reason;
                return await XeroAttachmentRefusal.FinishAsync(_links, link, why, cancellationToken).ConfigureAwait(false);
            }

            return XeroQuotePushHandler.Failed(uploaded);
        }

        link = link with { AttachmentFileName = name, AttachmentContentHash = file.Sha256, LastReadAtUtc = _time.GetUtcNow(), AttachmentNote = null };
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
    }
}
