using System.Globalization;
using System.Net;
using System.Text;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.QuickBooksOnline;
using Tempest.Core.Invoicing.Xero;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// `WP 21.6P`: the two real connectors expose the interactive sign-in
/// through <see cref="IAuthorisableConnector"/>, so the Settings area's
/// <em>Authorise</em> button can run it. Before this Work Package
/// <see cref="OAuthAuthoriser.AuthoriseAsync"/> had no caller in the
/// product. Each fact runs the whole round trip — a fake browser that
/// follows the redirect back to the real loopback listener, a stub token
/// endpoint — and then reads the state back through the connector's own
/// <see cref="IInvoicingConnector.AuthorisationStateAsync"/>.
/// </summary>
public sealed class AuthorisableConnectorTests
{
    [Fact]
    public async Task Xero_AuthoriseAsync_RunsTheBrowserRoundTrip_ThenTheConnectorReportsAuthorised()
    {
        var (connector, handler, launcher) = BuildXero();
        handler.When(HttpMethod.Post, "identity.example.test/token", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600,"token_type":"Bearer"}"""));
        handler.When(HttpMethod.Get, "api.example.test/connections", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """[{"tenantId":"tenant-xyz","tenantName":"Acme"}]"""));

        var before = await connector.AuthorisationStateAsync();
        Assert.Equal(ConnectorAuthorisation.NotAuthorised, before.Status);

        IAuthorisableConnector authorisable = connector;
        var result = await authorisable.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        if (launcher.LastSimulatedRedirect is { } redirect)
            await redirect;

        var after = await connector.AuthorisationStateAsync();
        Assert.Equal(ConnectorAuthorisation.Authorised, after.Status);
    }

    [Fact]
    public async Task Xero_AuthoriseAsync_WhenTheOperatorDeniesConsent_TheConnectorStaysNotAuthorised_AndNothingThrows()
    {
        var (connector, _, launcher) = BuildXero();
        launcher.Error = "access_denied";

        IAuthorisableConnector authorisable = connector;
        var result = await authorisable.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Failed, result.Outcome);
        Assert.Contains("access_denied", result.Reason, StringComparison.Ordinal);
        if (launcher.LastSimulatedRedirect is { } redirect)
            await redirect;

        var after = await connector.AuthorisationStateAsync();
        Assert.Equal(ConnectorAuthorisation.NotAuthorised, after.Status);
    }

    [Fact]
    public async Task QuickBooksOnline_AuthoriseAsync_RunsTheBrowserRoundTrip_ThenTheConnectorReportsAuthorised()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.example.test/") };
        var secretStore = new InMemorySecretStore();
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>("Invoicing:QuickBooksOnline:ClientId", "client-qbo"),
                new KeyValuePair<string, string>(OAuthAuthoriser.LoopbackPortConfigurationKey, "0"),
            ]))
            .Build();
        var profile = new OAuthProviderProfile(
            "QuickBooksOnline", new Uri("https://appcenter.example.test/connect/oauth2"), new Uri("https://oauth.example.test/oauth2/v1/tokens/bearer"),
            ["com.intuit.quickbooks.accounting"]);
        var launcher = new FakeBrowserLauncher { RealmId = "realm-42" };
        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, launcher, httpClient);
        var connector = new QuickBooksOnlineConnector(httpClient, authoriser, configuration);

        handler.When(HttpMethod.Post, "oauth.example.test/oauth2/v1/tokens/bearer", (_, _) =>
            JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600,"token_type":"Bearer"}"""));

        Assert.Equal(ConnectorAuthorisation.NotAuthorised, (await connector.AuthorisationStateAsync()).Status);

        IAuthorisableConnector authorisable = connector;
        var result = await authorisable.AuthoriseAsync(TestTimeout());

        Assert.Equal(OAuthOutcome.Ok, result.Outcome);
        if (launcher.LastSimulatedRedirect is { } redirect)
            await redirect;
        Assert.Equal(ConnectorAuthorisation.Authorised, (await connector.AuthorisationStateAsync()).Status);
        Assert.Equal("realm-42", await secretStore.GetAsync("Invoicing:QuickBooksOnline:TenantId"));
    }

    [Fact]
    public void TheFakeConnector_IsDeliberatelyNotAuthorisable()
    {
        // It is always authorised and has no provider to sign in to; the
        // Settings area's Authorise button only refreshes its state.
        Assert.IsNotAssignableFrom<IAuthorisableConnector>(new FakeInvoicingConnector());
    }

    private static (XeroConnector Connector, StubHttpMessageHandler Handler, FakeBrowserLauncher Launcher) BuildXero()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/api.xro/2.0/") };
        var secretStore = new InMemorySecretStore();
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>("Invoicing:Xero:ClientId", "client-abc"),
                // A free ephemeral port, never the default: the default sits
                // in Windows' dynamic range and collides under load (`TD-183`).
                new KeyValuePair<string, string>(OAuthAuthoriser.LoopbackPortConfigurationKey, "0"),
            ]))
            .Build();
        var profile = new OAuthProviderProfile(
            "Xero", new Uri("https://login.example.test/identity/connect/authorize"), new Uri("https://identity.example.test/token"),
            ["accounting.transactions", "offline_access"], new Uri("https://api.example.test/connections"));
        var launcher = new FakeBrowserLauncher();
        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, launcher, httpClient);

        return (new XeroConnector(httpClient, authoriser, configuration), handler, launcher);
    }

    private static CancellationToken TestTimeout() => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
