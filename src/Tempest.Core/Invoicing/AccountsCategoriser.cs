using Tempest.Core.Configuration;

namespace Tempest.Core.Invoicing;

/// <summary>
/// The four subscription buckets the Business dashboard's payable panel
/// groups repeating bills into (`WP 19.8B`, po-comments.md item 8; brief
/// §5, "Hardware · Software · Premises").
/// </summary>
public enum AccountsCategory
{
    /// <summary>Equipment, hardware, computers.</summary>
    Hardware,

    /// <summary>Software, subscriptions, licences.</summary>
    Software,

    /// <summary>Rent, premises, office, utilities.</summary>
    Premises,

    /// <summary>Matches none of the configured keyword lists.</summary>
    Other,
}

/// <summary>
/// Categorises a repeating bill's own <see cref="RepeatingBill.AccountName"/>
/// into an <see cref="AccountsCategory"/> by keyword — configurable, with
/// sensible defaults modelled on Xero's own account names and tracking
/// categories (Product Owner, 2026-09-14, `WP 19.8B`).
/// </summary>
/// <remarks>
/// Checked in a fixed order — Software, then Hardware, then Premises,
/// then <see cref="AccountsCategory.Other"/> — so an account name that
/// happens to match more than one list (for example "Office software
/// licence", which carries both a Software and a Premises keyword) still
/// resolves to exactly one category, deterministically, the same order
/// the Product Owner's own examples were given in.
/// </remarks>
public sealed class AccountsCategoriser
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming the comma-separated keyword list for <see cref="AccountsCategory.Hardware"/>.</summary>
    public const string HardwareKeywordsConfigurationKey = "Accounts:Categories:Hardware";

    /// <summary>The <see cref="IConfigurationProvider"/> key naming the comma-separated keyword list for <see cref="AccountsCategory.Software"/>.</summary>
    public const string SoftwareKeywordsConfigurationKey = "Accounts:Categories:Software";

    /// <summary>The <see cref="IConfigurationProvider"/> key naming the comma-separated keyword list for <see cref="AccountsCategory.Premises"/>.</summary>
    public const string PremisesKeywordsConfigurationKey = "Accounts:Categories:Premises";

    private static readonly IReadOnlyList<string> DefaultSoftwareKeywords = ["software", "subscription", "licence"];
    private static readonly IReadOnlyList<string> DefaultHardwareKeywords = ["equipment", "hardware", "computer"];
    private static readonly IReadOnlyList<string> DefaultPremisesKeywords = ["rent", "premises", "office", "utilities"];

    private readonly IReadOnlyList<string> _software;
    private readonly IReadOnlyList<string> _hardware;
    private readonly IReadOnlyList<string> _premises;

    /// <summary>Initialises a new instance of the <see cref="AccountsCategoriser"/> class, reading its own keyword overrides from <paramref name="configuration"/>.</summary>
    public AccountsCategoriser(IConfigurationProvider configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _software = ResolveKeywords(configuration, SoftwareKeywordsConfigurationKey, DefaultSoftwareKeywords);
        _hardware = ResolveKeywords(configuration, HardwareKeywordsConfigurationKey, DefaultHardwareKeywords);
        _premises = ResolveKeywords(configuration, PremisesKeywordsConfigurationKey, DefaultPremisesKeywords);
    }

    /// <summary>Categorises <paramref name="accountOrCategoryName"/> — <see cref="AccountsCategory.Other"/> for a blank name or one matching none of the configured keyword lists.</summary>
    public AccountsCategory Categorise(string? accountOrCategoryName)
    {
        if (string.IsNullOrWhiteSpace(accountOrCategoryName))
            return AccountsCategory.Other;

        if (ContainsAny(accountOrCategoryName, _software))
            return AccountsCategory.Software;

        if (ContainsAny(accountOrCategoryName, _hardware))
            return AccountsCategory.Hardware;

        if (ContainsAny(accountOrCategoryName, _premises))
            return AccountsCategory.Premises;

        return AccountsCategory.Other;
    }

    private static bool ContainsAny(string text, IReadOnlyList<string> keywords) =>
        keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> ResolveKeywords(IConfigurationProvider configuration, string key, IReadOnlyList<string> defaults) =>
        configuration.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : defaults;
}
