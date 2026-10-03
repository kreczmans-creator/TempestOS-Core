using Avalonia.Headless.XUnit;
using Tempest.Core.Audit;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.SettingsXeroSectionTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` final cleanup (U1 minors): Re-authorise into the same
/// organisation keeps picker choices not yet saved, and into another one
/// says they were reset; a Save after a partial load says the two switches
/// were not saved.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SettingsXeroCleanupTests
{
    [AvaloniaFact]
    public async Task ReauthoriseIntoTheSameOrganisation_KeepsAPickerChoiceNotYetSaved()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        var authoriser = new FakeAuthoriser { OnAuthorise = () => reader.Cached = XeroTestReadings.Demo() };
        await using var fixture = await SectionFixture.StartAsync(reader, services: s => new XeroSettingsSectionServices
        {
            Reader = s.Reader,
            Identity = s.Identity,
            Audit = s.Audit,
            TimeZone = s.TimeZone,
            Authoriser = authoriser,
            Connection = new FakeConnectionState(new ConnectorAuthorisationState(ConnectorAuthorisation.Authorised)),
        });
        var sales = ComboBox(fixture.Section, XeroSettingsSection.SalesAccountName);
        Choose(sales, "260");

        await fixture.Section.ReauthoriseAsync();

        Assert.Equal("260", ((Avalonia.Controls.ComboBoxItem)ComboBox(fixture.Section, XeroSettingsSection.SalesAccountName).SelectedItem!).Tag);
        Assert.Equal("Xero re-authorised.", Text(fixture.Section, "Xero status"));
    }

    [AvaloniaFact]
    public async Task ReauthoriseIntoAnotherOrganisation_ReloadsThePickers_AndSaysUnsavedChoicesWereReset()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        var authoriser = new FakeAuthoriser { OnAuthorise = () => reader.Cached = XeroTestReadings.Live() with { TenantId = "tenant-2" } };
        await using var fixture = await SectionFixture.StartAsync(reader, services: s => new XeroSettingsSectionServices
        {
            Reader = s.Reader,
            Identity = s.Identity,
            Audit = s.Audit,
            TimeZone = s.TimeZone,
            Authoriser = authoriser,
            Connection = new FakeConnectionState(new ConnectorAuthorisationState(ConnectorAuthorisation.Authorised)),
        });
        Choose(ComboBox(fixture.Section, XeroSettingsSection.SalesAccountName), "260");

        await fixture.Section.ReauthoriseAsync();

        Assert.NotEqual("260", ((Avalonia.Controls.ComboBoxItem)ComboBox(fixture.Section, XeroSettingsSection.SalesAccountName).SelectedItem!).Tag);
        Assert.Equal(XeroSettingsSection.ReauthorisedIntoAnotherOrganisationStatus, Text(fixture.Section, "Xero status"));
        Assert.Equal("Organisation: Tempest Live Ltd", Text(fixture.Section, "Xero organisation"));
    }

    [AvaloniaFact]
    public async Task SaveAfterAPartialLoad_SaysTheSwitchesWereNotSaved_AndKeepsThemStored()
    {
        var good = new FakeXeroSettingsReader { Cached = XeroTestReadings.Live() };
        await using var fixture = await SectionFixture.StartAsync(good);
        await fixture.Settings.SetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, "True");
        await fixture.Settings.SetValueAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey, "True");
        var partial = new XeroSettingsSection(fixture.Settings, new XeroSettingsSectionServices { Reader = new ThrowingReader(), Audit = fixture.Resolve<IAuditRecorder>() });
        var told = new List<(string Message, ActionOutcome Outcome)>();
        partial.ActionCompleted += (message, outcome) => told.Add((message, outcome));

        await Assert.ThrowsAsync<IOException>(() => partial.RefreshAsync());
        CheckBox(partial, XeroSettingsSection.IncludeOnlineName).IsChecked = false;
        await partial.SaveAsync();

        Assert.Equal(XeroSettingsSection.SwitchesNotSavedStatus, Text(partial, "Xero status"));
        Assert.Contains((XeroSettingsSection.SwitchesNotSavedStatus, ActionOutcome.Failed), told);
        Assert.Equal("True", await fixture.Settings.GetValueAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey));
        Assert.Equal("True", await fixture.Settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));
    }

    /// <summary>A reader whose cache cannot be read, so <see cref="XeroSettingsSection.RefreshAsync"/> stops part-way.</summary>
    private sealed class ThrowingReader : IXeroSettingsReader
    {
        public Task<XeroSettingsReading?> ReadCachedAsync(CancellationToken cancellationToken = default) =>
            throw new IOException("The cached reading could not be read.");

        public Task<ConnectorResult<XeroSettingsReading>> RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ConnectorResult<XeroSettingsReading>.Unavailable("offline"));
    }
}
