using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tempest.Core.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// The hashes X4 keeps an invoice's Xero copy in step with (`v0.24.0` X4,
/// design §5, §6.1): the <b>content hash</b> a link records as last pushed
/// (what the invoice says — number, client, reference, currency, lines —
/// never when it was sent), and the <b>body hash</b> an
/// <c>Idempotency-Key</c> embeds (the exact request body, dates included,
/// so a key is never reused with a different body, S7).
/// </summary>
public static class XeroInvoiceContent
{
    /// <summary>
    /// The content hash of <paramref name="document"/>: SHA-256 (lower-case
    /// hex) over its number, client, reference, currency and every line's
    /// source, description, quantity, unit rate and VAT rate. The same
    /// invoice always hashes the same, in any process; a revised line
    /// hashes differently.
    /// </summary>
    /// <param name="document">The invoice as the draft seam is handed it.</param>
    public static string ContentHash(InvoiceDraftDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var text = new StringBuilder();
        Append(text, document.InvoiceNumber);
        Append(text, document.ClientOrganisationId);
        Append(text, document.Reference);
        Append(text, document.Currency.ToString());
        foreach (var line in document.Lines)
        {
            Append(text, line.SourceKind);
            Append(text, line.SourceId.ToString("D"));
            Append(text, line.Description);
            Append(text, line.Quantity.ToString(CultureInfo.InvariantCulture));
            Append(text, line.UnitRate.Amount.ToString(CultureInfo.InvariantCulture));
            Append(text, line.VatRate.ToString());
        }

        return Sha256Hex(text.ToString());
    }

    /// <summary>The body hash of <paramref name="draft"/>: SHA-256 (lower-case hex) over its JSON — every field Xero is sent, dates included.</summary>
    /// <param name="draft">The resolved invoice.</param>
    public static string BodyHash(XeroSalesInvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return Sha256Hex(JsonSerializer.Serialize(draft));
    }

    /// <summary>SHA-256 (lower-case hex) of <paramref name="value"/> as UTF-8.</summary>
    /// <param name="value">The text.</param>
    public static string Sha256Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static void Append(StringBuilder text, string? value) => text.Append(value ?? "\u0000").Append('\u001f');
}
