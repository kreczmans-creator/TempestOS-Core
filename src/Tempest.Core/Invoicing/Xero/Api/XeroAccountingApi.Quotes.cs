using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero.Api;

// ============================================================================
// `v0.24.0` task X3 (D2, Q1) — the Quotes resource (`docs/releases/v0.24.0/
// Xero Technical Design.md` §3, §4.1). A read by id, the natural-key lookup
// by `QuoteNumber` (§6.4), the create (`PUT Quotes`, always `DRAFT`), the
// content update while Xero still holds a `DRAFT` (`POST Quotes/{id}`), and
// the status-only update that walks `DRAFT → SENT → ACCEPTED | DECLINED`.
// The write models cannot express anything else: no `SentToContact`, no
// email method, and a status only from `XeroQuoteWriteStatus`.
// ============================================================================
public sealed partial class XeroAccountingApi
{
    /// <summary>Xero's documented maximum length of <c>QuoteNumber</c>.</summary>
    public const int MaximumQuoteNumberLength = 255;

    /// <summary>Xero's maximum length of a quote's <c>Title</c>.</summary>
    public const int MaximumQuoteTitleLength = 100;

    /// <summary>Xero's maximum length of a quote's <c>Summary</c>.</summary>
    public const int MaximumQuoteSummaryLength = 3000;

    /// <summary>Xero's maximum length of a quote's <c>Terms</c>.</summary>
    public const int MaximumQuoteTermsLength = 4000;

    /// <summary>Reads one quote by its <c>QuoteID</c> (<c>GET Quotes/{QuoteID}</c>); a quote that no longer exists answers <see cref="XeroApiResult{T}.NotFound"/>.</summary>
    /// <param name="quoteId">Xero's <c>QuoteID</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireQuote>> GetQuoteAsync(string quoteId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteId);

        var result = await GetAsync<XeroWireQuotesEnvelope>($"Quotes/{Uri.EscapeDataString(quoteId.Trim())}", cancellationToken: cancellationToken).ConfigureAwait(false);
        return SingleQuote(result, "read");
    }

    /// <summary>
    /// The quotes whose <c>QuoteNumber</c> is <paramref name="quoteNumber"/>
    /// (<c>GET Quotes?QuoteNumber=…</c>) — the natural-key lookup before a
    /// first create and before any resend after a lost response (§6.4), so
    /// a quote is never created twice. Every status, <c>DELETED</c> included;
    /// the caller decides what a match means.
    /// </summary>
    /// <param name="quoteNumber">The number (TempestOS's quotation reference).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<IReadOnlyList<XeroWireQuote>>> FindQuotesByNumberAsync(string quoteNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteNumber);

        var result = await GetAsync<XeroWireQuotesEnvelope>("Quotes", [new("QuoteNumber", quoteNumber.Trim())], cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWireQuotesEnvelope, IReadOnlyList<XeroWireQuote>>(result);

        IReadOnlyList<XeroWireQuote> quotes = [.. (result.Value!.Quotes ?? []).Where(q => !string.IsNullOrWhiteSpace(q.QuoteID))];
        return new XeroApiResult<IReadOnlyList<XeroWireQuote>>(ConnectorOutcome.Ok, quotes, result.HttpStatus, null, []);
    }

    /// <summary>
    /// Creates a quote (<c>PUT Quotes</c>). TempestOS always creates it as
    /// <c>DRAFT</c> (Q1): the write model's <see cref="XeroWireQuoteWrite.Status"/>
    /// is forced to <see cref="XeroQuoteWriteStatus.Draft"/> here whatever the
    /// caller set, and a <see cref="XeroWireQuoteWrite.QuoteID"/> is refused
    /// (an update is <see cref="UpdateQuoteContentAsync"/>).
    /// </summary>
    /// <param name="quote">The quote to create.</param>
    /// <param name="idempotencyKey">The fixed key for this create; a repeat replays Xero's cached answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireQuote>> CreateQuoteAsync(XeroWireQuoteWrite quote, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quote);

        if (quote.QuoteID is not null)
            return Failure<XeroWireQuote>(ConnectorOutcome.Rejected, null, "A quote create carries no QuoteID; an existing Xero quote is updated, never created again.");

        if (ValidateQuoteWrite(quote) is { } problem)
            return Failure<XeroWireQuote>(ConnectorOutcome.Rejected, null, problem);

        var body = new XeroWireQuotesWriteEnvelope<XeroWireQuoteWrite>([quote with { Status = XeroQuoteWriteStatus.Draft }]);
        var result = await PutJsonAsync<XeroWireQuotesEnvelope>("Quotes", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleQuote(result, "create");
    }

    /// <summary>
    /// Replaces the content of an existing quote (<c>POST Quotes/{QuoteID}</c>)
    /// — a new revision Rn. Xero allows a content edit only while it holds
    /// the quote as <c>DRAFT</c> (§4.1), so the caller reads it first; the
    /// body states <c>Status: DRAFT</c> whatever <paramref name="quote"/>
    /// carries (`v0.24.0` review m1), so a quote the Product Owner sent (or
    /// marked accepted or declined) in Xero between that read and this write
    /// is refused by Xero — Xero has no move back to <c>DRAFT</c> — rather
    /// than having its content or status changed by TempestOS.
    /// </summary>
    /// <param name="quoteId">Xero's <c>QuoteID</c>.</param>
    /// <param name="quote">The new content.</param>
    /// <param name="idempotencyKey">The fixed key for this update.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireQuote>> UpdateQuoteContentAsync(
        string quoteId, XeroWireQuoteWrite quote, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteId);
        ArgumentNullException.ThrowIfNull(quote);

        if (ValidateQuoteWrite(quote) is { } problem)
            return Failure<XeroWireQuote>(ConnectorOutcome.Rejected, null, problem);

        var id = quoteId.Trim();
        var body = new XeroWireQuotesWriteEnvelope<XeroWireQuoteWrite>([quote with { QuoteID = id, Status = XeroQuoteWriteStatus.Draft }]);
        var result = await PostJsonAsync<XeroWireQuotesEnvelope>($"Quotes/{Uri.EscapeDataString(id)}", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleQuote(result, "update");
    }

    /// <summary>
    /// Moves an existing quote to <paramref name="update"/>'s status
    /// (<c>POST Quotes/{QuoteID}</c>, design §4.1): the body carries
    /// <c>QuoteID</c>, <c>QuoteNumber</c>, <c>Contact</c>, <c>Date</c> and
    /// <c>Status</c> and deliberately omits <c>LineItems</c>, so a status
    /// change can never also change content.
    /// </summary>
    /// <param name="update">The status-only update.</param>
    /// <param name="idempotencyKey">The fixed key for this update.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireQuote>> SetQuoteStatusAsync(
        XeroWireQuoteStatusUpdate update, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrWhiteSpace(update.QuoteID);

        var id = update.QuoteID.Trim();
        var body = new XeroWireQuotesWriteEnvelope<XeroWireQuoteStatusUpdate>([update with { QuoteID = id }]);
        var result = await PostJsonAsync<XeroWireQuotesEnvelope>($"Quotes/{Uri.EscapeDataString(id)}", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleQuote(result, "status update");
    }

    private static string? ValidateQuoteWrite(XeroWireQuoteWrite quote)
    {
        if (string.IsNullOrWhiteSpace(quote.QuoteNumber))
            return "A quote needs a QuoteNumber (TempestOS's quotation reference).";
        if (quote.QuoteNumber.Length > MaximumQuoteNumberLength)
            return $"QuoteNumber '{quote.QuoteNumber}' is longer than Xero's {MaximumQuoteNumberLength} characters.";
        if (string.IsNullOrWhiteSpace(quote.Contact?.ContactID))
            return "A quote names its contact by ContactID only; none was given.";
        if (quote.LineItems is null || quote.LineItems.Count == 0)
            return "A quote needs at least one line.";
        if (quote.Title is { Length: > MaximumQuoteTitleLength })
            return $"Title is longer than Xero's {MaximumQuoteTitleLength} characters.";
        if (quote.Summary is { Length: > MaximumQuoteSummaryLength })
            return $"Summary is longer than Xero's {MaximumQuoteSummaryLength} characters.";
        if (quote.Terms is { Length: > MaximumQuoteTermsLength })
            return $"Terms are longer than Xero's {MaximumQuoteTermsLength} characters.";
        return null;
    }

    private static XeroApiResult<XeroWireQuote> SingleQuote(XeroApiResult<XeroWireQuotesEnvelope> result, string act)
    {
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWireQuotesEnvelope, XeroWireQuote>(result);

        var quote = result.Value!.Quotes?.FirstOrDefault(q => !string.IsNullOrWhiteSpace(q.QuoteID));
        return quote is null
            ? Failure<XeroWireQuote>(ConnectorOutcome.Unknown, result.HttpStatus, $"Xero answered the quote {act} with no quote; whether it took effect is unknown.")
            : new XeroApiResult<XeroWireQuote>(ConnectorOutcome.Ok, quote, result.HttpStatus, null, []);
    }
}

/// <summary>Xero's <c>{ "Quotes": [ … ] }</c> envelope, as read.</summary>
/// <param name="Quotes">The quotes.</param>
public sealed record XeroWireQuotesEnvelope([property: JsonPropertyName("Quotes")] IReadOnlyList<XeroWireQuote>? Quotes);

/// <summary>Xero's <c>{ "Quotes": [ … ] }</c> envelope, as written (one quote per request, §3).</summary>
/// <typeparam name="T">The write model.</typeparam>
/// <param name="Quotes">The quotes.</param>
public sealed record XeroWireQuotesWriteEnvelope<T>([property: JsonPropertyName("Quotes")] IReadOnlyList<T> Quotes);

/// <summary>
/// A quote as TempestOS creates it or replaces its content (§3): the same
/// number, revision, title, project, contact (by <c>ContactID</c>), dates,
/// terms, currency and lines as the TempestOS quotation. There is no
/// <c>SentToContact</c> and no free-text status (D4, §7.2).
/// </summary>
/// <param name="QuoteNumber"><c>QuoteNumber</c> = the quotation's own reference.</param>
/// <param name="Reference"><c>Reference</c> = the approved revision (<c>R1</c>, <c>R2</c>, …); <see langword="null"/> for a quotation sent before revisions existed.</param>
/// <param name="Title"><c>Title</c> = the quotation's display name (≤ 100).</param>
/// <param name="Summary"><c>Summary</c> = the project (≤ 3,000).</param>
/// <param name="Contact">The linked contact, by <c>ContactID</c> only (X2).</param>
/// <param name="Date"><c>Date</c> = the quote date (<c>yyyy-MM-dd</c>).</param>
/// <param name="ExpiryDate"><c>ExpiryDate</c> = quote date + validity days.</param>
/// <param name="Terms">The quotation's terms (≤ 4,000).</param>
/// <param name="CurrencyCode">The quotation's currency.</param>
/// <param name="LineAmountTypes">Always <see cref="XeroWire.LineAmountTypesExclusive"/>: TempestOS prices net of VAT.</param>
/// <param name="LineItems">The lines, each with its output tax type and the sales account code (X1).</param>
/// <param name="Status">Set by <see cref="XeroAccountingApi.CreateQuoteAsync"/> to <c>DRAFT</c>; never sent on a content update.</param>
/// <param name="QuoteID">Set by <see cref="XeroAccountingApi.UpdateQuoteContentAsync"/>; never on a create.</param>
public sealed record XeroWireQuoteWrite(
    [property: JsonPropertyName("QuoteNumber")] string QuoteNumber,
    [property: JsonPropertyName("Reference")] string? Reference,
    [property: JsonPropertyName("Title")] string? Title,
    [property: JsonPropertyName("Summary")] string? Summary,
    [property: JsonPropertyName("Contact")] XeroWireContactRef Contact,
    [property: JsonPropertyName("Date")] string Date,
    [property: JsonPropertyName("ExpiryDate")] string? ExpiryDate,
    [property: JsonPropertyName("Terms")] string? Terms,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode,
    [property: JsonPropertyName("LineAmountTypes")] string LineAmountTypes,
    [property: JsonPropertyName("LineItems")] IReadOnlyList<XeroWireLineItem> LineItems,
    [property: JsonPropertyName("Status")] XeroQuoteWriteStatus? Status = null,
    [property: JsonPropertyName("QuoteID")] string? QuoteID = null);

/// <summary>
/// A status-only quote update (§4.1): the identifying fields Xero requires,
/// carried forward exactly as Xero holds them, and the new status — never
/// <c>LineItems</c>.
/// </summary>
/// <param name="QuoteID">Xero's <c>QuoteID</c>.</param>
/// <param name="QuoteNumber">The quote's number, as Xero holds it.</param>
/// <param name="Contact">The quote's contact, as Xero holds it (by <c>ContactID</c>).</param>
/// <param name="Date">The quote's date, as Xero holds it (<c>yyyy-MM-dd</c>).</param>
/// <param name="Status">The status TempestOS moves it to.</param>
public sealed record XeroWireQuoteStatusUpdate(
    [property: JsonPropertyName("QuoteID")] string QuoteID,
    [property: JsonPropertyName("QuoteNumber")] string? QuoteNumber,
    [property: JsonPropertyName("Contact")] XeroWireContactRef Contact,
    [property: JsonPropertyName("Date")] string Date,
    [property: JsonPropertyName("Status")] XeroQuoteWriteStatus Status);

/// <summary>A Xero quote, as Xero answers it (the fields X3 reads).</summary>
/// <param name="QuoteID">Xero's <c>QuoteID</c>.</param>
/// <param name="QuoteNumber">The quote's number.</param>
/// <param name="Reference">The quote's reference (TempestOS writes the revision, <c>Rn</c>).</param>
/// <param name="Status">Xero's status word, verbatim (<c>DRAFT</c>, <c>SENT</c>, <c>ACCEPTED</c>, <c>DECLINED</c>, <c>INVOICED</c>, <c>DELETED</c>) — read as text, so a word this build does not know is still shown.</param>
/// <param name="Contact">The quote's contact.</param>
/// <param name="Date">The quote's date (Microsoft JSON or ISO).</param>
/// <param name="ExpiryDate">The expiry date.</param>
/// <param name="Title">The title.</param>
/// <param name="Summary">The summary.</param>
/// <param name="Terms">The terms.</param>
/// <param name="CurrencyCode">The currency.</param>
/// <param name="LineAmountTypes">How line amounts are stated.</param>
/// <param name="LineItems">The lines.</param>
/// <param name="HasAttachments">Whether a file is attached.</param>
/// <param name="UpdatedDateUTC">When Xero last changed it (Microsoft JSON date).</param>
public sealed record XeroWireQuote(
    [property: JsonPropertyName("QuoteID")] string? QuoteID,
    [property: JsonPropertyName("QuoteNumber")] string? QuoteNumber = null,
    [property: JsonPropertyName("Reference")] string? Reference = null,
    [property: JsonPropertyName("Status")] string? Status = null,
    [property: JsonPropertyName("Contact")] XeroWireContactRef? Contact = null,
    [property: JsonPropertyName("Date")] string? Date = null,
    [property: JsonPropertyName("ExpiryDate")] string? ExpiryDate = null,
    [property: JsonPropertyName("Title")] string? Title = null,
    [property: JsonPropertyName("Summary")] string? Summary = null,
    [property: JsonPropertyName("Terms")] string? Terms = null,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode = null,
    [property: JsonPropertyName("LineAmountTypes")] string? LineAmountTypes = null,
    [property: JsonPropertyName("LineItems")] IReadOnlyList<XeroWireLineItem>? LineItems = null,
    [property: JsonPropertyName("HasAttachments")] bool? HasAttachments = null,
    [property: JsonPropertyName("UpdatedDateUTC")] string? UpdatedDateUTC = null);
