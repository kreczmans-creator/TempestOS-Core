using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero.Api;

// `v0.24.0` task X4: the Invoices resource for sales invoices (design §3,
// §4.2). TempestOS creates an ACCREC invoice only as DRAFT, updates its
// content only while Xero still holds it as DRAFT, and deletes only its own
// draft. The write model's status is `XeroInvoiceWriteStatus` (DRAFT or
// DELETED) — there is no way to express an approved or submitted invoice,
// and no write model carries a "sent to contact" flag (D3, D4, §7.2).
public sealed partial class XeroAccountingApi
{
    /// <summary>Xero's documented maximum length of an invoice <c>Reference</c>.</summary>
    public const int MaximumInvoiceReferenceLength = 255;

    /// <summary>Xero's documented maximum length of an <c>InvoiceNumber</c>.</summary>
    public const int MaximumInvoiceNumberLength = 255;

    /// <summary>
    /// Creates a sales invoice as <c>DRAFT</c> (<c>PUT Invoices</c>, §3). The
    /// type is forced to <c>ACCREC</c> and the status to
    /// <see cref="XeroInvoiceWriteStatus.Draft"/>, whatever
    /// <paramref name="invoice"/> carries — D3 holds by construction here,
    /// below the safety handler that holds it again.
    /// </summary>
    /// <param name="invoice">The invoice to create (contact by <c>ContactID</c>, number, lines).</param>
    /// <param name="idempotencyKey">The fixed key for this create; a repeat replays Xero's cached answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The invoice as Xero recorded it.</returns>
    public async Task<XeroApiResult<XeroWireInvoice>> CreateSalesInvoiceDraftAsync(
        XeroWireInvoiceWrite invoice, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (Validate(invoice) is { } problem)
            return Failure<XeroWireInvoice>(ConnectorOutcome.Rejected, null, problem);

        var draft = invoice with { InvoiceID = null, Type = XeroWire.InvoiceTypeSales, Status = XeroInvoiceWriteStatus.Draft };
        var result = await PutJsonAsync<XeroWireInvoicesEnvelope>(
            "Invoices", new XeroWireInvoicesWriteEnvelope([draft]), idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleInvoice(result, "create");
    }

    /// <summary>
    /// Updates the content of the invoice <paramref name="invoiceId"/>
    /// (<c>POST Invoices/{InvoiceID}</c>). The caller checks first that Xero
    /// still holds it as <c>DRAFT</c> (X4), and the body states
    /// <c>Status: DRAFT</c> whatever <paramref name="invoice"/> carries: should
    /// the Product Owner approve it in Xero between that check and this
    /// write, Xero refuses the move back to <c>DRAFT</c> rather than changing
    /// an approved invoice's content. Deleting is
    /// <see cref="DeleteInvoiceDraftAsync"/>.
    /// </summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c>.</param>
    /// <param name="invoice">The new content.</param>
    /// <param name="idempotencyKey">The fixed key for this update.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The invoice as Xero now holds it.</returns>
    public async Task<XeroApiResult<XeroWireInvoice>> UpdateInvoiceContentAsync(
        string invoiceId, XeroWireInvoiceWrite invoice, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);
        ArgumentNullException.ThrowIfNull(invoice);

        if (Validate(invoice) is { } problem)
            return Failure<XeroWireInvoice>(ConnectorOutcome.Rejected, null, problem);

        var update = invoice with { InvoiceID = invoiceId.Trim(), Type = null, Status = XeroInvoiceWriteStatus.Draft };
        var result = await PostJsonAsync<XeroWireInvoicesEnvelope>(
            $"Invoices/{Uri.EscapeDataString(invoiceId.Trim())}", new XeroWireInvoicesWriteEnvelope([update]), idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleInvoice(result, "update");
    }

    /// <summary>
    /// Deletes TempestOS's own draft (<c>POST Invoices/{InvoiceID}</c> carrying
    /// only <c>InvoiceID</c> and <c>Status: DELETED</c>). Xero accepts it only
    /// while the invoice is a draft; the caller checks first (X4: a voided
    /// TempestOS invoice deletes its Xero draft only).
    /// </summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c>.</param>
    /// <param name="idempotencyKey">The fixed key for this delete.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The invoice as Xero now holds it.</returns>
    public async Task<XeroApiResult<XeroWireInvoice>> DeleteInvoiceDraftAsync(
        string invoiceId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);

        var delete = new XeroWireInvoiceWrite(InvoiceID: invoiceId.Trim(), Status: XeroInvoiceWriteStatus.Deleted);
        var result = await PostJsonAsync<XeroWireInvoicesEnvelope>(
            $"Invoices/{Uri.EscapeDataString(invoiceId.Trim())}", new XeroWireInvoicesWriteEnvelope([delete]), idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleInvoice(result, "delete");
    }

    /// <summary>Reads one invoice by its <c>InvoiceID</c> (<c>GET Invoices/{InvoiceID}</c>); one Xero no longer has answers <see cref="XeroApiResult{T}.NotFound"/>.</summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireInvoice>> GetInvoiceAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);

        var result = await GetAsync<XeroWireInvoicesEnvelope>($"Invoices/{Uri.EscapeDataString(invoiceId.Trim())}", cancellationToken: cancellationToken).ConfigureAwait(false);
        return SingleInvoice(result, "read");
    }

    /// <summary>
    /// The invoices (and bills) whose <c>InvoiceNumber</c> is
    /// <paramref name="invoiceNumber"/> (<c>GET Invoices?InvoiceNumbers=…</c>)
    /// — the natural-key lookup before a first create and after a lost
    /// response (§6.4). Deleted invoices are included; the caller decides
    /// what a match means.
    /// </summary>
    /// <param name="invoiceNumber">The number (TempestOS's invoice identifier).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<IReadOnlyList<XeroWireInvoice>>> FindInvoicesByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);

        // A comma would split one number into two in Xero's list parameter.
        if (invoiceNumber.Contains(',', StringComparison.Ordinal))
            return Failure<IReadOnlyList<XeroWireInvoice>>(ConnectorOutcome.Rejected, null, $"Invoice number '{invoiceNumber}' contains a comma; Xero's InvoiceNumbers filter cannot look it up.");

        var result = await GetAsync<XeroWireInvoicesEnvelope>(
            "Invoices", [new("InvoiceNumbers", invoiceNumber.Trim())], cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.Outcome == ConnectorOutcome.Ok
            ? new XeroApiResult<IReadOnlyList<XeroWireInvoice>>(
                ConnectorOutcome.Ok, [.. (result.Value!.Invoices ?? []).Where(i => !string.IsNullOrWhiteSpace(i.InvoiceID))], result.HttpStatus, null, [])
            : Retype<XeroWireInvoicesEnvelope, IReadOnlyList<XeroWireInvoice>>(result);
    }

    private static string? Validate(XeroWireInvoiceWrite invoice)
    {
        if (invoice.InvoiceNumber is { Length: > MaximumInvoiceNumberLength })
            return $"InvoiceNumber '{invoice.InvoiceNumber}' is longer than Xero's {MaximumInvoiceNumberLength} characters.";

        if (invoice.Reference is { Length: > MaximumInvoiceReferenceLength })
            return $"Reference is {invoice.Reference.Length} characters; Xero allows {MaximumInvoiceReferenceLength}.";

        if (invoice.Contact is { } contact && string.IsNullOrWhiteSpace(contact.ContactID))
            return "A Xero invoice names its contact by ContactID; this one has none.";

        return null;
    }

    private static XeroApiResult<XeroWireInvoice> SingleInvoice(XeroApiResult<XeroWireInvoicesEnvelope> result, string act)
    {
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWireInvoicesEnvelope, XeroWireInvoice>(result);

        var invoice = result.Value!.Invoices?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.InvoiceID));
        return invoice is null
            ? Failure<XeroWireInvoice>(ConnectorOutcome.Unknown, result.HttpStatus, $"Xero answered the invoice {act} with no invoice; whether it took effect is unknown.")
            : new XeroApiResult<XeroWireInvoice>(ConnectorOutcome.Ok, invoice, result.HttpStatus, null, []);
    }
}

/// <summary>Xero's <c>{ "Invoices": [ … ] }</c> envelope, as read.</summary>
/// <param name="Invoices">The invoices.</param>
public sealed record XeroWireInvoicesEnvelope([property: JsonPropertyName("Invoices")] IReadOnlyList<XeroWireInvoice>? Invoices);

/// <summary>Xero's <c>{ "Invoices": [ … ] }</c> envelope, as written (one invoice per request, §3).</summary>
/// <param name="Invoices">The invoices.</param>
public sealed record XeroWireInvoicesWriteEnvelope([property: JsonPropertyName("Invoices")] IReadOnlyList<XeroWireInvoiceWrite> Invoices);

/// <summary>
/// A sales invoice as TempestOS writes it (§3): contact by <c>ContactID</c>,
/// TempestOS's own number, the project/deliverable reference, dates,
/// currency, net lines with tax type and account code. The only statuses it
/// can carry are <see cref="XeroInvoiceWriteStatus"/>'s (<c>DRAFT</c>,
/// <c>DELETED</c>); it has no field that sends anything to the client (D4).
/// </summary>
/// <param name="InvoiceID">Xero's id — only on an update or delete; <see langword="null"/> on a create.</param>
/// <param name="Type"><c>ACCREC</c> on a create; <see langword="null"/> on an update (Xero refuses a type change).</param>
/// <param name="Contact">The contact, by <c>ContactID</c> only (X2).</param>
/// <param name="InvoiceNumber">TempestOS's invoice identifier (the same number in both systems).</param>
/// <param name="Reference"><c>{project code} · {deliverable or "time &amp; expenses"}</c>.</param>
/// <param name="Date">The invoice date, <c>yyyy-MM-dd</c>.</param>
/// <param name="DueDate">The due date, <c>yyyy-MM-dd</c>.</param>
/// <param name="CurrencyCode">The ISO currency code.</param>
/// <param name="LineAmountTypes"><see cref="XeroWire.LineAmountTypesExclusive"/> (TempestOS prices net of VAT).</param>
/// <param name="LineItems">The lines.</param>
/// <param name="Status"><c>DRAFT</c> on a create, <c>DELETED</c> on a delete, <see langword="null"/> on a content update.</param>
public sealed record XeroWireInvoiceWrite(
    [property: JsonPropertyName("InvoiceID")] string? InvoiceID = null,
    [property: JsonPropertyName("Type")] string? Type = null,
    [property: JsonPropertyName("Contact")] XeroWireContactRef? Contact = null,
    [property: JsonPropertyName("InvoiceNumber")] string? InvoiceNumber = null,
    [property: JsonPropertyName("Reference")] string? Reference = null,
    [property: JsonPropertyName("Date")] string? Date = null,
    [property: JsonPropertyName("DueDate")] string? DueDate = null,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode = null,
    [property: JsonPropertyName("LineAmountTypes")] string? LineAmountTypes = null,
    [property: JsonPropertyName("LineItems")] IReadOnlyList<XeroWireLineItem>? LineItems = null,
    [property: JsonPropertyName("Status")] XeroInvoiceWriteStatus? Status = null);

/// <summary>A Xero invoice or bill, as Xero answers it (the fields X4 reads).</summary>
/// <param name="InvoiceID">Xero's <c>InvoiceID</c>.</param>
/// <param name="Type"><c>ACCREC</c> (sales) or <c>ACCPAY</c> (bill).</param>
/// <param name="Contact">The contact (<c>ContactID</c> and name).</param>
/// <param name="InvoiceNumber">The invoice number.</param>
/// <param name="Reference">The reference.</param>
/// <param name="Status">Xero's status word, verbatim (read only — never written back).</param>
/// <param name="Date">The invoice date (Microsoft JSON or ISO).</param>
/// <param name="DueDate">The due date.</param>
/// <param name="CurrencyCode">The currency.</param>
/// <param name="SubTotal">The net total.</param>
/// <param name="TotalTax">The VAT total.</param>
/// <param name="Total">The gross total.</param>
/// <param name="FullyPaidOnDate">When it was paid in full, read from Xero only.</param>
/// <param name="UpdatedDateUTC">When Xero last changed it.</param>
/// <param name="LineItems">The lines, as Xero holds them.</param>
public sealed record XeroWireInvoice(
    [property: JsonPropertyName("InvoiceID")] string? InvoiceID,
    [property: JsonPropertyName("Type")] string? Type = null,
    [property: JsonPropertyName("Contact")] XeroWireContactRef? Contact = null,
    [property: JsonPropertyName("InvoiceNumber")] string? InvoiceNumber = null,
    [property: JsonPropertyName("Reference")] string? Reference = null,
    [property: JsonPropertyName("Status")] string? Status = null,
    [property: JsonPropertyName("Date")] string? Date = null,
    [property: JsonPropertyName("DueDate")] string? DueDate = null,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode = null,
    [property: JsonPropertyName("SubTotal")] decimal? SubTotal = null,
    [property: JsonPropertyName("TotalTax")] decimal? TotalTax = null,
    [property: JsonPropertyName("Total")] decimal? Total = null,
    [property: JsonPropertyName("FullyPaidOnDate")] string? FullyPaidOnDate = null,
    [property: JsonPropertyName("UpdatedDateUTC")] string? UpdatedDateUTC = null,
    [property: JsonPropertyName("LineItems")] IReadOnlyList<XeroWireLineItem>? LineItems = null);
