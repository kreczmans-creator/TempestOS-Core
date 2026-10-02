using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;

namespace Tempest.Core.Invoicing.Xero;

/// <summary>
/// The real Xero <see cref="IInvoicingConnector"/>, over <c>HttpClient</c>
/// and <see cref="OAuthAuthoriser"/> (`WP 19.1A` part 2, brief §2). Every
/// call is relative to <paramref name="httpClient"/>'s own
/// <see cref="HttpClient.BaseAddress"/> — <c>https://api.xero.com/api.xro/2.0/</c>
/// in production, a recorded-response stub in tests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contacts, by <c>ContactID</c> only (`v0.24.0` X4; X2, review item
/// M21).</b> Until v0.24.0 an invoice's inline <c>Contact</c> carried the
/// client's name for Xero to match or silently create. It no longer does:
/// a sales invoice is created only from a <see cref="XeroSalesInvoiceDraft"/>
/// — the contact already linked by <c>ContactID</c>, every line's tax type
/// and account code already checked against Xero's own (X1) — through
/// <see cref="CreateDraftInvoiceAsync(XeroSalesInvoiceDraft, string, CancellationToken)"/>,
/// which <c>Tempest.Core.Invoicing.Xero.Sync.Invoices.XeroInvoiceDrafts</c>
/// drives for <see cref="InvoicingService"/>. The `WP 19.1A` snapshot
/// overload, which knows only the client's name, therefore refuses.
/// </para>
/// <para>
/// <b>Drafts only (D3, D4).</b> The invoice is created <c>DRAFT</c> with
/// TempestOS's own number; its content is changed only while Xero still
/// holds it as <c>DRAFT</c>, and only TempestOS's own draft is ever deleted.
/// No member approves, emails or marks anything sent; every request passes
/// <see cref="Api.XeroWriteSafetyHandler"/> in the shared Xero
/// <see cref="HttpClient"/>, which refuses such a request again.
/// </para>
/// </remarks>
public sealed class XeroConnector : IInvoicingConnector, IAccountsConnector, IAuthorisableConnector
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming the currency every <see cref="IAccountsConnector.ReadCashPositionAsync"/> balance is reported in — Xero's own Bank Summary report states each balance in the organisation's base currency without naming it in the grid itself (`XeroConnector.ReadCashPositionAsync`'s own remarks).</summary>
    public const string BaseCurrencyConfigurationKey = "Invoicing:Xero:BaseCurrency";

    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly HttpClient _httpClient;
    private readonly OAuthAuthoriser _authoriser;
    private readonly IConfigurationProvider? _configuration;
    private readonly XeroAccountingApi _api;

    /// <summary>Initialises a new instance of the <see cref="XeroConnector"/> class.</summary>
    /// <param name="configuration">Where <see cref="BaseCurrencyConfigurationKey"/> is read from. <see langword="null"/> is honoured — <see cref="IAccountsConnector.ReadCashPositionAsync"/> then falls back to GBP, this platform's own fixture currency throughout.</param>
    public XeroConnector(HttpClient httpClient, OAuthAuthoriser authoriser, IConfigurationProvider? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authoriser);

        _httpClient = httpClient;
        _authoriser = authoriser;
        _configuration = configuration;
        _api = new XeroAccountingApi(httpClient, authoriser);
    }

    /// <inheritdoc />
    public string Name => "Xero";

    /// <inheritdoc />
    /// <remarks>
    /// `v0.24.0` X4: <see cref="ConnectorOutcome.Reauthorise"/> when Xero is
    /// not configured, authorised or connected (as before); otherwise always
    /// <see cref="ConnectorOutcome.Rejected"/>, with no call to Xero. The snapshot knows the client only by name, and a Xero
    /// invoice names its contact by <c>ContactID</c> (X2, M21) — never a name
    /// for Xero to match or create. A Xero invoice is created by
    /// <see cref="CreateDraftInvoiceAsync(XeroSalesInvoiceDraft, string, CancellationToken)"/>,
    /// which <see cref="InvoicingService"/> reaches through its
    /// <see cref="IInvoiceDraftSync"/> seam.
    /// </remarks>
    public async Task<ConnectorResult<CreatedInvoice>> CreateDraftInvoiceAsync(
        InvoiceRequestSnapshot request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        // The authorisation answer comes first, exactly as before: an
        // unconfigured or unauthorised connector still says "re-authorise".
        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (access.Outcome != AccessTokenOutcome.Ok)
            return MapAccessFailure<CreatedInvoice>(access);

        if (string.IsNullOrEmpty(access.TenantId))
            return ConnectorResult<CreatedInvoice>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        return ConnectorResult<CreatedInvoice>.Rejected(
            $"A Xero invoice names its client by the linked Xero contact (ContactID), never by name; client organisation '{request.ClientOrganisationId}' "
            + "has to go through TempestOS's Xero invoice export (link it under Customers & suppliers first).");
    }

    /// <summary>
    /// Creates <paramref name="draft"/> in Xero as an <c>ACCREC</c> invoice
    /// with status <c>DRAFT</c> (<c>PUT Invoices</c>, `v0.24.0` X4, D3): the
    /// contact by <c>ContactID</c>, TempestOS's own <c>InvoiceNumber</c>, the
    /// reference text, dates, currency and net lines with their tax types and
    /// account codes. Nothing is sent to the client (D4).
    /// </summary>
    /// <param name="draft">The resolved invoice.</param>
    /// <param name="idempotencyKey">The fixed <c>Idempotency-Key</c> for this body (at most 128 characters); a repeat replays Xero's own answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The created invoice: Xero's <c>InvoiceID</c>, its number and the reference it carries.</returns>
    public async Task<ConnectorResult<CreatedInvoice>> CreateDraftInvoiceAsync(
        XeroSalesInvoiceDraft draft, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (FindDraftProblem(draft) is { } problem)
            return ConnectorResult<CreatedInvoice>.Rejected(problem);

        var created = await _api.CreateSalesInvoiceDraftAsync(ToWrite(draft), idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (created.Outcome != ConnectorOutcome.Ok)
            return ToConnectorResult<XeroWireInvoice, CreatedInvoice>(created);

        var invoice = created.Value!;
        return ConnectorResult<CreatedInvoice>.Ok(new CreatedInvoice(invoice.InvoiceID!, invoice.InvoiceNumber ?? draft.InvoiceNumber, invoice.Reference ?? draft.Reference));
    }

    /// <summary>
    /// The sales invoices Xero holds under <paramref name="invoiceNumber"/>
    /// (<c>GET Invoices?InvoiceNumbers=</c>, `v0.24.0` X4, design §6.4) —
    /// deleted ones and bills left out, since neither holds a sales number.
    /// </summary>
    /// <param name="invoiceNumber">TempestOS's invoice number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<ConnectorResult<IReadOnlyList<XeroInvoiceReading>>> FindSalesInvoicesByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);

        var found = await _api.FindInvoicesByNumberAsync(invoiceNumber, cancellationToken).ConfigureAwait(false);
        if (found.Outcome != ConnectorOutcome.Ok)
            return ToConnectorResult<IReadOnlyList<XeroWireInvoice>, IReadOnlyList<XeroInvoiceReading>>(found);

        IReadOnlyList<XeroInvoiceReading> readings = [.. found.Value!
            .Where(i => string.Equals(i.Type ?? XeroWire.InvoiceTypeSales, XeroWire.InvoiceTypeSales, StringComparison.OrdinalIgnoreCase))
            .Where(i => string.Equals(i.InvoiceNumber, invoiceNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(ToReading)
            .Where(r => !string.Equals(r.Status, DeletedStatus, StringComparison.OrdinalIgnoreCase))];

        return ConnectorResult<IReadOnlyList<XeroInvoiceReading>>.Ok(readings);
    }

    /// <summary>Reads the invoice <paramref name="invoiceId"/> back from Xero (<c>GET Invoices/{InvoiceID}</c>); one Xero no longer has is <see cref="ConnectorOutcome.Rejected"/>.</summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<ConnectorResult<XeroInvoiceReading>> ReadInvoiceAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);

        var read = await _api.GetInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        return read.Outcome == ConnectorOutcome.Ok
            ? ConnectorResult<XeroInvoiceReading>.Ok(ToReading(read.Value!))
            : ToConnectorResult<XeroWireInvoice, XeroInvoiceReading>(read);
    }

    /// <summary>
    /// Changes the content of TempestOS's draft <paramref name="invoiceId"/>
    /// to <paramref name="draft"/> (`v0.24.0` X4) — only while Xero still
    /// holds it as <c>DRAFT</c>: it is read first, and any other status is
    /// answered <see cref="InvoiceDraftChangeOutcome.NotDraft"/> with Xero's
    /// status word, and nothing is written. Never changes its status.
    /// </summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c>.</param>
    /// <param name="draft">The new content.</param>
    /// <param name="idempotencyKey">The fixed <c>Idempotency-Key</c> for this update.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    public async Task<ConnectorResult<InvoiceDraftChange>> UpdateDraftInvoiceAsync(
        string invoiceId, XeroSalesInvoiceDraft draft, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (FindDraftProblem(draft) is { } problem)
            return ConnectorResult<InvoiceDraftChange>.Rejected(problem);

        var current = await ReadInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (current.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroInvoiceReading, InvoiceDraftChange>(current);

        if (!IsDraft(current.Value!.Status))
            return ConnectorResult<InvoiceDraftChange>.Ok(new InvoiceDraftChange(InvoiceDraftChangeOutcome.NotDraft, current.Value.Status));

        var updated = await _api.UpdateInvoiceContentAsync(invoiceId, ToWrite(draft), idempotencyKey, cancellationToken).ConfigureAwait(false);
        return updated.Outcome == ConnectorOutcome.Ok
            ? ConnectorResult<InvoiceDraftChange>.Ok(new InvoiceDraftChange(InvoiceDraftChangeOutcome.Applied, updated.Value!.Status ?? DraftStatus))
            : ToConnectorResult<XeroWireInvoice, InvoiceDraftChange>(updated);
    }

    /// <summary>
    /// Deletes TempestOS's draft <paramref name="invoiceId"/> (`v0.24.0` X4,
    /// design §4.2: a voided TempestOS invoice deletes its Xero draft only) —
    /// read first; deleted only while Xero holds it as a draft
    /// (<c>DRAFT</c>, or <c>SUBMITTED</c> awaiting approval). Any other
    /// status is answered <see cref="InvoiceDraftChangeOutcome.NotDraft"/>
    /// with Xero's status word, and nothing is written.
    /// </summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c>.</param>
    /// <param name="idempotencyKey">The fixed <c>Idempotency-Key</c> for this delete.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    public async Task<ConnectorResult<InvoiceDraftChange>> DeleteDraftInvoiceAsync(
        string invoiceId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var current = await ReadInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (current.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroInvoiceReading, InvoiceDraftChange>(current);

        if (!IsDraft(current.Value!.Status) && !IsAwaitingApproval(current.Value.Status))
            return ConnectorResult<InvoiceDraftChange>.Ok(new InvoiceDraftChange(InvoiceDraftChangeOutcome.NotDraft, current.Value.Status));

        var deleted = await _api.DeleteInvoiceDraftAsync(invoiceId, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return deleted.Outcome == ConnectorOutcome.Ok
            ? ConnectorResult<InvoiceDraftChange>.Ok(new InvoiceDraftChange(InvoiceDraftChangeOutcome.Applied, deleted.Value!.Status ?? DeletedStatus))
            : ToConnectorResult<XeroWireInvoice, InvoiceDraftChange>(deleted);
    }

    /// <summary>
    /// Attaches TempestOS's PDF to the invoice <paramref name="invoiceId"/>
    /// (<c>PUT Invoices/{id}/Attachments/{FileName}</c>; <c>POST</c> to
    /// replace a file of the same name). <paramref name="includeOnline"/> is
    /// Q5's choice — off by default, so the client does not see it on Xero's
    /// online invoice.
    /// </summary>
    /// <param name="invoiceId">Xero's <c>InvoiceID</c>.</param>
    /// <param name="file">The PDF.</param>
    /// <param name="replaceExisting">Whether a file of the same name was uploaded before.</param>
    /// <param name="includeOnline">Whether the client sees it on Xero's online invoice (Q5).</param>
    /// <param name="idempotencyKey">The fixed <c>Idempotency-Key</c> for this upload.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<ConnectorResult<XeroWireAttachment>> AttachInvoiceFileAsync(
        string invoiceId, XeroDocumentFile file, bool replaceExisting, bool includeOnline, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceId);
        ArgumentNullException.ThrowIfNull(file);

        var uploaded = await _api.UploadAttachmentAsync(
            XeroAttachableResource.Invoices, invoiceId, file, idempotencyKey, replaceExisting, includeOnline, cancellationToken).ConfigureAwait(false);
        return uploaded.Outcome == ConnectorOutcome.Ok
            ? ConnectorResult<XeroWireAttachment>.Ok(uploaded.Value!)
            : ToConnectorResult<XeroWireAttachment, XeroWireAttachment>(uploaded);
    }

    /// <summary>Xero's status word for a draft invoice — the only status TempestOS creates (D3).</summary>
    public const string DraftStatus = "DRAFT";

    /// <summary>Xero's status word for a deleted draft — the only other status TempestOS writes.</summary>
    public const string DeletedStatus = "DELETED";

    /// <summary>Whether Xero's <paramref name="status"/> is <see cref="DraftStatus"/>.</summary>
    /// <param name="status">Xero's status word.</param>
    public static bool IsDraft(string? status) => string.Equals(status?.Trim(), DraftStatus, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether Xero's <paramref name="status"/> is its submitted-for-approval word (a draft awaiting approval in Xero) — read side only.</summary>
    /// <param name="status">Xero's status word.</param>
    public static bool IsAwaitingApproval(string? status) =>
        status is not null && status.Trim().StartsWith("SUBMIT", StringComparison.OrdinalIgnoreCase);

    private static string? FindDraftProblem(XeroSalesInvoiceDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.ContactId))
            return "The invoice has no Xero contact (ContactID); link the client under Customers & suppliers.";

        if (string.IsNullOrWhiteSpace(draft.InvoiceNumber))
            return "The invoice has no number.";

        if (draft.Lines.Count == 0)
            return "The invoice has no lines.";

        if (draft.Lines.FirstOrDefault(l => string.IsNullOrWhiteSpace(l.Description)) is not null)
            return "Every invoice line needs a description.";

        return draft.Lines.FirstOrDefault(l => string.IsNullOrWhiteSpace(l.TaxType) || string.IsNullOrWhiteSpace(l.AccountCode)) is { } line
            ? $"Line '{line.Description}' has no Xero tax type or account code."
            : null;
    }

    private static XeroWireInvoiceWrite ToWrite(XeroSalesInvoiceDraft draft) => new(
        Contact: new XeroWireContactRef(draft.ContactId.Trim()),
        InvoiceNumber: draft.InvoiceNumber.Trim(),
        Reference: draft.Reference,
        Date: XeroWire.FormatDate(draft.Date),
        DueDate: XeroWire.FormatDate(draft.DueDate),
        CurrencyCode: draft.CurrencyCode,
        LineAmountTypes: XeroWire.LineAmountTypesExclusive,
        LineItems: [.. draft.Lines.Select(l => new XeroWireLineItem(l.Description, l.Quantity, l.UnitAmount, l.AccountCode, l.TaxType))]);

    private static XeroInvoiceReading ToReading(XeroWireInvoice invoice) => new(
        invoice.InvoiceID!,
        invoice.InvoiceNumber,
        string.IsNullOrWhiteSpace(invoice.Status) ? "UNKNOWN" : invoice.Status.Trim(),
        invoice.Reference,
        invoice.Contact?.ContactID,
        invoice.Type,
        XeroWire.ParseDate(invoice.Date),
        XeroWire.ParseDate(invoice.FullyPaidOnDate));

    private static ConnectorResult<TTo> ToConnectorResult<TFrom, TTo>(XeroApiResult<TFrom> result) => result.Outcome switch
    {
        ConnectorOutcome.Rejected => ConnectorResult<TTo>.Rejected(string.IsNullOrWhiteSpace(result.Reason) ? "Xero rejected the request." : result.Reason),
        ConnectorOutcome.Reauthorise => ConnectorResult<TTo>.Reauthorise(result.Reason),
        ConnectorOutcome.Unavailable => ConnectorResult<TTo>.Unavailable(result.Reason),
        _ => ConnectorResult<TTo>.Unknown(result.Reason),
    };

    private static ConnectorResult<TTo> Retype<TFrom, TTo>(ConnectorResult<TFrom> result) => result.Outcome switch
    {
        ConnectorOutcome.Rejected => ConnectorResult<TTo>.Rejected(result.Reason ?? "Xero rejected the request."),
        ConnectorOutcome.Reauthorise => ConnectorResult<TTo>.Reauthorise(result.Reason),
        ConnectorOutcome.Unavailable => ConnectorResult<TTo>.Unavailable(result.Reason),
        _ => ConnectorResult<TTo>.Unknown(result.Reason),
    };

    /// <inheritdoc />
    public async Task<ConnectorResult<InvoiceStatusReading>> ReadStatusAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (access.Outcome != AccessTokenOutcome.Ok)
            return MapAccessFailure<InvoiceStatusReading>(access);

        if (string.IsNullOrEmpty(access.TenantId))
            return ConnectorResult<InvoiceStatusReading>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"Invoices/{Uri.EscapeDataString(externalId)}");
        ApplyAuthHeaders(httpRequest, access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<InvoiceStatusReading>(outcome, response, body);

            var invoice = TryParseInvoicesEnvelope(body)?.Invoices?.FirstOrDefault();
            if (invoice is null)
                return ConnectorResult<InvoiceStatusReading>.Unknown("Xero accepted the call but returned no invoice.");

            return ConnectorResult<InvoiceStatusReading>.Ok(new InvoiceStatusReading(
                invoice.Status ?? "UNKNOWN", invoice.InvoiceNumber, ParseXeroDate(invoice.Date), ParseXeroDate(invoice.FullyPaidOnDate)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return ConnectorResult<InvoiceStatusReading>.Unavailable(ConnectorHttpOutcome.DescribeTransportFailure(ex));
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<CreatedInvoice?>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (access.Outcome != AccessTokenOutcome.Ok)
            return MapAccessFailure<CreatedInvoice?>(access);

        if (string.IsNullOrEmpty(access.TenantId))
            return ConnectorResult<CreatedInvoice?>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        var where = Uri.EscapeDataString($"Reference==\"{reference}\"");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"Invoices?where={where}");
        ApplyAuthHeaders(httpRequest, access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<CreatedInvoice?>(outcome, response, body);

            var envelope = TryParseInvoicesEnvelope(body);
            var invoice = envelope?.Invoices?.FirstOrDefault();

            return ConnectorResult<CreatedInvoice?>.Ok(
                invoice is null ? null : new CreatedInvoice(invoice.InvoiceID ?? string.Empty, invoice.InvoiceNumber, invoice.Reference ?? reference));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return ConnectorResult<CreatedInvoice?>.Unavailable(ConnectorHttpOutcome.DescribeTransportFailure(ex));
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<IReadOnlyList<ConnectorContact>>> ListContactsAsync(CancellationToken cancellationToken = default)
    {
        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (access.Outcome != AccessTokenOutcome.Ok)
            return MapAccessFailure<IReadOnlyList<ConnectorContact>>(access);

        if (string.IsNullOrEmpty(access.TenantId))
            return ConnectorResult<IReadOnlyList<ConnectorContact>>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "Contacts");
        ApplyAuthHeaders(httpRequest, access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<IReadOnlyList<ConnectorContact>>(outcome, response, body);

            IReadOnlyList<ConnectorContact> contacts;
            try
            {
                var envelope = JsonSerializer.Deserialize<XeroContactsEnvelope>(body, JsonOptions);
                contacts = [.. (envelope?.Contacts ?? []).Select(c => new ConnectorContact(c.ContactID ?? string.Empty, c.Name ?? string.Empty))];
            }
            catch (JsonException)
            {
                return ConnectorResult<IReadOnlyList<ConnectorContact>>.Unknown("Xero's own response could not be read.");
            }

            return ConnectorResult<IReadOnlyList<ConnectorContact>>.Ok(contacts);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return ConnectorResult<IReadOnlyList<ConnectorContact>>.Unavailable(ConnectorHttpOutcome.DescribeTransportFailure(ex));
        }
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public Task<OAuthResult> AuthoriseAsync(CancellationToken cancellationToken = default) =>
        _authoriser.AuthoriseAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// `v0.24.0` X0: a usable token whose recorded grant lacks any of
    /// <see cref="XeroScopes.Required"/> answers
    /// <see cref="ConnectorAuthorisation.Expired"/> with the detail
    /// <i>"Xero needs re-authorising to allow: {missing}"</i>, so Settings
    /// offers Re-authorise before a call fails on the missing scope.
    /// </remarks>
    public async Task<ConnectorAuthorisationState> AuthorisationStateAsync(CancellationToken cancellationToken = default)
    {
        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        if (access.Outcome == AccessTokenOutcome.Ok)
        {
            var missing = FindMissingScopes(await _authoriser.ReadGrantedScopesAsync(cancellationToken).ConfigureAwait(false));
            return missing.Count == 0
                ? new ConnectorAuthorisationState(ConnectorAuthorisation.Authorised)
                : new ConnectorAuthorisationState(ConnectorAuthorisation.Expired, $"Xero needs re-authorising to allow: {string.Join(", ", missing)}");
        }

        return access.Outcome switch
        {
            AccessTokenOutcome.NotAuthorised => new ConnectorAuthorisationState(ConnectorAuthorisation.NotAuthorised),
            AccessTokenOutcome.NotConfigured => new ConnectorAuthorisationState(ConnectorAuthorisation.NotAuthorised, access.Reason),
            _ => new ConnectorAuthorisationState(ConnectorAuthorisation.Expired, access.Reason),
        };
    }

    /// <summary>
    /// The scopes of <see cref="XeroScopes.Required"/> that
    /// <paramref name="granted"/> lacks, in <see cref="XeroScopes.Required"/>'s
    /// order (`v0.24.0` X0). A <see langword="null"/> grant — tokens stored
    /// before v0.24.0 recorded none — counts as lacking exactly the scopes
    /// v0.24.0 added: <see cref="XeroScopes.Contacts"/>,
    /// <see cref="XeroScopes.SettingsRead"/> and <see cref="XeroScopes.Attachments"/>.
    /// </summary>
    /// <param name="granted">The recorded grant (<see cref="OAuthAuthoriser.ReadGrantedScopesAsync"/>).</param>
    public static IReadOnlyList<string> FindMissingScopes(IReadOnlyList<string>? granted)
    {
        if (granted is null)
            return [XeroScopes.Contacts, XeroScopes.SettingsRead, XeroScopes.Attachments];

        var grantedSet = new HashSet<string>(granted, StringComparer.Ordinal);
        return [.. XeroScopes.Required.Where(scope => !grantedSet.Contains(scope))];
    }

    // ====================================================================
    // `WP 19.8B` — `IAccountsConnector`: read-only bills, repeating bills
    // and cash position. Every member gates on `EnsureAccessTokenAsync`
    // exactly as the `IInvoicingConnector` members above, but answers
    // `Unavailable` rather than `Reauthorise` when there is no usable
    // access — deliberately, `IAccountsConnector`'s own remarks explain
    // why.
    // ====================================================================

    /// <inheritdoc />
    public async Task<ConnectorResult<IReadOnlyList<BillDue>>> ListBillsDueAsync(DateOnly asOf, int horizonDays, CancellationToken cancellationToken = default)
    {
        var access = await EnsureAccountsAccessAsync<IReadOnlyList<BillDue>>(cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
            return access.Failure;

        var where = Uri.EscapeDataString("Type==\"ACCPAY\"&&Status==\"AUTHORISED\"");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"Invoices?where={where}");
        ApplyAuthHeaders(httpRequest, access.Access!);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<IReadOnlyList<BillDue>>(outcome, response, body);

            var invoices = TryParseInvoicesEnvelope(body)?.Invoices ?? [];
            var cutoff = asOf.AddDays(horizonDays);

            IReadOnlyList<BillDue> bills = [.. invoices
                .Where(invoice => !string.IsNullOrWhiteSpace(invoice.CurrencyCode))
                .Select(ToBillDue)
                .Where(bill => bill.Due <= cutoff)];

            return ConnectorResult<IReadOnlyList<BillDue>>.Ok(bills);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return ConnectorResult<IReadOnlyList<BillDue>>.Unavailable(ConnectorHttpOutcome.DescribeTransportFailure(ex));
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<IReadOnlyList<RepeatingBill>>> ListRepeatingBillsAsync(CancellationToken cancellationToken = default)
    {
        var access = await EnsureAccountsAccessAsync<IReadOnlyList<RepeatingBill>>(cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
            return access.Failure;

        var where = Uri.EscapeDataString("Type==\"ACCPAY\"");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"RepeatingInvoices?where={where}");
        ApplyAuthHeaders(httpRequest, access.Access!);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<IReadOnlyList<RepeatingBill>>(outcome, response, body);

            var envelope = TryParse<XeroRepeatingInvoicesEnvelope>(body);

            IReadOnlyList<RepeatingBill> repeating = [.. (envelope?.RepeatingInvoices ?? [])
                .Where(invoice => !string.IsNullOrWhiteSpace(invoice.CurrencyCode) && invoice.Schedule?.NextScheduledDate is not null)
                .Select(ToRepeatingBill)
                .Where(bill => bill is not null)
                .Select(bill => bill!)];

            return ConnectorResult<IReadOnlyList<RepeatingBill>>.Ok(repeating);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return ConnectorResult<IReadOnlyList<RepeatingBill>>.Unavailable(ConnectorHttpOutcome.DescribeTransportFailure(ex));
        }
    }

    /// <summary>
    /// Reads Xero's own <c>Reports/BankSummary</c> — a generic reporting
    /// grid, not a typed resource the way <c>Invoices</c> is: the standard
    /// <c>Accounts</c> endpoint states a bank account's name and currency
    /// but never a balance, so this report is the documented way to read
    /// one. <b>Disclosed, not verified against a live sandbox</b> (`WP
    /// 19.8B` kill switch): this parses the <c>Header</c> row to find
    /// whichever column is titled "Closing Balance", then reads that
    /// column from every account row nested under a <c>Section</c> — the
    /// shape Xero's own published example carries — rather than assuming
    /// a fixed column index, so a harmless reordering of the grid's own
    /// columns does not silently misread a balance. The grid states no
    /// per-account currency; every balance is reported in
    /// <see cref="BaseCurrencyConfigurationKey"/> (GBP, this platform's own
    /// default), which is this connector's own honest limitation, not an
    /// invented fact about a specific account.
    /// </summary>
    /// <inheritdoc />
    public async Task<ConnectorResult<IReadOnlyList<CashAccountBalance>>> ReadCashPositionAsync(CancellationToken cancellationToken = default)
    {
        var access = await EnsureAccountsAccessAsync<IReadOnlyList<CashAccountBalance>>(cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
            return access.Failure;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "Reports/BankSummary");
        ApplyAuthHeaders(httpRequest, access.Access!);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<IReadOnlyList<CashAccountBalance>>(outcome, response, body);

            var report = TryParse<XeroReportsEnvelope>(body)?.Reports?.FirstOrDefault();
            if (report is null)
                return ConnectorResult<IReadOnlyList<CashAccountBalance>>.Unknown("Xero accepted the call but returned no report.");

            var currency = ResolveBaseCurrency();
            var asOf = ParseXeroDate(report.ReportDate) ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var balances = ParseBankSummary(report, currency, asOf);

            return ConnectorResult<IReadOnlyList<CashAccountBalance>>.Ok(balances);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return ConnectorResult<IReadOnlyList<CashAccountBalance>>.Unavailable(ConnectorHttpOutcome.DescribeTransportFailure(ex));
        }
    }

    private async Task<(AccessTokenResult? Access, ConnectorResult<T>? Failure)> EnsureAccountsAccessAsync<T>(CancellationToken cancellationToken)
    {
        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (access.Outcome != AccessTokenOutcome.Ok)
            return (null, ConnectorResult<T>.Unavailable(access.Reason ?? "Xero has not been authorised."));

        if (string.IsNullOrEmpty(access.TenantId))
            return (null, ConnectorResult<T>.Unavailable("No Xero organisation is connected; re-authorise to select one."));

        return (access, null);
    }

    private static BillDue ToBillDue(XeroInvoice invoice) => new(
        Supplier: invoice.Contact?.Name ?? string.Empty,
        Reference: invoice.Reference ?? invoice.InvoiceNumber ?? invoice.InvoiceID ?? string.Empty,
        Issued: ParseXeroDate(invoice.Date) ?? default,
        Due: ParseXeroDate(invoice.DueDate) ?? ParseXeroDate(invoice.Date) ?? default,
        Amount: new Money(invoice.Total ?? 0m, new CurrencyCode(invoice.CurrencyCode!)),
        Status: invoice.Status ?? "UNKNOWN");

    private static RepeatingBill? ToRepeatingBill(XeroRepeatingInvoice invoice)
    {
        var nextDue = ParseXeroDate(invoice.Schedule!.NextScheduledDate);
        if (nextDue is null)
            return null;

        var firstLine = invoice.LineItems?.FirstOrDefault();
        var accountName = firstLine?.Tracking?.FirstOrDefault()?.Option ?? firstLine?.AccountCode;
        var amount = invoice.Total ?? invoice.LineItems?.Sum(l => l.LineAmount ?? 0m) ?? 0m;
        var description = firstLine?.Description ?? invoice.Reference ?? "Repeating bill";

        return new RepeatingBill(
            Supplier: invoice.Contact?.Name ?? string.Empty,
            Description: description,
            Amount: new Money(amount, new CurrencyCode(invoice.CurrencyCode!)),
            Frequency: invoice.Schedule.Unit ?? "UNKNOWN",
            NextDue: nextDue.Value,
            AccountName: accountName);
    }

    private static IReadOnlyList<CashAccountBalance> ParseBankSummary(XeroReport report, CurrencyCode currency, DateOnly asOf)
    {
        var rows = report.Rows ?? [];
        var header = rows.FirstOrDefault(r => string.Equals(r.RowType, "Header", StringComparison.OrdinalIgnoreCase));
        var headerCells = header?.Cells ?? [];

        var closingBalanceIndex = headerCells.FindIndex(c => (c.Value ?? string.Empty).Contains("Closing Balance", StringComparison.OrdinalIgnoreCase));
        if (closingBalanceIndex < 0)
            closingBalanceIndex = headerCells.Count - 1;

        var balances = new List<CashAccountBalance>();

        foreach (var section in rows.Where(r => string.Equals(r.RowType, "Section", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var row in section.Rows ?? [])
            {
                if (!string.Equals(row.RowType, "Row", StringComparison.OrdinalIgnoreCase))
                    continue; // skips "SummaryRow" totals — not one account's own balance.

                var cells = row.Cells ?? [];
                if (cells.Count == 0 || closingBalanceIndex < 0 || closingBalanceIndex >= cells.Count)
                    continue;

                var name = cells[0].Value;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!decimal.TryParse(cells[closingBalanceIndex].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var balance))
                    continue;

                balances.Add(new CashAccountBalance(name, new Money(balance, currency), asOf));
            }
        }

        return balances;
    }

    private CurrencyCode ResolveBaseCurrency() =>
        _configuration is not null && _configuration.TryGetValue(BaseCurrencyConfigurationKey, out var configured) && !string.IsNullOrWhiteSpace(configured)
            ? new CurrencyCode(configured)
            : CurrencyCode.Gbp;

    private static ConnectorResult<T> MapNonOkOutcome<T>(ConnectorOutcome outcome, HttpResponseMessage response, string body) => outcome switch
    {
        ConnectorOutcome.Rejected => ConnectorResult<T>.Rejected(ExtractRejectionReason(body)),
        ConnectorOutcome.Reauthorise => ConnectorResult<T>.Reauthorise("Xero refused the stored access token."),
        ConnectorOutcome.Unavailable => ConnectorResult<T>.Unavailable($"Xero returned {(int)response.StatusCode} {response.StatusCode}."),
        _ => ConnectorResult<T>.Unknown($"Xero returned an unexpected status {(int)response.StatusCode}."),
    };

    private static ConnectorResult<T> MapAccessFailure<T>(AccessTokenResult access) => access.Outcome switch
    {
        AccessTokenOutcome.NotAuthorised => ConnectorResult<T>.Reauthorise("Xero has never been authorised."),
        AccessTokenOutcome.NotConfigured => ConnectorResult<T>.Reauthorise("not configured"),
        _ => ConnectorResult<T>.Reauthorise(access.Reason),
    };

    private static void ApplyAuthHeaders(HttpRequestMessage request, AccessTokenResult access)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        request.Headers.Add("xero-tenant-id", access.TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static XeroInvoicesEnvelope? TryParseInvoicesEnvelope(string body) => TryParse<XeroInvoicesEnvelope>(body);

    private static T? TryParse<T>(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string ExtractRejectionReason(string body)
    {
        try
        {
            var exception = JsonSerializer.Deserialize<XeroApiException>(body, JsonOptions);
            var detail = exception?.Elements?
                .SelectMany(e => e.ValidationErrors ?? [])
                .Select(e => e.Message)
                .Where(m => !string.IsNullOrWhiteSpace(m));

            var joined = detail is null ? null : string.Join("; ", detail);

            return !string.IsNullOrWhiteSpace(joined) ? joined! : exception?.Message ?? "Xero rejected the invoice.";
        }
        catch (JsonException)
        {
            return "Xero rejected the invoice.";
        }
    }

    /// <summary>
    /// Xero's own JSON API renders every date as
    /// <c>/Date(&lt;ms-since-epoch&gt;+&lt;tz-offset&gt;)/</c> — a legacy
    /// .NET convention Xero has never dropped — unless it happens to answer
    /// a plain ISO-8601 string instead; both are parsed here rather than
    /// either being assumed away.
    /// </summary>
    private static DateOnly? ParseXeroDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return iso;

        var start = raw.IndexOf('(') + 1;
        if (start <= 0)
            return null;

        var end = raw.IndexOf('+', start);
        if (end < 0)
            end = raw.IndexOf(')', start);

        return end > start && long.TryParse(raw.AsSpan(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime)
            : null;
    }
}
