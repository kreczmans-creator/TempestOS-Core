using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// <see cref="AccountsCategoriser"/>'s own contract (`WP 19.8B`,
/// po-comments.md item 8): the Product Owner's own default keyword lists
/// (2026-09-14), the fixed check order for a name matching more than one
/// list, and every keyword list's own configuration override.
/// </summary>
public sealed class AccountsCategoriserTests
{
    [Theory]
    [InlineData("Adobe Creative Cloud Software Subscription", AccountsCategory.Software)]
    [InlineData("Microsoft 365 Licence", AccountsCategory.Software)]
    [InlineData("Annual SaaS Subscription", AccountsCategory.Software)]
    [InlineData("Office Equipment", AccountsCategory.Hardware)]
    [InlineData("Hardware Maintenance", AccountsCategory.Hardware)]
    [InlineData("New Computer", AccountsCategory.Hardware)]
    [InlineData("Office Rent", AccountsCategory.Premises)]
    [InlineData("Premises Insurance", AccountsCategory.Premises)]
    [InlineData("Electricity Utilities", AccountsCategory.Premises)]
    [InlineData("General Overheads", AccountsCategory.Other)]
    public void Categorise_DefaultKeywordLists_MatchesThePackageOwnAccountName(string accountName, AccountsCategory expected)
    {
        var categoriser = new AccountsCategoriser(EmptyConfiguration());

        Assert.Equal(expected, categoriser.Categorise(accountName));
    }

    [Fact]
    public void Categorise_NullOrBlankName_IsOther()
    {
        var categoriser = new AccountsCategoriser(EmptyConfiguration());

        Assert.Equal(AccountsCategory.Other, categoriser.Categorise(null));
        Assert.Equal(AccountsCategory.Other, categoriser.Categorise("   "));
    }

    [Fact]
    public void Categorise_ANameMatchingBothSoftwareAndPremisesKeywords_ResolvesToSoftware_TheFixedCheckOrder()
    {
        // "Office" is a Premises keyword, "software" is a Software keyword
        // — Software is checked first (Product Owner, 2026-09-14).
        var categoriser = new AccountsCategoriser(EmptyConfiguration());

        Assert.Equal(AccountsCategory.Software, categoriser.Categorise("Office software licence"));
    }

    [Fact]
    public void Categorise_ConfiguredOverride_ReplacesTheDefaultKeywordList_ForEachCategoryIndependently()
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new(AccountsCategoriser.HardwareKeywordsConfigurationKey, "server, laptop"),
                new(AccountsCategoriser.SoftwareKeywordsConfigurationKey, "saas"),
            ]))
            .Build();
        var categoriser = new AccountsCategoriser(configuration);

        // The configured Hardware list no longer includes "computer" (the
        // default) — a name matching only the old default now falls to
        // Other, and a name matching the new override matches Hardware.
        Assert.Equal(AccountsCategory.Other, categoriser.Categorise("New computer"));
        Assert.Equal(AccountsCategory.Hardware, categoriser.Categorise("Dell Laptop"));

        // The Software list is overridden too; Premises keeps its default
        // (not configured), proving each category's own override is
        // independent of the others.
        Assert.Equal(AccountsCategory.Other, categoriser.Categorise("Adobe Software Subscription"));
        Assert.Equal(AccountsCategory.Software, categoriser.Categorise("Generic SaaS Tool"));
        Assert.Equal(AccountsCategory.Premises, categoriser.Categorise("Office Rent"));
    }

    [Fact]
    public void Categorise_IsCaseInsensitive()
    {
        var categoriser = new AccountsCategoriser(EmptyConfiguration());

        Assert.Equal(AccountsCategory.Software, categoriser.Categorise("SOFTWARE LICENCE"));
    }

    private static IConfigurationProvider EmptyConfiguration() =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build();
}
