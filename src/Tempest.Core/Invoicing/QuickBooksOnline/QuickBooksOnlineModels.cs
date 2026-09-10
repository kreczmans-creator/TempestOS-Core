using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.QuickBooksOnline;

/// <summary>The wire shape of a single-invoice response — <c>POST .../invoice</c> and <c>GET .../invoice/{id}</c> both answer this envelope.</summary>
internal sealed record QboInvoiceEnvelope([property: JsonPropertyName("Invoice")] QboInvoice? Invoice);

/// <summary>The wire shape of <c>POST .../customer</c>'s own response.</summary>
internal sealed record QboCustomerEnvelope([property: JsonPropertyName("Customer")] QboCustomer? Customer);

/// <summary>The wire shape of <c>GET .../query?query=...</c> — one of <see cref="Invoice"/>/<see cref="Customer"/>/<see cref="Payment"/> is populated, depending on the query's own <c>FROM</c> clause.</summary>
internal sealed record QboQueryEnvelope([property: JsonPropertyName("QueryResponse")] QboQueryResponse? QueryResponse);

/// <summary>See <see cref="QboQueryEnvelope"/>.</summary>
internal sealed record QboQueryResponse(
    [property: JsonPropertyName("Invoice")] List<QboInvoice>? Invoice = null,
    [property: JsonPropertyName("Customer")] List<QboCustomer>? Customer = null,
    [property: JsonPropertyName("Payment")] List<QboPayment>? Payment = null);

/// <summary>
/// One QuickBooks Online invoice — every field this connector actually
/// reads or writes. QBO carries no single "Status" word the way Xero does:
/// <c>DeriveExternalStatus</c> in <c>QuickBooksOnlineConnector</c>
/// synthesises one from <see cref="Balance"/>/<see cref="TotalAmt"/>/
/// <see cref="EmailStatus"/> so <c>InvoicingService.InterpretStatus</c>'s
/// own substring matching still works unchanged (`WP 19.1A` part 2 brief §2).
/// </summary>
internal sealed record QboInvoice(
    [property: JsonPropertyName("Id")] string? Id = null,
    [property: JsonPropertyName("DocNumber")] string? DocNumber = null,
    [property: JsonPropertyName("PrivateNote")] string? PrivateNote = null,
    [property: JsonPropertyName("TotalAmt")] decimal? TotalAmt = null,
    [property: JsonPropertyName("Balance")] decimal? Balance = null,
    [property: JsonPropertyName("EmailStatus")] string? EmailStatus = null,
    [property: JsonPropertyName("TxnDate")] string? TxnDate = null,
    [property: JsonPropertyName("CustomerRef")] QboRef? CustomerRef = null,
    [property: JsonPropertyName("Line")] List<QboLine>? Line = null,
    [property: JsonPropertyName("CurrencyRef")] QboRef? CurrencyRef = null);

/// <summary>One QuickBooks Online invoice line — <c>SalesItemLineDetail</c> only, the one detail type this connector ever writes.</summary>
internal sealed record QboLine(
    [property: JsonPropertyName("Amount")] decimal Amount,
    [property: JsonPropertyName("DetailType")] string DetailType,
    [property: JsonPropertyName("Description")] string? Description,
    [property: JsonPropertyName("SalesItemLineDetail")] QboSalesItemLineDetail? SalesItemLineDetail);

/// <summary>See <see cref="QboLine"/>. <see cref="ItemRef"/> is <see langword="null"/> unless <c>Invoicing:QuickBooksOnline:DefaultItemId</c> is configured — QuickBooks Online's real API requires a valid product/service item per line, which this Work Package's own object model carries no catalogue for; disclosed in <c>QuickBooksOnlineConnector</c>'s own remarks.</summary>
internal sealed record QboSalesItemLineDetail(
    [property: JsonPropertyName("Qty")] decimal? Qty,
    [property: JsonPropertyName("UnitPrice")] decimal? UnitPrice,
    [property: JsonPropertyName("ItemRef")] QboRef? ItemRef = null);

/// <summary>A QuickBooks Online <c>"value"</c>/<c>"name"</c> reference pair — <c>CustomerRef</c>, <c>CurrencyRef</c>, <c>ItemRef</c> all share this shape.</summary>
internal sealed record QboRef(
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("name")] string? Name = null);

/// <summary>A QuickBooks Online customer — matched (and, when absent, created) by <see cref="DisplayName"/>, the one textual identifier <c>InvoiceRequestSnapshot.ClientOrganisationId</c> actually carries.</summary>
internal sealed record QboCustomer(
    [property: JsonPropertyName("Id")] string? Id = null,
    [property: JsonPropertyName("DisplayName")] string? DisplayName = null);

/// <summary>A QuickBooks Online payment — read only for its own <see cref="TxnDate"/>, to answer <see cref="InvoiceStatusReading.PaidDate"/> once an invoice's own balance reaches zero.</summary>
internal sealed record QboPayment([property: JsonPropertyName("TxnDate")] string? TxnDate);

/// <summary>QuickBooks Online's own error envelope for a 400/422 response.</summary>
internal sealed record QboFaultEnvelope([property: JsonPropertyName("Fault")] QboFault? Fault);

/// <summary>See <see cref="QboFaultEnvelope"/>.</summary>
internal sealed record QboFault([property: JsonPropertyName("Error")] List<QboFaultError>? Error);

/// <summary>One QuickBooks Online fault detail.</summary>
internal sealed record QboFaultError(
    [property: JsonPropertyName("Message")] string? Message,
    [property: JsonPropertyName("Detail")] string? Detail);
