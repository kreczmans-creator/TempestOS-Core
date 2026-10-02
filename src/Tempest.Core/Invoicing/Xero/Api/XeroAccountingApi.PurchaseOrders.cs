using System.Net;
using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero.Api;

// ============================================================================
// `v0.24.0` task X5 (D5, Q2) — the PurchaseOrders resource
// (`docs/releases/v0.24.0/Xero Technical Design.md` §3, §4.3). A read by
// id, the natural-key lookup by `PurchaseOrderNumber` (§6.4), the create
// (`PUT PurchaseOrders`, always `DRAFT`) and the delete of a cancelled order
// (`POST PurchaseOrders/{id}` with `Status: DELETED`). The write models
// cannot express anything else: no `SentToContact`, no email method, and a
// status only from `XeroPurchaseOrderWriteStatus` (DRAFT, DELETED).
// ============================================================================
public sealed partial class XeroAccountingApi
{
    /// <summary>Xero's documented maximum length of <c>PurchaseOrderNumber</c>.</summary>
    public const int MaximumPurchaseOrderNumberLength = 255;

    /// <summary>Xero's maximum length of a purchase order's <c>Reference</c>.</summary>
    public const int MaximumPurchaseOrderReferenceLength = 255;

    /// <summary>Reads one purchase order by its <c>PurchaseOrderID</c> (<c>GET PurchaseOrders/{PurchaseOrderID}</c>); one that no longer exists answers <see cref="XeroApiResult{T}.NotFound"/>.</summary>
    /// <param name="purchaseOrderId">Xero's <c>PurchaseOrderID</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWirePurchaseOrder>> GetPurchaseOrderAsync(string purchaseOrderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purchaseOrderId);

        var result = await GetAsync<XeroWirePurchaseOrdersEnvelope>(
            $"PurchaseOrders/{Uri.EscapeDataString(purchaseOrderId.Trim())}", cancellationToken: cancellationToken).ConfigureAwait(false);
        return SinglePurchaseOrder(result, "read");
    }

    /// <summary>
    /// The purchase orders Xero holds under <paramref name="purchaseOrderNumber"/>
    /// (<c>GET PurchaseOrders/{PurchaseOrderNumber}</c>, design §3) — the
    /// natural-key lookup before a first create and before any resend after a
    /// lost response (§6.4), so an order is never created twice. Xero
    /// answers 404 for a number it does not hold: that is an empty list here,
    /// not a failure. Xero answers a single order for a number — a live one
    /// before a deleted one — so the deleted copies under it are read with
    /// <see cref="FindDeletedPurchaseOrdersAsync"/>; the caller decides what a
    /// match means.
    /// </summary>
    /// <param name="purchaseOrderNumber">The number (TempestOS's purchase-order reference).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<IReadOnlyList<XeroWirePurchaseOrder>>> FindPurchaseOrdersByNumberAsync(
        string purchaseOrderNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purchaseOrderNumber);

        var number = purchaseOrderNumber.Trim();
        var result = await GetAsync<XeroWirePurchaseOrdersEnvelope>(
            $"PurchaseOrders/{Uri.EscapeDataString(number)}", cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Outcome != ConnectorOutcome.Ok)
        {
            return result.NotFound
                ? new XeroApiResult<IReadOnlyList<XeroWirePurchaseOrder>>(ConnectorOutcome.Ok, [], (int)HttpStatusCode.NotFound, null, [])
                : Retype<XeroWirePurchaseOrdersEnvelope, IReadOnlyList<XeroWirePurchaseOrder>>(result);
        }

        // The path takes an id or a number: keep only what carries this number.
        IReadOnlyList<XeroWirePurchaseOrder> orders = [.. (result.Value!.PurchaseOrders ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o.PurchaseOrderID)
                        && string.Equals(o.PurchaseOrderNumber?.Trim(), number, StringComparison.OrdinalIgnoreCase))];
        return new XeroApiResult<IReadOnlyList<XeroWirePurchaseOrder>>(ConnectorOutcome.Ok, orders, result.HttpStatus, null, []);
    }

    /// <summary>Xero's page size for <c>GET PurchaseOrders</c> with <c>page=</c> (§6.5).</summary>
    public const int PurchaseOrdersPageSize = 100;

    /// <summary>The most pages <see cref="FindDeletedPurchaseOrdersAsync"/> reads before it answers that it could not read them all.</summary>
    public const int MaximumDeletedPurchaseOrderPages = 10;

    /// <summary>
    /// The <c>DELETED</c> purchase orders Xero holds under
    /// <paramref name="purchaseOrderNumber"/>
    /// (<c>GET PurchaseOrders?Status=DELETED&amp;DateFrom=…&amp;DateTo=…&amp;page=n</c>,
    /// kept to the number here). <c>GET PurchaseOrders/{PurchaseOrderNumber}</c>
    /// answers a single order, a live one before a deleted one, so a copy
    /// TempestOS made and someone deleted in Xero is only seen this way — and
    /// it is evidence the ownership rule counts (<c>XeroPurchasingOwnership</c>).
    /// Pages until a short page, at most <see cref="MaximumDeletedPurchaseOrderPages"/>;
    /// <see cref="XeroDeletedPurchaseOrders.Complete"/> says whether every page was read.
    /// </summary>
    /// <param name="purchaseOrderNumber">The number.</param>
    /// <param name="date">The order date the orders were created with, when known: narrows the read to that day.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroDeletedPurchaseOrders>> FindDeletedPurchaseOrdersAsync(
        string purchaseOrderNumber, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purchaseOrderNumber);

        var number = purchaseOrderNumber.Trim();
        var orders = new List<XeroWirePurchaseOrder>();
        for (var page = 1; page <= MaximumDeletedPurchaseOrderPages; page++)
        {
            List<KeyValuePair<string, string?>> query =
            [
                new("Status", "DELETED"),
                new("page", page.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ];
            if (date is { } day)
            {
                query.Add(new("DateFrom", XeroWire.FormatDate(day)));
                query.Add(new("DateTo", XeroWire.FormatDate(day)));
            }

            var result = await GetAsync<XeroWirePurchaseOrdersEnvelope>("PurchaseOrders", query, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.Outcome != ConnectorOutcome.Ok)
                return Retype<XeroWirePurchaseOrdersEnvelope, XeroDeletedPurchaseOrders>(result);

            var read = result.Value!.PurchaseOrders ?? [];
            orders.AddRange(read.Where(o => !string.IsNullOrWhiteSpace(o.PurchaseOrderID)
                                            && string.Equals(o.Status?.Trim(), "DELETED", StringComparison.OrdinalIgnoreCase)
                                            && string.Equals(o.PurchaseOrderNumber?.Trim(), number, StringComparison.OrdinalIgnoreCase)));
            if (read.Count < PurchaseOrdersPageSize)
                return new XeroApiResult<XeroDeletedPurchaseOrders>(ConnectorOutcome.Ok, new XeroDeletedPurchaseOrders(orders, Complete: true), result.HttpStatus, null, []);
        }

        return new XeroApiResult<XeroDeletedPurchaseOrders>(ConnectorOutcome.Ok, new XeroDeletedPurchaseOrders(orders, Complete: false), (int)HttpStatusCode.OK, null, []);
    }

    /// <summary>
    /// Creates a purchase order (<c>PUT PurchaseOrders</c>). TempestOS always
    /// creates it as <c>DRAFT</c> (Q2): the write model's
    /// <see cref="XeroWirePurchaseOrderWrite.Status"/> is forced to
    /// <see cref="XeroPurchaseOrderWriteStatus.Draft"/> here whatever the
    /// caller set, and a <see cref="XeroWirePurchaseOrderWrite.PurchaseOrderID"/>
    /// is refused (a TempestOS order's lines are fixed once issued, so an
    /// existing Xero order is never re-sent).
    /// </summary>
    /// <param name="order">The purchase order to create.</param>
    /// <param name="idempotencyKey">The fixed key for this create; a repeat replays Xero's cached answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWirePurchaseOrder>> CreatePurchaseOrderAsync(
        XeroWirePurchaseOrderWrite order, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.PurchaseOrderID is not null)
            return Failure<XeroWirePurchaseOrder>(ConnectorOutcome.Rejected, null, "A purchase order create carries no PurchaseOrderID; an existing Xero purchase order is never created again.");

        if (ValidatePurchaseOrderWrite(order) is { } problem)
            return Failure<XeroWirePurchaseOrder>(ConnectorOutcome.Rejected, null, problem);

        var body = new XeroWirePurchaseOrdersWriteEnvelope<XeroWirePurchaseOrderWrite>([order with { Status = XeroPurchaseOrderWriteStatus.Draft }]);
        var result = await PutJsonAsync<XeroWirePurchaseOrdersEnvelope>("PurchaseOrders", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SinglePurchaseOrder(result, "create");
    }

    /// <summary>
    /// Deletes a purchase order because the TempestOS order was cancelled
    /// (<c>POST PurchaseOrders/{PurchaseOrderID}</c> carrying only
    /// <c>PurchaseOrderID</c> and <c>Status: DELETED</c>, design §4.3). Xero
    /// refuses it once the order is <c>BILLED</c>; the caller reads first.
    /// </summary>
    /// <param name="purchaseOrderId">Xero's <c>PurchaseOrderID</c>.</param>
    /// <param name="idempotencyKey">The fixed key for this write.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWirePurchaseOrder>> DeletePurchaseOrderAsync(
        string purchaseOrderId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purchaseOrderId);

        var id = purchaseOrderId.Trim();
        var body = new XeroWirePurchaseOrdersWriteEnvelope<XeroWirePurchaseOrderStatusUpdate>(
            [new XeroWirePurchaseOrderStatusUpdate(id, XeroPurchaseOrderWriteStatus.Deleted)]);
        var result = await PostJsonAsync<XeroWirePurchaseOrdersEnvelope>($"PurchaseOrders/{Uri.EscapeDataString(id)}", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SinglePurchaseOrder(result, "delete");
    }

    /// <summary>What is wrong with <paramref name="order"/> before it is sent, or <see langword="null"/>.</summary>
    private static string? ValidatePurchaseOrderWrite(XeroWirePurchaseOrderWrite order)
    {
        if (string.IsNullOrWhiteSpace(order.Contact?.ContactID))
            return "A purchase order names its supplier by ContactID; none was given.";

        if (order.LineItems is not { Count: > 0 })
            return "A purchase order needs at least one line.";

        if (order.LineItems.Any(l => string.IsNullOrWhiteSpace(l.Description)))
            return "Every purchase order line needs a description.";

        if (order.PurchaseOrderNumber is { Length: > MaximumPurchaseOrderNumberLength })
            return $"PurchaseOrderNumber '{order.PurchaseOrderNumber}' is longer than Xero's {MaximumPurchaseOrderNumberLength} characters.";

        if (order.Reference is { Length: > MaximumPurchaseOrderReferenceLength })
            return $"The purchase order's Reference is longer than Xero's {MaximumPurchaseOrderReferenceLength} characters.";

        return null;
    }

    private static XeroApiResult<XeroWirePurchaseOrder> SinglePurchaseOrder(XeroApiResult<XeroWirePurchaseOrdersEnvelope> result, string act)
    {
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWirePurchaseOrdersEnvelope, XeroWirePurchaseOrder>(result);

        var order = result.Value!.PurchaseOrders?.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.PurchaseOrderID));
        return order is null
            ? Failure<XeroWirePurchaseOrder>(ConnectorOutcome.Unknown, result.HttpStatus, $"Xero answered the purchase order {act} with no purchase order; whether it took effect is unknown.")
            : new XeroApiResult<XeroWirePurchaseOrder>(ConnectorOutcome.Ok, order, result.HttpStatus, null, []);
    }
}

/// <summary>What <see cref="XeroAccountingApi.FindDeletedPurchaseOrdersAsync"/> read.</summary>
/// <param name="Orders">The deleted orders under the number.</param>
/// <param name="Complete">Whether every page was read (<see langword="false"/>: more deleted orders than <see cref="XeroAccountingApi.MaximumDeletedPurchaseOrderPages"/> pages hold).</param>
public sealed record XeroDeletedPurchaseOrders(IReadOnlyList<XeroWirePurchaseOrder> Orders, bool Complete);

/// <summary>Xero's <c>{ "PurchaseOrders": [ … ] }</c> envelope, as read.</summary>
/// <param name="PurchaseOrders">The purchase orders.</param>
public sealed record XeroWirePurchaseOrdersEnvelope([property: JsonPropertyName("PurchaseOrders")] IReadOnlyList<XeroWirePurchaseOrder>? PurchaseOrders);

/// <summary>Xero's <c>{ "PurchaseOrders": [ … ] }</c> envelope, as written (one order per request, §3).</summary>
/// <typeparam name="T">The write model.</typeparam>
/// <param name="PurchaseOrders">The purchase orders.</param>
public sealed record XeroWirePurchaseOrdersWriteEnvelope<T>([property: JsonPropertyName("PurchaseOrders")] IReadOnlyList<T> PurchaseOrders);

/// <summary>
/// A purchase order as TempestOS creates it (design §3): the same number as
/// TempestOS, the project code as reference, the supplier by
/// <c>ContactID</c>, the issue and expected-delivery dates, and the lines
/// with input tax types and account codes. There is deliberately no
/// <c>SentToContact</c>, and the only statuses are
/// <see cref="XeroPurchaseOrderWriteStatus"/>'s.
/// </summary>
/// <param name="PurchaseOrderNumber">TempestOS's purchase-order reference.</param>
/// <param name="Reference">The project code.</param>
/// <param name="Contact">The supplier's <c>ContactID</c> (X2).</param>
/// <param name="Date">The issue date (<c>yyyy-MM-dd</c>).</param>
/// <param name="DeliveryDate">The expected delivery date; <see langword="null"/> when none.</param>
/// <param name="CurrencyCode">The order's currency (ISO 4217).</param>
/// <param name="LineAmountTypes">Always <see cref="XeroWire.LineAmountTypesExclusive"/>: TempestOS prices are net.</param>
/// <param name="LineItems">The lines.</param>
/// <param name="Status">Forced to <see cref="XeroPurchaseOrderWriteStatus.Draft"/> by <see cref="XeroAccountingApi.CreatePurchaseOrderAsync"/>.</param>
/// <param name="PurchaseOrderID">Never set on a create.</param>
public sealed record XeroWirePurchaseOrderWrite(
    [property: JsonPropertyName("PurchaseOrderNumber")] string PurchaseOrderNumber,
    [property: JsonPropertyName("Reference")] string? Reference,
    [property: JsonPropertyName("Contact")] XeroWireContactRef Contact,
    [property: JsonPropertyName("Date")] string Date,
    [property: JsonPropertyName("DeliveryDate")] string? DeliveryDate,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode,
    [property: JsonPropertyName("LineAmountTypes")] string LineAmountTypes,
    [property: JsonPropertyName("LineItems")] IReadOnlyList<XeroWireLineItem> LineItems,
    [property: JsonPropertyName("Status")] XeroPurchaseOrderWriteStatus? Status = XeroPurchaseOrderWriteStatus.Draft,
    [property: JsonPropertyName("PurchaseOrderID")] string? PurchaseOrderID = null);

/// <summary>The one change TempestOS makes to an existing Xero purchase order: <c>DELETED</c>, because the TempestOS order was cancelled.</summary>
/// <param name="PurchaseOrderID">Xero's <c>PurchaseOrderID</c>.</param>
/// <param name="Status">The status.</param>
public sealed record XeroWirePurchaseOrderStatusUpdate(
    [property: JsonPropertyName("PurchaseOrderID")] string PurchaseOrderID,
    [property: JsonPropertyName("Status")] XeroPurchaseOrderWriteStatus Status);

/// <summary>A Xero purchase order, as Xero answers it (the fields X5 and the read-back use).</summary>
/// <param name="PurchaseOrderID">Xero's <c>PurchaseOrderID</c>.</param>
/// <param name="PurchaseOrderNumber">The number.</param>
/// <param name="Reference">The reference (TempestOS writes the project code).</param>
/// <param name="Status">Xero's status word, verbatim (<c>DRAFT</c>, <c>SUBMITTED</c>, <c>AUTHORISED</c>, <c>BILLED</c>, <c>DELETED</c>).</param>
/// <param name="Contact">The supplier.</param>
/// <param name="Date">The order date (Microsoft JSON or ISO).</param>
/// <param name="DeliveryDate">The delivery date.</param>
/// <param name="CurrencyCode">The currency.</param>
/// <param name="LineItems">The lines.</param>
/// <param name="Total">The total including tax.</param>
/// <param name="HasAttachments">Whether a file is attached.</param>
/// <param name="UpdatedDateUTC">When Xero last changed it (Microsoft JSON date).</param>
/// <param name="SubTotal">The net total (what the purchasing ownership rule compares with what TempestOS sent).</param>
public sealed record XeroWirePurchaseOrder(
    [property: JsonPropertyName("PurchaseOrderID")] string? PurchaseOrderID,
    [property: JsonPropertyName("PurchaseOrderNumber")] string? PurchaseOrderNumber = null,
    [property: JsonPropertyName("Reference")] string? Reference = null,
    [property: JsonPropertyName("Status")] string? Status = null,
    [property: JsonPropertyName("Contact")] XeroWireContactRef? Contact = null,
    [property: JsonPropertyName("Date")] string? Date = null,
    [property: JsonPropertyName("DeliveryDate")] string? DeliveryDate = null,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode = null,
    [property: JsonPropertyName("LineItems")] IReadOnlyList<XeroWireLineItem>? LineItems = null,
    [property: JsonPropertyName("Total")] decimal? Total = null,
    [property: JsonPropertyName("HasAttachments")] bool? HasAttachments = null,
    [property: JsonPropertyName("UpdatedDateUTC")] string? UpdatedDateUTC = null,
    [property: JsonPropertyName("SubTotal")] decimal? SubTotal = null);
