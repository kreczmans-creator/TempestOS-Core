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
    /// Q8). <see langword="null"/> (the default) uses the moment the planner
    /// first ran in this workspace, recorded once in
    /// <see cref="XeroQuotePlanner.StateCollection"/> and never moved.
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
/// revision Rn that has been exported (its PDF is held,
/// <see cref="IXeroDocumentFileSource"/>), or already sent, accepted or
/// declined. A draft, a quotation in review, or an approved one edited back
/// to draft plans nothing — the next approval issues Rn+1. Q8: a quotation
/// issued before Xero sync began in this workspace
/// (<see cref="XeroQuotePlannerOptions.AutomaticFromUtc"/>) is planned only
/// after its explicit <see cref="SendToXeroAsync"/>, or once it is linked.
/// </para>
/// <para>
/// <b>What it plans, in order</b> (the outbox keeps per-document order, so a
/// status change never overtakes its create):
/// <see cref="XeroOperation.PushQuote"/> while the content differs from what
/// was last pushed and TempestOS has not yet sent it (Q1: Xero accepts content
/// only while <c>DRAFT</c>; a sent TempestOS quotation's lines are fixed);
/// <see cref="XeroOperation.UploadAttachment"/> when the issued PDF differs
/// from the one last uploaded; then <see cref="XeroOperation.SetQuoteStatus"/>
/// for each step of <c>DRAFT → SENT → ACCEPTED | DECLINED</c> Xero has not
/// yet reached. A linked quote Xero holds off that walk (<c>INVOICED</c>,
/// <c>DELETED</c>) plans nothing: its badge shows the drift
/// (<see cref="XeroQuoteMapper.DriftNote"/>), and TempestOS never moves it.
/// </para>
/// </remarks>
public sealed class XeroQuotePlanner : IXeroSyncPlanner
{
    /// <summary>The <see cref="IPersistenceStore"/> collection holding the planner's own small state: when automatic sync began, and each quotation's <em>Send to Xero</em> opt-in.</summary>
    public const string StateCollection = "Xero.QuoteSync";

    /// <summary>The key, in <see cref="StateCollection"/>, of the moment automatic quote sync began (round-trip ISO-8601).</summary>
    public const string AutomaticFromKey = "automatic-from";

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
    }

    /// <inheritdoc />
    public XeroDocumentKind Kind => XeroDocumentKind.Quote;

    /// <inheritdoc />
    public string CanonicalKind => Quotation.CanonicalKind;

    /// <summary>The key, in <see cref="StateCollection"/>, of <paramref name="quotationId"/>'s Q8 opt-in.</summary>
    /// <param name="quotationId">The quotation.</param>
    public static string OptInKey(Guid quotationId) => $"send-to-xero/{quotationId:D}";

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroPlannedOperation>> PlanAsync(Guid objectId, XeroLink? link, CancellationToken cancellationToken = default)
    {
        var quote = await _quotes.FindAsync(objectId, cancellationToken).ConfigureAwait(false);
        if (quote is null || !quote.IsIssued)
            return [];

        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return [];

        var document = XeroDocumentRef.For(XeroDocumentKind.Quote, objectId);
        var file = _files is null ? null : await _files.FindAsync(document, cancellationToken).ConfigureAwait(false);

        if (link is null)
        {
            var optedIn = await IsOptedInAsync(objectId, cancellationToken).ConfigureAwait(false);

            // Exported: an approved revision counts once its PDF is held (or it was sent, or the PO asked).
            if (quote.Status == QuotationStatus.Approved && file is null && !optedIn)
                return [];

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

        var contentHash = XeroQuoteMapper.ContentHash(quote);
        var contentMayChange = link is null || quote.Status == QuotationStatus.Approved;
        if (contentMayChange && !string.Equals(link?.LastPushedContentHash, contentHash, StringComparison.Ordinal))
            planned.Add(new XeroPlannedOperation(XeroOperation.PushQuote, contentHash));

        if (file is not null && !string.Equals(link?.AttachmentContentHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
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
        var entries = new List<XeroOutboxEntry>(planned.Count);
        foreach (var operation in planned)
            entries.Add(await _outbox.EnqueueAsync(operation.Operation, document, operation.ContentHash, operation.Argument, cancellationToken).ConfigureAwait(false));

        return entries;
    }

    /// <summary>The start-up and Refresh scan (§6.2): <see cref="PlanAndEnqueueAsync"/> for every quotation, so a change committed before a crash, or made offline, is still queued.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>How many outbox entries the scan produced or found (planned writes).</returns>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var id in await _quotes.ListIdsAsync(cancellationToken).ConfigureAwait(false))
            count += (await PlanAndEnqueueAsync(id, cancellationToken).ConfigureAwait(false)).Count;
        return count;
    }

    /// <summary>
    /// The Product Owner's <em>Send to Xero</em> on one quotation (Q8): a
    /// quotation issued before Xero sync began is not pushed automatically;
    /// this records the explicit request (audited) and queues its writes. Also
    /// counts as "exported" for an approved revision whose PDF is not held.
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

        await _state.WriteAsync(
            StateCollection, OptInKey(quotationId),
            JsonSerializer.Serialize(new { requestedAtUtc = _time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture) }),
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

    /// <summary>When automatic quote sync began: <see cref="XeroQuotePlannerOptions.AutomaticFromUtc"/>, else the moment recorded the first time the planner was asked (written once, then read back).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<DateTimeOffset> AutomaticFromAsync(CancellationToken cancellationToken = default)
    {
        if (_options.AutomaticFromUtc is { } configured)
            return configured;

        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = await _state.ReadAsync(StateCollection, AutomaticFromKey, cancellationToken).ConfigureAwait(false);
            if (stored is not null
                && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var recorded))
            {
                return recorded;
            }

            // First run (or an unreadable record, rewritten): sync begins now.
            var now = _time.GetUtcNow();
            await _state.WriteAsync(StateCollection, AutomaticFromKey, now.ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            return now;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private async Task<bool> IsOptedInAsync(Guid quotationId, CancellationToken cancellationToken) =>
        await _state.ReadAsync(StateCollection, OptInKey(quotationId), cancellationToken).ConfigureAwait(false) is not null;

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
