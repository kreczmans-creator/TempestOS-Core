using System.Security.Cryptography;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Quotations;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>
/// The <see cref="IXeroDocumentFileSource"/> over the record's own
/// attachments (`v0.24.0` X6, design §1 "Settings/PDFs", Principle "the
/// same number and the same PDF"): the newest issued PDF Desktop attached to
/// a quotation, invoice request or purchase order when it sent, exported or
/// issued it (as <c>ProjectQuoteView.OnSendAsync</c> attaches the quote
/// sheet), or the newest receipt attached to an expense. Core never renders
/// a PDF; it only reads back what was attached. Never a network call.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which attachment.</b> Attachments are kept in the order they were
/// attached, so the last matching one is the newest. A quotation, invoice
/// request or purchase order counts only a PDF (<c>application/pdf</c>, or a
/// <c>.pdf</c> file name); an expense counts a PDF, JPEG or PNG receipt. An
/// attachment whose stored content is missing or fails its integrity check is
/// passed over for the next newest, never sent.
/// </para>
/// <para>
/// <b>File name.</b> An issued document's file is named after the record's
/// own number plus <c>.pdf</c> (<see cref="Quotation.Reference"/>,
/// <see cref="PurchaseOrder.Reference"/>, the invoice number
/// <see cref="InvoicingService.InvoiceNumberFor"/>), stable across revisions
/// so a new revision replaces it in Xero; a receipt keeps its own name.
/// <see cref="XeroDocumentFile.Sha256"/> is the SHA-256 of the bytes, lower-case
/// hex, so the same file is never uploaded twice. A file over
/// <see cref="XeroDocumentFile.MaximumSizeInBytes"/> is still returned: the
/// upload refuses it with the reason (the record itself still syncs).
/// </para>
/// <para>
/// A contact, a record that no longer exists or is deleted, or one with no
/// matching attachment has no file (<see langword="null"/>): its upload waits,
/// Blocked, rather than failing.
/// </para>
/// </remarks>
public sealed class AttachmentXeroDocumentFileSource : IXeroDocumentFileSource
{
    private const string PdfType = "application/pdf";

    private static readonly IReadOnlyDictionary<string, string> ReceiptTypesByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = PdfType,
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
    };

    private readonly EngineeringDomainContext _domain;

    /// <summary>Initialises a new instance of the <see cref="AttachmentXeroDocumentFileSource"/> class.</summary>
    /// <param name="domain">The workspace the records and their attachments are read from.</param>
    public AttachmentXeroDocumentFileSource(EngineeringDomainContext domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        _domain = domain;
    }

    /// <inheritdoc />
    public async Task<XeroDocumentFile?> FindAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Kind == XeroDocumentKind.Contact || !Guid.TryParse(document.TempestKey, out var id))
            return null;

        var found = await _domain.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (found is not IHasAttachments owner || found is IDeletable { IsDeleted: true })
            return null;

        var receipt = document.Kind == XeroDocumentKind.ExpenseBill;
        var attachments = await owner.GetAttachmentsAsync(cancellationToken).ConfigureAwait(false);

        for (var i = attachments.Count - 1; i >= 0; i--)
        {
            var attachment = attachments[i];
            var contentType = ContentTypeOf(attachment, receipt);
            if (contentType is null)
                continue;

            var content = await owner.ReadAttachmentContentAsync(attachment.Id, cancellationToken).ConfigureAwait(false);
            if (!content.IsAvailable)
                continue;

            var bytes = content.Bytes;
            var fileName = receipt ? attachment.FileName : $"{NumberOf(found) ?? Path.GetFileNameWithoutExtension(attachment.FileName)}.pdf";
            return new XeroDocumentFile(fileName, contentType, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }

        return null;
    }

    /// <summary>The MIME type <paramref name="attachment"/> is sent as, or <see langword="null"/> when it is not the kind of file this record's Xero copy carries.</summary>
    private static string? ContentTypeOf(IAttachment attachment, bool receipt)
    {
        var declared = attachment.ContentType.Trim().ToLowerInvariant();
        var extension = Path.GetExtension(attachment.FileName);

        if (!receipt)
            return declared == PdfType || string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ? PdfType : null;

        if (ReceiptTypesByExtension.Values.Contains(declared))
            return declared;

        return ReceiptTypesByExtension.TryGetValue(extension, out var byExtension) ? byExtension : null;
    }

    /// <summary>The record's own number, which names its issued PDF in Xero.</summary>
    private static string? NumberOf(IEngineeringObject record)
    {
        var number = record switch
        {
            Quotation quotation => quotation.Reference,
            PurchaseOrder order => order.Reference,
            InvoiceRequest request => InvoicingService.InvoiceNumberFor(request),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(number) ? null : number.Trim();
    }
}
