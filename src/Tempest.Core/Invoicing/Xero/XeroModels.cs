using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero;

/// <summary>The wire shape of Xero's own <c>/api.xro/2.0/Invoices</c> envelope, both request and response — a caller sends and reads back a list, never a single invoice.</summary>
internal sealed record XeroInvoicesEnvelope([property: JsonPropertyName("Invoices")] List<XeroInvoice>? Invoices);

/// <summary>One Xero invoice, as sent (create) or read back (status/find/bills due) — every field this connector actually uses; Xero's own real payload carries many more.</summary>
internal sealed record XeroInvoice(
    [property: JsonPropertyName("Type")] string? Type = null,
    [property: JsonPropertyName("Contact")] XeroContact? Contact = null,
    [property: JsonPropertyName("LineItems")] List<XeroLineItem>? LineItems = null,
    [property: JsonPropertyName("Reference")] string? Reference = null,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode = null,
    [property: JsonPropertyName("InvoiceID")] string? InvoiceID = null,
    [property: JsonPropertyName("InvoiceNumber")] string? InvoiceNumber = null,
    [property: JsonPropertyName("Status")] string? Status = null,
    [property: JsonPropertyName("Date")] string? Date = null,
    [property: JsonPropertyName("DueDate")] string? DueDate = null,
    [property: JsonPropertyName("Total")] decimal? Total = null,
    [property: JsonPropertyName("FullyPaidOnDate")] string? FullyPaidOnDate = null);

/// <summary>A Xero line item — <c>Description</c>, <c>Quantity</c> and <c>UnitAmount</c> are what Xero prices from; <c>LineAmount</c> is sent alongside rather than left for Xero to recompute, matching <c>InvoiceRequestLine.Amount</c>'s own "carried alongside" convention. <c>TaxType</c> (`WP 21.3B`) is this line's own <see cref="Tempest.Core.BusinessGovernance.VatRate"/>, mapped through <see cref="Tempest.Core.Invoicing.VatRateTaxTypeMapping"/> before this record is ever built — never a raw enum value handed to Xero directly.</summary>
internal sealed record XeroLineItem(
    [property: JsonPropertyName("Description")] string Description,
    [property: JsonPropertyName("Quantity")] decimal Quantity,
    [property: JsonPropertyName("UnitAmount")] decimal UnitAmount,
    [property: JsonPropertyName("LineAmount")] decimal LineAmount,
    [property: JsonPropertyName("TaxType")] string? TaxType = null);

/// <summary>A Xero contact — sending only <see cref="Name"/> lets Xero itself match an existing contact by name or create one when absent (Xero's own documented behaviour for an invoice's inline <c>Contact</c>).</summary>
internal sealed record XeroContact(
    [property: JsonPropertyName("ContactID")] string? ContactID = null,
    [property: JsonPropertyName("Name")] string? Name = null);

/// <summary>The wire shape of Xero's own <c>/api.xro/2.0/Contacts</c> envelope.</summary>
internal sealed record XeroContactsEnvelope([property: JsonPropertyName("Contacts")] List<XeroContact>? Contacts);

/// <summary>Xero's own validation-failure envelope for a 400/422 response — <c>Message</c> alone when <see cref="Elements"/> carries nothing more specific.</summary>
internal sealed record XeroApiException(
    [property: JsonPropertyName("Message")] string? Message,
    [property: JsonPropertyName("Elements")] List<XeroValidationElement>? Elements);

/// <summary>One rejected element of a Xero write — the line or invoice a validation error was raised against.</summary>
internal sealed record XeroValidationElement([property: JsonPropertyName("ValidationErrors")] List<XeroValidationError>? ValidationErrors);

/// <summary>One Xero validation error message.</summary>
internal sealed record XeroValidationError([property: JsonPropertyName("Message")] string? Message);

// ========================================================================
// `WP 19.8B` — read-only accounts data (`IAccountsConnector`): repeating
// (ACCPAY) invoices and the bank summary report. Never used to build an
// outbound request; read-only wire shapes only.
// ========================================================================

/// <summary>The wire shape of Xero's own <c>/api.xro/2.0/RepeatingInvoices</c> envelope.</summary>
internal sealed record XeroRepeatingInvoicesEnvelope([property: JsonPropertyName("RepeatingInvoices")] List<XeroRepeatingInvoice>? RepeatingInvoices);

/// <summary>One Xero repeating invoice — every field this connector's own accounts read actually uses.</summary>
internal sealed record XeroRepeatingInvoice(
    [property: JsonPropertyName("Type")] string? Type = null,
    [property: JsonPropertyName("Contact")] XeroContact? Contact = null,
    [property: JsonPropertyName("LineItems")] List<XeroRepeatingLineItem>? LineItems = null,
    [property: JsonPropertyName("Reference")] string? Reference = null,
    [property: JsonPropertyName("Status")] string? Status = null,
    [property: JsonPropertyName("Schedule")] XeroSchedule? Schedule = null,
    [property: JsonPropertyName("Total")] decimal? Total = null,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode = null);

/// <summary>One line of a Xero repeating invoice — read-only; carries <see cref="Tracking"/>/<see cref="AccountCode"/>, the "package's own account or category name" <see cref="Tempest.Core.Invoicing.AccountsCategoriser"/> categorises (`WP 19.8B` brief §1).</summary>
internal sealed record XeroRepeatingLineItem(
    [property: JsonPropertyName("Description")] string? Description = null,
    [property: JsonPropertyName("LineAmount")] decimal? LineAmount = null,
    [property: JsonPropertyName("AccountCode")] string? AccountCode = null,
    [property: JsonPropertyName("Tracking")] List<XeroTrackingCategory>? Tracking = null);

/// <summary>One Xero tracking category assignment on a line item — <see cref="Option"/> is the specific value chosen (for example "Software" under a "Cost Centre" category), the more human-readable of the two.</summary>
internal sealed record XeroTrackingCategory(
    [property: JsonPropertyName("Name")] string? Name = null,
    [property: JsonPropertyName("Option")] string? Option = null);

/// <summary>A repeating invoice's own schedule — <see cref="Unit"/> is Xero's own frequency word (for example <c>"MONTHLY"</c>), verbatim.</summary>
internal sealed record XeroSchedule(
    [property: JsonPropertyName("Unit")] string? Unit = null,
    [property: JsonPropertyName("NextScheduledDate")] string? NextScheduledDate = null);

/// <summary>The wire shape of Xero's own <c>/api.xro/2.0/Reports/BankSummary</c> envelope — a generic reporting grid, not a typed resource; parsed defensively by <c>XeroConnector.ReadCashPositionAsync</c>, disclosed in that method's own remarks.</summary>
internal sealed record XeroReportsEnvelope([property: JsonPropertyName("Reports")] List<XeroReport>? Reports);

/// <summary>One Xero report — <see cref="ReportDate"/> is read as the "as of" date for every balance the report carries.</summary>
internal sealed record XeroReport(
    [property: JsonPropertyName("ReportDate")] string? ReportDate = null,
    [property: JsonPropertyName("Rows")] List<XeroReportRow>? Rows = null);

/// <summary>One row of a Xero report grid — a <c>Header</c> row states the column titles; a <c>Section</c> row nests one <c>Row</c> per bank account in its own <see cref="Rows"/>.</summary>
internal sealed record XeroReportRow(
    [property: JsonPropertyName("RowType")] string? RowType = null,
    [property: JsonPropertyName("Title")] string? Title = null,
    [property: JsonPropertyName("Cells")] List<XeroReportCell>? Cells = null,
    [property: JsonPropertyName("Rows")] List<XeroReportRow>? Rows = null);

/// <summary>One cell of a Xero report grid row — always text on the wire, parsed by the reader that knows which column it is.</summary>
internal sealed record XeroReportCell([property: JsonPropertyName("Value")] string? Value = null);
