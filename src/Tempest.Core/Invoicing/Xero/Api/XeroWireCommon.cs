using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero.Api;

// ============================================================================
// `v0.24.0` task B1 — the wire shapes every Xero resource file shares
// (`docs/releases/v0.24.0/Xero Technical Design.md` §3, §6.6, §7.2). A
// resource file (`XeroAccountingApi.{Quotes,Invoices,PurchaseOrders,Bills,
// Contacts,Settings}.cs`) owns its own envelope and document shapes; these
// are the pieces they have in common. Named `XeroWire*` so they never
// collide with the `WP 19.1A` connector's own `XeroModels.cs` shapes in the
// parent namespace.
// ============================================================================

/// <summary>
/// The JSON conventions of Xero's Accounting API: PascalCase property names
/// (read case-insensitively), nulls omitted on write, Microsoft JSON dates
/// (<c>/Date(…)/</c>) on read and ISO dates on write.
/// </summary>
public static class XeroWire
{
    /// <summary>The serializer options every Xero request body is written and every response read with.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Xero's <c>LineAmountTypes</c> word for net line amounts (every TempestOS document is priced net of VAT).</summary>
    public const string LineAmountTypesExclusive = "Exclusive";

    /// <summary>Xero's invoice <c>Type</c> for a sales invoice.</summary>
    public const string InvoiceTypeSales = "ACCREC";

    /// <summary>Xero's invoice <c>Type</c> for a bill (accounts payable).</summary>
    public const string InvoiceTypeBill = "ACCPAY";

    /// <summary>Formats <paramref name="date"/> the way Xero accepts a date on write (<c>yyyy-MM-dd</c>).</summary>
    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses a Xero date — Microsoft JSON (<c>/Date(1735689600000+0000)/</c>)
    /// or ISO-8601 — to a <see cref="DateTimeOffset"/> in UTC;
    /// <see langword="null"/> when blank or unreadable.
    /// </summary>
    public static DateTimeOffset? ParseDateTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var start = raw.IndexOf("/Date(", StringComparison.Ordinal);
        if (start < 0)
        {
            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var iso)
                ? iso
                : null;
        }

        start += "/Date(".Length;
        var end = start;
        if (end < raw.Length && raw[end] == '-')
            end++;
        while (end < raw.Length && char.IsAsciiDigit(raw[end]))
            end++;

        return end > start && long.TryParse(raw.AsSpan(start, end - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : null;
    }

    /// <summary>Parses a Xero date to its calendar date (UTC); <see langword="null"/> when blank or unreadable.</summary>
    public static DateOnly? ParseDate(string? raw) =>
        ParseDateTime(raw) is { } value ? DateOnly.FromDateTime(value.UtcDateTime) : null;
}

/// <summary>
/// The only statuses TempestOS may write on a Xero sales invoice or bill
/// (D3, `ADR-0162`): it creates <c>DRAFT</c>, and may delete its own draft.
/// There is deliberately no member for any approved or submitted status —
/// the write model cannot express one.
/// </summary>
[JsonConverter(typeof(XeroUpperCaseEnumConverter<XeroInvoiceWriteStatus>))]
public enum XeroInvoiceWriteStatus
{
    /// <summary><c>DRAFT</c>: for the Product Owner to review and send from Xero.</summary>
    Draft,

    /// <summary><c>DELETED</c>: TempestOS removes its own draft (void or delete in TempestOS while Xero still holds a draft).</summary>
    Deleted,
}

/// <summary>The statuses TempestOS may write on a Xero quote: it follows TempestOS along <c>DRAFT → SENT → ACCEPTED | DECLINED</c> (D2, Q1).</summary>
[JsonConverter(typeof(XeroUpperCaseEnumConverter<XeroQuoteWriteStatus>))]
public enum XeroQuoteWriteStatus
{
    /// <summary><c>DRAFT</c>: exported, not yet sent in TempestOS.</summary>
    Draft,

    /// <summary><c>SENT</c>: sent in TempestOS.</summary>
    Sent,

    /// <summary><c>ACCEPTED</c>: accepted in TempestOS.</summary>
    Accepted,

    /// <summary><c>DECLINED</c>: declined in TempestOS.</summary>
    Declined,
}

/// <summary>The only statuses TempestOS may write on a Xero purchase order (Q2: drafts, as invoices): <c>DRAFT</c>, and <c>DELETED</c> when cancelled.</summary>
[JsonConverter(typeof(XeroUpperCaseEnumConverter<XeroPurchaseOrderWriteStatus>))]
public enum XeroPurchaseOrderWriteStatus
{
    /// <summary><c>DRAFT</c>.</summary>
    Draft,

    /// <summary><c>DELETED</c>: the purchase order was cancelled in TempestOS.</summary>
    Deleted,
}

/// <summary>Writes an enum as its member name in upper case (Xero's status words); reads it case-insensitively. A word with no member fails the read rather than inventing one.</summary>
/// <typeparam name="TEnum">The enum.</typeparam>
public sealed class XeroUpperCaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var word = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (word is not null && !word.Any(char.IsAsciiDigit) && Enum.TryParse<TEnum>(word, ignoreCase: true, out var value) && Enum.IsDefined(value))
            return value;

        throw new JsonException($"'{word}' is not a {typeof(TEnum).Name}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (!Enum.IsDefined(value))
            throw new JsonException($"{value} is not a defined {typeof(TEnum).Name}.");

        writer.WriteStringValue(value.ToString().ToUpperInvariant());
    }
}

/// <summary>A contact reference on a document: TempestOS always sends the linked <c>ContactID</c> (X2), never a name for Xero to match.</summary>
/// <param name="ContactID">Xero's <c>ContactID</c>.</param>
/// <param name="Name">The contact's name, as Xero answers it on read; never sent.</param>
public sealed record XeroWireContactRef(
    [property: JsonPropertyName("ContactID")] string? ContactID,
    [property: JsonPropertyName("Name")] string? Name = null);

/// <summary>One line on a quote, invoice, bill or purchase order (§3).</summary>
/// <param name="Description">The line's description (required by Xero).</param>
/// <param name="Quantity">The quantity (hours, or 1).</param>
/// <param name="UnitAmount">The net unit price.</param>
/// <param name="AccountCode">The account code from the X1 mapping; <see langword="null"/> for none.</param>
/// <param name="TaxType">Xero's tax type for the line's VAT rate (output on sales, input on purchases).</param>
/// <param name="TaxAmount">The VAT amount, sent only when the recorded figure must be kept (an expense receipt); otherwise Xero computes it.</param>
/// <param name="LineAmount">The net line amount, as Xero answers it; <see langword="null"/> on write lets Xero compute it.</param>
/// <param name="LineItemID">Xero's line id, as read back; <see langword="null"/> on create.</param>
public sealed record XeroWireLineItem(
    [property: JsonPropertyName("Description")] string Description,
    [property: JsonPropertyName("Quantity")] decimal Quantity,
    [property: JsonPropertyName("UnitAmount")] decimal UnitAmount,
    [property: JsonPropertyName("AccountCode")] string? AccountCode = null,
    [property: JsonPropertyName("TaxType")] string? TaxType = null,
    [property: JsonPropertyName("TaxAmount")] decimal? TaxAmount = null,
    [property: JsonPropertyName("LineAmount")] decimal? LineAmount = null,
    [property: JsonPropertyName("LineItemID")] string? LineItemID = null);

/// <summary>Xero's error body for a 400 (<c>"Type": "ValidationException"</c>) and the shape the safety handler answers with when it blocks a request (§6.6, §7.1).</summary>
/// <param name="ErrorNumber">Xero's error number (10 for a validation exception).</param>
/// <param name="Type">Xero's exception type (<c>"ValidationException"</c>).</param>
/// <param name="Message">Xero's summary message.</param>
/// <param name="Elements">The documents that failed, each with its own validation errors.</param>
public sealed record XeroWireError(
    [property: JsonPropertyName("ErrorNumber")] int? ErrorNumber,
    [property: JsonPropertyName("Type")] string? Type,
    [property: JsonPropertyName("Message")] string? Message,
    [property: JsonPropertyName("Elements")] IReadOnlyList<XeroWireElement>? Elements);

/// <summary>One document of a write, as Xero reports its errors — inside <see cref="XeroWireError.Elements"/> on a 400, or as the document itself on a 200 with per-element errors.</summary>
/// <param name="HasErrors">Whether the document failed (present on a per-element 200 answer).</param>
/// <param name="ValidationErrors">The document's validation errors.</param>
public sealed record XeroWireElement(
    [property: JsonPropertyName("HasErrors")] bool? HasErrors,
    [property: JsonPropertyName("ValidationErrors")] IReadOnlyList<XeroWireValidationError>? ValidationErrors);

/// <summary>One Xero validation error.</summary>
/// <param name="Message">The message, verbatim.</param>
public sealed record XeroWireValidationError([property: JsonPropertyName("Message")] string? Message);

/// <summary>One attachment on a Xero quote, invoice, bill or purchase order, as Xero answers it.</summary>
/// <param name="AttachmentID">Xero's attachment id.</param>
/// <param name="FileName">The file name.</param>
/// <param name="Url">Where Xero serves the file.</param>
/// <param name="MimeType">The MIME type.</param>
/// <param name="ContentLength">The size in bytes.</param>
/// <param name="IncludeOnline">Whether the client sees it on Xero's online invoice (invoices only, Q5).</param>
public sealed record XeroWireAttachment(
    [property: JsonPropertyName("AttachmentID")] string? AttachmentID,
    [property: JsonPropertyName("FileName")] string? FileName,
    [property: JsonPropertyName("Url")] string? Url = null,
    [property: JsonPropertyName("MimeType")] string? MimeType = null,
    [property: JsonPropertyName("ContentLength")] long? ContentLength = null,
    [property: JsonPropertyName("IncludeOnline")] bool? IncludeOnline = null);

/// <summary>Xero's <c>{ "Attachments": [ … ] }</c> envelope.</summary>
/// <param name="Attachments">The attachments.</param>
public sealed record XeroWireAttachmentsEnvelope([property: JsonPropertyName("Attachments")] IReadOnlyList<XeroWireAttachment>? Attachments);

/// <summary>The rate-limit facts one Xero response carried (§6.5): the per-tenant minute and day allowances left and the app-wide minute allowance left.</summary>
/// <param name="MinuteRemaining"><c>X-MinLimit-Remaining</c>; <see langword="null"/> when absent.</param>
/// <param name="DayRemaining"><c>X-DayLimit-Remaining</c>; <see langword="null"/> when absent.</param>
/// <param name="AppMinuteRemaining"><c>X-AppMinLimit-Remaining</c>; <see langword="null"/> when absent.</param>
/// <param name="ReadAtUtc">When the response arrived.</param>
public sealed record XeroRateLimitReading(int? MinuteRemaining, int? DayRemaining, int? AppMinuteRemaining, DateTimeOffset ReadAtUtc);
