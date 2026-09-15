using Tempest.Core.Events;
using Tempest.Core.Settings;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.10G` (PO finding D4): Settings → Organisation identity round-
/// trips through <see cref="ISettingsProvider"/> exactly like every other
/// Desktop-local preference (`UserSettings`, `WindowUiState`), pre-fills
/// at the Tempest defaults on first run, and reads back as the flat
/// <see cref="OrganisationIdentity"/> snapshot <see cref="Documents.DocumentTemplate"/>
/// renders from.
/// </summary>
public sealed class OrganisationIdentitySettingsTests
{
    private static ISettingsProvider NewProvider() => new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

    [Fact]
    public void Constructed_BeforeAnyLoad_PreFillsTheTempestDefaults()
    {
        var settings = new OrganisationIdentitySettings(NewProvider());

        Assert.Equal("Tempest Design Engineering Ltd", settings.LegalName);
        Assert.Equal("17349874", settings.CompanyNumber);
        Assert.Equal("www.tempest-engineering.co.uk", settings.Website);
        Assert.Equal(string.Empty, settings.AddressLine1);
        Assert.Equal(string.Empty, settings.Email);
        Assert.Equal(string.Empty, settings.Phone);
    }

    [Fact]
    public async Task LoadAsync_FirstRun_NothingStored_LeavesTheTempestDefaults()
    {
        var provider = NewProvider();
        var settings = new OrganisationIdentitySettings(provider);

        await settings.LoadAsync();

        Assert.Equal(OrganisationIdentity.TempestDefaults, settings.ToIdentity());
    }

    [Fact]
    public async Task SaveThenLoad_OnASeparateInstance_RoundTripsEveryField()
    {
        var provider = NewProvider();

        var written = new OrganisationIdentitySettings(provider)
        {
            LegalName = "Acme Consulting Engineers Ltd",
            CompanyNumber = "01234567",
            Website = "www.acme-engineers.example",
            AddressLine1 = "1 Example Street",
            AddressLine2 = "Sometown, SM1 2AB",
            Email = "hello@acme-engineers.example",
            Phone = "+44 20 7946 0000",
        };
        await written.SaveAsync();

        var read = new OrganisationIdentitySettings(provider);
        await read.LoadAsync();

        Assert.Equal(written.LegalName, read.LegalName);
        Assert.Equal(written.CompanyNumber, read.CompanyNumber);
        Assert.Equal(written.Website, read.Website);
        Assert.Equal(written.AddressLine1, read.AddressLine1);
        Assert.Equal(written.AddressLine2, read.AddressLine2);
        Assert.Equal(written.Email, read.Email);
        Assert.Equal(written.Phone, read.Phone);
    }

    [Fact]
    public void ToIdentity_BlankOptionalFields_MapToNull_NeverEmptyStrings()
    {
        var settings = new OrganisationIdentitySettings(NewProvider())
        {
            CompanyNumber = "  ",
            Website = string.Empty,
            AddressLine1 = string.Empty,
            AddressLine2 = string.Empty,
            Email = string.Empty,
            Phone = string.Empty,
        };

        var identity = settings.ToIdentity();

        Assert.Null(identity.CompanyNumber);
        Assert.Null(identity.Website);
        Assert.Null(identity.AddressLine1);
        Assert.Null(identity.AddressLine2);
        Assert.Null(identity.Email);
        Assert.Null(identity.Phone);
        Assert.Equal("Tempest Design Engineering Ltd", identity.LegalName);
    }

    [Fact]
    public void ToIdentity_BlankLegalName_FallsBackToTheTempestDefault_NeverEmpty()
    {
        var settings = new OrganisationIdentitySettings(NewProvider()) { LegalName = "   " };

        Assert.Equal(OrganisationIdentity.TempestDefaults.LegalName, settings.ToIdentity().LegalName);
    }
}
