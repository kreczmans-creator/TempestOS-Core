using System.IO;
using Tempest.Core.Quotations;

namespace Tempest.Desktop.Quotations;

/// <summary>
/// How a <see cref="Quotation"/>'s own revision shows on its exported sheet
/// and in the exported file name (runbook C3, PO: "review by second person,
/// then export becomes R1") — one place, shared by the project's Quote tab
/// and the Business → Quotes list.
/// </summary>
/// <remarks>
/// <b>A draft is labelled, not watermarked.</b> An export of a quotation no
/// second person has approved (Draft, or In review — including a new draft
/// after an earlier revision was approved) prints <see cref="DraftMarker"/>
/// exactly where an approved one prints <c>R1</c>: in the sheet's header
/// band, its revision line ("DRAFT — not approved for issue"), its footer,
/// and the file name. That is the one slot a reader already looks to for
/// "which issue is this", it survives text extraction and printing in
/// black and white, and it needs no change to the shared document
/// template's draw machinery — a diagonal watermark would, and would still
/// leave the revision slot either blank or misleading.
/// </remarks>
public static class QuotationExport
{
    /// <summary>What an unapproved quotation prints in place of its revision.</summary>
    public const string DraftMarker = "DRAFT";

    /// <summary>The revision <paramref name="quote"/> prints — <c>R1</c>, <c>R2</c>, … — or <see cref="DraftMarker"/>.</summary>
    public static string RevisionText(Quotation quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return quote.RevisionLabel ?? DraftMarker;
    }

    /// <summary>The file name an export (or the sheet attached on Send) of <paramref name="quote"/> carries — <c>&lt;reference&gt;-R1-quote.pdf</c>, or <c>&lt;reference&gt;-DRAFT-quote.pdf</c>.</summary>
    public static string FileName(Quotation quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return $"{SanitiseFileNameSegment(quote.Reference)}-{RevisionText(quote)}-quote.pdf";
    }

    /// <summary>The quotation's own status as a person reads it — "In review" rather than "InReview", "Approved R1" rather than "Approved".</summary>
    public static string StatusText(Quotation quote)
    {
        ArgumentNullException.ThrowIfNull(quote);

        return quote.Status switch
        {
            QuotationStatus.InReview => "In review",
            QuotationStatus.Approved => $"Approved {quote.RevisionLabel}",
            _ => quote.Status.ToString(),
        };
    }

    private static string SanitiseFileNameSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
    }
}
