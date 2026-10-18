using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Settings;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U1 (design §8 "PDF identity"): documents print the company
/// details from Xero — name, company number, VAT number, address, phone,
/// website and bank account — when a reading exists, and the Settings →
/// Organisation values without one (offline first run); Settings → Save
/// saves the Xero section with the rest of the page.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SettingsXeroIdentityTests
{
    [Fact]
    public void WithoutAReading_DocumentsUseTheTypedOrganisation_WithNoVatNumber()
    {
        var identity = new OrganisationIdentitySettings(new InMemorySettings()) { LegalName = "Typed Ltd", CompanyNumber = "555", BankSortCode = "11-22-33", BankAccountNumber = "99887766" };

        var printed = identity.ToIdentity();

        Assert.Equal(identity.ToSettingsIdentity(), printed);
        Assert.Equal("Typed Ltd", printed.LegalName);
        Assert.Null(printed.VatNumber);
        Assert.Equal("Typed Ltd · Company No. 555", printed.FooterLeft());
    }

    [Fact]
    public void WithAReading_DocumentsPrintXerosDetails_NeverMixedWithTypedOnes()
    {
        var identity = new OrganisationIdentitySettings(new InMemorySettings())
        {
            LegalName = "Typed Ltd", CompanyNumber = "555", Website = "typed.example", Email = "office@typed.example",
            AddressLine1 = "Typed street", BankSortCode = "11-22-33", BankAccountNumber = "99887766", BankIban = "GB00TYPED",
        };
        identity.UseXeroCompanyDetails(XeroCompanyDetails.From(XeroTestReadings.Demo()));

        var printed = identity.ToIdentity();

        Assert.Equal("Demo Company (UK) Limited", printed.LegalName);
        Assert.Equal("01234567", printed.CompanyNumber);
        Assert.Equal("GB 123 4567 89", printed.VatNumber);
        Assert.Equal("23 Main Street", printed.AddressLine1);
        Assert.Equal("Central City, Marineville, Oxfordshire OX1 1AA, United Kingdom", printed.AddressLine2);
        Assert.Equal("01234 567890", printed.Phone);
        Assert.Equal("www.demo.example", printed.Website);
        Assert.Equal("office@typed.example", printed.Email); // Xero holds no email
        Assert.Equal("Demo Company (UK) Limited", printed.BankAccountName);
        Assert.Equal("12-34-56 12345678", printed.BankAccountNumber);
        Assert.Null(printed.BankSortCode);
        Assert.Null(printed.BankIban);
        Assert.Equal(
            "Demo Company (UK) Limited · Company No. 01234567 · VAT No. GB 123 4567 89 · 23 Main Street · Central City, Marineville, Oxfordshire OX1 1AA, United Kingdom · office@typed.example · 01234 567890",
            printed.FooterLeft());

        // The typed values are untouched — back to them when the reading goes.
        Assert.Equal("Typed Ltd", identity.LegalName);
        identity.UseXeroCompanyDetails(null);
        Assert.Equal("Typed Ltd", identity.ToIdentity().LegalName);
    }

    [Fact]
    public void AXeroOrganisationWithoutDetails_PrintsNoTypedCompanyNumber_ButKeepsTypedBankDetails()
    {
        var reading = XeroTestReadings.Demo();
        reading = reading with
        {
            Organisation = reading.Organisation with { RegistrationNumber = null, TaxNumber = null, Address = null, Phone = null, Website = null, BankAccounts = [] },
        };
        var identity = new OrganisationIdentitySettings(new InMemorySettings()) { CompanyNumber = "555", BankSortCode = "11-22-33", BankAccountNumber = "99887766" };
        identity.UseXeroCompanyDetails(XeroCompanyDetails.From(reading));

        var printed = identity.ToIdentity();

        Assert.Equal("Demo Company (UK) Limited", printed.LegalName);
        Assert.Null(printed.CompanyNumber);
        Assert.Null(printed.VatNumber);
        Assert.Null(printed.AddressLine1);
        Assert.Equal("11-22-33", printed.BankSortCode);
        Assert.Equal("99887766", printed.BankAccountNumber);
    }

    [Fact]
    public async Task LoadXeroCompanyDetails_ReadsOnlyTheCache_AndNoReadingFallsBack()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        var identity = new OrganisationIdentitySettings(new InMemorySettings());

        var details = await identity.LoadXeroCompanyDetailsAsync(reader);

        Assert.NotNull(details);
        Assert.Equal(0, reader.RefreshCalls);
        Assert.Equal("GB 123 4567 89", identity.ToIdentity().VatNumber);

        reader.Cached = null; // another organisation connected, never read
        Assert.Null(await identity.LoadXeroCompanyDetailsAsync(reader));
        Assert.Equal(OrganisationIdentity.TempestDefaults.LegalName, identity.ToIdentity().LegalName);
    }

    [AvaloniaFact]
    public async Task SettingsView_ShowsTheXeroSection_SaysWhereDocumentsTakeDetailsFrom_AndSaveSavesIt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        Window? window = null;
        try
        {
            await host.StartAsync();
            var settings = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));
            var identity = new OrganisationIdentitySettings(settings);
            var section = new XeroSettingsSection(settings, new XeroSettingsSectionServices
            {
                Reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Live() },
                Identity = identity,
                Audit = (IAuditRecorder)host.Services!.GetService(typeof(IAuditRecorder)),
            });
            var view = new SettingsView(
                new ThemeService(settings), new UserSettings(settings), settings,
                (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider)), "(test)",
                organisationIdentity: identity, xeroSettings: section);
            window = new Window { Width = 1200, Height = 900, Content = view };
            window.Show();
            await view.RefreshAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Xero");
            var note = view.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Organisation identity source");
            Assert.True(note.IsVisible);
            Assert.StartsWith("Documents print the company details from Xero (from Xero, read at", note.Text, StringComparison.Ordinal);

            SettingsXeroSectionTests.CheckBox(view, XeroSettingsSection.AllowLiveOrganisationName).IsChecked = true;
            view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Save settings")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await SettingsXeroSectionTests.UntilAsync(() => settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey).GetAwaiter().GetResult() == "True");

            var audit = await ((IAuditQuery)host.Services!.GetService(typeof(IAuditQuery))).QueryAsync(new AuditQueryCriteria(action: XeroSettingsSection.AllowLiveOrganisationChangedAction));
            Assert.Single(audit);
        }
        finally
        {
            window?.Close();
            Dispatcher.UIThread.RunJobs();
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SettingsView_WithoutXero_ShowsNoXeroSection_AndNoSourceNote()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settings = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));
            var view = new SettingsView(
                new ThemeService(settings), new UserSettings(settings), settings,
                (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider)), "(test)",
                organisationIdentity: new OrganisationIdentitySettings(settings));
            await view.RefreshAsync();

            Assert.Empty(view.GetLogicalDescendants().OfType<XeroSettingsSection>());
            Assert.False(view.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Organisation identity source").IsVisible);

            // The Fake connector's host registers no Xero services: the application builds no section.
            Assert.Null(XeroSettingsSectionServices.FromServices(host.Services!));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    internal sealed class InMemorySettings : ISettingsProvider
    {
        private readonly Dictionary<string, string> _values = [];

        public void RegisterDefinition(ISettingDefinition definition)
        {
            if (!_values.TryAdd(definition.Key, definition.DefaultValue))
                throw new DuplicateSettingDefinitionException(definition.Key);
        }

        public Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default) =>
            _values.TryGetValue(key, out var value) ? Task.FromResult(value) : throw new SettingNotFoundException(key);

        public Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
    }
}
