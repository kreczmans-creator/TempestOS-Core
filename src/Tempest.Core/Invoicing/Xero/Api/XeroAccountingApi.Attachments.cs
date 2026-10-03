using System.Globalization;
using Tempest.Core.Invoicing.Xero.Sync;

namespace Tempest.Core.Invoicing.Xero.Api;

/// <summary>The Xero documents TempestOS attaches a file to (§3). A bill is an <c>Invoices</c> document (<c>Type: ACCPAY</c>).</summary>
public enum XeroAttachableResource
{
    /// <summary><c>Quotes/{QuoteID}/Attachments/…</c>.</summary>
    Quotes,

    /// <summary><c>Invoices/{InvoiceID}/Attachments/…</c> — sales invoices and bills.</summary>
    Invoices,

    /// <summary><c>PurchaseOrders/{PurchaseOrderID}/Attachments/…</c>.</summary>
    PurchaseOrders,
}

// `v0.24.0` task B1: the attachments resource (§3, X3–X5).
public sealed partial class XeroAccountingApi
{
    /// <summary>
    /// Uploads <paramref name="file"/> to the Xero document
    /// <paramref name="documentId"/>: <c>PUT {resource}/{id}/Attachments/{FileName}</c>
    /// for a first upload, <c>POST</c> (Xero's update-by-file-name) when
    /// <paramref name="replaceExisting"/> — a new revision's PDF keeps the
    /// same file name, and Xero has no attachment delete (§3).
    /// </summary>
    /// <param name="resource">Which kind of document.</param>
    /// <param name="documentId">Xero's id for the document (<c>QuoteID</c>, <c>InvoiceID</c>, <c>PurchaseOrderID</c>).</param>
    /// <param name="file">The file; larger than <see cref="XeroDocumentFile.MaximumSizeInBytes"/> is refused before sending.</param>
    /// <param name="idempotencyKey">The fixed key for this upload.</param>
    /// <param name="replaceExisting">Whether a file of the same name was uploaded before (send <c>POST</c>, not <c>PUT</c>).</param>
    /// <param name="includeOnline">For an invoice only: whether the client sees the file on Xero's online invoice (Q5; default off). Ignored for quotes and purchase orders, which have no such flag.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The attachment as Xero recorded it.</returns>
    public async Task<XeroApiResult<XeroWireAttachment>> UploadAttachmentAsync(
        XeroAttachableResource resource, string documentId, XeroDocumentFile file, string idempotencyKey,
        bool replaceExisting = false, bool includeOnline = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.FileName);

        if (file.Content.Length > XeroDocumentFile.MaximumSizeInBytes)
        {
            return Failure<XeroWireAttachment>(
                ConnectorOutcome.Rejected, null,
                $"'{file.FileName}' is {file.Content.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes; TempestOS attaches files up to {XeroDocumentFile.MaximumSizeInBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes.");
        }

        IEnumerable<KeyValuePair<string, string?>>? query = resource == XeroAttachableResource.Invoices
            ? [new("IncludeOnline", includeOnline ? "true" : "false")]
            : null;

        var result = await SendBinaryAsync<XeroWireAttachmentsEnvelope>(
            replaceExisting ? HttpMethod.Post : HttpMethod.Put,
            AttachmentsPath(resource, documentId, file.FileName),
            query, file.Content, file.ContentType, idempotencyKey, cancellationToken).ConfigureAwait(false);

        return SingleAttachment(result);
    }

    /// <summary>Lists the attachments on the Xero document <paramref name="documentId"/> (<c>GET {resource}/{id}/Attachments</c>).</summary>
    /// <param name="resource">Which kind of document.</param>
    /// <param name="documentId">Xero's id for the document.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<IReadOnlyList<XeroWireAttachment>>> ListAttachmentsAsync(
        XeroAttachableResource resource, string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        var result = await GetAsync<XeroWireAttachmentsEnvelope>(
            $"{ResourceSegment(resource)}/{Uri.EscapeDataString(documentId)}/Attachments", cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.Outcome == ConnectorOutcome.Ok
            ? new XeroApiResult<IReadOnlyList<XeroWireAttachment>>(ConnectorOutcome.Ok, result.Value!.Attachments ?? [], result.HttpStatus, null, [])
            : Retype<XeroWireAttachmentsEnvelope, IReadOnlyList<XeroWireAttachment>>(result);
    }

    /// <summary>The attachment path for <paramref name="fileName"/> on a document, each segment escaped (the file name as <see cref="XeroFileName"/> gives it).</summary>
    internal static string AttachmentsPath(XeroAttachableResource resource, string documentId, string fileName) =>
        $"{ResourceSegment(resource)}/{Uri.EscapeDataString(documentId)}/Attachments/{Uri.EscapeDataString(XeroFileName(fileName))}";

    /// <summary>
    /// The name <paramref name="fileName"/> is attached under in Xero. A name
    /// holding a literal escape sequence (a receipt named <c>Invoice%20A.pdf</c>)
    /// would, once escaped for the path, read as escaped twice, which
    /// <see cref="XeroWriteSafetyHandler"/> refuses (B1); such a name is sent
    /// with each <c>'%'</c> as <c>'_'</c> (<c>Invoice_20A.pdf</c>). Every
    /// other name, a lone <c>'%'</c> included (<c>50% off.jpg</c>), is sent
    /// as it is. Deterministic, so a replacing upload (<c>POST</c>, Xero's
    /// update-by-file-name) reaches the same attachment.
    /// </summary>
    internal static string XeroFileName(string fileName) =>
        fileName.Contains('%', StringComparison.Ordinal) && Uri.UnescapeDataString(fileName) != fileName
            ? fileName.Replace('%', '_')
            : fileName;

    private static string ResourceSegment(XeroAttachableResource resource) => resource switch
    {
        XeroAttachableResource.Quotes => "Quotes",
        XeroAttachableResource.Invoices => "Invoices",
        XeroAttachableResource.PurchaseOrders => "PurchaseOrders",
        _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Not a Xero document that takes attachments."),
    };

    private static XeroApiResult<XeroWireAttachment> SingleAttachment(XeroApiResult<XeroWireAttachmentsEnvelope> result)
    {
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWireAttachmentsEnvelope, XeroWireAttachment>(result);

        var attachment = result.Value!.Attachments?.FirstOrDefault();
        return attachment is null
            ? Failure<XeroWireAttachment>(ConnectorOutcome.Unknown, result.HttpStatus, "Xero accepted the upload but returned no attachment.")
            : new XeroApiResult<XeroWireAttachment>(ConnectorOutcome.Ok, attachment, result.HttpStatus, null, []);
    }
}
