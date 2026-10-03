using System.Globalization;
using System.Text.Json;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Persistence;
using Tempest.Core.Quotations;
using Tempest.Core.Secrets;

namespace Tempest.Core.Invoicing.Xero.Sync.Quotes;

/// <summary>How <see cref="XeroQuotePlanner"/> decides which quotations are synced automatically (Q8).</summary>
public sealed record XeroQuotePlannerOptions
{
    /// <summary>
    /// Quotations whose current revision was issued (approved) at or after
    /// this moment are synced automatically; earlier ones — raised before
    /// `v0.24.0`'s Xero sync existed — only after the Product Owner's explicit
    /// <em>Send to Xero</em> (<see cref="XeroQuotePlanner.SendToXeroAsync"/>,
    /// Q8). <see langword="null"/> (the default) keeps the moment <b>per
    /// Xero organisation</b> (F1, M1; <see cref="XeroQuotePlanner.AutomaticFromAsync(string?, CancellationToken)"/>):
    /// for the first organisation, when Xero quote sync started running in
    /// this workspace — when the planner was built, at start-up; for every
    /// later one, when TempestOS first saw it connected — each recorded once
    /// in <see cref="XeroQuotePlanner.StateCollection"/> and never moved. A
    /// configured moment applies to every organisation.
    /// </summary>
    public DateTimeOffset? AutomaticFromUtc { get; init; }
}

/// <summary>What <see cref="XeroQuotePlanner.SendToXeroAsync"/> did.</summary>
/// <param name="Queued">Whether the quotation is now synced (it was issued, and is opted in); <see langword="false"/> with <paramref name="Reason"/> otherwise.</param>
/// <param name="Entries">The outbox entries the request queued or found already queued, in order.</param>
/// <param name="Reason">Why nothing was queued; <see langword="null"/> when <paramref name="Queued"/>.</param>
public sealed record XeroQuoteSendRequest(bool Queued, IReadOnlyList<XeroOutboxEntry> Entries, string? Reason = null);

/// <summary>
/// Decides, from a TempestOS quotation's current state and its Xero link,
/// which Xero writes are still needed (`v0.24.0` X3, D2, Q1, Q8; design
/// §4.1, §6.2) — desired state, not events — and queues them in the outbox.
/// Never a network call.
/// </summary>
/// <remarks>
/// <para>
/// <b>When a quotation is in scope.</b> Once it is issued: an approved
/// revision Rn that has been exported (a PDF is held,
/// <see cref="IXeroDocumentFileSource"/>, that has not already been
/// uploaded to its Xero quote — so a linked Rn+1 waits for its own export,
/// not just its approval; or the Product Owner sent this revision to Xero),
/// or already sent, accepted or declined (§4.1: "Approved Rn, then Export
/// or Send"). A draft, a quotation in review, or an approved one edited back
/// to draft plans nothing — the next approval issues Rn+1. Q8: a quotation
/// issued before Xero sync began in this workspace
/// (<see cref="XeroQuotePlannerOptions.AutomaticFromUtc"/>) is planned only
/// after its explicit <see cref="SendToXeroAsync"/>, or once it is linked.
/// </para>
/// <para>
/// <b>What it plans, in order</b> (the outbox keeps per-document order, so a
/// status change never overtakes its create):
/// <see cref="XeroOperation.PushQuote"/> while the content differs from what
/// was last pushed and Xero is not known to hold the quote past <c>DRAFT</c>
/// (Q1: Xero accepts content only while <c>DRAFT</c>; past it the badge
/// shows the drift, <see cref="XeroQuoteMapper.DriftNote(XeroQuoteSnapshot, XeroLink)"/>, instead);
/// <see cref="XeroOperation.UploadAttachment"/> when the issued PDF differs
/// from the one last uploaded — unless Xero holds the quote past <c>DRAFT</c>
/// with an older revision's content (<see cref="XeroQuoteMapper.IsRevisionNotSent"/>):
/// the PDF follows its content, and the badge says that revision was not
/// sent (<see cref="XeroQuoteMapper.DriftNote(XeroQuoteSnapshot, XeroLink)"/>);
/// then <see cref="XeroOperation.SetQuoteStatus"/>
/// for each step of <c>DRAFT → SENT → ACCEPTED | DECLINED</c> Xero has not
/// yet reached. A linked quote Xero holds off that walk (<c>INVOICED</c>,
/// <c>DELETED</c>) plans nothing: its badge shows the drift
/// (<see cref="XeroQuoteMapper.DriftNote(string?, QuotationStatus)"/>), and TempestOS never moves it.
/// </para>
/// <para>
/// <b>Known gaps for X6 (engine).</b>
/// (1) <em>Start-up order (Q8).</em> When automatic sync began is recorded
/// by the first plan or scan of a run, with the moment the planner was
/// built. X6 must call <see cref="ScanAsync"/> (or <see cref="AutomaticFromAsync"/>)
/// first thing at start-up, before a quotation can be approved: if the very
/// first run ends before any plan or scan, the next run records its own
/// build time, and a quotation approved in between waits for
/// <em>Send to Xero</em>.
/// (2) <em>A PDF is not tied to its revision.</em> "Exported" means a PDF is
/// held that the Xero quote does not already carry; <see cref="IXeroDocumentFileSource"/>
/// does not say which revision a held PDF belongs to. When the previous
/// revision's PDF never reached Xero (an unlinked quotation, a Blocked or
/// offline first push, an upload still pending), approving Rn+1 without
/// exporting it counts as exported, so Rn+1 is pushed on approval with Rn's
/// PDF. X6's file source should record the revision (or content hash) a PDF
/// was exported for, and this planner then compare it with the current one.
/// </para>
/// </remarks>
public sealed class XeroQuotePlanner : IXeroSyncPlanner
{
    /// <summary>The <see cref="IPersistenceStore"/> collection holding the planner's own small state: when automatic sync began, and each quotation's <em>Send to Xero</em> opt-in.</summary>
    public const string StateCollection = "Xero.QuoteSync";

    /// <summary>The key, in <see cref="StateCollection"/>, of the moment automatic quote sync began in this workspace (round-trip ISO-8601): the first organisation's start (<see cref="AutomaticFromKeyFor"/>), also recorded while no organisation is connected.</summary>
    public const string AutomaticFromKey = "automatic-from";

    /// <summary>`v0.24.0` F1 (M1): the key, in <see cref="StateCollection"/>, naming the organisation (tenant id) that took over <see cref="AutomaticFromKey"/> and the opt-ins recorded under <see cref="OptInKey(Guid)"/> — the first organisation connected since F1.</summary>
    public const string AutomaticFromOwnerKey = "automatic-from-owner";

    /// <summary>The audit action for a quotation the Product Owner sent to Xero on demand (Q8).</summary>
    public const string AuditSendToXero = "xero.quote.send-to-xero";

    private readonly IXeroQuoteSource _quotes;
    private readonly IXeroLinkStore _links;
    private readonly IXeroOutbox _outbox;
    private readonly IPersistenceStore _state;
    private readonly ISecretStore _secretStore;
    private readonly IXeroDocumentFileSource? _files;
    private readonly Audit.IAuditRecorder? _audit;
    private readonly TimeProvider _time;
    private readonly XeroQuotePlannerOptions _options;
    private readonly DateTimeOffset _startedAtUtc;
    private readonly SemaphoreSlim _stateGate = new(1, 1);

    /// <summary>Initialises a new instance of the <see cref="XeroQuotePlanner"/> class.</summary>
    /// <param name="quotes">Reads quotations.</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="outbox">The outbox (B2) planned writes are queued in.</param>
    /// <param name="state">Where the planner keeps when automatic sync began and the Q8 opt-ins (<see cref="StateCollection"/>).</param>
    /// <param name="secretStore">Where the connected Xero organisation's tenant id is kept (read only).</param>
    /// <param name="files">The issued PDFs (X6); <see langword="null"/> while none is registered — then nothing is uploaded, and an approved quotation counts as exported only once sent.</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    /// <param name="options">Q8 choices; <see langword="null"/> for the defaults.</param>
    public XeroQuotePlanner(
        IXeroQuoteSource quotes,
        IXeroLinkStore links,
        IXeroOutbox outbox,
        IPersistenceStore state,
        ISecretStore secretStore,
        IXeroDocumentFileSource? files = null,
        Audit.IAuditRecorder? audit = null,
        TimeProvider? timeProvider = null,
        XeroQuotePlannerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(secretStore);

        _quotes = quotes;
        _links = links;
        _outbox = outbox;
        _state = state;
        _secretStore = secretStore;
        _files = files;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
        _options = options ?? new XeroQuotePlannerOptions();

        // Q8: sync starts when this build starts planning in the workspace.
        // Built at start-up, before any quotation can be approved in this
        // run, so a quotation approved now is never "older" than the start —
        // even when its first plan runs later (no PDF yet) or a moment after
        // its approval commit.
        _startedAtUtc = _time.GetUtcNow();
    }

    /// <inheritdoc />
    public XeroDocumentKind Kind => XeroDocumentKind.Quote;

    /// <inheritdoc />
    public string CanonicalKind => Quotation.CanonicalKind;

    /// <summary>The key, in <see cref="StateCollection"/>, of <paramref name="quotationId"/>'s Q8 opt-in recorded before F1 or with no organisation connected; it counts only for the organisation named by <see cref="AutomaticFromOwnerKey"/>.</summary>
    /// <param name="quotationId">The quotation.</param>
    public static string OptInKey(Guid quotationId) => $"send-to-xero/{quotationId:D}";

    /// <summary>`v0.24.0` F1 (M1): the key, in <see cref="StateCollection"/>, of <paramref name="quotationId"/>'s Q8 opt-in for the organisation <paramref name="tenantId"/> — a <em>Send to Xero</em> made for the Demo Company does not send the quotation to another organisation.</summary>
    /// <param name="quotationId">The quotation.</param>
    /// <param name="tenantId">The organisation.</param>
    public static string OptInKey(Guid quotationId, string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return $"send-to-xero/{tenantId}/{quotationId:D}";
    }

    /// <summary>`v0.24.0` F1 (M1): the key, in <see cref="StateCollection"/>, of the moment automatic quote sync began for the organisation <paramref name="tenantId"/>.</summary>
    /// <param name="tenantId">The organisation.</param>
    public static string AutomaticFromKeyFor(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return $"{AutomaticFromKey}/{tenantId}";
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroPlannedOperation>> PlanAsync(Guid objectId, XeroLink? link, CancellationToken cancellationToken = default)
    {
        // Q8: the start is recorded before anything can return early, so the
        // first plan of a run (a draft, an approval not yet exported) fixes it.
        await AutomaticFromAsync(cancellationToken).ConfigureAwait(false);

        var quote = await _quotes.FindAsync(objectId, cancellationToken).ConfigureAwait(false);
        if (quote is null || !quote.IsIssued)
            return [];

        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return [];

        var document = XeroDocumentRef.For(XeroDocumentKind.Quote, objectId);
        var file = _files is null ? null : await _files.FindAsync(document, cancellationToken).ConfigureAwait(false);
        var contentHash = XeroQuoteMapper.ContentHash(quote);

        // §4.1: "Approved Rn, then Export or Send". Sent, accepted and
        // declined quotations are issued; an approved revision counts once
        // exported — a PDF is held that its Xero quote does not already carry
        // (the previous revision's PDF does not count) — or once the Product
        // Owner sent this revision to Xero.
        var exported = quote.Status != QuotationStatus.Approved
                       || (file is not null && !string.Equals(link?.AttachmentContentHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                       || await IsRevisionSentToXeroAsync(objectId, contentHash, cancellationToken).ConfigureAwait(false);
        if (!exported)
            return [];

        if (link is null)
        {
            var optedIn = await IsOptedInAsync(objectId, cancellationToken).ConfigureAwait(false);
            if (!optedIn && !await IsAutomaticAsync(quote, cancellationToken).ConfigureAwait(false))
                return [];
        }

        int xeroRank;
        if (link is null)
        {
            xeroRank = 0; // the create makes it DRAFT
        }
        else if (link.LastKnownXeroStatus is null)
        {
            xeroRank = 0;
        }
        else if (XeroQuoteMapper.Rank(link.LastKnownXeroStatus) is { } rank)
        {
            xeroRank = rank;
        }
        else
        {
            // INVOICED, DELETED or a word this build does not know: drift, shown on the badge; never moved.
            return [];
        }

        var planned = new List<XeroPlannedOperation>();

        // Content goes only while Xero holds the quote as DRAFT (Q1). Past it
        // the push would be refused and hold the document's queue; the badge
        // shows the drift instead: "revision Rn was not sent; change it in
        // Xero" (XeroQuoteMapper.DriftNote(quote, link)).
        if (xeroRank == 0 && !string.Equals(link?.LastPushedContentHash, contentHash, StringComparison.Ordinal))
            planned.Add(new XeroPlannedOperation(XeroOperation.PushQuote, contentHash));

        // The PDF follows its content: past DRAFT, a revision whose content
        // Xero refused or never received keeps its PDF too, so the Xero copy
        // never shows one revision's sheet beside another's amounts.
        if (file is not null
            && !string.Equals(link?.AttachmentContentHash, file.Sha256, StringComparison.OrdinalIgnoreCase)
            && !(link is not null && xeroRank > 0 && XeroQuoteMapper.IsRevisionNotSent(quote, link)))
            planned.Add(new XeroPlannedOperation(XeroOperation.UploadAttachment, file.Sha256, XeroQuoteMapper.AttachmentFileName(quote.Reference)));

        foreach (var step in XeroQuoteMapper.StatusPath(quote.Status))
        {
            if (XeroQuoteMapper.Rank(XeroQuoteMapper.StatusWord(step)) > xeroRank)
                planned.Add(new XeroPlannedOperation(XeroOperation.SetQuoteStatus, XeroQuoteMapper.StatusHash(step), XeroQuoteMapper.StatusWord(step)));
        }

        return planned;
    }

    /// <summary>
    /// Plans <paramref name="quotationId"/> against its link in the connected
    /// Xero organisation and queues every planned write in the outbox, in
    /// order. A write already queued or sent for the same content is not
    /// queued again (the outbox returns the existing entry). No network call.
    /// </summary>
    /// <param name="quotationId">The quotation.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The outbox entries for the planned writes, in order; empty when Xero already matches or the quotation is not in scope.</returns>
    public async Task<IReadOnlyList<XeroOutboxEntry>> PlanAndEnqueueAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        var document = XeroDocumentRef.For(XeroDocumentKind.Quote, quotationId);
        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var link = tenantId is null ? null : await _links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false);

        var planned = await PlanAsync(quotationId, link, cancellationToken).ConfigureAwait(false);
        await ReleaseRefusedContentAsync(quotationId, document, link, cancellationToken).ConfigureAwait(false);

        var entries = new List<XeroOutboxEntry>(planned.Count);
        foreach (var operation in planned)
            entries.Add(await _outbox.EnqueueAsync(operation.Operation, document, operation.ContentHash, operation.Argument, cancellationToken).ConfigureAwait(false));

        return entries;
    }

    /// <summary>The start-up and Refresh scan (§6.2) — X6 calls it first thing at start-up (see the remarks on <see cref="XeroQuotePlanner"/>): records when automatic sync began (Q8) if it is not yet recorded, then <see cref="PlanAndEnqueueAsync"/> for every quotation, so a change committed before a crash, or made offline, is still queued.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>How many outbox entries the scan produced or found (planned writes).</returns>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        await AutomaticFromAsync(cancellationToken).ConfigureAwait(false);

        var count = 0;
        foreach (var id in await _quotes.ListIdsAsync(cancellationToken).ConfigureAwait(false))
            count += (await PlanAndEnqueueAsync(id, cancellationToken).ConfigureAwait(false)).Count;
        return count;
    }

    /// <summary>
    /// The Product Owner's <em>Send to Xero</em> on one quotation (Q8): a
    /// quotation issued before Xero sync began is not pushed automatically;
    /// this records the explicit request (audited) and queues its writes. Also
    /// counts as "exported" for the approved revision it was asked for, when
    /// its PDF is not held.
    /// </summary>
    /// <param name="quotationId">The quotation.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<XeroQuoteSendRequest> SendToXeroAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        var quote = await _quotes.FindAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return new XeroQuoteSendRequest(false, [], $"Quotation {quotationId:D} does not exist.");

        if (!quote.IsIssued)
            return new XeroQuoteSendRequest(false, [], $"Quotation {quote.Reference} is {quote.Status}; only an approved revision, or a sent or answered quotation, goes to Xero.");

        // F1 (M1): the opt-in is for the organisation connected now.
        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        await _state.WriteAsync(
            StateCollection, tenantId is null ? OptInKey(quotationId) : OptInKey(quotationId, tenantId),
            JsonSerializer.Serialize(new SendToXeroRecord(_time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), XeroQuoteMapper.ContentHash(quote))),
            cancellationToken).ConfigureAwait(false);

        if (_audit is not null)
        {
            await _audit.RecordAsync(AuditSendToXero, new Dictionary<string, string>
            {
                ["document"] = XeroDocumentRef.For(XeroDocumentKind.Quote, quotationId).TempestKey,
                ["number"] = quote.Reference,
            }, cancellationToken).ConfigureAwait(false);
        }

        var entries = await PlanAndEnqueueAsync(quotationId, cancellationToken).ConfigureAwait(false);
        return new XeroQuoteSendRequest(true, entries);
    }

    /// <summary>
    /// Whether <paramref name="quote"/> is synced without an explicit request
    /// (Q8): its current revision was issued at or after automatic sync
    /// began. A quotation with no known issue time is treated as older.
    /// </summary>
    /// <param name="quote">The quotation.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<bool> IsAutomaticAsync(XeroQuoteSnapshot quote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quote);

        if (quote.IssuedAtUtc is not { } issued)
            return false;

        return issued >= await AutomaticFromAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// When automatic quote sync began for the organisation connected now
    /// (<see cref="AutomaticFromAsync(string?, CancellationToken)"/>). The
    /// engine (X6) calls this, or <see cref="ScanAsync"/>, first thing at
    /// start-up and whenever another organisation is connected, so the start
    /// is recorded even if the run ends before its first plan.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<DateTimeOffset> AutomaticFromAsync(CancellationToken cancellationToken = default)
    {
        if (_options.AutomaticFromUtc is { } configured)
            return configured;

        return await AutomaticFromAsync(await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// `v0.24.0` F1 (M1): when automatic quote sync began for the organisation
    /// <paramref name="tenantId"/> — <see cref="XeroQuotePlannerOptions.AutomaticFromUtc"/>
    /// when configured; else the moment recorded for it
    /// (<see cref="AutomaticFromKeyFor"/>); else, recorded now and never moved:
    /// for the first organisation connected since F1, the workspace's own start
    /// (<see cref="AutomaticFromKey"/>, written before F1 or with no
    /// organisation connected; when none is recorded yet, the moment this
    /// planner was built — start-up, never later than a quotation approved in
    /// this run); for any later organisation, now — the moment TempestOS first
    /// sees it connected. So a quotation issued before an organisation was
    /// first connected (the Demo Company's test quotes, when the live
    /// organisation is connected) needs an explicit <em>Send to Xero</em>
    /// there. With no organisation connected (<see langword="null"/>), the
    /// workspace's own start.
    /// </summary>
    /// <param name="tenantId">The organisation; <see langword="null"/> when none is connected.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<DateTimeOffset> AutomaticFromAsync(string? tenantId, CancellationToken cancellationToken = default)
    {
        if (_options.AutomaticFromUtc is { } configured)
            return configured;

        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var workspaceStart = await ReadTimeAsync(AutomaticFromKey, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                if (workspaceStart is { } recorded)
                    return recorded;

                // First run (or an unreadable record, rewritten): sync began when
                // this run's planner was built, not when it was first asked — an
                // approval committed a moment before its plan is still automatic.
                await WriteTimeAsync(AutomaticFromKey, _startedAtUtc, cancellationToken).ConfigureAwait(false);
                return _startedAtUtc;
            }

            if (await ReadTimeAsync(AutomaticFromKeyFor(tenantId), cancellationToken).ConfigureAwait(false) is { } own)
                return own;

            // The owner is written before the organisation's own moment, so a
            // crash between the two still finds this organisation the owner.
            var owner = await _state.ReadAsync(StateCollection, AutomaticFromOwnerKey, cancellationToken).ConfigureAwait(false);
            DateTimeOffset start;
            if (owner is null || string.Equals(owner, tenantId, StringComparison.Ordinal))
            {
                start = workspaceStart ?? _startedAtUtc;
                if (workspaceStart is null)
                    await WriteTimeAsync(AutomaticFromKey, start, cancellationToken).ConfigureAwait(false);
                if (owner is null)
                    await _state.WriteAsync(StateCollection, AutomaticFromOwnerKey, tenantId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                start = _time.GetUtcNow();
            }

            await WriteTimeAsync(AutomaticFromKeyFor(tenantId), start, cancellationToken).ConfigureAwait(false);
            return start;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private async Task<DateTimeOffset?> ReadTimeAsync(string key, CancellationToken cancellationToken)
    {
        var stored = await _state.ReadAsync(StateCollection, key, cancellationToken).ConfigureAwait(false);
        return stored is not null
               && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var recorded)
            ? recorded
            : null;
    }

    private Task WriteTimeAsync(string key, DateTimeOffset at, CancellationToken cancellationToken) =>
        _state.WriteAsync(StateCollection, key, at.ToString("O", CultureInfo.InvariantCulture), cancellationToken);

    private async Task<bool> IsOptedInAsync(Guid quotationId, CancellationToken cancellationToken) =>
        await ReadOptInAsync(quotationId, cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>
    /// The quotation's Q8 opt-in for the organisation connected now: its own
    /// (<see cref="OptInKey(Guid, string)"/>), or one recorded before F1 or
    /// with no organisation connected (<see cref="OptInKey(Guid)"/>) when this
    /// organisation is the first since F1 (<see cref="AutomaticFromOwnerKey"/>).
    /// </summary>
    private async Task<string?> ReadOptInAsync(Guid quotationId, CancellationToken cancellationToken)
    {
        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return await _state.ReadAsync(StateCollection, OptInKey(quotationId), cancellationToken).ConfigureAwait(false);

        if (await _state.ReadAsync(StateCollection, OptInKey(quotationId, tenantId), cancellationToken).ConfigureAwait(false) is { } own)
            return own;

        var owner = await _state.ReadAsync(StateCollection, AutomaticFromOwnerKey, cancellationToken).ConfigureAwait(false);
        return owner is null || string.Equals(owner, tenantId, StringComparison.Ordinal)
            ? await _state.ReadAsync(StateCollection, OptInKey(quotationId), cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>Whether the Product Owner's <em>Send to Xero</em> was asked for this very revision (content), which counts as its export.</summary>
    private async Task<bool> IsRevisionSentToXeroAsync(Guid quotationId, string contentHash, CancellationToken cancellationToken)
    {
        var stored = await ReadOptInAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
            return false;

        try
        {
            var record = JsonSerializer.Deserialize<SendToXeroRecord>(stored);
            return string.Equals(record?.ContentHash, contentHash, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// A content push Xero refused because it holds the quote past
    /// <c>DRAFT</c> stays Failed and holds the document's queue (B2). Once the
    /// TempestOS quotation is no longer Approved its content is fixed, so
    /// there is nothing left to push: the refused entry is put back to
    /// Pending, the handler then answers NothingToDo (recording the drift),
    /// and the status changes behind it run. Local only; no network call.
    /// </summary>
    private async Task ReleaseRefusedContentAsync(Guid quotationId, XeroDocumentRef document, XeroLink? link, CancellationToken cancellationToken)
    {
        if (link is null || XeroQuoteMapper.Rank(link.LastKnownXeroStatus) is not > 0)
            return;

        var quote = await _quotes.FindAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null || !quote.IsIssued || quote.Status == QuotationStatus.Approved)
            return;

        foreach (var entry in await _outbox.ListForDocumentAsync(document, cancellationToken).ConfigureAwait(false))
        {
            if (entry.Operation == XeroOperation.PushQuote && entry.State == XeroOutboxState.Failed)
                await _outbox.RetryAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record SendToXeroRecord(
        [property: System.Text.Json.Serialization.JsonPropertyName("requestedAtUtc")] string RequestedAtUtc,
        [property: System.Text.Json.Serialization.JsonPropertyName("contentHash")] string? ContentHash);

    private async Task<string?> ReadTenantIdAsync(CancellationToken cancellationToken)
    {
        var tenantId = await _secretStore.GetAsync(XeroContactLinker.TenantIdSecretKey, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
    }
}

/// <summary>The status words of the Xero quote walk, for the planner and handlers.</summary>
internal static class XeroQuoteStatusWords
{
    /// <summary><c>DRAFT</c>.</summary>
    public static readonly string Draft = XeroQuoteMapper.StatusWord(XeroQuoteWriteStatus.Draft);

    /// <summary><c>DELETED</c> — not a status TempestOS writes; a quote deleted in Xero.</summary>
    public const string Deleted = "DELETED";

    /// <summary><c>INVOICED</c> — reached only by invoicing in Xero.</summary>
    public const string Invoiced = "INVOICED";
}
