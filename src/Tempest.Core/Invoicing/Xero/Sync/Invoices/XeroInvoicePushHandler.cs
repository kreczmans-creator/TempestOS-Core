namespace Tempest.Core.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// Sends the invoice outbox entries (`v0.24.0` X4, design §4.2, §6.3):
/// <see cref="XeroOperation.PushInvoiceDraft"/> (a send Xero could not
/// reach, sent again through <see cref="InvoicingService"/> so the request's
/// own lifecycle records it), <see cref="XeroOperation.UpdateInvoiceDraft"/>
/// and <see cref="XeroOperation.DeleteInvoiceDraft"/> (only while Xero still
/// holds the invoice as a draft; otherwise Rejected with the reason).
/// </summary>
/// <remarks>
/// Never throws for anything Xero or the network did (`ADR-0151`); never
/// creates a Xero invoice for a request already linked (the drafts seam
/// refuses); a request found <see cref="InvoiceRequestStatus.Unknown"/> is
/// reconciled by its number before anything is resent (§6.4 item 3).
/// </remarks>
public sealed class XeroInvoicePushHandler : IXeroPushHandler
{
    private readonly IInvoicingService _invoicing;
    private readonly XeroInvoiceDrafts _drafts;

    /// <summary>Initialises a new instance of the <see cref="XeroInvoicePushHandler"/> class.</summary>
    /// <param name="invoicing">The invoicing service (its <see cref="InvoicingService"/> implementation sends, reconciles and builds the draft document).</param>
    /// <param name="drafts">Xero's drafts seam.</param>
    public XeroInvoicePushHandler(IInvoicingService invoicing, XeroInvoiceDrafts drafts)
    {
        ArgumentNullException.ThrowIfNull(invoicing);
        ArgumentNullException.ThrowIfNull(drafts);

        _invoicing = invoicing;
        _drafts = drafts;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<XeroOperation> Operations { get; } =
        [XeroOperation.PushInvoiceDraft, XeroOperation.UpdateInvoiceDraft, XeroOperation.DeleteInvoiceDraft];

    /// <inheritdoc />
    public async Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Document.Kind != XeroDocumentKind.Invoice || !Guid.TryParse(entry.Document.TempestKey, out var requestId))
            return new XeroPushResult(XeroPushOutcome.Rejected, $"{entry.Document.Kind} '{entry.Document.TempestKey}' is not an invoice request; the invoice handler does not send it.");

        if (!Operations.Contains(entry.Operation))
            return new XeroPushResult(XeroPushOutcome.Rejected, $"The invoice handler does not send {entry.Operation}.");

        if (_invoicing is not InvoicingService service)
            return new XeroPushResult(XeroPushOutcome.Blocked, "The invoicing service in this build has no Xero drafts seam.");

        var connected = await _drafts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (connected is null)
            return new XeroPushResult(XeroPushOutcome.Reauthorise, "No Xero organisation is connected; re-authorise to select one.");

        if (!string.Equals(connected, tenantId, StringComparison.Ordinal))
            return new XeroPushResult(XeroPushOutcome.Rejected, "This entry was queued for another Xero organisation than the one now connected; it is not sent here.");

        var found = await service.FindDraftDocumentAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (found is not { } current)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Invoice request '{requestId}' no longer exists.");

        return entry.Operation switch
        {
            XeroOperation.PushInvoiceDraft => await PushDraftAsync(service, current.Request, cancellationToken).ConfigureAwait(false),
            XeroOperation.UpdateInvoiceDraft => await UpdateAsync(current.Request, current.Document, cancellationToken).ConfigureAwait(false),
            _ => await DeleteAsync(current.Document, cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<XeroPushResult> PushDraftAsync(InvoicingService service, InvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted or InvoiceRequestStatus.Voided)
            return new XeroPushResult(XeroPushOutcome.NothingToDo);

        if (request.Status is InvoiceRequestStatus.Unknown)
        {
            // §6.4 item 3: look it up by its number before any resend.
            var reconciled = await service.ReconcileAsync(request.Id, cancellationToken).ConfigureAwait(false);
            request = reconciled.Request ?? request;
            if (request.Status == InvoiceRequestStatus.Sent)
                return new XeroPushResult(XeroPushOutcome.Succeeded);
            if (request.Status == InvoiceRequestStatus.Unknown)
                return new XeroPushResult(XeroPushOutcome.Unknown, request.LastError ?? "Xero could not confirm whether it holds this invoice yet.");
        }

        if (request.Status is not InvoiceRequestStatus.Draft)
            return new XeroPushResult(XeroPushOutcome.Rejected, $"Invoice request '{request.Id}' is {request.Status}; it is not sent again automatically. {request.LastError}".TrimEnd());

        var sent = await service.SendAsync(request.Id, queueWhenUnavailable: false, cancellationToken).ConfigureAwait(false);
        if (!sent.Succeeded)
            return new XeroPushResult(XeroPushOutcome.Blocked, sent.Reason);

        return sent.Request!.Status switch
        {
            InvoiceRequestStatus.Sent => new XeroPushResult(XeroPushOutcome.Succeeded),
            InvoiceRequestStatus.Draft => new XeroPushResult(XeroPushOutcome.RetryLater, sent.Request.LastError),
            InvoiceRequestStatus.Unknown => new XeroPushResult(XeroPushOutcome.Unknown, sent.Request.LastError),
            InvoiceRequestStatus.Reauthorise => new XeroPushResult(XeroPushOutcome.Reauthorise, sent.Request.LastError),
            _ => new XeroPushResult(XeroPushOutcome.Rejected, sent.Request.LastError ?? $"Invoice request '{request.Id}' is {sent.Request.Status}."),
        };
    }

    private async Task<XeroPushResult> UpdateAsync(InvoiceRequest request, InvoiceDraftDocument document, CancellationToken cancellationToken)
    {
        if (request.Status is not InvoiceRequestStatus.Sent)
            return new XeroPushResult(XeroPushOutcome.NothingToDo, $"Invoice request '{request.Id}' is {request.Status}; its Xero draft is not changed from TempestOS.");

        var change = await _drafts.UpdateDraftAsync(document, cancellationToken).ConfigureAwait(false);
        if (change.Outcome != ConnectorOutcome.Ok)
            return XeroInvoiceDrafts.ToPushResult(change.Outcome, change.Reason);

        var answer = change.Value!;
        return answer.Outcome switch
        {
            InvoiceDraftChangeOutcome.Applied => new XeroPushResult(XeroPushOutcome.Succeeded),
            InvoiceDraftChangeOutcome.NotDraft => new XeroPushResult(XeroPushOutcome.Rejected, $"Xero holds it as {answer.ExternalStatus}; change it in Xero."),
            _ => new XeroPushResult(XeroPushOutcome.Blocked, answer.Reason),
        };
    }

    private async Task<XeroPushResult> DeleteAsync(InvoiceDraftDocument document, CancellationToken cancellationToken)
    {
        var change = await _drafts.DeleteDraftAsync(document, cancellationToken).ConfigureAwait(false);
        if (change.Outcome != ConnectorOutcome.Ok)
            return XeroInvoiceDrafts.ToPushResult(change.Outcome, change.Reason);

        var answer = change.Value!;
        return answer.Outcome switch
        {
            InvoiceDraftChangeOutcome.Applied => new XeroPushResult(XeroPushOutcome.Succeeded),
            InvoiceDraftChangeOutcome.NotDraft when IsGone(answer.ExternalStatus) => new XeroPushResult(XeroPushOutcome.NothingToDo),
            InvoiceDraftChangeOutcome.NotDraft => new XeroPushResult(XeroPushOutcome.Rejected, $"Xero holds it as {answer.ExternalStatus}; void it in Xero — TempestOS reads it back."),
            _ => new XeroPushResult(XeroPushOutcome.Blocked, answer.Reason),
        };
    }

    private static bool IsGone(string? status) =>
        string.Equals(status, XeroConnector.DeletedStatus, StringComparison.OrdinalIgnoreCase)
        || (status?.Contains("VOID", StringComparison.OrdinalIgnoreCase) ?? false);
}

/// <summary>
/// Sends <see cref="XeroOperation.UploadAttachment"/> entries for invoices
/// (`v0.24.0` X4, design §3): the request's issued PDF onto its Xero
/// invoice, once per content hash, <c>IncludeOnline</c> per Q5. Entries for
/// other kinds of document are not this handler's — the engine (X6)
/// dispatches by <see cref="XeroDocumentRef.Kind"/> as well as operation.
/// </summary>
public sealed class XeroInvoiceAttachmentPushHandler : IXeroPushHandler
{
    private readonly XeroInvoiceDrafts _drafts;

    /// <summary>Initialises a new instance of the <see cref="XeroInvoiceAttachmentPushHandler"/> class.</summary>
    /// <param name="drafts">Xero's drafts seam (it holds the link and uploads).</param>
    public XeroInvoiceAttachmentPushHandler(XeroInvoiceDrafts drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        _drafts = drafts;
    }

    /// <summary>The kind of document this handler uploads for.</summary>
    public static XeroDocumentKind Kind => XeroDocumentKind.Invoice;

    /// <inheritdoc />
    public IReadOnlyCollection<XeroOperation> Operations { get; } = [XeroOperation.UploadAttachment];

    /// <inheritdoc />
    public Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Operation != XeroOperation.UploadAttachment || entry.Document.Kind != Kind || !Guid.TryParse(entry.Document.TempestKey, out var requestId))
        {
            return Task.FromResult(new XeroPushResult(
                XeroPushOutcome.Rejected, $"The invoice attachment handler does not send {entry.Operation} for {entry.Document.Kind} '{entry.Document.TempestKey}'."));
        }

        return _drafts.UploadAttachmentAsync(tenantId, requestId, cancellationToken);
    }
}
