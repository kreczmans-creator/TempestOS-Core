using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Settings;

namespace Tempest.Core.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// Xero's numbered-draft seam for <see cref="InvoicingService"/> (`v0.24.0`
/// X4, `ADR-0162` D3/D4; design §3, §4.2, §6.4): an invoice export creates
/// the <c>ACCREC</c> invoice in Xero as <c>DRAFT</c> with TempestOS's own
/// invoice number, the client by <c>ContactID</c> (X2), every line's tax
/// type and the sales account resolved against the X1 reading, the project
/// and deliverable in <c>Reference</c>, and the PDF attached
/// (<c>IncludeOnline</c> per Q5, off by default).
/// </summary>
/// <remarks>
/// <para>
/// <b>Never two invoices (§6.4).</b> A create is issued only when the
/// request has no link in the connected organisation; before it, Xero is
/// asked for the number (<c>GET Invoices?InvoiceNumbers=</c>): an invoice
/// that is TempestOS's own (same contact, same reference — a lost response)
/// is linked as <c>"reconciled"</c> and brought up to date while still a
/// draft; one that is not is refused (<i>"{number} is already used in
/// Xero"</i>), never duplicated. The create carries a fixed
/// <c>Idempotency-Key</c> embedding the body's hash.
/// </para>
/// <para>
/// <b>Drafts only.</b> Content is changed, and the invoice deleted, only
/// while Xero still holds it as a draft; otherwise the answer is
/// <see cref="InvoiceDraftChangeOutcome.NotDraft"/> with Xero's own status,
/// for <see cref="InvoicingService"/> to refuse with the reason. Nothing here
/// approves or emails anything; every request passes the Xero
/// <see cref="HttpClient"/>'s safety handler.
/// </para>
/// <para>
/// <b>Offline.</b> <see cref="FindBlockingReasonAsync"/> reads only local
/// state (the contact link, the cached X1 reading, Settings). A send Xero
/// could not reach is queued again as <see cref="XeroOperation.PushInvoiceDraft"/>
/// (<see cref="QueueSendAsync"/>); an attachment that could not be uploaded
/// as <see cref="XeroOperation.UploadAttachment"/>.
/// </para>
/// </remarks>
public sealed class XeroInvoiceDrafts : IInvoiceDraftSync
{
    /// <summary>The Settings key for Q5: whether the attached PDF is shown on Xero's online invoice to the client (default <c>false</c>).</summary>
    public const string IncludeOnlineSettingKey = "Xero.Invoice.AttachmentIncludeOnline";

    /// <summary><see cref="XeroLink.LinkedBy"/> for an invoice TempestOS created in Xero.</summary>
    public const string LinkedByCreated = "created";

    /// <summary><see cref="XeroLink.LinkedBy"/> for an invoice found by its number instead of created (a lost response).</summary>
    public const string LinkedByReconciled = "reconciled";

    /// <summary>Audit action (§6.7): TempestOS created the Xero draft and linked it.</summary>
    public const string AuditLinkCreated = "xero.link.created";

    /// <summary>Audit action: the Xero invoice was found by its number and linked instead of created.</summary>
    public const string AuditLinkReconciled = "xero.link.reconciled";

    /// <summary>Audit action: a draft update, delete or attachment upload Xero confirmed.</summary>
    public const string AuditPushSucceeded = "xero.push.succeeded";

    private readonly XeroConnector _connector;
    private readonly XeroContactLinker _contacts;
    private readonly XeroTaxTypeResolver _taxTypes;
    private readonly XeroAccountCodeMap _accounts;
    private readonly IXeroLinkStore _links;
    private readonly IXeroOutbox? _outbox;
    private readonly IXeroDocumentFileSource? _files;
    private readonly ISettingsProvider? _settings;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _definitionEnsured;

    /// <summary>Initialises a new instance of the <see cref="XeroInvoiceDrafts"/> class.</summary>
    /// <param name="connector">The Xero connector (shares the Xero <see cref="HttpClient"/> and its safety handler).</param>
    /// <param name="contacts">The X2 linker: the client's <c>ContactID</c>, or why it is not linked; the connected tenant.</param>
    /// <param name="taxTypes">The X1 tax-type resolver (output side).</param>
    /// <param name="accounts">The X1 account-code map (sales account).</param>
    /// <param name="links">Where the invoice's link to its Xero copy is kept.</param>
    /// <param name="outbox">Where an unreachable send or a failed upload is queued; <see langword="null"/> queues nothing.</param>
    /// <param name="files">Where the issued PDF is read from (X6); <see langword="null"/> attaches nothing until one is composed.</param>
    /// <param name="settings">Where Q5's <see cref="IncludeOnlineSettingKey"/> is read from; <see langword="null"/> reads the default (off).</param>
    /// <param name="audit">Records link and push audit rows; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock links are stamped with; <see langword="null"/> for the system clock.</param>
    public XeroInvoiceDrafts(
        XeroConnector connector,
        XeroContactLinker contacts,
        XeroTaxTypeResolver taxTypes,
        XeroAccountCodeMap accounts,
        IXeroLinkStore links,
        IXeroOutbox? outbox = null,
        IXeroDocumentFileSource? files = null,
        ISettingsProvider? settings = null,
        IAuditRecorder? audit = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(taxTypes);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(links);

        _connector = connector;
        _contacts = contacts;
        _taxTypes = taxTypes;
        _accounts = accounts;
        _links = links;
        _outbox = outbox;
        _files = files;
        _settings = settings;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string ConnectorName => _connector.Name;

    /// <inheritdoc />
    public string CreatedStatus => XeroConnector.DraftStatus;

    /// <summary>The link-store and outbox reference for the invoice request <paramref name="requestId"/>.</summary>
    /// <param name="requestId">The request's id.</param>
    public static XeroDocumentRef DocumentFor(Guid requestId) => XeroDocumentRef.For(XeroDocumentKind.Invoice, requestId);

    /// <summary>
    /// Registers Q5's <see cref="IncludeOnlineSettingKey"/> (default
    /// <c>false</c>) unless already registered — idempotent, so this class
    /// and the Settings UI may each call it.
    /// </summary>
    /// <param name="settings">The settings provider.</param>
    public static void EnsureDefinitions(ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            settings.RegisterDefinition(new SettingDefinition(
                IncludeOnlineSettingKey, "Show the attached invoice PDF to the client on Xero's online invoice", "false"));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Registered already.
        }
    }

    /// <inheritdoc />
    public async Task<string?> FindBlockingReasonAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        // No organisation connected: not a local gap this check can name —
        // the send itself answers "re-authorise".
        var tenantId = await _contacts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return null;

        var resolved = await ResolveAsync(tenantId, document, cancellationToken).ConfigureAwait(false);
        return resolved.BlockedReason;
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<CreatedInvoice>> CreateDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tenantId = await _contacts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return ConnectorResult<CreatedInvoice>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var reference = DocumentFor(document.RequestId);
            var existing = await _links.FindAsync(tenantId, reference, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                // Never a second create for a linked request (§6.4 item 1).
                return PersistenceXeroLinkStore.IsFromNewerVersion(existing)
                    ? ConnectorResult<CreatedInvoice>.Rejected($"This invoice's Xero link was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not use it.")
                    : ConnectorResult<CreatedInvoice>.Ok(new CreatedInvoice(existing.XeroId, existing.XeroNumber ?? document.InvoiceNumber, document.Reference));
            }

            var resolved = await ResolveAsync(tenantId, document, cancellationToken).ConfigureAwait(false);
            if (resolved.Draft is not { } draft)
                return ConnectorResult<CreatedInvoice>.Rejected(resolved.BlockedReason!);

            // Before a first create, the number (§6.4 item 4): TempestOS's own
            // invoice from a lost response is linked; anyone else's refuses.
            var found = await FindOwnAsync(tenantId, document, draft.ContactId, cancellationToken).ConfigureAwait(false);
            if (found.Outcome != ConnectorOutcome.Ok)
                return Retype<CreatedInvoice?, CreatedInvoice>(found);
            if (found.Value is { } reconciled)
            {
                await BringUpToDateAsync(tenantId, document, draft, cancellationToken).ConfigureAwait(false);
                await AttachIfReadyAsync(tenantId, document.RequestId, cancellationToken).ConfigureAwait(false);
                return ConnectorResult<CreatedInvoice>.Ok(reconciled);
            }

            var key = XeroIdempotencyKey.Create(reference, XeroOperation.PushInvoiceDraft, XeroInvoiceContent.BodyHash(draft));
            var created = await _connector.CreateDraftInvoiceAsync(draft, key, cancellationToken).ConfigureAwait(false);
            if (created.Outcome != ConnectorOutcome.Ok)
                return created;

            var invoice = created.Value!;
            var link = NewLink(tenantId, reference, invoice.ExternalId, invoice.ExternalInvoiceNumber ?? draft.InvoiceNumber, XeroInvoiceContent.ContentHash(document), LinkedByCreated);
            await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
            await AuditAsync(AuditLinkCreated, link, XeroOperation.PushInvoiceDraft, key, cancellationToken).ConfigureAwait(false);

            await AttachIfReadyAsync(tenantId, document.RequestId, cancellationToken).ConfigureAwait(false);
            return ConnectorResult<CreatedInvoice>.Ok(invoice);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<CreatedInvoice?>> FindByInvoiceNumberAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tenantId = await _contacts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return ConnectorResult<CreatedInvoice?>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await _links.FindAsync(tenantId, DocumentFor(document.RequestId), cancellationToken).ConfigureAwait(false);
            if (existing is not null && !PersistenceXeroLinkStore.IsFromNewerVersion(existing))
                return ConnectorResult<CreatedInvoice?>.Ok(new CreatedInvoice(existing.XeroId, existing.XeroNumber ?? document.InvoiceNumber, document.Reference));

            var contact = await _contacts.ResolveForPushAsync(tenantId, document.ClientReference, cancellationToken).ConfigureAwait(false);
            return await FindOwnAsync(tenantId, document, contact.ContactId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<InvoiceNumberFinding>> FindNumberHolderAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tenantId = await _contacts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return ConnectorResult<InvoiceNumberFinding>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await _links.FindAsync(tenantId, DocumentFor(document.RequestId), cancellationToken).ConfigureAwait(false);
            if (existing is not null && !PersistenceXeroLinkStore.IsFromNewerVersion(existing))
            {
                return ConnectorResult<InvoiceNumberFinding>.Ok(new InvoiceNumberFinding(
                    InvoiceNumberHolder.Own,
                    new CreatedInvoice(existing.XeroId, existing.XeroNumber ?? document.InvoiceNumber, document.Reference),
                    existing.LastKnownXeroStatus));
            }

            var contact = await _contacts.ResolveForPushAsync(tenantId, document.ClientReference, cancellationToken).ConfigureAwait(false);
            return await ClassifyNumberAsync(tenantId, document, contact.ContactId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<InvoiceDraftChange>> UpdateDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tenantId = await _contacts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return ConnectorResult<InvoiceDraftChange>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        var reference = DocumentFor(document.RequestId);
        var link = await _links.FindAsync(tenantId, reference, cancellationToken).ConfigureAwait(false);
        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return Blocked($"This invoice's Xero link was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        var invoiceId = link?.XeroId ?? document.ExternalId;
        if (invoiceId is null)
            return Blocked("This invoice is not in Xero yet; send it first.");

        var resolved = await ResolveAsync(tenantId, document, cancellationToken).ConfigureAwait(false);
        if (resolved.Draft is not { } draft)
            return Blocked(resolved.BlockedReason!);

        // The "from" content keeps A → B → A from replaying the first
        // update's cached answer (S7): each update's key names what it
        // changed from.
        var key = XeroIdempotencyKey.Create(
            reference, XeroOperation.UpdateInvoiceDraft, XeroInvoiceContent.BodyHash(draft), argument: link?.LastPushedContentHash ?? "unknown");
        var change = await _connector.UpdateDraftInvoiceAsync(invoiceId, draft, key, cancellationToken).ConfigureAwait(false);
        if (change.Outcome != ConnectorOutcome.Ok)
            return change;

        var answer = change.Value!;
        var now = _time.GetUtcNow();
        var updated = (link ?? NewLink(tenantId, reference, invoiceId, document.InvoiceNumber, null, XeroInvoiceLinkImporter.ImportedLinkedBy)) with
        {
            LastKnownXeroStatus = answer.ExternalStatus,
            LastReadAtUtc = now,
        };

        if (answer.Outcome == InvoiceDraftChangeOutcome.Applied)
        {
            updated = updated with { LastPushedContentHash = XeroInvoiceContent.ContentHash(document) };
            await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            await AuditAsync(AuditPushSucceeded, updated, XeroOperation.UpdateInvoiceDraft, key, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        }

        return change;
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<InvoiceDraftChange>> DeleteDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tenantId = await _contacts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return ConnectorResult<InvoiceDraftChange>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        var reference = DocumentFor(document.RequestId);
        var link = await _links.FindAsync(tenantId, reference, cancellationToken).ConfigureAwait(false);
        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return Blocked($"This invoice's Xero link was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        var invoiceId = link?.XeroId ?? document.ExternalId;
        if (invoiceId is null)
            return Blocked("This invoice is not in Xero; there is no draft to delete.");

        var key = XeroIdempotencyKey.Create(reference, XeroOperation.DeleteInvoiceDraft, XeroInvoiceContent.Sha256Hex(invoiceId));
        var change = await _connector.DeleteDraftInvoiceAsync(invoiceId, key, cancellationToken).ConfigureAwait(false);
        if (change.Outcome != ConnectorOutcome.Ok)
            return change;

        var answer = change.Value!;
        var updated = (link ?? NewLink(tenantId, reference, invoiceId, document.InvoiceNumber, null, XeroInvoiceLinkImporter.ImportedLinkedBy)) with
        {
            LastKnownXeroStatus = answer.ExternalStatus,
            LastReadAtUtc = _time.GetUtcNow(),
        };
        await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

        if (answer.Outcome == InvoiceDraftChangeOutcome.Applied)
            await AuditAsync(AuditPushSucceeded, updated, XeroOperation.DeleteInvoiceDraft, key, cancellationToken).ConfigureAwait(false);

        return change;
    }

    /// <inheritdoc />
    public async Task QueueSendAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_outbox is null)
            return;

        await _outbox.EnqueueAsync(
            XeroOperation.PushInvoiceDraft, DocumentFor(document.RequestId), XeroInvoiceContent.ContentHash(document),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecordStatusReadingAsync(InvoiceDraftDocument document, string externalStatus, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalStatus);

        var tenantId = await _contacts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return;

        var link = await _links.FindAsync(tenantId, DocumentFor(document.RequestId), cancellationToken).ConfigureAwait(false);
        if (link is null || PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return;

        await _links.SaveAsync(link with { LastKnownXeroStatus = externalStatus.Trim(), LastReadAtUtc = _time.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads the request's issued PDF to its Xero invoice (design §3:
    /// <c>PUT Invoices/{id}/Attachments/{file}?IncludeOnline=</c>, Q5), unless
    /// the same file (by SHA-256) is already there — the
    /// <see cref="XeroOperation.UploadAttachment"/> push for invoices. A new
    /// revision's PDF of the same name replaces it (<c>POST</c>).
    /// </summary>
    /// <param name="tenantId">The Xero organisation.</param>
    /// <param name="requestId">The invoice request.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    public async Task<XeroPushResult> UploadAttachmentAsync(string tenantId, Guid requestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var reference = DocumentFor(requestId);
        var link = await _links.FindAsync(tenantId, reference, cancellationToken).ConfigureAwait(false);
        if (link is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, "The invoice is not in Xero yet; its PDF is attached once it is.");

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return new XeroPushResult(XeroPushOutcome.Blocked, $"This invoice's Xero link was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        if (_files is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, "No source of issued PDFs is composed in this build.");

        var file = await _files.FindAsync(reference, cancellationToken).ConfigureAwait(false);
        if (file is null)
            return new XeroPushResult(XeroPushOutcome.Blocked, "The invoice has no issued PDF yet; it is attached once one is saved.");

        if (string.Equals(file.Sha256, link.AttachmentContentHash, StringComparison.OrdinalIgnoreCase))
            return new XeroPushResult(XeroPushOutcome.NothingToDo, Link: link);

        var replace = string.Equals(file.FileName, link.AttachmentFileName, StringComparison.OrdinalIgnoreCase);
        var key = XeroIdempotencyKey.Create(reference, XeroOperation.UploadAttachment, file.Sha256, argument: file.FileName);
        var includeOnline = await ReadIncludeOnlineAsync(cancellationToken).ConfigureAwait(false);

        var uploaded = await _connector.AttachInvoiceFileAsync(link.XeroId, file, replace, includeOnline, key, cancellationToken).ConfigureAwait(false);
        if (uploaded.Outcome != ConnectorOutcome.Ok)
            return ToPushResult(uploaded.Outcome, uploaded.Reason);

        var updated = link with { AttachmentFileName = file.FileName, AttachmentContentHash = file.Sha256 };
        await _links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        await AuditAsync(AuditPushSucceeded, updated, XeroOperation.UploadAttachment, key, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.Succeeded, Link: updated);
    }

    /// <summary>The connected Xero organisation's tenant id — local, no network.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public Task<string?> ReadTenantIdAsync(CancellationToken cancellationToken = default) => _contacts.ReadTenantIdAsync(cancellationToken);

    /// <summary>The <see cref="XeroPushResult"/> for a connector outcome other than Ok.</summary>
    /// <param name="outcome">The connector's outcome.</param>
    /// <param name="reason">Its reason.</param>
    internal static XeroPushResult ToPushResult(ConnectorOutcome outcome, string? reason) => outcome switch
    {
        ConnectorOutcome.Ok => new XeroPushResult(XeroPushOutcome.Succeeded),
        ConnectorOutcome.Rejected => new XeroPushResult(XeroPushOutcome.Rejected, reason ?? "Xero rejected the request."),
        ConnectorOutcome.Reauthorise => new XeroPushResult(XeroPushOutcome.Reauthorise, reason ?? "Xero needs re-authorising."),
        ConnectorOutcome.Unavailable => new XeroPushResult(XeroPushOutcome.RetryLater, reason ?? "Xero could not be reached."),
        _ => new XeroPushResult(XeroPushOutcome.Unknown, reason ?? "Xero's answer was lost."),
    };

    /// <summary>
    /// The invoice Xero holds under <paramref name="document"/>'s number when
    /// it is TempestOS's own (same contact and reference; linked as
    /// reconciled), <see langword="null"/> when none holds it, or Rejected
    /// when another invoice does — or one carrying its reference under
    /// another contact (never linked, never re-created over).
    /// </summary>
    private async Task<ConnectorResult<CreatedInvoice?>> FindOwnAsync(
        string tenantId, InvoiceDraftDocument document, string? contactId, CancellationToken cancellationToken)
    {
        var found = await ClassifyNumberAsync(tenantId, document, contactId, cancellationToken).ConfigureAwait(false);
        if (found.Outcome != ConnectorOutcome.Ok)
            return Retype<InvoiceNumberFinding, CreatedInvoice?>(found);

        var finding = found.Value!;
        return finding.Holder switch
        {
            InvoiceNumberHolder.Nobody => ConnectorResult<CreatedInvoice?>.Ok(null),
            InvoiceNumberHolder.Own => ConnectorResult<CreatedInvoice?>.Ok(finding.Invoice),
            InvoiceNumberHolder.OwnReferenceOtherContact => ConnectorResult<CreatedInvoice?>.Rejected(
                $"Xero holds {document.InvoiceNumber} with this invoice's reference under another contact ({finding.ExternalStatus}); TempestOS will not create a second — check Xero."),
            _ => ConnectorResult<CreatedInvoice?>.Rejected(
                $"{document.InvoiceNumber} is already used in Xero by another invoice ({finding.ExternalStatus}); TempestOS will not create a second — check Xero."),
        };
    }

    /// <summary>
    /// Who holds <paramref name="document"/>'s number in Xero: TempestOS's
    /// own invoice (its reference under <paramref name="contactId"/>, or any
    /// contact when that is <see langword="null"/> — linked as reconciled),
    /// one with its reference under another contact, another invoice, or
    /// nobody. A look-up Xero did not answer is returned as it came.
    /// </summary>
    private async Task<ConnectorResult<InvoiceNumberFinding>> ClassifyNumberAsync(
        string tenantId, InvoiceDraftDocument document, string? contactId, CancellationToken cancellationToken)
    {
        var found = await _connector.FindSalesInvoicesByNumberAsync(document.InvoiceNumber, cancellationToken).ConfigureAwait(false);
        if (found.Outcome != ConnectorOutcome.Ok)
            return Retype<IReadOnlyList<XeroInvoiceReading>, InvoiceNumberFinding>(found);

        if (found.Value!.Count == 0)
            return ConnectorResult<InvoiceNumberFinding>.Ok(new InvoiceNumberFinding(InvoiceNumberHolder.Nobody));

        var withReference = found.Value
            .Where(i => string.Equals(i.Reference?.Trim(), document.Reference.Trim(), StringComparison.Ordinal))
            .ToList();
        var own = withReference.FirstOrDefault(i => contactId is null || string.Equals(i.ContactId, contactId, StringComparison.OrdinalIgnoreCase));

        if (own is null)
        {
            return withReference.Count > 0
                // The status of one still live there when any is, so a caller can tell "every one voided or deleted" apart.
                ? ConnectorResult<InvoiceNumberFinding>.Ok(new InvoiceNumberFinding(
                    InvoiceNumberHolder.OwnReferenceOtherContact, null, (withReference.FirstOrDefault(i => !IsGone(i.Status)) ?? withReference[0]).Status))
                : ConnectorResult<InvoiceNumberFinding>.Ok(new InvoiceNumberFinding(InvoiceNumberHolder.AnotherInvoice, null, found.Value[0].Status));
        }

        var link = NewLink(tenantId, DocumentFor(document.RequestId), own.InvoiceId, own.InvoiceNumber ?? document.InvoiceNumber, null, LinkedByReconciled)
            with { LastKnownXeroStatus = own.Status };
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        await AuditAsync(AuditLinkReconciled, link, XeroOperation.PushInvoiceDraft, null, cancellationToken).ConfigureAwait(false);

        var invoice = new CreatedInvoice(own.InvoiceId, own.InvoiceNumber ?? document.InvoiceNumber, own.Reference ?? document.Reference);
        return ConnectorResult<InvoiceNumberFinding>.Ok(new InvoiceNumberFinding(InvoiceNumberHolder.Own, invoice, own.Status));
    }

    /// <summary>Whether Xero's <paramref name="status"/> is one an invoice never comes back from (<c>VOIDED</c>, <c>DELETED</c>).</summary>
    private static bool IsGone(string? status) =>
        string.Equals(status, "VOIDED", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "DELETED", StringComparison.OrdinalIgnoreCase);

    /// <summary>After reconciling a lost create: the draft brought to the request's current content (while still a draft); a failure is left for the planner's <see cref="XeroOperation.UpdateInvoiceDraft"/>.</summary>
    private async Task BringUpToDateAsync(string tenantId, InvoiceDraftDocument document, XeroSalesInvoiceDraft draft, CancellationToken cancellationToken)
    {
        var reference = DocumentFor(document.RequestId);
        var link = await _links.FindAsync(tenantId, reference, cancellationToken).ConfigureAwait(false);
        if (link is null || !XeroConnector.IsDraft(link.LastKnownXeroStatus))
            return;

        var key = XeroIdempotencyKey.Create(reference, XeroOperation.UpdateInvoiceDraft, XeroInvoiceContent.BodyHash(draft), argument: "reconciled");
        var change = await _connector.UpdateDraftInvoiceAsync(link.XeroId, draft, key, cancellationToken).ConfigureAwait(false);
        if (change.Outcome == ConnectorOutcome.Ok && change.Value!.Outcome == InvoiceDraftChangeOutcome.Applied)
        {
            await _links.SaveAsync(
                link with { LastPushedContentHash = XeroInvoiceContent.ContentHash(document), LastKnownXeroStatus = change.Value.ExternalStatus },
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Right after a create: the PDF uploaded at once when it exists; when the upload fails it is queued, and when there is no PDF yet the planner queues it once one appears.</summary>
    private async Task AttachIfReadyAsync(string tenantId, Guid requestId, CancellationToken cancellationToken)
    {
        if (_files is null)
            return;

        var file = await _files.FindAsync(DocumentFor(requestId), cancellationToken).ConfigureAwait(false);
        if (file is null)
            return;

        var result = await UploadAttachmentAsync(tenantId, requestId, cancellationToken).ConfigureAwait(false);
        if (result.Outcome is XeroPushOutcome.Succeeded or XeroPushOutcome.NothingToDo || _outbox is null)
            return;

        await _outbox.EnqueueAsync(XeroOperation.UploadAttachment, DocumentFor(requestId), file.Sha256, file.FileName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The resolved Xero draft for <paramref name="document"/> — contact by <c>ContactID</c>, every line's tax type, the sales account — or the first reason it cannot be built. Local only.</summary>
    private async Task<(XeroSalesInvoiceDraft? Draft, string? BlockedReason)> ResolveAsync(
        string tenantId, InvoiceDraftDocument document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.ClientReference))
            return (null, $"client organisation '{document.ClientOrganisationId}' is not in the catalogue, so it has no Xero contact.");

        var contact = await _contacts.ResolveForPushAsync(tenantId, document.ClientReference, cancellationToken).ConfigureAwait(false);
        if (!contact.IsLinked)
            return (null, contact.BlockedReason);

        if (document.Lines.Count == 0)
            return (null, "the invoice has no lines.");

        var account = await _accounts.ResolveSalesAsync(cancellationToken).ConfigureAwait(false);
        if (account.IsBlocked)
            return (null, account.BlockedReason);

        var lines = new List<XeroSalesInvoiceDraftLine>(document.Lines.Count);
        foreach (var line in document.Lines)
        {
            var taxType = await _taxTypes.ResolveAsync(line.VatRate, VatTaxDirection.Sales, cancellationToken).ConfigureAwait(false);
            if (taxType.IsBlocked)
                return (null, $"line '{line.Description}': {taxType.BlockedReason}");

            lines.Add(new XeroSalesInvoiceDraftLine(line.Description, line.Quantity, line.UnitRate.Amount, taxType.Code!, account.Code!));
        }

        return (new XeroSalesInvoiceDraft(
            contact.ContactId!, document.InvoiceNumber, document.Reference, document.Date, document.DueDate, document.Currency.ToString(), lines), null);
    }

    private async Task<bool> ReadIncludeOnlineAsync(CancellationToken cancellationToken)
    {
        if (_settings is null)
            return false;

        if (Interlocked.Exchange(ref _definitionEnsured, 1) == 0)
            EnsureDefinitions(_settings);

        try
        {
            var value = await _settings.GetValueAsync(IncludeOnlineSettingKey, cancellationToken).ConfigureAwait(false);
            return bool.TryParse(value, out var include) && include;
        }
        catch (SettingNotFoundException)
        {
            return false;
        }
    }

    private XeroLink NewLink(string tenantId, XeroDocumentRef document, string invoiceId, string? number, string? contentHash, string linkedBy)
    {
        var now = _time.GetUtcNow();
        return new XeroLink(
            XeroLink.CurrentSchemaVersion,
            tenantId,
            document,
            invoiceId.Trim(),
            number,
            contentHash,
            LastKnownXeroStatus: XeroConnector.DraftStatus,
            AttachmentFileName: null,
            AttachmentContentHash: null,
            LinkedAtUtc: now,
            LastReadAtUtc: now,
            LinkedBy: linkedBy);
    }

    private async Task AuditAsync(string action, XeroLink link, XeroOperation operation, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;

        var detail = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["document"] = $"{link.Document.Kind}:{link.Document.TempestKey}",
            ["operation"] = operation.ToString(),
            ["xeroId"] = link.XeroId,
            ["tenant"] = link.TenantId,
        };
        if (link.XeroNumber is { } number)
            detail["xeroNumber"] = number;
        if (idempotencyKey is not null)
            detail["idempotencyKey"] = idempotencyKey;

        await _audit.RecordAsync(action, detail, cancellationToken).ConfigureAwait(false);
    }

    private static ConnectorResult<InvoiceDraftChange> Blocked(string reason) =>
        ConnectorResult<InvoiceDraftChange>.Ok(new InvoiceDraftChange(InvoiceDraftChangeOutcome.Blocked, Reason: reason));

    private static ConnectorResult<TTo> Retype<TFrom, TTo>(ConnectorResult<TFrom> result) => result.Outcome switch
    {
        ConnectorOutcome.Rejected => ConnectorResult<TTo>.Rejected(result.Reason ?? "Xero rejected the request."),
        ConnectorOutcome.Reauthorise => ConnectorResult<TTo>.Reauthorise(result.Reason),
        ConnectorOutcome.Unavailable => ConnectorResult<TTo>.Unavailable(result.Reason),
        _ => ConnectorResult<TTo>.Unknown(result.Reason),
    };
}

/// <summary>
/// Answers <see cref="IInvoiceDraftSync"/> with the one registered
/// <see cref="XeroInvoiceDrafts"/> instance, so <see cref="InvoicingService"/>
/// and the X4 push handlers share its write gate (`ADR-0122`: each service
/// type registered once).
/// </summary>
public sealed class XeroInvoiceDraftsForwarder : IInvoiceDraftSync
{
    private readonly XeroInvoiceDrafts _drafts;

    /// <summary>Initialises a new instance of the <see cref="XeroInvoiceDraftsForwarder"/> class.</summary>
    /// <param name="drafts">The registered drafts seam.</param>
    public XeroInvoiceDraftsForwarder(XeroInvoiceDrafts drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        _drafts = drafts;
    }

    /// <inheritdoc />
    public string ConnectorName => _drafts.ConnectorName;

    /// <inheritdoc />
    public string CreatedStatus => _drafts.CreatedStatus;

    /// <inheritdoc />
    public Task<string?> FindBlockingReasonAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) =>
        _drafts.FindBlockingReasonAsync(document, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<CreatedInvoice>> CreateDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) =>
        _drafts.CreateDraftAsync(document, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<CreatedInvoice?>> FindByInvoiceNumberAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) =>
        _drafts.FindByInvoiceNumberAsync(document, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<InvoiceNumberFinding>> FindNumberHolderAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) =>
        _drafts.FindNumberHolderAsync(document, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<InvoiceDraftChange>> UpdateDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) =>
        _drafts.UpdateDraftAsync(document, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<InvoiceDraftChange>> DeleteDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) =>
        _drafts.DeleteDraftAsync(document, cancellationToken);

    /// <inheritdoc />
    public Task QueueSendAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) =>
        _drafts.QueueSendAsync(document, cancellationToken);

    /// <inheritdoc />
    public Task RecordStatusReadingAsync(InvoiceDraftDocument document, string externalStatus, CancellationToken cancellationToken = default) =>
        _drafts.RecordStatusReadingAsync(document, externalStatus, cancellationToken);
}
