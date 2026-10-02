using System.Globalization;
using Tempest.Core.Audit;
using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>What one read-back pass did (<see cref="XeroReadBack.ReadAsync"/>).</summary>
/// <param name="Read">Records whose status was read from Xero.</param>
/// <param name="Changed">The documents whose Xero status (or number) changed since it was last read — the engine re-plans each.</param>
/// <param name="Stopped">Whether the pass stopped early: Xero unreachable, rate-limited, or needing re-authorisation; the rest are read on the next pass.</param>
/// <param name="StopReason">Why it stopped; <see langword="null"/> when it did not.</param>
public sealed record XeroReadBackReport(int Read, IReadOnlyList<XeroDocumentRef> Changed, bool Stopped, string? StopReason = null);

/// <summary>
/// Reads Xero's own status back for every linked quote, invoice, purchase
/// order and bill that can still change there (`v0.24.0` X6, design §3
/// "Status read-back", §4): an invoice approved, paid, voided or deleted; a
/// quote moved, invoiced or deleted; a purchase order approved, billed or
/// deleted; a bill approved, paid, voided or deleted. Never writes to Xero.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read only, oldest reading first.</b> Each pass reads at most
/// <c>budget</c> records — those read longest ago (never read first) — so
/// the read-back never crowds the outbox drain out of the per-tenant minute
/// limit, and every record is reached in turn. A terminal record (an invoice
/// or bill <c>PAID</c>, <c>VOIDED</c> or <c>DELETED</c>; a quote or purchase
/// order <c>DELETED</c>) is not read again. One <c>GET</c> per record by its
/// Xero id: the typed client has no batched <c>IDs=</c> read (the design's
/// batched read is a later optimisation; at TempestOS's volumes one call per
/// record inside the budget stays far below the limits).
/// </para>
/// <para>
/// <b>Invoices go through their own lifecycle.</b> A linked invoice whose
/// request is sent is reconciled through
/// <see cref="IInvoicingService.ReconcileAsync"/> (`ADR-0151`: "paid is read,
/// never set"), so the request itself moves to Accepted (with its issued and
/// paid dates) or Voided, and the link is updated by the X4 drafts seam; with
/// no invoicing service the link alone is updated.
/// </para>
/// <para>
/// <b>Stops at once</b> when Xero is unreachable, answers 429 (the client-side
/// limiter's pause is honoured too) or needs re-authorising — never retried
/// in a loop. A record Xero no longer has (404) is recorded as
/// <c>DELETED</c> (badge: <em>Deleted in Xero</em>).
/// </para>
/// </remarks>
public sealed class XeroReadBack
{
    /// <summary>The audit action written when a record's status in Xero is found changed.</summary>
    public const string AuditStatusRead = "xero.status.read";

    /// <summary>The status recorded for a linked record Xero answers 404 for.</summary>
    public const string DeletedStatus = "DELETED";

    private readonly XeroAccountingApi _api;
    private readonly IXeroLinkStore _links;
    private readonly IInvoicingService? _invoicing;
    private readonly XeroRateLimiter? _rateLimiter;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="XeroReadBack"/> class.</summary>
    /// <param name="api">The typed Xero client.</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="invoicing">The invoicing service, through which a linked invoice is reconciled; <see langword="null"/> updates the link only.</param>
    /// <param name="rateLimiter">The client-side limiter, whose pause stops a pass early; <see langword="null"/> when none.</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock links are stamped with; <see langword="null"/> for the system clock.</param>
    public XeroReadBack(
        XeroAccountingApi api, IXeroLinkStore links, IInvoicingService? invoicing = null, XeroRateLimiter? rateLimiter = null,
        IAuditRecorder? audit = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(links);

        _api = api;
        _links = links;
        _invoicing = invoicing;
        _rateLimiter = rateLimiter;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Whether <paramref name="link"/> can still change in Xero, so is worth reading back.</summary>
    /// <param name="link">The link.</param>
    public static bool IsLive(XeroLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return false;

        var status = Word(link.LastKnownXeroStatus);
        return link.Document.Kind switch
        {
            XeroDocumentKind.Invoice or XeroDocumentKind.ExpenseBill => status is not ("PAID" or "VOIDED" or DeletedStatus),
            XeroDocumentKind.Quote or XeroDocumentKind.PurchaseOrder => status is not DeletedStatus,
            _ => false,
        };
    }

    /// <summary>Reads back up to <paramref name="budget"/> live linked records in <paramref name="tenantId"/>, oldest reading first.</summary>
    /// <param name="tenantId">The connected Xero organisation.</param>
    /// <param name="budget">The most records to read this pass.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    public async Task<XeroReadBackReport> ReadAsync(string tenantId, int budget, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var due = (await _links.ListAsync(tenantId, cancellationToken: cancellationToken).ConfigureAwait(false))
            .Where(IsLive)
            .OrderBy(l => l.LastReadAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(l => l.LinkedAtUtc)
            .Take(Math.Max(0, budget))
            .ToList();

        var changed = new List<XeroDocumentRef>();
        var read = 0;
        foreach (var link in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_rateLimiter?.PausedUntilUtc is { } paused)
                return new XeroReadBackReport(read, changed, true, $"Xero's rate limit: paused until {paused:O}.");

            var reading = await ReadOneAsync(tenantId, link, cancellationToken).ConfigureAwait(false);
            if (reading.Stop is { } stop)
                return new XeroReadBackReport(read, changed, true, stop);

            read++;
            if (reading.Changed)
                changed.Add(link.Document);
        }

        return new XeroReadBackReport(read, changed, false);
    }

    private async Task<(bool Changed, string? Stop)> ReadOneAsync(string tenantId, XeroLink link, CancellationToken cancellationToken)
    {
        if (link.Document.Kind == XeroDocumentKind.Invoice && _invoicing is not null && Guid.TryParse(link.Document.TempestKey, out var requestId))
        {
            var reconciled = await _invoicing.ReconcileAsync(requestId, cancellationToken).ConfigureAwait(false);
            if (_rateLimiter?.PausedUntilUtc is { } paused)
                return (false, $"Xero's rate limit: paused until {paused:O}.");

            var after = await _links.FindAsync(tenantId, link.Document, cancellationToken).ConfigureAwait(false);
            if (after is null)
                return (false, null);

            // The drafts seam stamps the link whenever the service read Xero's
            // status; otherwise (Xero unreachable, or a request the service
            // does not reconcile) the link is read directly below.
            if (reconciled.Succeeded && after.LastReadAtUtc != link.LastReadAtUtc)
            {
                var moved = !string.Equals(Word(after.LastKnownXeroStatus), Word(link.LastKnownXeroStatus), StringComparison.Ordinal);
                if (moved)
                    await AuditAsync(after, link.LastKnownXeroStatus, cancellationToken).ConfigureAwait(false);
                return (moved, null);
            }
        }

        var answer = await ReadStatusAsync(link, cancellationToken).ConfigureAwait(false);
        if (answer.Stop is not null)
            return (false, answer.Stop);

        var now = _time.GetUtcNow();
        var current = await _links.FindAsync(tenantId, link.Document, cancellationToken).ConfigureAwait(false) ?? link;
        if (!string.Equals(current.XeroId, link.XeroId, StringComparison.Ordinal))
            return (false, null);

        var status = answer.Status ?? current.LastKnownXeroStatus;
        var number = string.IsNullOrWhiteSpace(answer.Number) ? current.XeroNumber : answer.Number.Trim();
        var changed = !string.Equals(Word(status), Word(current.LastKnownXeroStatus), StringComparison.Ordinal)
                      || !string.Equals(number, current.XeroNumber, StringComparison.Ordinal);

        await _links.SaveAsync(current with { LastKnownXeroStatus = status, XeroNumber = number, LastReadAtUtc = now }, cancellationToken).ConfigureAwait(false);
        if (changed)
            await AuditAsync(current with { LastKnownXeroStatus = status, XeroNumber = number }, current.LastKnownXeroStatus, cancellationToken).ConfigureAwait(false);

        return (changed, null);
    }

    private async Task<(string? Status, string? Number, string? Stop)> ReadStatusAsync(XeroLink link, CancellationToken cancellationToken)
    {
        switch (link.Document.Kind)
        {
            case XeroDocumentKind.Quote:
            {
                var result = await _api.GetQuoteAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
                return Interpret(result, q => (q.Status, q.QuoteNumber));
            }

            case XeroDocumentKind.Invoice:
            {
                var result = await _api.GetInvoiceAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
                return Interpret(result, i => (i.Status, i.InvoiceNumber));
            }

            case XeroDocumentKind.ExpenseBill:
            {
                var result = await _api.GetBillAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
                return Interpret(result, b => (b.Status, b.InvoiceNumber));
            }

            case XeroDocumentKind.PurchaseOrder:
            {
                var result = await _api.GetPurchaseOrderAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
                return Interpret(result, p => (p.Status, p.PurchaseOrderNumber));
            }

            default:
                return (null, null, null);
        }
    }

    private static (string? Status, string? Number, string? Stop) Interpret<T>(XeroApiResult<T> result, Func<T, (string? Status, string? Number)> read)
    {
        if (result.Outcome == ConnectorOutcome.Ok && result.Value is { } value)
        {
            var (status, number) = read(value);
            return (string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant(), number, null);
        }

        if (result.NotFound)
            return (DeletedStatus, null, null);

        return result.Outcome switch
        {
            ConnectorOutcome.Reauthorise => (null, null, $"Xero needs re-authorising: {result.Reason}"),
            ConnectorOutcome.Unavailable => (null, null, $"Xero is unavailable: {result.Reason}"),
            ConnectorOutcome.Rejected => (null, null, null),
            _ => (null, null, null),
        };
    }

    private async Task AuditAsync(XeroLink link, string? previous, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;

        await _audit.RecordAsync(AuditStatusRead, new Dictionary<string, string>
        {
            ["document"] = link.Document.TempestKey,
            ["kind"] = link.Document.Kind.ToString(),
            ["xeroId"] = link.XeroId,
            ["xeroNumber"] = link.XeroNumber ?? string.Empty,
            ["previousStatus"] = previous ?? string.Empty,
            ["status"] = link.LastKnownXeroStatus ?? string.Empty,
            ["readAtUtc"] = _time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string? Word(string? status) => string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
}
