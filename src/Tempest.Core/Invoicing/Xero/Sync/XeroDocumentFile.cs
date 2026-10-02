namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>
/// The file TempestOS attaches to a record's copy in Xero: the PDF it
/// issued for a quote, invoice or purchase order, or the receipt recorded
/// against an expense.
/// </summary>
/// <param name="FileName">The attachment's file name in Xero. For an issued document, the record's own number plus <c>.pdf</c> (for example <c>"P0012-Q-001.pdf"</c>) — stable across revisions, so a new revision replaces it; for a receipt, the receipt's own file name.</param>
/// <param name="ContentType">The MIME type (<c>"application/pdf"</c>, <c>"image/jpeg"</c>, <c>"image/png"</c>).</param>
/// <param name="Content">The bytes, exactly as TempestOS holds them.</param>
/// <param name="Sha256">The SHA-256 of <see cref="Content"/>, lower-case hex — what <see cref="XeroLink.AttachmentContentHash"/> compares, so the same file is never uploaded twice.</param>
public sealed record XeroDocumentFile(string FileName, string ContentType, ReadOnlyMemory<byte> Content, string Sha256)
{
    /// <summary>
    /// The largest file TempestOS uploads (10 MB) — deliberately well under
    /// Xero's own documented per-file attachment limit, so Xero is never the
    /// first place a size problem is found. A larger file fails the upload
    /// entry with a reason; the record itself still syncs.
    /// </summary>
    public const long MaximumSizeInBytes = 10L * 1024 * 1024;
}

/// <summary>
/// Supplies the file a record's copy in Xero carries (Principle: "the same
/// number and the same PDF"). PDFs are drawn in <c>Tempest.Desktop</c>
/// (Skia), so Core never renders: the Desktop act that issues a document
/// (Send, Export, Issue) stores the rendered PDF as an attachment on the
/// record — as <c>ProjectQuoteView.OnSendAsync</c> already does for a quote
/// sheet — and this source reads the newest one back. An expense's receipt
/// is already an ordinary attachment on the expense.
/// </summary>
public interface IXeroDocumentFileSource
{
    /// <summary>The newest issued PDF (or, for an expense, receipt) for <paramref name="document"/>, or <see langword="null"/> when there is none yet — the upload entry then waits, Blocked, rather than failing.</summary>
    Task<XeroDocumentFile?> FindAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);
}
