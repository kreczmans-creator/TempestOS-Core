using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero.Api;

// ============================================================================
// `v0.24.0` task X5 (D5, Q3, Q4) — expense bills: Xero `Invoices` of
// `Type: ACCPAY` (`docs/releases/v0.24.0/Xero Technical Design.md` §3,
// §4.4). A read by id, the natural-key lookup by `InvoiceNumber` +
// `ContactID` (§6.4: a bill's `Reference` is not available, so the number
// and the supplier together are its key), the create (`PUT Invoices`,
// always `ACCPAY` and `DRAFT`), the content update while Xero still holds a
// `DRAFT` (`POST Invoices/{id}`), and the delete of a draft
// (`Status: DELETED`). The write models cannot express anything else: a
// status only from `XeroInvoiceWriteStatus` (DRAFT, DELETED), no
// `SentToContact`, no email method. Names are `Bill*` so they never collide
// with X4's sales-invoice resource in the same partial class.
// ============================================================================
public sealed partial class XeroAccountingApi
{
    /// <summary>Xero's documented maximum length of an invoice's (and so a bill's) <c>InvoiceNumber</c>.</summary>
    public const int MaximumBillNumberLength = 255;

    /// <summary>Xero's maximum length of a line's <c>Description</c> (4,000 characters).</summary>
    public const int MaximumBillLineDescriptionLength = 4000;

    /// <summary>Reads one bill by its <c>InvoiceID</c> (<c>GET Invoices/{InvoiceID}</c>); one that no longer exists answers <see cref="XeroApiResult{T}.NotFound"/>. Refused as <see cref="ConnectorOutcome.Rejected"/> when the id names a sales invoice, not a bill.</summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c> for the bill.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireBill>> GetBillAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);

        var result = await GetAsync<XeroWireBillsEnvelope>($"Invoices/{Uri.EscapeDataString(invoiceId.Trim())}", cancellationToken: cancellationToken).ConfigureAwait(false);
        return SingleBill(result, "read");
    }

    /// <summary>
    /// The bills (<c>ACCPAY</c> only) from <paramref name="contactId"/> numbered
    /// <paramref name="invoiceNumber"/> (<c>GET Invoices?InvoiceNumbers=…&amp;ContactIDs=…</c>)
    /// — the natural-key lookup before a first create and before any resend
    /// after a lost response (§6.4), so an expense is never billed twice. A
    /// number holding a comma (which <c>InvoiceNumbers=</c> would split) is
    /// asked for with <c>where=InvoiceNumber=="…"</c> instead. Every status,
    /// <c>DELETED</c> and <c>VOIDED</c> included; the caller decides what a
    /// match means.
    /// </summary>
    /// <param name="invoiceNumber">The bill's number (the supplier's invoice number, or <c>EXP-{id}</c>).</param>
    /// <param name="contactId">The supplier's <c>ContactID</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<IReadOnlyList<XeroWireBill>>> FindBillsAsync(
        string invoiceNumber, string contactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);

        var number = invoiceNumber.Trim();
        IEnumerable<KeyValuePair<string, string?>> query = number.Contains(',', StringComparison.Ordinal)
            ? [new("where", WhereEquals("InvoiceNumber", number)), new("ContactIDs", contactId.Trim())]
            : [new("InvoiceNumbers", number), new("ContactIDs", contactId.Trim())];

        var result = await GetAsync<XeroWireBillsEnvelope>("Invoices", query, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWireBillsEnvelope, IReadOnlyList<XeroWireBill>>(result);

        IReadOnlyList<XeroWireBill> bills = [.. (result.Value!.Invoices ?? [])
            .Where(b => !string.IsNullOrWhiteSpace(b.InvoiceID)
                        && string.Equals(b.Type, XeroWire.InvoiceTypeBill, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(b.InvoiceNumber?.Trim(), number, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(b.Contact?.ContactID, contactId.Trim(), StringComparison.OrdinalIgnoreCase))];
        return new XeroApiResult<IReadOnlyList<XeroWireBill>>(ConnectorOutcome.Ok, bills, result.HttpStatus, null, []);
    }

    /// <summary>
    /// Creates a draft bill (<c>PUT Invoices</c>). TempestOS always creates it
    /// as <c>ACCPAY</c> and <c>DRAFT</c> (D3, D5): the write model's
    /// <see cref="XeroWireBillWrite.Type"/> and <see cref="XeroWireBillWrite.Status"/>
    /// are forced here whatever the caller set, and an
    /// <see cref="XeroWireBillWrite.InvoiceID"/> is refused (an update is
    /// <see cref="UpdateBillAsync"/>).
    /// </summary>
    /// <param name="bill">The bill to create.</param>
    /// <param name="idempotencyKey">The fixed key for this create; a repeat replays Xero's cached answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireBill>> CreateBillAsync(XeroWireBillWrite bill, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);

        if (bill.InvoiceID is not null)
            return Failure<XeroWireBill>(ConnectorOutcome.Rejected, null, "A bill create carries no InvoiceID; an existing Xero bill is updated, never created again.");

        if (ValidateBillWrite(bill) is { } problem)
            return Failure<XeroWireBill>(ConnectorOutcome.Rejected, null, problem);

        var body = new XeroWireBillsWriteEnvelope<XeroWireBillWrite>([bill with { Type = XeroWire.InvoiceTypeBill, Status = XeroInvoiceWriteStatus.Draft }]);
        var result = await PutJsonAsync<XeroWireBillsEnvelope>("Invoices", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleBill(result, "create");
    }

    /// <summary>
    /// Replaces the content of an existing draft bill (<c>POST Invoices/{InvoiceID}</c>)
    /// — the expense was amended. Xero holds the content editable only while
    /// the bill is a draft (§4.4), so the caller reads it first; the body
    /// states <c>Status: DRAFT</c> whatever <paramref name="bill"/> carries
    /// (`v0.24.0` review m1, as invoices do): a bill the Product Owner
    /// approved in Xero between that read and this write is refused by Xero
    /// (no move from <c>AUTHORISED</c> back to <c>DRAFT</c>) rather than
    /// having its content changed. As for invoices, one only submitted for
    /// approval in that window goes back to <c>DRAFT</c> — never forward.
    /// </summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c> for the bill.</param>
    /// <param name="bill">The new content.</param>
    /// <param name="idempotencyKey">The fixed key for this update.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireBill>> UpdateBillAsync(
        string invoiceId, XeroWireBillWrite bill, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);
        ArgumentNullException.ThrowIfNull(bill);

        if (ValidateBillWrite(bill) is { } problem)
            return Failure<XeroWireBill>(ConnectorOutcome.Rejected, null, problem);

        var id = invoiceId.Trim();
        var body = new XeroWireBillsWriteEnvelope<XeroWireBillWrite>([bill with { InvoiceID = id, Type = XeroWire.InvoiceTypeBill, Status = XeroInvoiceWriteStatus.Draft }]);
        var result = await PostJsonAsync<XeroWireBillsEnvelope>($"Invoices/{Uri.EscapeDataString(id)}", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleBill(result, "update");
    }

    /// <summary>
    /// Deletes a draft bill because the expense was deleted in TempestOS
    /// (<c>POST Invoices/{InvoiceID}</c> carrying only <c>InvoiceID</c> and
    /// <c>Status: DELETED</c>, §4.4). Xero deletes only a draft; the caller
    /// reads first.
    /// </summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c> for the bill.</param>
    /// <param name="idempotencyKey">The fixed key for this write.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireBill>> DeleteBillAsync(string invoiceId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);

        var id = invoiceId.Trim();
        var body = new XeroWireBillsWriteEnvelope<XeroWireBillStatusUpdate>([new XeroWireBillStatusUpdate(id, XeroInvoiceWriteStatus.Deleted)]);
        var result = await PostJsonAsync<XeroWireBillsEnvelope>($"Invoices/{Uri.EscapeDataString(id)}", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleBill(result, "delete");
    }

    /// <summary>What is wrong with <paramref name="bill"/> before it is sent, or <see langword="null"/>.</summary>
    private static string? ValidateBillWrite(XeroWireBillWrite bill)
    {
        if (string.IsNullOrWhiteSpace(bill.Contact?.ContactID))
            return "A bill names its supplier by ContactID; none was given.";

        if (string.IsNullOrWhiteSpace(bill.InvoiceNumber))
            return "TempestOS always numbers a bill (the supplier's invoice number, or EXP-{id}); none was given.";

        if (bill.InvoiceNumber.Length > MaximumBillNumberLength)
            return $"The bill number '{bill.InvoiceNumber}' is longer than Xero's {MaximumBillNumberLength} characters.";

        if (bill.LineItems is not { Count: > 0 })
            return "A bill needs at least one line.";

        if (bill.LineItems.Any(l => string.IsNullOrWhiteSpace(l.Description)))
            return "Every bill line needs a description.";

        if (bill.LineItems.Any(l => l.Description.Length > MaximumBillLineDescriptionLength))
            return $"A bill line's description is longer than Xero's {MaximumBillLineDescriptionLength} characters.";

        return null;
    }

    private static XeroApiResult<XeroWireBill> SingleBill(XeroApiResult<XeroWireBillsEnvelope> result, string act)
    {
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWireBillsEnvelope, XeroWireBill>(result);

        var bill = result.Value!.Invoices?.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.InvoiceID));
        if (bill is null)
            return Failure<XeroWireBill>(ConnectorOutcome.Unknown, result.HttpStatus, $"Xero answered the bill {act} with no bill; whether it took effect is unknown.");

        return string.Equals(bill.Type, XeroWire.InvoiceTypeBill, StringComparison.OrdinalIgnoreCase)
            ? new XeroApiResult<XeroWireBill>(ConnectorOutcome.Ok, bill, result.HttpStatus, null, [])
            : Failure<XeroWireBill>(ConnectorOutcome.Rejected, result.HttpStatus, $"Xero's invoice {bill.InvoiceNumber ?? bill.InvoiceID} is a {bill.Type ?? "document of unknown type"}, not a bill (ACCPAY).");
    }
}

/// <summary>Xero's <c>{ "Invoices": [ … ] }</c> envelope, as read for bills.</summary>
/// <param name="Invoices">The bills (and, unfiltered, any other invoices).</param>
public sealed record XeroWireBillsEnvelope([property: JsonPropertyName("Invoices")] IReadOnlyList<XeroWireBill>? Invoices);

/// <summary>Xero's <c>{ "Invoices": [ … ] }</c> envelope, as written for a bill (one per request, §3).</summary>
/// <typeparam name="T">The write model.</typeparam>
/// <param name="Invoices">The bills.</param>
public sealed record XeroWireBillsWriteEnvelope<T>([property: JsonPropertyName("Invoices")] IReadOnlyList<T> Invoices);

/// <summary>
/// An expense's draft bill as TempestOS writes it (design §3): <c>ACCPAY</c>,
/// numbered with the supplier's invoice number or <c>EXP-{id}</c> (Q4),
/// against the supplier's or the "General expenses" contact (Q3), dated
/// the expense date, one line carrying the recorded net and VAT. No
/// <c>Reference</c> (Xero keeps it for sales invoices), no
/// <c>SentToContact</c>, and the only statuses are
/// <see cref="XeroInvoiceWriteStatus"/>'s.
/// </summary>
/// <param name="InvoiceNumber">The bill number.</param>
/// <param name="Contact">The supplier's <c>ContactID</c> (X2).</param>
/// <param name="Date">The expense date (<c>yyyy-MM-dd</c>).</param>
/// <param name="CurrencyCode">The expense's currency (ISO 4217).</param>
/// <param name="LineAmountTypes">Always <see cref="XeroWire.LineAmountTypesExclusive"/>: the recorded amounts are net, the VAT carried separately.</param>
/// <param name="LineItems">The line(s).</param>
/// <param name="Type">Forced to <see cref="XeroWire.InvoiceTypeBill"/> by <see cref="XeroAccountingApi"/>.</param>
/// <param name="Status">Forced to <see cref="XeroInvoiceWriteStatus.Draft"/> on a create, omitted on an update.</param>
/// <param name="InvoiceID">Set only on an update.</param>
public sealed record XeroWireBillWrite(
    [property: JsonPropertyName("InvoiceNumber")] string InvoiceNumber,
    [property: JsonPropertyName("Contact")] XeroWireContactRef Contact,
    [property: JsonPropertyName("Date")] string Date,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode,
    [property: JsonPropertyName("LineAmountTypes")] string LineAmountTypes,
    [property: JsonPropertyName("LineItems")] IReadOnlyList<XeroWireLineItem> LineItems,
    [property: JsonPropertyName("Type")] string Type = XeroWire.InvoiceTypeBill,
    [property: JsonPropertyName("Status")] XeroInvoiceWriteStatus? Status = XeroInvoiceWriteStatus.Draft,
    [property: JsonPropertyName("InvoiceID")] string? InvoiceID = null);

/// <summary>The one status change TempestOS makes to a bill: <c>DELETED</c>, while Xero still holds it as a draft.</summary>
/// <param name="InvoiceID">Xero's <c>InvoiceID</c>.</param>
/// <param name="Status">The status.</param>
public sealed record XeroWireBillStatusUpdate(
    [property: JsonPropertyName("InvoiceID")] string InvoiceID,
    [property: JsonPropertyName("Status")] XeroInvoiceWriteStatus Status);

/// <summary>A Xero bill (an <c>ACCPAY</c> invoice), as Xero answers it (the fields X5 and the read-back use).</summary>
/// <param name="InvoiceID">Xero's <c>InvoiceID</c>.</param>
/// <param name="Type">Xero's invoice type (<c>ACCPAY</c> for a bill).</param>
/// <param name="InvoiceNumber">The bill number.</param>
/// <param name="Status">Xero's status word, verbatim (<c>DRAFT</c>, <c>DELETED</c>, <c>VOIDED</c>, <c>PAID</c>, and the approved and submitted words).</param>
/// <param name="Contact">The supplier.</param>
/// <param name="Date">The bill date (Microsoft JSON or ISO).</param>
/// <param name="DueDate">The due date.</param>
/// <param name="CurrencyCode">The currency.</param>
/// <param name="LineItems">The lines.</param>
/// <param name="SubTotal">The net total.</param>
/// <param name="TotalTax">The tax total.</param>
/// <param name="Total">The total including tax.</param>
/// <param name="AmountDue">What remains to be paid.</param>
/// <param name="FullyPaidOnDate">When it was paid in full.</param>
/// <param name="HasAttachments">Whether a file is attached.</param>
/// <param name="UpdatedDateUTC">When Xero last changed it (Microsoft JSON date).</param>
public sealed record XeroWireBill(
    [property: JsonPropertyName("InvoiceID")] string? InvoiceID,
    [property: JsonPropertyName("Type")] string? Type = null,
    [property: JsonPropertyName("InvoiceNumber")] string? InvoiceNumber = null,
    [property: JsonPropertyName("Status")] string? Status = null,
    [property: JsonPropertyName("Contact")] XeroWireContactRef? Contact = null,
    [property: JsonPropertyName("Date")] string? Date = null,
    [property: JsonPropertyName("DueDate")] string? DueDate = null,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode = null,
    [property: JsonPropertyName("LineItems")] IReadOnlyList<XeroWireLineItem>? LineItems = null,
    [property: JsonPropertyName("SubTotal")] decimal? SubTotal = null,
    [property: JsonPropertyName("TotalTax")] decimal? TotalTax = null,
    [property: JsonPropertyName("Total")] decimal? Total = null,
    [property: JsonPropertyName("AmountDue")] decimal? AmountDue = null,
    [property: JsonPropertyName("FullyPaidOnDate")] string? FullyPaidOnDate = null,
    [property: JsonPropertyName("HasAttachments")] bool? HasAttachments = null,
    [property: JsonPropertyName("UpdatedDateUTC")] string? UpdatedDateUTC = null);
