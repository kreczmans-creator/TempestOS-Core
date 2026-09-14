using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing.OAuth;

namespace Tempest.Core.Invoicing.QuickBooksOnline;

/// <summary>
/// The real QuickBooks Online <see cref="IInvoicingConnector"/>, over
/// <c>HttpClient</c> and <see cref="OAuthAuthoriser"/> (`WP 19.1A` part 2,
/// brief §2). Every call is relative to <c>httpClient</c>'s own
/// <see cref="HttpClient.BaseAddress"/> (<c>https://sandbox-quickbooks.api.intuit.com/</c>
/// or <c>https://quickbooks.api.intuit.com/</c>, by
/// <c>Invoicing:QuickBooksOnline:Environment</c>) with the realm id —
/// unlike Xero's fixed API host, QuickBooks Online's own path carries the
/// company (<c>v3/company/&lt;realmId&gt;/...</c>), so it is read off
/// <see cref="AccessTokenResult.TenantId"/> — the value
/// <see cref="OAuthAuthoriser"/> stores generically for every provider —
/// fresh on every call rather than baked into <c>BaseAddress</c> at
/// construction, since it is only known once the operator has connected.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contacts.</b> Unlike Xero, QuickBooks Online requires a resolved
/// <c>CustomerRef.value</c> before an invoice can be created at all — a
/// bare name in the invoice body is not enough. This connector therefore
/// queries <c>Customer</c> by <c>DisplayName</c> (matching
/// <see cref="Invoicing.InvoiceRequestSnapshot.ClientName"/> — the client
/// organisation's own name, resolved from the Organisation catalogue by
/// <c>InvoicingService</c> and filled onto the snapshot before this
/// connector ever sees it, never <see cref="Invoicing.InvoiceRequestSnapshot.ClientOrganisationId"/>
/// itself, `WP 19.1A-R1` disclosure #3) and creates one when absent — the
/// brief's own "contacts matched by name, created when absent" (§2),
/// genuinely implemented here rather than deferred, because QuickBooks
/// Online's own API leaves no other way to create the invoice. A
/// <see langword="null"/> or blank <c>ClientName</c> means the id did not
/// resolve in the catalogue at all; <see cref="CreateDraftInvoiceAsync"/>
/// rejects outright rather than querying/creating a customer named after
/// a raw, meaningless id.
/// </para>
/// <para>
/// <b>Disclosed gap: no item catalogue.</b> QuickBooks Online's real API
/// requires every invoice line to carry a valid <c>ItemRef</c> naming a
/// product/service item already defined in the company — a catalogue this
/// Work Package's own object model has no concept of
/// (<c>InvoiceRequestLine</c> carries a description and a rate, never an
/// item). <c>Invoicing:QuickBooksOnline:DefaultItemId</c>, when configured,
/// is attached to every line; left unconfigured, a real sandbox will likely
/// answer 400/422 (mapped to <see cref="ConnectorOutcome.Rejected"/>, never
/// a crash) rather than silently succeed — the physical review's own first
/// finding once a real sandbox is registered, not a defect this connector
/// hides.
/// </para>
/// <para>
/// <b>Status is synthesised, never provided directly.</b> QuickBooks
/// Online carries no single status word; <c>DeriveExternalStatus</c> reads
/// <c>Balance</c>/<c>TotalAmt</c>/<c>EmailStatus</c> into the same
/// vocabulary <c>InvoicingService.InterpretStatus</c> already matches by
/// substring (<c>PAID</c>, <c>VOIDED</c>, otherwise unchanged) — a paid
/// invoice's own <see cref="InvoiceStatusReading.PaidDate"/> is then read,
/// best-effort, from its linked <c>Payment</c>'s own <c>TxnDate</c>.
/// </para>
/// </remarks>
public sealed class QuickBooksOnlineConnector : IInvoicingConnector, IAccountsConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly HttpClient _httpClient;
    private readonly OAuthAuthoriser _authoriser;
    private readonly IConfigurationProvider _configuration;

    /// <summary>Initialises a new instance of the <see cref="QuickBooksOnlineConnector"/> class.</summary>
    public QuickBooksOnlineConnector(HttpClient httpClient, OAuthAuthoriser authoriser, IConfigurationProvider configuration)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authoriser);
        ArgumentNullException.ThrowIfNull(configuration);

        _httpClient = httpClient;
        _authoriser = authoriser;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public string Name => "QuickBooks Online";

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
            return ConnectorResult<CreatedInvoice>.Reauthorise("No QuickBooks Online company is connected; re-authorise to select one.");

        if (string.IsNullOrWhiteSpace(request.ClientName))
            return ConnectorResult<CreatedInvoice>.Rejected($"client organisation '{request.ClientOrganisationId}' is not in the catalogue");

        var (customerId, failure) = await ResolveOrCreateCustomerIdAsync<CreatedInvoice>(request.ClientName, access, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
            return failure;

        var defaultItemId = _configuration.TryGetValue("Invoicing:QuickBooksOnline:DefaultItemId", out var itemId) && !string.IsNullOrWhiteSpace(itemId)
            ? itemId
            : null;

        var body = new
        {
            DocNumber = DeriveDocNumber(idempotencyKey),
            PrivateNote = $"TempestOS request {idempotencyKey}",
            CustomerRef = new { value = customerId },
            CurrencyRef = new { value = request.Currency.ToString() },
            Line = request.Lines.Select(l => new
            {
                Amount = l.Amount.Amount,
                DetailType = "SalesItemLineDetail",
                Description = l.Description,
                SalesItemLineDetail = new
                {
                    Qty = (decimal?)l.Quantity,
                    UnitPrice = (decimal?)l.UnitRate.Amount,
                    ItemRef = defaultItemId is null ? null : new { value = defaultItemId },
                },
            }).ToArray(),
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"v3/company/{access.TenantId}/invoice?requestid={Uri.EscapeDataString(idempotencyKey)}")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        ApplyAuthHeaders(httpRequest, access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var responseBody = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<CreatedInvoice>(outcome, response, responseBody);

            var invoice = TryParse<QboInvoiceEnvelope>(responseBody)?.Invoice;
            if (invoice?.Id is null)
                return ConnectorResult<CreatedInvoice>.Unknown("QuickBooks Online accepted the call but returned no invoice.");

            return ConnectorResult<CreatedInvoice>.Ok(new CreatedInvoice(invoice.Id, invoice.DocNumber, invoice.DocNumber ?? idempotencyKey));
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
            return ConnectorResult<InvoiceStatusReading>.Reauthorise("No QuickBooks Online company is connected; re-authorise to select one.");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v3/company/{access.TenantId}/invoice/{Uri.EscapeDataString(externalId)}");
        ApplyAuthHeaders(httpRequest, access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<InvoiceStatusReading>(outcome, response, body);

            var invoice = TryParse<QboInvoiceEnvelope>(body)?.Invoice;
            if (invoice is null)
                return ConnectorResult<InvoiceStatusReading>.Unknown("QuickBooks Online accepted the call but returned no invoice.");

            var status = DeriveExternalStatus(invoice);
            var paidDate = string.Equals(status, "PAID", StringComparison.Ordinal)
                ? await FindLinkedPaymentDateAsync(invoice.Id!, access, cancellationToken).ConfigureAwait(false)
                : null;

            return ConnectorResult<InvoiceStatusReading>.Ok(
                new InvoiceStatusReading(status, invoice.DocNumber, ParseIsoDate(invoice.TxnDate), paidDate));
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
            return ConnectorResult<CreatedInvoice?>.Reauthorise("No QuickBooks Online company is connected; re-authorise to select one.");

        // `reference` is our own request id (`IInvoicingConnector.FindByReferenceAsync`'s
        // own remarks), the same 36-character value `CreateDraftInvoiceAsync`
        // itself derived `DocNumber` from — never the derived value directly,
        // so it is derived again here rather than assumed already-shortened
        // (`WP 19.1A-R1` disclosure #2).
        var query = Uri.EscapeDataString($"select * from Invoice where DocNumber = '{EscapeForQuery(DeriveDocNumber(reference))}'");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v3/company/{access.TenantId}/query?query={query}");
        ApplyAuthHeaders(httpRequest, access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<CreatedInvoice?>(outcome, response, body);

            var invoice = TryParse<QboQueryEnvelope>(body)?.QueryResponse?.Invoice?.FirstOrDefault();

            return ConnectorResult<CreatedInvoice?>.Ok(
                invoice is null ? null : new CreatedInvoice(invoice.Id ?? string.Empty, invoice.DocNumber, invoice.DocNumber ?? reference));
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
            return ConnectorResult<IReadOnlyList<ConnectorContact>>.Reauthorise("No QuickBooks Online company is connected; re-authorise to select one.");

        var query = Uri.EscapeDataString("select * from Customer");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v3/company/{access.TenantId}/query?query={query}");
        ApplyAuthHeaders(httpRequest, access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<IReadOnlyList<ConnectorContact>>(outcome, response, body);

            var customers = TryParse<QboQueryEnvelope>(body)?.QueryResponse?.Customer ?? [];
            IReadOnlyList<ConnectorContact> contacts = [.. customers.Select(c => new ConnectorContact(c.Id ?? string.Empty, c.DisplayName ?? string.Empty))];

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

    // ====================================================================
    // `WP 19.8B` — `IAccountsConnector`: read-only bills, recurring
    // transactions and bank balances. Secondary to Xero (Product Owner,
    // 2026-09-14). Every member gates on `EnsureAccessTokenAsync` exactly
    // as the `IInvoicingConnector` members above, but answers
    // `Unavailable` rather than `Reauthorise` when there is no usable
    // access — `IAccountsConnector`'s own remarks explain why.
    // ====================================================================

    /// <inheritdoc />
    public async Task<ConnectorResult<IReadOnlyList<BillDue>>> ListBillsDueAsync(DateOnly asOf, int horizonDays, CancellationToken cancellationToken = default)
    {
        var access = await EnsureAccountsAccessAsync<IReadOnlyList<BillDue>>(cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
            return access.Failure;

        var query = Uri.EscapeDataString("select * from Bill");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v3/company/{access.Access!.TenantId}/query?query={query}");
        ApplyAuthHeaders(httpRequest, access.Access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<IReadOnlyList<BillDue>>(outcome, response, body);

            var bills = TryParse<QboQueryEnvelope>(body)?.QueryResponse?.Bill ?? [];
            var cutoff = asOf.AddDays(horizonDays);

            IReadOnlyList<BillDue> due = [.. bills
                .Where(b => !string.IsNullOrWhiteSpace(b.CurrencyRef?.Value))
                .Select(ToBillDue)
                .Where(b => b.Due <= cutoff)];

            return ConnectorResult<IReadOnlyList<BillDue>>.Ok(due);
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

    /// <summary>
    /// Reads recurring transaction templates of <c>Type == "Bill"</c> — a
    /// recurring invoice template (<c>"Invoice"</c>) is a receivable, not a
    /// payable, and is skipped. <b>Disclosed, not verified against a live
    /// sandbox</b> (`WP 19.8B` kill switch, secondary to Xero per the
    /// Product Owner): QuickBooks Online's <c>RecurringTransaction</c>
    /// entity is queried the same way every other resource on this
    /// connector is.
    /// </summary>
    /// <inheritdoc />
    public async Task<ConnectorResult<IReadOnlyList<RepeatingBill>>> ListRepeatingBillsAsync(CancellationToken cancellationToken = default)
    {
        var access = await EnsureAccountsAccessAsync<IReadOnlyList<RepeatingBill>>(cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
            return access.Failure;

        var query = Uri.EscapeDataString("select * from RecurringTransaction");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v3/company/{access.Access!.TenantId}/query?query={query}");
        ApplyAuthHeaders(httpRequest, access.Access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<IReadOnlyList<RepeatingBill>>(outcome, response, body);

            var templates = TryParse<QboQueryEnvelope>(body)?.QueryResponse?.RecurringTransaction ?? [];

            IReadOnlyList<RepeatingBill> repeating = [.. templates
                .Where(t => string.Equals(t.Type, "Bill", StringComparison.OrdinalIgnoreCase)
                    && t.Bill is not null
                    && !string.IsNullOrWhiteSpace(t.Bill.CurrencyRef?.Value)
                    && t.ScheduleInfo?.NextDate is not null)
                .Select(ToRepeatingBill)
                .Where(b => b is not null)
                .Select(b => b!)];

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

    /// <inheritdoc />
    public async Task<ConnectorResult<IReadOnlyList<CashAccountBalance>>> ReadCashPositionAsync(CancellationToken cancellationToken = default)
    {
        var access = await EnsureAccountsAccessAsync<IReadOnlyList<CashAccountBalance>>(cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
            return access.Failure;

        var query = Uri.EscapeDataString("select * from Account where AccountType='Bank'");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v3/company/{access.Access!.TenantId}/query?query={query}");
        ApplyAuthHeaders(httpRequest, access.Access);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            if (outcome != ConnectorOutcome.Ok)
                return MapNonOkOutcome<IReadOnlyList<CashAccountBalance>>(outcome, response, body);

            var accounts = TryParse<QboQueryEnvelope>(body)?.QueryResponse?.Account ?? [];
            var asOf = DateOnly.FromDateTime(DateTime.UtcNow);

            IReadOnlyList<CashAccountBalance> balances = [.. accounts
                .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.CurrencyRef?.Value))
                .Select(a => new CashAccountBalance(a.Name!, new Money(a.CurrentBalance ?? 0m, new CurrencyCode(a.CurrencyRef!.Value!)), asOf))];

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
            return (null, ConnectorResult<T>.Unavailable(access.Reason ?? "QuickBooks Online has not been authorised."));

        if (string.IsNullOrEmpty(access.TenantId))
            return (null, ConnectorResult<T>.Unavailable("No QuickBooks Online company is connected; re-authorise to select one."));

        return (access, null);
    }

    private static BillDue ToBillDue(QboBill bill) => new(
        Supplier: bill.VendorRef?.Name ?? string.Empty,
        Reference: bill.DocNumber ?? bill.Id ?? string.Empty,
        Issued: ParseIsoDate(bill.TxnDate) ?? default,
        Due: ParseIsoDate(bill.DueDate) ?? ParseIsoDate(bill.TxnDate) ?? default,
        Amount: new Money(bill.TotalAmt ?? 0m, new CurrencyCode(bill.CurrencyRef!.Value!)),
        Status: (bill.Balance ?? bill.TotalAmt ?? 0m) == 0m ? "PAID" : "OPEN");

    private static RepeatingBill? ToRepeatingBill(QboRecurringTransaction template)
    {
        var nextDue = ParseIsoDate(template.ScheduleInfo!.NextDate);
        if (nextDue is null)
            return null;

        var bill = template.Bill!;
        var firstLine = bill.Line?.FirstOrDefault(l => l.AccountBasedExpenseLineDetail?.AccountRef?.Name is not null);
        var amount = bill.Line?.Sum(l => l.Amount) ?? 0m;

        return new RepeatingBill(
            Supplier: bill.VendorRef?.Name ?? string.Empty,
            Description: template.Name ?? "Recurring bill",
            Amount: new Money(amount, new CurrencyCode(bill.CurrencyRef!.Value!)),
            Frequency: template.ScheduleInfo.IntervalType ?? "UNKNOWN",
            NextDue: nextDue.Value,
            AccountName: firstLine?.AccountBasedExpenseLineDetail?.AccountRef?.Name);
    }

    private async Task<(string? CustomerId, ConnectorResult<T>? Failure)> ResolveOrCreateCustomerIdAsync<T>(
        string displayName, AccessTokenResult access, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"select * from Customer where DisplayName = '{EscapeForQuery(displayName)}'");
        using var queryRequest = new HttpRequestMessage(HttpMethod.Get, $"v3/company/{access.TenantId}/query?query={query}");
        ApplyAuthHeaders(queryRequest, access);

        try
        {
            using (var queryResponse = await _httpClient.SendAsync(queryRequest, cancellationToken).ConfigureAwait(false))
            {
                var outcome = ConnectorHttpOutcome.Classify(queryResponse.StatusCode);
                var body = await ConnectorHttpOutcome.ReadBodyAsync(queryResponse, cancellationToken).ConfigureAwait(false);

                if (outcome != ConnectorOutcome.Ok)
                    return (null, MapNonOkOutcome<T>(outcome, queryResponse, body));

                var existing = TryParse<QboQueryEnvelope>(body)?.QueryResponse?.Customer?.FirstOrDefault();
                if (existing?.Id is not null)
                    return (existing.Id, null);
            }

            using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"v3/company/{access.TenantId}/customer")
            {
                Content = JsonContent.Create(new { DisplayName = displayName }, options: JsonOptions),
            };
            ApplyAuthHeaders(createRequest, access);

            using var createResponse = await _httpClient.SendAsync(createRequest, cancellationToken).ConfigureAwait(false);
            var createOutcome = ConnectorHttpOutcome.Classify(createResponse.StatusCode);
            var createBody = await ConnectorHttpOutcome.ReadBodyAsync(createResponse, cancellationToken).ConfigureAwait(false);

            if (createOutcome != ConnectorOutcome.Ok)
                return (null, MapNonOkOutcome<T>(createOutcome, createResponse, createBody));

            var created = TryParse<QboCustomerEnvelope>(createBody)?.Customer;

            return created?.Id is not null
                ? (created.Id, null)
                : (null, ConnectorResult<T>.Unknown("QuickBooks Online accepted the customer create but returned no id."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return (null, ConnectorResult<T>.Unavailable(ConnectorHttpOutcome.DescribeTransportFailure(ex)));
        }
    }

    private async Task<DateOnly?> FindLinkedPaymentDateAsync(string invoiceId, AccessTokenResult access, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"select * from Payment where Line.LinkedTxn.TxnId = '{EscapeForQuery(invoiceId)}'");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v3/company/{access.TenantId}/query?query={query}");
        ApplyAuthHeaders(request, access);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            var payment = TryParse<QboQueryEnvelope>(body)?.QueryResponse?.Payment?.FirstOrDefault();

            return ParseIsoDate(payment?.TxnDate);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // Best-effort, as ResolveTenantIdAsync's own remarks describe:
            // the status reading still answers "PAID" without a paid date
            // rather than failing the whole read over it.
            return null;
        }
    }

    private static ConnectorResult<T> MapNonOkOutcome<T>(ConnectorOutcome outcome, HttpResponseMessage response, string body) => outcome switch
    {
        ConnectorOutcome.Rejected => ConnectorResult<T>.Rejected(ExtractRejectionReason(body)),
        ConnectorOutcome.Reauthorise => ConnectorResult<T>.Reauthorise("QuickBooks Online refused the stored access token."),
        ConnectorOutcome.Unavailable => ConnectorResult<T>.Unavailable($"QuickBooks Online returned {(int)response.StatusCode} {response.StatusCode}."),
        _ => ConnectorResult<T>.Unknown($"QuickBooks Online returned an unexpected status {(int)response.StatusCode}."),
    };

    private static ConnectorResult<T> MapAccessFailure<T>(AccessTokenResult access) => access.Outcome switch
    {
        AccessTokenOutcome.NotAuthorised => ConnectorResult<T>.Reauthorise("QuickBooks Online has never been authorised."),
        AccessTokenOutcome.NotConfigured => ConnectorResult<T>.Reauthorise("not configured"),
        _ => ConnectorResult<T>.Reauthorise(access.Reason),
    };

    private static void ApplyAuthHeaders(HttpRequestMessage request, AccessTokenResult access)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

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
        var fault = TryParse<QboFaultEnvelope>(body)?.Fault?.Error?.FirstOrDefault();
        if (fault is null)
            return "QuickBooks Online rejected the invoice.";

        return fault.Message is { Length: > 0 } message
            ? (fault.Detail is { Length: > 0 } detail ? $"{message}: {detail}" : message)
            : "QuickBooks Online rejected the invoice.";
    }

    private static string EscapeForQuery(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>
    /// Derives a stable, exactly-21-character QuickBooks Online <c>DocNumber</c>
    /// from <paramref name="idempotencyKey"/> — <c>InvoiceRequest.Id</c>'s own
    /// 36-character <c>"D"</c>-format string, which exceeds the field's own
    /// 21-character limit (`WP 19.1A-R1` disclosure #2): <c>"TOS-"</c> (4
    /// characters) plus the first 17 hex digits found in
    /// <paramref name="idempotencyKey"/> (its own dashes skipped),
    /// upper-cased, right-padded with <c>'0'</c> if fewer than 17 hex
    /// digits are present at all.
    /// </summary>
    /// <remarks>
    /// <b>Stable, not claimed unique.</b> The same id always derives the
    /// same <c>DocNumber</c> — what <see cref="FindByReferenceAsync"/>
    /// depends on to look a request back up by reference — but 17 of a
    /// GUID's own 32 hex digits is 68 bits of the original 128, so two
    /// different request ids sharing a derived number is merely
    /// astronomically unlikely, never impossible. Nothing here claims
    /// otherwise, and nothing is lost either way: <see cref="CreateDraftInvoiceAsync"/>
    /// writes <paramref name="idempotencyKey"/> in full into <c>PrivateNote</c>
    /// alongside this derived number.
    /// </remarks>
    internal static string DeriveDocNumber(string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        const int HexDigitsWanted = 17;
        Span<char> hex = stackalloc char[HexDigitsWanted];
        var count = 0;

        foreach (var c in idempotencyKey)
        {
            if (count == HexDigitsWanted)
                break;

            if (Uri.IsHexDigit(c))
                hex[count++] = char.ToUpperInvariant(c);
        }

        for (var i = count; i < HexDigitsWanted; i++)
            hex[i] = '0';

        return $"TOS-{new string(hex)}";
    }

    /// <summary>
    /// Derives the status word <c>InvoicingService.InterpretStatus</c>
    /// matches by substring, since QuickBooks Online's own <c>Invoice</c>
    /// carries no single status field the way Xero's does. A zeroed
    /// <c>TotalAmt</c> alongside a zeroed <c>Balance</c> is QuickBooks
    /// Online's own on-the-wire shape for a voided invoice (every line
    /// removed); a positive <c>TotalAmt</c> with a zeroed <c>Balance</c> is
    /// fully paid; <c>EmailStatus</c> of <c>"EmailSent"</c> without either
    /// names it merely sent; anything else is reported as
    /// <c>"SUBMITTED"</c>, which <c>InterpretStatus</c> leaves unchanged.
    /// </summary>
    private static string DeriveExternalStatus(QboInvoice invoice)
    {
        var balance = invoice.Balance ?? 0m;
        var total = invoice.TotalAmt ?? 0m;

        if (total == 0m && balance == 0m)
            return "VOIDED";

        if (balance == 0m && total > 0m)
            return "PAID";

        if (string.Equals(invoice.EmailStatus, "EmailSent", StringComparison.OrdinalIgnoreCase))
            return "SENT";

        return "SUBMITTED";
    }

    private static DateOnly? ParseIsoDate(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
