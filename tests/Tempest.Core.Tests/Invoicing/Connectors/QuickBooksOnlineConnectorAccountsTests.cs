using System.Net;
using System.Text;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.QuickBooksOnline;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// <c>QuickBooksOnlineConnector</c>'s own <see cref="IAccountsConnector"/>
/// contract, against recorded responses only (`WP 19.8B`, po-comments.md
/// item 8). Secondary to Xero (Product Owner, 2026-09-14: the consultancy
/// uses Xero) — kept to the brief's own three reads (bills, recurring
/// transactions, bank account balances) and the shared "unavailable when
/// not authorised" gate, without Xero's own wider edge-case coverage.
/// </summary>
public sealed class QuickBooksOnlineConnectorAccountsTests
{
    private const string Realm = "realm-1";

    [Fact]
    public async Task ListBillsDueAsync_Ok_MapsSupplierReferenceDatesAmountAndSynthesisedStatus()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"QueryResponse":{"Bill":[{"Id":"bill-1","DocNumber":"BILL-001","VendorRef":{"value":"v1","name":"Acme Supplies"},"TxnDate":"2026-03-01","DueDate":"2026-03-20","TotalAmt":500,"Balance":500,"CurrencyRef":{"value":"GBP"}}]}}
            """));

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var bill = Assert.Single(result.Value!);
        Assert.Equal("Acme Supplies", bill.Supplier);
        Assert.Equal("BILL-001", bill.Reference);
        Assert.Equal(new DateOnly(2026, 3, 1), bill.Issued);
        Assert.Equal(new DateOnly(2026, 3, 20), bill.Due);
        Assert.Equal(new Money(500m, CurrencyCode.Gbp), bill.Amount);
        Assert.Equal("OPEN", bill.Status);
    }

    [Fact]
    public async Task ListBillsDueAsync_AZeroBalance_IsSynthesisedAsPaid()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"QueryResponse":{"Bill":[{"Id":"bill-2","DocNumber":"BILL-002","VendorRef":{"name":"Acme"},"TxnDate":"2026-03-01","DueDate":"2026-03-20","TotalAmt":500,"Balance":0,"CurrencyRef":{"value":"GBP"}}]}}
            """));

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal("PAID", Assert.Single(result.Value!).Status);
    }

    [Fact]
    public async Task ListBillsDueAsync_FiltersOutABillDueAfterTheHorizon()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"QueryResponse":{"Bill":[
              {"Id":"in","DocNumber":"B1","VendorRef":{"name":"A"},"TxnDate":"2026-03-01","DueDate":"2026-03-29","TotalAmt":10,"Balance":10,"CurrencyRef":{"value":"GBP"}},
              {"Id":"out","DocNumber":"B2","VendorRef":{"name":"B"},"TxnDate":"2026-03-01","DueDate":"2026-04-05","TotalAmt":10,"Balance":10,"CurrencyRef":{"value":"GBP"}}
            ]}}
            """));

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 30);

        Assert.Equal("B1", Assert.Single(result.Value!).Reference);
    }

    [Fact]
    public async Task ListBillsDueAsync_NeverAuthorised_ReturnsUnavailable_NeverCallsQuickBooksOnline()
    {
        var (connector, handler, _) = await BuildUnauthorisedAsync();

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ListRepeatingBillsAsync_Ok_MapsTheTemplatedBill_AccountNameFromTheExpenseLineDetail()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"QueryResponse":{"RecurringTransaction":[{"Name":"Monthly Software","Type":"Bill",
              "ScheduleInfo":{"IntervalType":"Monthly","NextDate":"2026-04-01"},
              "Bill":{"VendorRef":{"name":"Contoso Cloud"},"CurrencyRef":{"value":"GBP"},
                "Line":[{"Amount":99,"DetailType":"AccountBasedExpenseLineDetail","Description":"Licence","AccountBasedExpenseLineDetail":{"AccountRef":{"value":"a1","name":"Software Subscriptions"}}}]}}]}}
            """));

        var result = await connector.ListRepeatingBillsAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var bill = Assert.Single(result.Value!);
        Assert.Equal("Contoso Cloud", bill.Supplier);
        Assert.Equal("Monthly Software", bill.Description);
        Assert.Equal(new Money(99m, CurrencyCode.Gbp), bill.Amount);
        Assert.Equal("Monthly", bill.Frequency);
        Assert.Equal(new DateOnly(2026, 4, 1), bill.NextDue);
        Assert.Equal("Software Subscriptions", bill.AccountName);
    }

    [Fact]
    public async Task ListRepeatingBillsAsync_SkipsARecurringInvoiceTemplate_OnlyBillTypeIsAPayable()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"QueryResponse":{"RecurringTransaction":[{"Name":"Monthly Retainer","Type":"Invoice","ScheduleInfo":{"IntervalType":"Monthly","NextDate":"2026-04-01"}}]}}
            """));

        var result = await connector.ListRepeatingBillsAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListRepeatingBillsAsync_SkipsATemplateWithNoNextDate()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"QueryResponse":{"RecurringTransaction":[{"Name":"No Schedule","Type":"Bill","ScheduleInfo":{"IntervalType":"Monthly"},
              "Bill":{"VendorRef":{"name":"X"},"CurrencyRef":{"value":"GBP"},"Line":[{"Amount":10,"DetailType":"AccountBasedExpenseLineDetail","Description":"X"}]}}]}}
            """));

        var result = await connector.ListRepeatingBillsAsync();

        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListRepeatingBillsAsync_NeverAuthorised_ReturnsUnavailable()
    {
        var (connector, handler, _) = await BuildUnauthorisedAsync();

        var result = await connector.ListRepeatingBillsAsync();

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ReadCashPositionAsync_Ok_ReadsTheCurrentBalanceDirectly()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, $"v3/company/{Realm}/query", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"QueryResponse":{"Account":[{"Name":"Business Chequing","AccountType":"Bank","CurrentBalance":15342.67,"CurrencyRef":{"value":"GBP"}}]}}
            """));

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var balance = Assert.Single(result.Value!);
        Assert.Equal("Business Chequing", balance.Name);
        Assert.Equal(new Money(15342.67m, CurrencyCode.Gbp), balance.Balance);
    }

    [Fact]
    public async Task ReadCashPositionAsync_NeverAuthorised_ReturnsUnavailable_NeverCallsQuickBooksOnline()
    {
        var (connector, handler, _) = await BuildUnauthorisedAsync();

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ReadCashPositionAsync_NoRealmConnected_ReturnsUnavailable()
    {
        var (connector, handler, _) = await BuildAsync(tenantId: null);

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    // ====================================================================
    // Fixtures
    // ====================================================================

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

    /// <summary>QuickBooks Online has never been authorised at all — no client id configured, no token stored.</summary>
    private static Task<(QuickBooksOnlineConnector Connector, StubHttpMessageHandler Handler, InMemorySecretStore SecretStore)> BuildUnauthorisedAsync()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox-quickbooks.api.intuit.com/") };
        var secretStore = new InMemorySecretStore();
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build();

        var profile = new OAuthProviderProfile(
            "QuickBooksOnline", new Uri("https://appcenter.intuit.com/connect/oauth2"), new Uri("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer"),
            ["com.intuit.quickbooks.accounting"]);

        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, new FakeBrowserLauncher(), httpClient);

        return Task.FromResult((new QuickBooksOnlineConnector(httpClient, authoriser, configuration), handler, secretStore));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
