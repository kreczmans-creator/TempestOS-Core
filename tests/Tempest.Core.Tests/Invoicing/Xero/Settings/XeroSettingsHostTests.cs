using Tempest.Core.BusinessGovernance;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing.Xero.Settings;

/// <summary>
/// `v0.24.0` X1 against the real host: with <c>Invoicing:Connector</c> =
/// <c>Xero</c>, the <c>RegisterSettings</c> hook registers the reader (the one
/// the D7 safety handler resolves), its file cache under the persistence
/// root, the tax-type resolver and the account-code map — each once
/// (`ADR-0122`: a second registration would fail start-up).
/// </summary>
public sealed class XeroSettingsHostTests
{
    [Fact]
    public async Task AXeroHost_ResolvesTheSettingsServices_WithTheCacheUnderThePersistenceRoot()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path,
            new(InvoicingService.ConnectorConfigurationKey, "Xero"),
            new("Invoicing:Xero:ClientId", "test-client-id"));

        try
        {
            var services = host.Services!;
            var reader = Assert.IsType<XeroSettingsReader>(services.GetService(typeof(IXeroSettingsReader)));
            Assert.Same(reader, services.GetService(typeof(IXeroSettingsReader)));
            var cache = Assert.IsType<FileXeroSettingsCache>(services.GetService(typeof(IXeroSettingsCache)));
            Assert.Equal(Path.Combine(temp.Path, "accounts", FileXeroSettingsCache.FileName), cache.FilePath);
            Assert.IsType<XeroTaxTypeResolver>(services.GetService(typeof(XeroTaxTypeResolver)));
            Assert.IsType<XeroAccountCodeMap>(services.GetService(typeof(XeroAccountCodeMap)));

            // Backlog X1-5: X1's choices are defined at start-up, so Settings lists them before any line resolves a code.
            var settings = (Tempest.Core.Settings.ISettingsProvider)services.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
            Assert.Equal("200", await settings.GetValueAsync(XeroAccountCodeMap.SalesSettingKey));
            Assert.Equal("OUTPUT2", await settings.GetValueAsync(XeroTaxTypeResolver.SettingKey(VatTaxDirection.Sales, VatRate.Standard)));

            // Never authorised: no tenant, so no reading and no network call.
            Assert.Null(await reader.ReadCachedAsync());
            Assert.Equal(ConnectorOutcome.Reauthorise, (await reader.RefreshAsync()).Outcome);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public void AddXeroSettings_RegistersEachServiceOnce()
    {
        var services = new ServiceCollection();

        XeroServiceRegistration.AddXeroSettings(services);

        Assert.Equal(
            [typeof(IXeroSettingsCache), typeof(IXeroSettingsReader), typeof(XeroAccountCodeMap), typeof(XeroTaxTypeResolver)],
            services.Descriptors.Select(d => d.ServiceType).OrderBy(t => t.Name, StringComparer.Ordinal));
        Assert.Throws<DuplicateServiceRegistrationException>(() => XeroServiceRegistration.AddXeroSettings(services));
    }
}
