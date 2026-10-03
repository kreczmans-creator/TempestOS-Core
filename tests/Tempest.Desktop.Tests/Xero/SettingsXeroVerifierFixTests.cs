using Avalonia.Headless.XUnit;
using Tempest.Core.Audit;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Settings;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.SettingsXeroSectionTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U1 verifier follow-ups: Re-authorise into another organisation
/// drops the previous organisation's reading; the bank account printed is
/// one in the organisation's base currency; Save before the section has
/// loaded never overwrites the stored switches.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SettingsXeroVerifierFixTests
{
    [AvaloniaFact]
    public async Task ReauthoriseIntoAnotherOrganisation_WithNoReadingForIt_DropsThePreviousOrganisationsDetails()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        var authoriser = new FakeAuthoriser { OnAuthorise = () => reader.Cached = null }; // another tenant: nothing cached for it
        await using var fixture = await SectionFixture.StartAsync(reader, services: s => new XeroSettingsSectionServices
        {
            Reader = s.Reader,
            Identity = s.Identity,
            Outbox = s.Outbox,
            Organisations = s.Organisations,
            Audit = s.Audit,
            TimeZone = s.TimeZone,
            Authoriser = authoriser,
            Connection = new FakeConnectionState(new ConnectorAuthorisationState(ConnectorAuthorisation.Authorised)),
        });
        fixture.Identity.LegalName = "Typed Ltd";
        Assert.Equal("Organisation: Demo Company (UK)", Text(fixture.Section, "Xero organisation"));
        Assert.Equal("Demo Company (UK) Limited", fixture.Identity.ToIdentity().LegalName);

        await fixture.Section.ReauthoriseAsync();

        Assert.Equal(XeroSettingsSection.ReauthorisedIntoAnotherOrganisationStatus, Text(fixture.Section, "Xero status"));
        Assert.Equal("Organisation: not read yet.", Text(fixture.Section, "Xero organisation"));
        Assert.Equal("Demo Company: unknown until Xero is read.", Text(fixture.Section, "Xero Demo Company"));
        Assert.Null(fixture.Identity.XeroCompanyDetails);
        var printed = fixture.Identity.ToIdentity();
        Assert.Equal(fixture.Identity.ToSettingsIdentity(), printed);
        Assert.Equal("Typed Ltd", printed.LegalName);
        Assert.Null(printed.VatNumber);
    }

    [AvaloniaFact]
    public async Task ReauthoriseIntoAnotherOrganisation_WithAReadingForIt_ShowsThatOrganisation()
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

        await fixture.Section.ReauthoriseAsync();

        Assert.Equal("Organisation: Tempest Live Ltd", Text(fixture.Section, "Xero organisation"));
        Assert.Equal("Demo Company: no — a live organisation.", Text(fixture.Section, "Xero Demo Company"));
        Assert.Equal("Tempest Live Ltd", fixture.Identity.ToIdentity().LegalName);
    }

    [Fact]
    public void Overlay_PrefersABankAccountInTheBaseCurrency_ThenAnyWithANumber()
    {
        var local = OrganisationIdentity.TempestDefaults with { BankSortCode = "11-22-33", BankAccountNumber = "999" };
        var xero = new XeroCompanyDetails(
            "Acme Ltd", null, null, [], null, null,
            [new XeroBankAccount("Credit card", null, "GBP"), new XeroBankAccount("USD account", "US-1", "USD"), new XeroBankAccount("GBP account", "GB-1", "GBP")],
            XeroTestReadings.ReadAt);

        Assert.Equal("GB-1", OrganisationIdentitySettings.Overlay(local, xero, "GBP").BankAccountNumber);
        Assert.Equal("GB-1", OrganisationIdentitySettings.Overlay(local, xero, " gbp ").BankAccountNumber);
        Assert.Equal("US-1", OrganisationIdentitySettings.Overlay(local, xero, "USD").BankAccountNumber);
        Assert.Equal("US-1", OrganisationIdentitySettings.Overlay(local, xero, "EUR").BankAccountNumber); // none in EUR: first with a number
        Assert.Equal("US-1", OrganisationIdentitySettings.Overlay(local, xero, null).BankAccountNumber);
    }

    [Fact]
    public void DocumentsPrint_TheBankAccountInTheOrganisationsBaseCurrency()
    {
        var demo = XeroTestReadings.Demo();
        var reading = demo with
        {
            Organisation = demo.Organisation with
            {
                BaseCurrency = "GBP",
                BankAccounts = [new XeroBankAccount("Credit card", null, "GBP"), new XeroBankAccount("USD account", "US-1", "USD"), new XeroBankAccount("GBP account", "GB-1", "GBP")],
            },
        };
        var identity = new OrganisationIdentitySettings(new SettingsXeroIdentityTests.InMemorySettings());

        identity.UseXeroReading(reading);

        Assert.Equal("GBP", identity.XeroBaseCurrency);
        Assert.Equal("GB-1", identity.ToIdentity().BankAccountNumber);
    }

    [AvaloniaFact]
    public async Task SaveBeforeTheSectionHasLoaded_LeavesTheStoredSwitchesAlone()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Live() };
        await using var fixture = await SectionFixture.StartAsync(reader);
        await fixture.Settings.SetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, "True");
        await fixture.Settings.SetValueAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey, "True");
        var unloaded = new XeroSettingsSection(fixture.Settings, new XeroSettingsSectionServices { Reader = reader, Audit = fixture.Resolve<IAuditRecorder>() });

        await unloaded.SaveAsync();

        Assert.Equal("True", await fixture.Settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));
        Assert.Equal("True", await fixture.Settings.GetValueAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey));
        var audit = await fixture.Resolve<IAuditQuery>().QueryAsync(new AuditQueryCriteria(action: XeroSettingsSection.AllowLiveOrganisationChangedAction));
        Assert.Empty(audit);

        // Once loaded, Save writes the switches as shown.
        await unloaded.RefreshAsync();
        CheckBox(unloaded, XeroSettingsSection.AllowLiveOrganisationName).IsChecked = false;
        await unloaded.SaveAsync();
        Assert.Equal("False", await fixture.Settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));
        Assert.Equal("True", await fixture.Settings.GetValueAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey));
    }
}
