using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero;

/// <summary>The wire shape of Xero's own <c>/api.xro/2.0/Invoices</c> envelope, both request and response — a caller sends and reads back a list, never a single invoice.</summary>
internal sealed record XeroInvoicesEnvelope([property: JsonPropertyName("Invoices")] List<XeroInvoice>? Invoices);

/// <summary>One Xero invoice, as sent (create) or read back (status/find) — every field this connector actually uses; Xero's own real payload carries many more.</summary>
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
    [property: JsonPropertyName("FullyPaidOnDate")] string? FullyPaidOnDate = null);

/// <summary>A Xero line item — <c>Description</c>, <c>Quantity</c> and <c>UnitAmount</c> are what Xero prices from; <c>LineAmount</c> is sent alongside rather than left for Xero to recompute, matching <c>InvoiceRequestLine.Amount</c>'s own "carried alongside" convention.</summary>
internal sealed record XeroLineItem(
    [property: JsonPropertyName("Description")] string Description,
    [property: JsonPropertyName("Quantity")] decimal Quantity,
    [property: JsonPropertyName("UnitAmount")] decimal UnitAmount,
    [property: JsonPropertyName("LineAmount")] decimal LineAmount);

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
