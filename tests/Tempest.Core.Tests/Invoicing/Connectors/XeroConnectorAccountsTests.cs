using System.Net;
using System.Text;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// <c>XeroConnector</c>'s own <see cref="IAccountsConnector"/> contract,
/// against recorded responses only — no test in this class ever touches
/// the network (`WP 19.8B`, po-comments.md item 8). The Product Owner's
/// own priority (2026-09-14, the consultancy uses Xero): this is the
/// fuller of the two connectors' accounts-read contract tests, covering
/// bills (ACCPAY invoices), repeating invoices (subscriptions, including
/// the tracking-category and account-code fallbacks
/// <see cref="AccountsCategoriser"/> reads) and the Bank Summary report's
/// own defensive column lookup, plus the shared "unavailable when not
/// authorised" gate every <see cref="IAccountsConnector"/> member honours.
/// </summary>
public sealed class XeroConnectorAccountsTests
{
    [Fact]
    public async Task ListBillsDueAsync_Ok_MapsSupplierReferenceDatesAmountAndStatus()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Invoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"Invoices":[{"InvoiceID":"bill-1","InvoiceNumber":"BILL-001","Reference":"SUP-REF-1","Contact":{"Name":"Acme Supplies"},"Status":"AUTHORISED","Date":"2026-03-01","DueDate":"2026-03-20","Total":452.10,"CurrencyCode":"GBP"}]}
            """));

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var bill = Assert.Single(result.Value!);
        Assert.Equal("Acme Supplies", bill.Supplier);
        Assert.Equal("SUP-REF-1", bill.Reference);
        Assert.Equal(new DateOnly(2026, 3, 1), bill.Issued);
        Assert.Equal(new DateOnly(2026, 3, 20), bill.Due);
        Assert.Equal(new Money(452.10m, CurrencyCode.Gbp), bill.Amount);
        Assert.Equal("AUTHORISED", bill.Status);
    }

    [Fact]
    public async Task ListBillsDueAsync_FallsBackToInvoiceNumber_WhenReferenceIsBlank()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Invoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"Invoices":[{"InvoiceID":"bill-2","InvoiceNumber":"BILL-002","Contact":{"Name":"Acme Supplies"},"Status":"AUTHORISED","Date":"2026-03-01","DueDate":"2026-03-20","Total":100,"CurrencyCode":"GBP"}]}
            """));

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal("BILL-002", Assert.Single(result.Value!).Reference);
    }

    [Fact]
    public async Task ListBillsDueAsync_FiltersOutABillDueAfterTheHorizon()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Invoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"Invoices":[
              {"InvoiceID":"in-range","InvoiceNumber":"B1","Contact":{"Name":"A"},"Status":"AUTHORISED","Date":"2026-03-01","DueDate":"2026-03-29","Total":10,"CurrencyCode":"GBP"},
              {"InvoiceID":"out-of-range","InvoiceNumber":"B2","Contact":{"Name":"B"},"Status":"AUTHORISED","Date":"2026-03-01","DueDate":"2026-04-05","Total":10,"CurrencyCode":"GBP"}
            ]}
            """));

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 30);

        var bill = Assert.Single(result.Value!);
        Assert.Equal("B1", bill.Reference);
        Assert.Equal(new DateOnly(2026, 3, 29), bill.Due);
    }

    [Fact]
    public async Task ListBillsDueAsync_SkipsAnInvoiceWithNoCurrencyCode_RatherThanInventingOne()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Invoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"Invoices":[{"InvoiceID":"no-currency","InvoiceNumber":"B1","Contact":{"Name":"A"},"Status":"AUTHORISED","Date":"2026-03-01","DueDate":"2026-03-20","Total":10}]}
            """));

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListBillsDueAsync_NoTenantConnected_ReturnsUnavailable_NeverCallsXero()
    {
        var (connector, handler, _) = await BuildAsync(tenantId: null);

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ListBillsDueAsync_NeverAuthorised_ReturnsUnavailable_NotReauthorise()
    {
        var (connector, handler, _) = await BuildUnauthorisedAsync();

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ListBillsDueAsync_401FromALiveCall_StillMapsToReauthorise_TheOrdinaryHttpMapping()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Invoices?where=", (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await connector.ListBillsDueAsync(new DateOnly(2026, 3, 1), 90);

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
    }

    [Fact]
    public async Task ListRepeatingBillsAsync_Ok_MapsEveryField_AccountNameFromTheTrackingCategoryOption()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "RepeatingInvoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"RepeatingInvoices":[{"Contact":{"Name":"Contoso Cloud"},"Reference":"REF-1","Status":"AUTHORISED","CurrencyCode":"GBP","Total":99.00,
              "Schedule":{"Unit":"MONTHLY","NextScheduledDate":"2026-04-01"},
              "LineItems":[{"Description":"Software licence","LineAmount":99.00,"AccountCode":"400","Tracking":[{"Name":"Cost Centre","Option":"Software"}]}]}]}
            """));

        var result = await connector.ListRepeatingBillsAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var bill = Assert.Single(result.Value!);
        Assert.Equal("Contoso Cloud", bill.Supplier);
        Assert.Equal("Software licence", bill.Description);
        Assert.Equal(new Money(99.00m, CurrencyCode.Gbp), bill.Amount);
        Assert.Equal("MONTHLY", bill.Frequency);
        Assert.Equal(new DateOnly(2026, 4, 1), bill.NextDue);
        Assert.Equal("Software", bill.AccountName);
    }

    [Fact]
    public async Task ListRepeatingBillsAsync_NoTrackingCategory_FallsBackToTheAccountCode()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "RepeatingInvoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"RepeatingInvoices":[{"Contact":{"Name":"Landlord Co"},"Status":"AUTHORISED","CurrencyCode":"GBP","Total":1200,
              "Schedule":{"Unit":"MONTHLY","NextScheduledDate":"2026-04-01"},
              "LineItems":[{"Description":"Office rent","LineAmount":1200,"AccountCode":"720"}]}]}
            """));

        var result = await connector.ListRepeatingBillsAsync();

        Assert.Equal("720", Assert.Single(result.Value!).AccountName);
    }

    [Fact]
    public async Task ListRepeatingBillsAsync_SumsLineAmounts_WhenTotalIsNotStated()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "RepeatingInvoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"RepeatingInvoices":[{"Contact":{"Name":"Supplier"},"Status":"AUTHORISED","CurrencyCode":"GBP",
              "Schedule":{"Unit":"MONTHLY","NextScheduledDate":"2026-04-01"},
              "LineItems":[{"Description":"Part A","LineAmount":40},{"Description":"Part B","LineAmount":60}]}]}
            """));

        var result = await connector.ListRepeatingBillsAsync();

        Assert.Equal(new Money(100m, CurrencyCode.Gbp), Assert.Single(result.Value!).Amount);
    }

    [Fact]
    public async Task ListRepeatingBillsAsync_SkipsARepeatingInvoiceWithNoScheduledDate()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "RepeatingInvoices?where=", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"RepeatingInvoices":[{"Contact":{"Name":"No Schedule Ltd"},"Status":"DRAFT","CurrencyCode":"GBP","Total":10,"Schedule":{"Unit":"MONTHLY"}}]}
            """));

        var result = await connector.ListRepeatingBillsAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
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
    public async Task ReadCashPositionAsync_Ok_FindsTheClosingBalanceColumnByHeaderTitle_SkipsTheSummaryRow()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Reports/BankSummary", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"Reports":[{"ReportDate":"2026-03-30","Rows":[
              {"RowType":"Header","Cells":[{"Value":""},{"Value":"Opening Balance"},{"Value":"Cash Received"},{"Value":"Cash Spent"},{"Value":"Closing Balance"}]},
              {"RowType":"Section","Title":"Bank","Rows":[
                {"RowType":"Row","Cells":[{"Value":"Business Current Account"},{"Value":"1000.00"},{"Value":"500.00"},{"Value":"200.00"},{"Value":"1300.00"}]},
                {"RowType":"SummaryRow","Cells":[{"Value":"Total"},{"Value":"1000.00"},{"Value":"500.00"},{"Value":"200.00"},{"Value":"1300.00"}]}
              ]}
            ]}]}
            """));

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        var balance = Assert.Single(result.Value!);
        Assert.Equal("Business Current Account", balance.Name);
        Assert.Equal(new Money(1300.00m, CurrencyCode.Gbp), balance.Balance);
        Assert.Equal(new DateOnly(2026, 3, 30), balance.AsOf);
    }

    [Fact]
    public async Task ReadCashPositionAsync_MultipleAccounts_ReadsEachOne()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.When(HttpMethod.Get, "Reports/BankSummary", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"Reports":[{"ReportDate":"2026-03-30","Rows":[
              {"RowType":"Header","Cells":[{"Value":""},{"Value":"Closing Balance"}]},
              {"RowType":"Section","Title":"Bank","Rows":[
                {"RowType":"Row","Cells":[{"Value":"Current Account"},{"Value":"1300.00"}]},
                {"RowType":"Row","Cells":[{"Value":"Savings Account"},{"Value":"9000.00"}]}
              ]}
            ]}]}
            """));

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value!, a => a.Name == "Current Account" && a.Balance == new Money(1300.00m, CurrencyCode.Gbp));
        Assert.Contains(result.Value!, a => a.Name == "Savings Account" && a.Balance == new Money(9000.00m, CurrencyCode.Gbp));
    }

    [Fact]
    public async Task ReadCashPositionAsync_ConfiguredBaseCurrency_IsUsedInsteadOfTheDefaultGbp()
    {
        var (connector, handler, _) = await BuildAsync(extraConfiguration: [new(XeroConnector.BaseCurrencyConfigurationKey, "EUR")]);
        handler.When(HttpMethod.Get, "Reports/BankSummary", (_, _) => JsonResponse(HttpStatusCode.OK, """
            {"Reports":[{"ReportDate":"2026-03-30","Rows":[
              {"RowType":"Header","Cells":[{"Value":""},{"Value":"Closing Balance"}]},
              {"RowType":"Section","Rows":[{"RowType":"Row","Cells":[{"Value":"EUR Account"},{"Value":"500.00"}]}]}
            ]}]}
            """));

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(new CurrencyCode("EUR"), Assert.Single(result.Value!).Balance.Currency);
    }

    [Fact]
    public async Task ReadCashPositionAsync_NeverAuthorised_ReturnsUnavailable_NeverCallsXero()
    {
        var (connector, handler, _) = await BuildUnauthorisedAsync();

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ReadCashPositionAsync_NoTenantConnected_ReturnsUnavailable()
    {
        var (connector, handler, _) = await BuildAsync(tenantId: null);

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ReadCashPositionAsync_ATransportTimeout_MapsToUnavailable()
    {
        var (connector, handler, _) = await BuildAsync();
        handler.WhenThrows(HttpMethod.Get, "Reports/BankSummary", new TaskCanceledException("The request timed out."));

        var result = await connector.ReadCashPositionAsync();

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
    }

    // ====================================================================
    // Fixtures
    // ====================================================================

    private static async Task<(XeroConnector Connector, StubHttpMessageHandler Handler, InMemorySecretStore SecretStore)> BuildAsync(
        string? tenantId = "tenant-1", IReadOnlyList<KeyValuePair<string, string>>? extraConfiguration = null)
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") };
        var secretStore = new InMemorySecretStore();

        await secretStore.SetAsync("Invoicing:Xero:AccessToken", "seeded-access-token");
        await secretStore.SetAsync("Invoicing:Xero:RefreshToken", "seeded-refresh-token");
        await secretStore.SetAsync("Invoicing:Xero:ExpiresAtUtc", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));

        if (tenantId is not null)
            await secretStore.SetAsync("Invoicing:Xero:TenantId", tenantId);

        var entries = new List<KeyValuePair<string, string>> { new("Invoicing:Xero:ClientId", "client-abc") };
        if (extraConfiguration is not null)
            entries.AddRange(extraConfiguration);

        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(entries)).Build();

        var profile = new OAuthProviderProfile(
            "Xero", new Uri("https://login.xero.com/identity/connect/authorize"), new Uri("https://identity.xero.com/connect/token"),
            ["accounting.transactions"], new Uri("https://api.xero.com/connections"));

        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, new FakeBrowserLauncher(), httpClient);

        return (new XeroConnector(httpClient, authoriser, configuration), handler, secretStore);
    }

    /// <summary>Xero has never been authorised at all — no client id configured, no token stored.</summary>
    private static Task<(XeroConnector Connector, StubHttpMessageHandler Handler, InMemorySecretStore SecretStore)> BuildUnauthorisedAsync()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") };
        var secretStore = new InMemorySecretStore();
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build();

        var profile = new OAuthProviderProfile(
            "Xero", new Uri("https://login.xero.com/identity/connect/authorize"), new Uri("https://identity.xero.com/connect/token"),
            ["accounting.transactions"], new Uri("https://api.xero.com/connections"));

        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, new FakeBrowserLauncher(), httpClient);

        return Task.FromResult((new XeroConnector(httpClient, authoriser, configuration), handler, secretStore));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
