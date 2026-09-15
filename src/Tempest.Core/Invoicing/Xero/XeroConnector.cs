using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
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
public sealed class XeroConnector : IInvoicingConnector, IAccountsConnector, IAuthorisableConnector
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming the currency every <see cref="IAccountsConnector.ReadCashPositionAsync"/> balance is reported in — Xero's own Bank Summary report states each balance in the organisation's base currency without naming it in the grid itself (`XeroConnector.ReadCashPositionAsync`'s own remarks).</summary>
    public const string BaseCurrencyConfigurationKey = "Invoicing:Xero:BaseCurrency";

    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly HttpClient _httpClient;
    private readonly OAuthAuthoriser _authoriser;
    private readonly IConfigurationProvider? _configuration;

    /// <summary>Initialises a new instance of the <see cref="XeroConnector"/> class.</summary>
    /// <param name="configuration">Where <see cref="BaseCurrencyConfigurationKey"/> is read from. <see langword="null"/> is honoured — <see cref="IAccountsConnector.ReadCashPositionAsync"/> then falls back to GBP, this platform's own fixture currency throughout.</param>
    public XeroConnector(HttpClient httpClient, OAuthAuthoriser authoriser, IConfigurationProvider? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authoriser);

        _httpClient = httpClient;
        _authoriser = authoriser;
        _configuration = configuration;
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

        // `WP 21.3B`: refuse outright, before ever building the payload,
        // rather than send Xero a line it would itself reject — the
        // identical "a result, never an exception" discipline this
        // connector already applies to every other rejection (`ADR-0151`).
        if (VatRateTaxTypeMapping.FindUnmappableReason(request.Lines) is { } unmappableReason)
            return ConnectorResult<CreatedInvoice>.Rejected(unmappableReason);

        var payload = new XeroInvoicesEnvelope(
        [
            new XeroInvoice(
                Type: "ACCREC",
                Contact: new XeroContact(Name: request.ClientName),
                LineItems: [.. request.Lines.Select(ToXeroLineItem)],
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
    /// <inheritdoc />
    public Task<OAuthResult> AuthoriseAsync(CancellationToken cancellationToken = default) =>
        _authoriser.AuthoriseAsync(cancellationToken);

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

    /// <summary>Builds one outbound line item, mapping <paramref name="line"/>'s own <see cref="Tempest.Core.BusinessGovernance.VatRate"/> to Xero's own tax type (`WP 21.3B`) — never called once <see cref="VatRateTaxTypeMapping.FindUnmappableReason"/> has already found a line this table cannot map, so <see cref="VatRateTaxTypeMapping.TryMap"/> always succeeds here.</summary>
    private static XeroLineItem ToXeroLineItem(InvoiceRequestLine line)
    {
        VatRateTaxTypeMapping.TryMap(line.VatRate, out var taxType);
        return new XeroLineItem(line.Description, line.Quantity, line.UnitRate.Amount, line.Amount.Amount, taxType);
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
