using Avalonia.Headless.XUnit;
using Tempest.Core.Invoicing;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.SettingsXeroSectionTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` final cleanup (U1 minors): Re-authorise into the same
/// organisation keeps picker choices not yet saved, and into another one
/// says they were reset.
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
}
