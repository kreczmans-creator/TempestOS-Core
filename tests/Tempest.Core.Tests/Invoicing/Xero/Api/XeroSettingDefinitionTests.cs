using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing.Xero.Api;

/// <summary>
/// `v0.24.0` B1 (verifier defect 7): <c>Xero.AllowLiveOrganisation</c> (D7)
/// is defined once, at start-up, by B1 — not lazily on the first write — and
/// <see cref="XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition"/>
/// is idempotent, so the Settings UI (U1) can ensure it too without throwing.
/// </summary>
public sealed class XeroSettingDefinitionTests
{
    [Fact]
    public async Task AXeroHost_DefinesAllowLiveOrganisation_AtStartUp_DefaultOff()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path,
            new(InvoicingService.ConnectorConfigurationKey, "Xero"),
            new("Invoicing:Xero:ClientId", "test-client-id"));

        try
        {
            var settings = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));

            // Already defined: no write has read it yet.
            Assert.Throws<DuplicateSettingDefinitionException>(() => settings.RegisterDefinition(
                new SettingDefinition(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, XeroWriteSafetyHandler.AllowLiveOrganisationDisplayName, "false")));
            Assert.Equal("false", await settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));

            // Idempotent for any later caller (U1).
            XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition(settings);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public void EnsureAllowLiveOrganisationDefinition_IsIdempotent()
    {
        var settings = new RecordingSettingsProvider();

        XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition(settings);
        XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition(settings);

        var definition = Assert.Single(settings.Definitions);
        Assert.Equal(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, definition.Key);
        Assert.Equal("false", definition.DefaultValue);
    }

    private sealed class RecordingSettingsProvider : ISettingsProvider
    {
        public List<ISettingDefinition> Definitions { get; } = [];

        public void RegisterDefinition(ISettingDefinition definition)
        {
            if (Definitions.Any(d => d.Key == definition.Key))
                throw new DuplicateSettingDefinitionException(definition.Key);

            Definitions.Add(definition);
        }

        public Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult("false");

        public Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
