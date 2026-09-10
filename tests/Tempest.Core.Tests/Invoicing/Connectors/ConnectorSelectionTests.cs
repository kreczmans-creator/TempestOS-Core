using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.QuickBooksOnline;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// <c>Invoicing:Connector</c> selection at composition (`WP 19.1A` part 2
/// brief §4): <c>"Fake"</c> (default), <c>"Xero"</c>,
/// <c>"QuickBooksOnline"</c> — and a missing client id never crashes
/// composition, only answers <see cref="ConnectorOutcome.Reauthorise"/>
/// with reason <c>"not configured"</c> at call time.
/// </summary>
public sealed class ConnectorSelectionTests
{
    private static readonly InvoiceRequestSnapshot Snapshot = new(
        Guid.NewGuid(), "ORG-1", "Org One Ltd", "PO-1", CurrencyCode.Gbp, [], new(0m, CurrencyCode.Gbp));

    [Fact]
    public async Task NoConnectorConfigured_BindsTheFakeConnector()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(temp.Path);

        Assert.IsType<FakeInvoicingConnector>(ConnectorHostFixture.Connector(host));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ConnectorConfiguredAsXero_BindsXeroConnector()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path,
            new(InvoicingService.ConnectorConfigurationKey, "Xero"),
            new("Invoicing:Xero:ClientId", "test-client-id"));

        Assert.IsType<XeroConnector>(ConnectorHostFixture.Connector(host));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ConnectorConfiguredAsQuickBooksOnline_BindsQuickBooksOnlineConnector()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path,
            new(InvoicingService.ConnectorConfigurationKey, "QuickBooksOnline"),
            new("Invoicing:QuickBooksOnline:ClientId", "test-client-id"));

        Assert.IsType<QuickBooksOnlineConnector>(ConnectorHostFixture.Connector(host));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task UnrecognisedConnectorValue_FallsBackToFake()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path,
            new KeyValuePair<string, string>(InvocingConnectorKey, "SomethingElse"));

        Assert.IsType<FakeInvoicingConnector>(ConnectorHostFixture.Connector(host));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task XeroWithNoClientIdConfigured_NeverCrashes_AnswersReauthoriseNotConfigured()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path, new KeyValuePair<string, string>(InvocingConnectorKey, "Xero"));

        var connector = ConnectorHostFixture.Connector(host);
        Assert.IsType<XeroConnector>(connector);

        var authorisation = await connector.AuthorisationStateAsync();
        Assert.Equal(ConnectorAuthorisation.NotAuthorised, authorisation.Status);
        Assert.Equal("not configured", authorisation.Detail);

        var result = await connector.CreateDraftInvoiceAsync(Snapshot, Guid.NewGuid().ToString());
        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Equal("not configured", result.Reason);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task QuickBooksOnlineWithNoClientIdConfigured_NeverCrashes_AnswersReauthoriseNotConfigured()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path, new KeyValuePair<string, string>(InvocingConnectorKey, "QuickBooksOnline"));

        var connector = ConnectorHostFixture.Connector(host);
        Assert.IsType<QuickBooksOnlineConnector>(connector);

        var result = await connector.CreateDraftInvoiceAsync(Snapshot, Guid.NewGuid().ToString());
        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Equal("not configured", result.Reason);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    // A local alias, purely to keep the fixture's own KeyValuePair
    // construction lines under a readable width.
    private const string InvocingConnectorKey = InvoicingService.ConnectorConfigurationKey;
}
