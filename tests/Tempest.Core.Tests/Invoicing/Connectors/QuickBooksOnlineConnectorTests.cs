using System.Net;
using System.Text;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.QuickBooksOnline;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// <c>QuickBooksOnlineConnector</c>'s own contract, against recorded
/// responses only — no test in this class ever touches the network
/// (`WP 19.1A` part 2 brief §3): create ok/rejected/401/429/timeout,
/// find-by-reference found/not found, and a paid status reading (derived
/// from <c>Balance</c>/<c>TotalAmt</c> and a linked payment, since
/// QuickBooks Online carries no single status word), each mapped to the
/// outcome §2 names. "Create ok" also exercises the customer
/// query-then-create step QuickBooks Online's own API requires before an
/// invoice can exist at all (unlike Xero, which matches/creates a contact
/// by name on the invoice call itself).
/// </summary>
public sealed class QuickBooksOnlineConnectorTests
{
    private const string Realm = "realm-1";

    [Fact]
    public async Task CreateDraftInvoiceAsync_Ok_QueriesThenCreatesTheCustomerByName_CarriesTheDerivedDocNumber_KeepsTheFullIdInThePrivateNote()
    {
        var (connector, handler, _) = await BuildAsync();
        var requestId = Guid.NewGuid();
        var idempotencyKey = requestId.ToString();
        var expectedDocNumber = QuickBooksOnlineConnector.DeriveDocNumber(idempotencyKey);

        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (request, _) =>
        {
            Assert.Contains("Customer", request.RequestUri!.ToString(), StringComparison.Ordinal);
            return JsonResponse(HttpStatusCode.OK, """{"QueryResponse":{}}"""); // no existing customer
        });

        handler.When(HttpMethod.Post, $"v3/company/{Realm}/customer", (_, body) =>
        {
            Assert.Contains("\"DisplayName\":\"Fictional Client Ltd\"", body, StringComparison.Ordinal);
            Assert.DoesNotContain("\"DisplayName\":\"ORG-1\"", body, StringComparison.Ordinal);
            return JsonResponse(HttpStatusCode.OK, """{"Customer":{"Id":"cust-1","DisplayName":"Fictional Client Ltd"}}""");
        });

        handler.When(HttpMethod.Post, $"v3/company/{Realm}/invoice", (request, body) =>
        {
            Assert.Contains($"requestid={idempotencyKey}", request.RequestUri!.ToString(), StringComparison.Ordinal);
            Assert.Contains($"\"DocNumber\":\"{expectedDocNumber}\"", body, StringComparison.Ordinal);
            Assert.Contains($"TempestOS request {idempotencyKey}", body, StringComparison.Ordinal);
            Assert.Contains("\"value\":\"cust-1\"", body, StringComparison.Ordinal);
            Assert.Equal("Bearer seeded-access-token", request.Headers.Authorization!.ToString());

            return JsonResponse(HttpStatusCode.OK, "{\"Invoice\":{\"Id\":\"inv-100\",\"DocNumber\":\"" + expectedDocNumber + "\"}}");
        });

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(requestId), idempotencyKey);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(21, expectedDocNumber.Length);
        Assert.Equal("inv-100", result.Value!.ExternalId);
        Assert.Equal(expectedDocNumber, result.Value.ExternalInvoiceNumber);
        Assert.Equal(expectedDocNumber, result.Value.Reference);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_NoClientName_ReturnsRejected_NamingTheOrganisationId_NeverCallsQuickBooksOnline()
    {
        var (connector, handler, _) = await BuildAsync();

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(clientName: null), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("ORG-1", result.Reason, StringComparison.Ordinal);
        Assert.Contains("not in the catalogue", result.Reason, StringComparison.Ordinal);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_ExistingCustomer_SkipsCreate_UsesTheFoundId()
    {
        var (connector, handler, _) = await BuildAsync();

        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (request, _) =>
            request.RequestUri!.ToString().Contains("Customer", StringComparison.Ordinal)
                ? JsonResponse(HttpStatusCode.OK, """{"QueryResponse":{"Customer":[{"Id":"cust-existing","DisplayName":"Fictional Client Ltd"}]}}""")
                : JsonResponse(HttpStatusCode.OK, """{"QueryResponse":{}}"""));

        handler.When(HttpMethod.Post, $"v3/company/{Realm}/invoice", (_, body) =>
        {
            Assert.Contains("\"value\":\"cust-existing\"", body, StringComparison.Ordinal);
            return JsonResponse(HttpStatusCode.OK, """{"Invoice":{"Id":"inv-101","DocNumber":"DOC-1"}}""");
        });

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.DoesNotContain(handler.Calls, c => c.Uri.ToString().Contains("/customer", StringComparison.Ordinal) && c.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_Rejected_ReturnsTheFaultMessageFromTheBody()
    {
        var (connector, handler, _) = await BuildAsync();
        StubExistingCustomer(handler);
        handler.When(HttpMethod.Post, $"v3/company/{Realm}/invoice", (_, _) => JsonResponse(
            HttpStatusCode.BadRequest,
            """{"Fault":{"Error":[{"Message":"Required param missing, need to supply the Line","Detail":"Line is a required field."}],"type":"ValidationFault"}}"""));

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("Required param missing", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_401_MapsToReauthorise()
    {
        var (connector, handler, _) = await BuildAsync();
        StubExistingCustomer(handler);
        handler.When(HttpMethod.Post, $"v3/company/{Realm}/invoice", (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_429_MapsToUnavailable()
    {
        var (connector, handler, _) = await BuildAsync();
        StubExistingCustomer(handler);
        handler.When(HttpMethod.Post, $"v3/company/{Realm}/invoice", (_, _) => new HttpResponseMessage((HttpStatusCode)429));

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_ATransportTimeout_MapsToUnavailable()
    {
        var (connector, handler, _) = await BuildAsync();
        StubExistingCustomer(handler);
        handler.WhenThrows(HttpMethod.Post, $"v3/company/{Realm}/invoice", new TaskCanceledException("The request timed out."));

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task FindByReferenceAsync_Found_QueriesByDocNumber_ReturnsTheInvoice()
    {
        var (connector, handler, _) = await BuildAsync();
        const string reference = "ref-abc";
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (request, _) =>
        {
            Assert.Contains("DocNumber", request.RequestUri!.ToString(), StringComparison.Ordinal);
            return JsonResponse(HttpStatusCode.OK, "{\"QueryResponse\":{\"Invoice\":[{\"Id\":\"inv-200\",\"DocNumber\":\"" + reference + "\"}]}}");
        });

        var result = await connector.FindByReferenceAsync(reference);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.NotNull(result.Value);
        Assert.Equal("inv-200", result.Value!.ExternalId);
        Assert.Equal(reference, result.Value.Reference);
    }

    [Fact]
    public async Task FindByReferenceAsync_QueriesByTheDerivedDocNumber_NotTheRawRequestId()
    {
        var (connector, handler, _) = await BuildAsync();
        var requestId = Guid.NewGuid().ToString();
        var expectedDocNumber = QuickBooksOnlineConnector.DeriveDocNumber(requestId);

        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (request, _) =>
        {
            var uri = request.RequestUri!.ToString();
            Assert.Contains(Uri.EscapeDataString(expectedDocNumber), uri, StringComparison.Ordinal);
            Assert.DoesNotContain(Uri.EscapeDataString(requestId), uri, StringComparison.Ordinal);
            return JsonResponse(HttpStatusCode.OK, "{\"QueryResponse\":{\"Invoice\":[{\"Id\":\"inv-777\",\"DocNumber\":\"" + expectedDocNumber + "\"}]}}");
        });

        var result = await connector.FindByReferenceAsync(requestId);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(expectedDocNumber, result.Value!.Reference);
    }

    [Fact]
    public async Task FindByReferenceAsync_NotFound_ReturnsOkWithNoInvoice()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(HttpStatusCode.OK, """{"QueryResponse":{}}"""));

        var result = await connector.FindByReferenceAsync("missing-ref");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ReadStatusAsync_Paid_DerivesPaidFromZeroBalance_AndReadsTheLinkedPaymentDate()
    {
        var (connector, handler, _) = await BuildAsync();

        handler.When(HttpMethod.Get, $"v3/company/{Realm}/invoice/inv-300", (_, _) => JsonResponse(
            HttpStatusCode.OK,
            """{"Invoice":{"Id":"inv-300","DocNumber":"DOC-300","TotalAmt":500,"Balance":0,"TxnDate":"2026-03-01"}}"""));

        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (request, _) =>
        {
            Assert.Contains("Payment", request.RequestUri!.ToString(), StringComparison.Ordinal);
            return JsonResponse(HttpStatusCode.OK, """{"QueryResponse":{"Payment":[{"TxnDate":"2026-03-15"}]}}""");
        });

        var result = await connector.ReadStatusAsync("inv-300");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal("PAID", result.Value!.ExternalStatus);
        Assert.Equal(new DateOnly(2026, 3, 1), result.Value.IssuedDate);
        Assert.Equal(new DateOnly(2026, 3, 15), result.Value.PaidDate);
    }

    [Fact]
    public async Task ReadStatusAsync_UnpaidWithBalanceOutstanding_DerivesSubmitted()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/invoice/inv-301", (_, _) => JsonResponse(
            HttpStatusCode.OK, """{"Invoice":{"Id":"inv-301","DocNumber":"DOC-301","TotalAmt":500,"Balance":500}}"""));

        var result = await connector.ReadStatusAsync("inv-301");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal("SUBMITTED", result.Value!.ExternalStatus);
        Assert.Null(result.Value.PaidDate);
    }

    [Fact]
    public async Task ListContactsAsync_ReturnsEveryCustomer()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(
            HttpStatusCode.OK, """{"QueryResponse":{"Customer":[{"Id":"cust-9","DisplayName":"Acme"}]}}"""));

        var result = await connector.ListContactsAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var contact = Assert.Single(result.Value!);
        Assert.Equal("cust-9", contact.ExternalId);
        Assert.Equal("Acme", contact.Name);
    }

    [Fact]
    public async Task AuthorisationStateAsync_Authorised_WhenATokenIsStored()
    {
        var (connector, _, _) = await BuildAsync();

        var state = await connector.AuthorisationStateAsync();

        Assert.Equal(ConnectorAuthorisation.Authorised, state.Status);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_NoCompanyConnected_ReturnsReauthorise_NeverCallsQuickBooksOnline()
    {
        var (connector, handler, _) = await BuildAsync(tenantId: null);

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_NoRouteRegistered_TheStubRefuses_NoRealNetworkCallIsEverMade()
    {
        var (connector, _, _) = await BuildAsync();

        var exception = await Record.ExceptionAsync(() => connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString()));

        var invalidOperation = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("Network guard", invalidOperation.Message, StringComparison.Ordinal);
    }

    // ====================================================================
    // DeriveDocNumber (`WP 19.1A-R1` disclosure #2)
    // ====================================================================

    [Fact]
    public void DeriveDocNumber_Is21Characters_StartsWithTOS_AndIsUppercase()
    {
        var docNumber = QuickBooksOnlineConnector.DeriveDocNumber(Guid.NewGuid().ToString());

        Assert.Equal(21, docNumber.Length);
        Assert.StartsWith("TOS-", docNumber, StringComparison.Ordinal);
        Assert.Equal(docNumber, docNumber.ToUpperInvariant(), StringComparer.Ordinal);
    }

    [Fact]
    public void DeriveDocNumber_IsStable_TheSameIdAlwaysProducesTheSameNumber()
    {
        var id = Guid.NewGuid().ToString();

        Assert.Equal(QuickBooksOnlineConnector.DeriveDocNumber(id), QuickBooksOnlineConnector.DeriveDocNumber(id));
    }

    [Fact]
    public void DeriveDocNumber_DifferentIds_TypicallyProduceDifferentNumbers()
    {
        // A sample, not an exhaustive proof — the method's own remarks are
        // explicit that uniqueness across the whole id space is never
        // claimed, only stability for one given id.
        var ids = Enumerable.Range(0, 500).Select(_ => Guid.NewGuid().ToString()).ToList();
        var derived = ids.Select(QuickBooksOnlineConnector.DeriveDocNumber).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(ids.Count, derived.Count);
    }

    [Fact]
    public void DeriveDocNumber_AShortIdWithFewHexDigits_IsStillPaddedToExactly21Characters()
    {
        // "z-z-z-a1" carries exactly two hex digits ('a' and '1'); the rest
        // of the 17-character prefix is zero-padded rather than left short.
        var docNumber = QuickBooksOnlineConnector.DeriveDocNumber("z-z-z-a1");

        Assert.Equal(21, docNumber.Length);
        Assert.StartsWith("TOS-A1", docNumber, StringComparison.Ordinal);
        Assert.Equal(new string('0', 15), docNumber["TOS-A1".Length..]);
    }

    // ====================================================================
    // Fixtures
    // ====================================================================

    private static void StubExistingCustomer(StubHttpMessageHandler handler) =>
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(
            HttpStatusCode.OK, """{"QueryResponse":{"Customer":[{"Id":"cust-existing","DisplayName":"Fictional Client Ltd"}]}}"""));

    private static InvoiceRequestSnapshot Snapshot(Guid? requestId = null, string? clientName = "Fictional Client Ltd") => new(
        requestId ?? Guid.NewGuid(), "ORG-1", clientName, "PO-1", CurrencyCode.Gbp,
        [new InvoiceRequestLine("TimesheetEntry", Guid.NewGuid(), "Engineering time", 5m, new Money(100m, CurrencyCode.Gbp), new Money(500m, CurrencyCode.Gbp))],
        new Money(500m, CurrencyCode.Gbp));

    private static async Task<(QuickBooksOnlineConnector Connector, StubHttpMessageHandler Handler, InMemorySecretStore SecretStore)> BuildAsync(string? tenantId = Realm)
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox-quickbooks.api.intuit.com/") };
        var secretStore = new InMemorySecretStore();

        await secretStore.SetAsync("Invoicing:QuickBooksOnline:AccessToken", "seeded-access-token");
        await secretStore.SetAsync("Invoicing:QuickBooksOnline:RefreshToken", "seeded-refresh-token");
        await secretStore.SetAsync("Invoicing:QuickBooksOnline:ExpiresAtUtc", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));

        if (tenantId is not null)
            await secretStore.SetAsync("Invoicing:QuickBooksOnline:TenantId", tenantId);

        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>("Invoicing:QuickBooksOnline:ClientId", "client-abc")]))
            .Build();

        var profile = new OAuthProviderProfile(
            "QuickBooksOnline", new Uri("https://appcenter.intuit.com/connect/oauth2"), new Uri("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer"),
            ["com.intuit.quickbooks.accounting"]);

        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, new FakeBrowserLauncher(), httpClient);

        return (new QuickBooksOnlineConnector(httpClient, authoriser, configuration), handler, secretStore);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
