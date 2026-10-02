using System.Text.Json.Nodes;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing.Xero.Settings;

/// <summary>
/// `v0.24.0` X1 (design §8): <c>{root}/accounts/xero-settings.json</c> is
/// schema-versioned, and a corrupt, incomplete or newer file reads as "no
/// reading" — never an exception.
/// </summary>
public sealed class FileXeroSettingsCacheTests
{
    private static readonly XeroSettingsReading Sample = new(
        XeroSettingsReading.CurrentSchemaVersion,
        "tenant-1",
        new XeroOrganisationProfile(
            "org-1", "Demo Company (UK)", "Demo Company (UK) Ltd", "GB 123 4567 89", "01234567",
            new XeroAddress("STREET", ["23 Main Street"], "Marineville", "Wessex", "MA12 3BC", "United Kingdom"),
            "01234 567890", "https://www.example.co.uk", "GBP", "GB", true, true,
            [new XeroBankAccount("Business Bank Account", "12-34-56 12345678", "GBP")]),
        [new XeroTaxRate("OUTPUT2", "20% (VAT on Income)", 20.0000m, "ACTIVE", true, false)],
        [new XeroAccount("acc-200", "200", "Sales", "REVENUE", "REVENUE", "ACTIVE", "OUTPUT2"), new XeroAccount("acc-bank", null, "Bank", "BANK", "ASSET", "ACTIVE", null)],
        new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task ASavedReading_RoundTrips_AndNoFileReadsAsNoReading()
    {
        using var temp = new TempDirectory();
        var cache = new FileXeroSettingsCache(Path.Combine(temp.Path, "accounts"));

        Assert.Null(await cache.ReadAsync());

        await cache.SaveAsync(Sample);
        Assert.Equivalent(Sample, await cache.ReadAsync(), strict: true);

        // Replaced, not appended; no temporary file left behind.
        var later = Sample with { ReadAtUtc = Sample.ReadAtUtc.AddDays(1) };
        await cache.SaveAsync(later);
        Assert.Equal(later.ReadAtUtc, (await cache.ReadAsync())!.ReadAtUtc);
        Assert.Equal([cache.FilePath], Directory.GetFiles(Path.Combine(temp.Path, "accounts")));
    }

    [Fact]
    public async Task TheConfiguredPersistenceRoot_DecidesWhereTheFileLives_BesideLastReading()
    {
        using var temp = new TempDirectory();
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, temp.Path)]))
            .Build();

        var cache = new FileXeroSettingsCache(configuration);
        await cache.SaveAsync(Sample);

        Assert.Equal(Path.Combine(temp.Path, "accounts", "xero-settings.json"), cache.FilePath);
        Assert.True(File.Exists(cache.FilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"SchemaVersion\": 1, \"TenantId\": ")]
    [InlineData("[]")]
    [InlineData("{ \"TenantId\": \"tenant-1\" }")]
    [InlineData("{ \"SchemaVersion\": \"one\" }")]
    [InlineData("{ \"SchemaVersion\": 0 }")]
    public async Task ACorruptFile_ReadsAsNoReading(string text)
    {
        using var temp = new TempDirectory();
        var cache = new FileXeroSettingsCache(temp.Path);
        await File.WriteAllTextAsync(cache.FilePath, text);

        Assert.Null(await cache.ReadAsync());
    }

    [Fact]
    public async Task AFileFromANewerBuild_ReadsAsNoReading_AndIsReplacedByTheNextSave()
    {
        using var temp = new TempDirectory();
        var cache = new FileXeroSettingsCache(temp.Path);
        await cache.SaveAsync(Sample);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(cache.FilePath))!.AsObject();
        json["SchemaVersion"] = XeroSettingsReading.CurrentSchemaVersion + 1;
        await File.WriteAllTextAsync(cache.FilePath, json.ToJsonString());

        Assert.Null(await cache.ReadAsync());

        await cache.SaveAsync(Sample);
        Assert.Equivalent(Sample, await cache.ReadAsync(), strict: true);
    }

    [Fact]
    public async Task UnknownProperties_AreIgnored()
    {
        using var temp = new TempDirectory();
        var cache = new FileXeroSettingsCache(temp.Path);
        await cache.SaveAsync(Sample);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(cache.FilePath))!.AsObject();
        json["AddedLater"] = "by a newer build at the same version";
        json["Organisation"]!["Nickname"] = "Demo";
        await File.WriteAllTextAsync(cache.FilePath, json.ToJsonString());

        Assert.Equivalent(Sample, await cache.ReadAsync(), strict: true);
    }

    [Theory]
    [InlineData("TenantId")]
    [InlineData("Organisation")]
    [InlineData("TaxRates")]
    [InlineData("Accounts")]
    [InlineData("Organisation.Name")]
    [InlineData("Organisation.OrganisationId")]
    [InlineData("Organisation.BankAccounts")]
    [InlineData("Organisation.Address.Lines")]
    // Backlog X1-1: value-typed fields read back as their default when missing.
    [InlineData("ReadAtUtc")]
    [InlineData("Organisation.PaysTax")]
    [InlineData("Organisation.IsDemoCompany")]
    [InlineData("TaxRates.0.CanApplyToRevenue")]
    [InlineData("TaxRates.0.CanApplyToExpenses")]
    [InlineData("TaxRates.0.EffectiveRate")]
    public async Task AFileMissingAFieldTheReadingNeeds_ReadsAsNoReading(string path)
    {
        using var temp = new TempDirectory();
        var cache = new FileXeroSettingsCache(temp.Path);
        await cache.SaveAsync(Sample);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(cache.FilePath))!.AsObject();
        var parts = path.Split('.');
        var parent = parts.Take(parts.Length - 1).Aggregate((JsonNode)json, (node, part) => int.TryParse(part, out var index) ? node[index]! : node[part]!).AsObject();
        parent.Remove(parts[^1]);
        await File.WriteAllTextAsync(cache.FilePath, json.ToJsonString());

        Assert.Null(await cache.ReadAsync());
    }

    [Fact]
    public async Task AFileWhoseReadAtIsTheDefaultDate_ReadsAsNoReading()
    {
        using var temp = new TempDirectory();
        var cache = new FileXeroSettingsCache(temp.Path);
        await cache.SaveAsync(Sample with { ReadAtUtc = default });

        Assert.Null(await cache.ReadAsync());
    }
}
