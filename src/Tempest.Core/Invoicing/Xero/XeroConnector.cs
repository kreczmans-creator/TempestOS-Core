using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Core.Invoicing.OAuth;

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
/// <b>Contacts.</b> An invoice's own <c>Contact</c> carries
/// <see cref="Invoicing.InvoiceRequestSnapshot.ClientName"/> as its
/// <c>Name</c> — the client organisation's own name, resolved from the
/// Organisation catalogue by <c>InvoicingService</c> and filled onto the
/// snapshot before this connector ever sees it (<c>WP 19.1A-R1</c>
/// disclosure #3; previously this connector matched by
/// <see cref="Invoicing.InvoiceRequestSnapshot.ClientOrganisationId"/>
/// itself, a bare catalogue id no accounting system's own contact list
/// was ever going to already hold). Xero itself matches an existing
/// contact by that name, or creates one when absent — documented Xero
/// behaviour for an invoice's inline <c>Contact</c> object, so this
/// connector defers contact search/creation to Xero rather than adding
/// its own <c>/Contacts</c> round trip before every send. A
/// <see langword="null"/> or blank <c>ClientName</c> means the id did not
/// resolve in the catalogue at all; <see cref="CreateDraftInvoiceAsync"/>
/// rejects outright rather than handing Xero a contact named after a raw,
/// meaningless id.
/// </para>
/// </remarks>
public sealed class XeroConnector : IInvoicingConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly HttpClient _httpClient;
    private readonly OAuthAuthoriser _authoriser;

    /// <summary>Initialises a new instance of the <see cref="XeroConnector"/> class.</summary>
    public XeroConnector(HttpClient httpClient, OAuthAuthoriser authoriser)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authoriser);

        _httpClient = httpClient;
        _authoriser = authoriser;
    }

    /// <inheritdoc />
    public string Name => "Xero";

    /// <inheritdoc />
    public async Task<ConnectorResult<CreatedInvoice>> CreateDraftInvoiceAsync(
        InvoiceRequestSnapshot request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (access.Outcome != AccessTokenOutcome.Ok)
            return MapAccessFailure<CreatedInvoice>(access);

        if (string.IsNullOrEmpty(access.TenantId))
            return ConnectorResult<CreatedInvoice>.Reauthorise("No Xero organisation is connected; re-authorise to select one.");

        if (string.IsNullOrWhiteSpace(request.ClientName))
            return ConnectorResult<CreatedInvoice>.Rejected($"client organisation '{request.ClientOrganisationId}' is not in the catalogue");

        var payload = new XeroInvoicesEnvelope(
        [
            new XeroInvoice(
                Type: "ACCREC",
                Contact: new XeroContact(Name: request.ClientName),
                LineItems: [.. request.Lines.Select(l => new XeroLineItem(l.Description, l.Quantity, l.UnitRate.Amount, l.Amount.Amount))],
                Reference: idempotencyKey,
                CurrencyCode: request.Currency.ToString()),
        ]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "Invoices") { Content = JsonContent.Create(payload, options: JsonOptions) };
        ApplyAuthHeaders(httpRequest, access);
        httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return await InterpretCreateResponseAsync(response, idempotencyKey, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return ConnectorResult<CreatedInvoice>.Unavailable(ConnectorHttpOutcome.DescribeTransportFailure(ex));
        }
    }

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
    public async Task<ConnectorAuthorisationState> AuthorisationStateAsync(CancellationToken cancellationToken = default)
    {
        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        return access.Outcome switch
        {
            AccessTokenOutcome.Ok => new ConnectorAuthorisationState(ConnectorAuthorisation.Authorised),
            AccessTokenOutcome.NotAuthorised => new ConnectorAuthorisationState(ConnectorAuthorisation.NotAuthorised),
            AccessTokenOutcome.NotConfigured => new ConnectorAuthorisationState(ConnectorAuthorisation.NotAuthorised, access.Reason),
            _ => new ConnectorAuthorisationState(ConnectorAuthorisation.Expired, access.Reason),
        };
    }

    private async Task<ConnectorResult<CreatedInvoice>> InterpretCreateResponseAsync(HttpResponseMessage response, string idempotencyKey, CancellationToken cancellationToken)
    {
        var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
        var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

        if (outcome != ConnectorOutcome.Ok)
            return MapNonOkOutcome<CreatedInvoice>(outcome, response, body);

        var invoice = TryParseInvoicesEnvelope(body)?.Invoices?.FirstOrDefault();
        if (invoice?.InvoiceID is null)
            return ConnectorResult<CreatedInvoice>.Unknown("Xero accepted the call but returned no invoice id.");

        return ConnectorResult<CreatedInvoice>.Ok(new CreatedInvoice(invoice.InvoiceID, invoice.InvoiceNumber, invoice.Reference ?? idempotencyKey));
    }

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

    private static XeroInvoicesEnvelope? TryParseInvoicesEnvelope(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<XeroInvoicesEnvelope>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
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
