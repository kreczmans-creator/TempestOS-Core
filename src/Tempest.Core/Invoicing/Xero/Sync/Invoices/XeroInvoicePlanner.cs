namespace Tempest.Core.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// Decides, from an invoice request and its Xero link, which writes Xero
/// still needs (`v0.24.0` X4, design §6.2) — desired state, not events, and
/// never a network call:
/// <list type="bullet">
/// <item><see cref="XeroOperation.PushInvoiceDraft"/> — a request sent to
/// Xero that Xero could not reach (back to Draft with the reason), or whose
/// answer was lost (Unknown), and that has no link yet.</item>
/// <item><see cref="XeroOperation.UpdateInvoiceDraft"/> — a Sent request
/// whose content differs from what was last pushed, while Xero was last seen
/// to hold it as <c>DRAFT</c>.</item>
/// <item><see cref="XeroOperation.DeleteInvoiceDraft"/> — a Voided request
/// whose Xero copy was last seen as a draft.</item>
/// <item><see cref="XeroOperation.UploadAttachment"/> — a linked invoice whose
/// issued PDF (by SHA-256) is not the one last uploaded.</item>
/// </list>
/// A request sent through any other connector, a pre-v0.24 request never
/// sent through Xero (Q8: pushed on demand only), and a link written by a
/// newer TempestOS plan nothing.
/// </summary>
public sealed class XeroInvoicePlanner : IXeroSyncPlanner
{
    private readonly IInvoicingService _invoicing;
    private readonly IXeroDocumentFileSource? _files;

    /// <summary>Initialises a new instance of the <see cref="XeroInvoicePlanner"/> class.</summary>
    /// <param name="invoicing">The invoicing service (its <see cref="InvoicingService"/> implementation builds the request's draft document locally).</param>
    /// <param name="files">Where the issued PDF is read from (X6); <see langword="null"/> plans no upload.</param>
    public XeroInvoicePlanner(IInvoicingService invoicing, IXeroDocumentFileSource? files = null)
    {
        ArgumentNullException.ThrowIfNull(invoicing);

        _invoicing = invoicing;
        _files = files;
    }

    /// <inheritdoc />
    public XeroDocumentKind Kind => XeroDocumentKind.Invoice;

    /// <inheritdoc />
    public string CanonicalKind => InvoiceRequest.CanonicalKind;

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroPlannedOperation>> PlanAsync(Guid objectId, XeroLink? link, CancellationToken cancellationToken = default)
    {
        if (_invoicing is not InvoicingService service)
            return [];

        if (await service.FindDraftDocumentAsync(objectId, cancellationToken).ConfigureAwait(false) is not { } found)
            return [];

        var (request, document) = found;
        if (!string.Equals(request.Connector, XeroInvoiceLinkImporter.XeroConnectorName, StringComparison.Ordinal))
            return [];

        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return [];

        var contentHash = XeroInvoiceContent.ContentHash(document);
        var planned = new List<XeroPlannedOperation>();

        if (link is null)
        {
            var interrupted = (request.Status == InvoiceRequestStatus.Draft && !string.IsNullOrWhiteSpace(request.LastError))
                || request.Status == InvoiceRequestStatus.Unknown;
            if (interrupted)
                planned.Add(new XeroPlannedOperation(XeroOperation.PushInvoiceDraft, contentHash));

            return planned;
        }

        var status = link.LastKnownXeroStatus;
        var heldAsDraft = XeroConnector.IsDraft(status);

        if (request.Status == InvoiceRequestStatus.Sent && heldAsDraft
            && link.LastPushedContentHash is not null && !string.Equals(link.LastPushedContentHash, contentHash, StringComparison.Ordinal))
        {
            planned.Add(new XeroPlannedOperation(XeroOperation.UpdateInvoiceDraft, contentHash));
        }

        if (request.Status == InvoiceRequestStatus.Voided && (heldAsDraft || XeroConnector.IsAwaitingApproval(status)))
            planned.Add(new XeroPlannedOperation(XeroOperation.DeleteInvoiceDraft, XeroInvoiceContent.Sha256Hex(link.XeroId)));

        var gone = string.Equals(status, XeroConnector.DeletedStatus, StringComparison.OrdinalIgnoreCase)
            || (status?.Contains("VOID", StringComparison.OrdinalIgnoreCase) ?? false)
            || request.Status == InvoiceRequestStatus.Voided;

        if (!gone && _files is not null
            && await _files.FindAsync(XeroInvoiceDrafts.DocumentFor(objectId), cancellationToken).ConfigureAwait(false) is { } file
            && !string.Equals(file.Sha256, link.AttachmentContentHash, StringComparison.OrdinalIgnoreCase))
        {
            planned.Add(new XeroPlannedOperation(XeroOperation.UploadAttachment, file.Sha256, file.FileName));
        }

        return planned;
    }
}
