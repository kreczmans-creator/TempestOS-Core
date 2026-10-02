using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// Uploads an issued purchase order's PDF to its Xero purchase order
/// (`v0.24.0` X5, <see cref="XeroOperation.UploadAttachment"/> for
/// <see cref="XeroDocumentKind.PurchaseOrder"/> documents; design §3, §4.3):
/// under the order's own reference (<c>{Reference}.pdf</c>), replacing a
/// file of that name by <c>POST</c>; never the same PDF twice
/// (<see cref="XeroLink.AttachmentContentHash"/>). Blocked until the Xero
/// purchase order exists and the PDF is held.
/// </summary>
/// <remarks>The engine (X6) dispatches <see cref="XeroOperation.UploadAttachment"/> by <see cref="DocumentKind"/>.</remarks>
public sealed class XeroPurchaseOrderAttachmentHandler : IXeroPushHandler
{
    private readonly XeroPurchasingUploader _uploader;
    private readonly IXeroPurchaseOrderSource _orders;

    /// <summary>Initialises a new instance of the <see cref="XeroPurchaseOrderAttachmentHandler"/> class.</summary>
    /// <param name="api">The typed Xero client.</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="orders">Reads purchase orders (for the file name).</param>
    /// <param name="files">The issued PDFs (X6); <see langword="null"/> while none is registered — every upload is then Blocked.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    public XeroPurchaseOrderAttachmentHandler(
        XeroAccountingApi api, IXeroLinkStore links, IXeroPurchaseOrderSource orders, IXeroDocumentFileSource? files = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(orders);

        _uploader = new XeroPurchasingUploader(api, links, files, timeProvider);
        _orders = orders;
    }

    /// <summary>The kind of document whose files this handler uploads.</summary>
    public XeroDocumentKind DocumentKind => XeroDocumentKind.PurchaseOrder;

    /// <inheritdoc />
    public IReadOnlyCollection<XeroOperation> Operations { get; } = [XeroOperation.UploadAttachment];

    /// <inheritdoc />
    public async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Operation != XeroOperation.UploadAttachment || entry.Document.Kind != XeroDocumentKind.PurchaseOrder || !Guid.TryParse(entry.Document.TempestKey, out var orderId))
            return new XeroPushResult(XeroPushOutcome.Rejected, $"The purchase order attachment handler uploads purchase order PDFs only; not {entry.Operation} for {entry.Document.Kind} {entry.Document.TempestKey}.");

        return await _uploader.UploadAsync(
            tenantId, entry, XeroAttachableResource.PurchaseOrders, "purchase order",
            async ct => XeroPurchasingMapper.PurchaseOrderAttachmentFileName(
                (await _orders.FindAsync(orderId, ct).ConfigureAwait(false))?.Reference ?? orderId.ToString("D")),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Uploads an expense's receipt to its Xero draft bill (`v0.24.0` X5,
/// <see cref="XeroOperation.UploadAttachment"/> for
/// <see cref="XeroDocumentKind.ExpenseBill"/> documents; design §3, §4.4):
/// under the receipt's own file name, never shown on Xero's online invoice
/// (Q5), never the same receipt twice. Blocked until the Xero bill exists
/// and a receipt is attached to the expense.
/// </summary>
/// <remarks>The engine (X6) dispatches <see cref="XeroOperation.UploadAttachment"/> by <see cref="DocumentKind"/>.</remarks>
public sealed class XeroExpenseBillAttachmentHandler : IXeroPushHandler
{
    private readonly XeroPurchasingUploader _uploader;

    /// <summary>Initialises a new instance of the <see cref="XeroExpenseBillAttachmentHandler"/> class.</summary>
    /// <param name="api">The typed Xero client.</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="files">The receipts (X6); <see langword="null"/> while none is registered — every upload is then Blocked.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    public XeroExpenseBillAttachmentHandler(XeroAccountingApi api, IXeroLinkStore links, IXeroDocumentFileSource? files = null, TimeProvider? timeProvider = null) =>
        _uploader = new XeroPurchasingUploader(api, links, files, timeProvider);

    /// <summary>The kind of document whose files this handler uploads.</summary>
    public XeroDocumentKind DocumentKind => XeroDocumentKind.ExpenseBill;

    /// <inheritdoc />
    public IReadOnlyCollection<XeroOperation> Operations { get; } = [XeroOperation.UploadAttachment];

    /// <inheritdoc />
    public Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Operation != XeroOperation.UploadAttachment || entry.Document.Kind != XeroDocumentKind.ExpenseBill)
        {
            return Task.FromResult(new XeroPushResult(
                XeroPushOutcome.Rejected, $"The expense bill attachment handler uploads receipts only; not {entry.Operation} for {entry.Document.Kind} {entry.Document.TempestKey}."));
        }

        return _uploader.UploadAsync(tenantId, entry, XeroAttachableResource.Invoices, "bill", _ => Task.FromResult<string?>(null), cancellationToken);
    }
}

/// <summary>The upload both purchasing attachment handlers share.</summary>
internal sealed class XeroPurchasingUploader
{
    private readonly XeroAccountingApi _api;
    private readonly IXeroLinkStore _links;
    private readonly IXeroDocumentFileSource? _files;
    private readonly TimeProvider _time;

    public XeroPurchasingUploader(XeroAccountingApi api, IXeroLinkStore links, IXeroDocumentFileSource? files, TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(links);

        _api = api;
        _links = links;
        _files = files;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Uploads the document's current file under the entry's file name (or
    /// <paramref name="defaultName"/>'s, else the file's own), replacing a
    /// file of that name; nothing when the link already carries this file,
    /// or a newer file replaced the one the entry was queued for.
    /// </summary>
    public async Task<XeroPushResult> UploadAsync(
        string tenantId, XeroOutboxEntry entry, XeroAttachableResource resource, string what, Func<CancellationToken, Task<string?>> defaultName,
        CancellationToken cancellationToken)
    {
        var link = await _links.FindAsync(tenantId, entry.Document, cancellationToken).ConfigureAwait(false);
        if (link is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The {what} is not in Xero yet; its file is attached once the Xero {what} exists.");

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The Xero link for this {what} was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        if (XeroPurchasingMapper.Word(link.LastKnownXeroStatus) is XeroPurchasingMapper.StatusDeleted or XeroPurchasingMapper.StatusVoided)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"The Xero {what} {link.XeroNumber ?? link.XeroId} is deleted; nothing is attached.", Link: link);

        var file = _files is null ? null : await _files.FindAsync(entry.Document, cancellationToken).ConfigureAwait(false);
        if (file is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, $"The {what}'s file is not held yet; it is attached once TempestOS holds it.");

        if (string.Equals(link.AttachmentContentHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            return new XeroPushResult(XeroPushOutcome.NothingToDo, Link: link);

        // A different file than this entry was queued for: the newer entry uploads it under its own key.
        if (!string.Equals(file.Sha256, entry.ContentHash, StringComparison.OrdinalIgnoreCase))
            return new XeroPushResult(XeroPushOutcome.NothingToDo, "A newer file replaced the one this upload was queued for; the newer upload sends it.", Link: link);

        var name = !string.IsNullOrWhiteSpace(entry.Argument)
            ? entry.Argument.Trim()
            : await defaultName(cancellationToken).ConfigureAwait(false) ?? XeroPurchasingMapper.ReceiptFileName(file.FileName);

        // Replace by name once a file of that name was uploaded (Xero has no attachment delete, §3).
        var replace = string.Equals(link.AttachmentFileName, name, StringComparison.OrdinalIgnoreCase);
        var uploaded = await _api.UploadAttachmentAsync(
            resource, link.XeroId, file with { FileName = name }, entry.IdempotencyKey, replace, includeOnline: false, cancellationToken).ConfigureAwait(false);

        if (uploaded.Outcome != ConnectorOutcome.Ok)
        {
            return uploaded.NotFound
                ? new XeroPushResult(XeroPushOutcome.Rejected, $"The Xero {what} {link.XeroNumber ?? link.XeroId} was deleted in Xero; its file was not attached.", Link: link)
                : XeroPurchasingMapper.Failed(uploaded);
        }

        link = link with { AttachmentFileName = name, AttachmentContentHash = file.Sha256, LastReadAtUtc = _time.GetUtcNow() };
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: link);
    }
}
