using System.Net;
using System.Text;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// <c>XeroConnector</c>'s own contract, against recorded responses only —
/// no test in this class ever touches the network (`WP 19.1A` part 2 brief
/// §3): create ok/rejected/401/429/timeout, find-by-reference found/not
/// found, and a paid status reading, each mapped to the outcome §2 names.
/// </summary>
public sealed class XeroConnectorTests
{
    [Fact]
    public async Task CreateDraftInvoiceAsync_Ok_CarriesTheRequestIdAsReferenceAndIdempotencyKey_AndReturnsTheCreatedInvoice()
    {
        var (connector, handler, _) = await BuildAsync();
        var requestId = Guid.NewGuid();
        var idempotencyKey = requestId.ToString();

        handler.When(HttpMethod.Post, "Invoices", (request, body) =>
        {
            Assert.Contains($"\"Reference\":\"{idempotencyKey}\"", body, StringComparison.Ordinal);
            Assert.Equal(idempotencyKey, request.Headers.GetValues("Idempotency-Key").Single());
            Assert.Equal("Bearer seeded-access-token", request.Headers.Authorization!.ToString());
            Assert.Equal("tenant-1", request.Headers.GetValues("xero-tenant-id").Single());

            return JsonResponse(HttpStatusCode.OK, $$"""{"Invoices":[{"InvoiceID":"inv-001","InvoiceNumber":"INV-0001","Reference":"{{idempotencyKey}}","Status":"DRAFT"}]}""");
        });

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(requestId), idempotencyKey);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal("inv-001", result.Value!.ExternalId);
        Assert.Equal("INV-0001", result.Value.ExternalInvoiceNumber);
        Assert.Equal(idempotencyKey, result.Value.Reference);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_Rejected_ReturnsTheValidationMessageFromTheBody()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Post, "Invoices", (_, _) => JsonResponse(
            HttpStatusCode.BadRequest,
            """{"Message":"A validation exception occurred","Elements":[{"ValidationErrors":[{"Message":"The contact name is required."}]}]}"""));

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Equal("The contact name is required.", result.Reason);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_401_MapsToReauthorise()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Post, "Invoices", (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_429_MapsToUnavailable()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Post, "Invoices", (_, _) => new HttpResponseMessage((HttpStatusCode)429));

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task CreateDraftInvoiceAsync_ATransportTimeout_MapsToUnavailable()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.WhenThrows(HttpMethod.Post, "Invoices", new TaskCanceledException("The request timed out."));

        var result = await connector.CreateDraftInvoiceAsync(Snapshot(), Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task FindByReferenceAsync_Found_ReturnsTheInvoice()
    {
        var (connector, handler, _) = await BuildAsync();
        const string reference = "ref-abc";
        handler.When(HttpMethod.Get, "Invoices?where=", (_, _) =>
            JsonResponse(HttpStatusCode.OK, $$"""{"Invoices":[{"InvoiceID":"inv-002","InvoiceNumber":"INV-0002","Reference":"{{reference}}"}]}"""));

        var result = await connector.FindByReferenceAsync(reference);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.NotNull(result.Value);
        Assert.Equal("inv-002", result.Value!.ExternalId);
        Assert.Equal(reference, result.Value.Reference);
    }

    [Fact]
    public async Task FindByReferenceAsync_NotFound_ReturnsOkWithNoInvoice()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Invoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """{"Invoices":[]}"""));

        var result = await connector.FindByReferenceAsync("missing-ref");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ReadStatusAsync_Paid_ReturnsThePaidDate()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Invoices/inv-003", (_, _) => JsonResponse(
            HttpStatusCode.OK,
            """{"Invoices":[{"InvoiceID":"inv-003","InvoiceNumber":"INV-0003","Status":"PAID","Date":"2026-03-01","FullyPaidOnDate":"2026-03-15"}]}"""));

        var result = await connector.ReadStatusAsync("inv-003");

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal("PAID", result.Value!.ExternalStatus);
        Assert.Equal("INV-0003", result.Value.ExternalInvoiceNumber);
        Assert.Equal(new DateOnly(2026, 3, 1), result.Value.IssuedDate);
        Assert.Equal(new DateOnly(2026, 3, 15), result.Value.PaidDate);
    }

    [Fact]
    public async Task ReadStatusAsync_ParsesXeroOwnLegacyDateFormat()
    {
        var (connector, handler, _) = await BuildAsync();
        var paidOn = new DateOnly(2026, 2, 15);
        var ms = new DateTimeOffset(paidOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

        handler.When(HttpMethod.Get, "Invoices/inv-004", (_, _) => JsonResponse(
            HttpStatusCode.OK, $$"""{"Invoices":[{"InvoiceID":"inv-004","Status":"PAID","FullyPaidOnDate":"/Date({{ms}}+0000)/"}]}"""));

        var result = await connector.ReadStatusAsync("inv-004");

        Assert.Equal(paidOn, result.Value!.PaidDate);
    }

    [Fact]
    public async Task ListContactsAsync_ReturnsEveryContact()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Contacts", (_, _) => JsonResponse(HttpStatusCode.OK, """{"Contacts":[{"ContactID":"c-1","Name":"Acme"}]}"""));

        var result = await connector.ListContactsAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var contact = Assert.Single(result.Value!);
        Assert.Equal("c-1", contact.ExternalId);
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
    public async Task CreateDraftInvoiceAsync_NoTenantConnected_ReturnsReauthorise_NeverCallsXero()
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
    // Fixtures
    // ====================================================================

    private static InvoiceRequestSnapshot Snapshot(Guid? requestId = null) => new(
        requestId ?? Guid.NewGuid(), "ORG-1", "PO-1", CurrencyCode.Gbp,
        [new InvoiceRequestLine("TimesheetEntry", Guid.NewGuid(), "Engineering time", 5m, new Money(100m, CurrencyCode.Gbp), new Money(500m, CurrencyCode.Gbp))],
        new Money(500m, CurrencyCode.Gbp));

    private static async Task<(XeroConnector Connector, StubHttpMessageHandler Handler, InMemorySecretStore SecretStore)> BuildAsync(string? tenantId = "tenant-1")
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") };
        var secretStore = new InMemorySecretStore();

        await secretStore.SetAsync("Invoicing:Xero:AccessToken", "seeded-access-token");
        await secretStore.SetAsync("Invoicing:Xero:RefreshToken", "seeded-refresh-token");
        await secretStore.SetAsync("Invoicing:Xero:ExpiresAtUtc", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));

        if (tenantId is not null)
            await secretStore.SetAsync("Invoicing:Xero:TenantId", tenantId);

        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>("Invoicing:Xero:ClientId", "client-abc")]))
            .Build();

        var profile = new OAuthProviderProfile(
            "Xero", new Uri("https://login.xero.com/identity/connect/authorize"), new Uri("https://identity.xero.com/connect/token"),
            ["accounting.transactions"], new Uri("https://api.xero.com/connections"));

        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, new FakeBrowserLauncher(), httpClient);

        return (new XeroConnector(httpClient, authoriser), handler, secretStore);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
