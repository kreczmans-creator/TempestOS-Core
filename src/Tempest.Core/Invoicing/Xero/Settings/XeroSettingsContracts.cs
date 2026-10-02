namespace Tempest.Core.Invoicing.Xero.Settings;

// ============================================================================
// `v0.24.0` X1 (D6): the company's own details, tax rates and chart of
// accounts, read from Xero and cached so PDFs and line validation work
// offline from the last reading. Contracts only; see
// `docs/releases/v0.24.0/Xero Technical Design.md` §8.
// ============================================================================

/// <summary>A postal address as Xero holds it on the organisation (<c>Addresses[]</c>, the <c>STREET</c> one preferred, else <c>POBOX</c>).</summary>
/// <param name="AddressType">Xero's own address type word, verbatim (<c>"STREET"</c>, <c>"POBOX"</c>).</param>
/// <param name="Lines">Address lines 1–4, blanks removed.</param>
/// <param name="City">The town or city.</param>
/// <param name="Region">The region or county.</param>
/// <param name="PostalCode">The postcode.</param>
/// <param name="Country">The country, as Xero states it.</param>
public sealed record XeroAddress(string AddressType, IReadOnlyList<string> Lines, string? City, string? Region, string? PostalCode, string? Country);

/// <summary>The company's own details, read from Xero's <c>GET /Organisation</c> (and, for bank details, <c>GET /Accounts</c> where <c>Type == "BANK"</c>).</summary>
/// <param name="OrganisationId">Xero's <c>OrganisationID</c>.</param>
/// <param name="Name">Xero's display name.</param>
/// <param name="LegalName">The legal name shown on reports; the name every TempestOS PDF prints.</param>
/// <param name="TaxNumber">The VAT number (Xero <c>TaxNumber</c>); <see langword="null"/> when not set.</param>
/// <param name="RegistrationNumber">The company number (Xero <c>RegistrationNumber</c>); <see langword="null"/> when not set.</param>
/// <param name="Address">The organisation's address; <see langword="null"/> when none.</param>
/// <param name="Phone">The default phone number; <see langword="null"/> when none.</param>
/// <param name="Website">The website from <c>ExternalLinks</c> (<c>LinkType == "Website"</c>); <see langword="null"/> when none.</param>
/// <param name="BaseCurrency">The organisation's base currency (ISO 4217).</param>
/// <param name="CountryCode">The organisation's country (ISO 3166-1 alpha-2).</param>
/// <param name="PaysTax">Whether the organisation is VAT-registered.</param>
/// <param name="IsDemoCompany">Whether this is Xero's Demo Company — the only organisation TempestOS writes to until the Product Owner allows the live one (D7).</param>
/// <param name="BankAccounts">The organisation's bank accounts, for the invoice's payment details.</param>
public sealed record XeroOrganisationProfile(
    string OrganisationId,
    string Name,
    string? LegalName,
    string? TaxNumber,
    string? RegistrationNumber,
    XeroAddress? Address,
    string? Phone,
    string? Website,
    string BaseCurrency,
    string CountryCode,
    bool PaysTax,
    bool IsDemoCompany,
    IReadOnlyList<XeroBankAccount> BankAccounts);

/// <summary>One bank account, from <c>GET /Accounts</c> where <c>Type == "BANK"</c> and <c>Status == "ACTIVE"</c>.</summary>
/// <param name="Name">The account's own name.</param>
/// <param name="BankAccountNumber">Xero's <c>BankAccountNumber</c>, verbatim (for a UK account, sort code and number as the organisation entered them).</param>
/// <param name="CurrencyCode">The account's currency.</param>
public sealed record XeroBankAccount(string Name, string? BankAccountNumber, string? CurrencyCode);

/// <summary>One tax rate, from <c>GET /TaxRates</c>.</summary>
/// <param name="TaxType">The code a line item carries (for example <c>"OUTPUT2"</c>, <c>"INPUT2"</c>).</param>
/// <param name="Name">The rate's display name.</param>
/// <param name="EffectiveRate">The rate as a percentage (for example <c>20.0000</c>).</param>
/// <param name="Status">Xero's status word (<c>"ACTIVE"</c>, <c>"DELETED"</c>, <c>"ARCHIVED"</c>, <c>"PENDING"</c>); only <c>ACTIVE</c> may be posted.</param>
/// <param name="CanApplyToRevenue">Whether a sales line may use it.</param>
/// <param name="CanApplyToExpenses">Whether a purchase line may use it.</param>
public sealed record XeroTaxRate(string TaxType, string Name, decimal EffectiveRate, string Status, bool CanApplyToRevenue, bool CanApplyToExpenses);

/// <summary>One account in the chart of accounts, from <c>GET /Accounts</c>.</summary>
/// <param name="AccountId">Xero's <c>AccountID</c>.</param>
/// <param name="Code">The account code a line item carries; <see langword="null"/> for an account with no code (some bank accounts).</param>
/// <param name="Name">The account's own name.</param>
/// <param name="Type">Xero's account type word (for example <c>"REVENUE"</c>, <c>"EXPENSE"</c>, <c>"DIRECTCOSTS"</c>, <c>"BANK"</c>).</param>
/// <param name="Class">Xero's account class (<c>"REVENUE"</c>, <c>"EXPENSE"</c>, …).</param>
/// <param name="Status">Xero's status word; only <c>ACTIVE</c> may be posted.</param>
/// <param name="TaxType">The account's default tax type; <see langword="null"/> when none.</param>
public sealed record XeroAccount(string AccountId, string? Code, string Name, string Type, string? Class, string Status, string? TaxType);

/// <summary>
/// The last reading of the organisation, tax rates and accounts, as cached
/// on disk (<c>{root}/accounts/xero-settings.json</c>) so it is usable
/// offline. Settings shows "from Xero, read at <see cref="ReadAtUtc"/>".
/// </summary>
/// <param name="SchemaVersion">The cached shape's version; <see cref="CurrentSchemaVersion"/> when written by this build. An unreadable or newer-than-known file reads as "no reading yet", never an exception.</param>
/// <param name="TenantId">The Xero organisation it was read from; a reading for another tenant is never used.</param>
/// <param name="Organisation">The company's own details.</param>
/// <param name="TaxRates">Every tax rate, every status.</param>
/// <param name="Accounts">Every account, every status.</param>
/// <param name="ReadAtUtc">When it was read.</param>
public sealed record XeroSettingsReading(
    int SchemaVersion,
    string TenantId,
    XeroOrganisationProfile Organisation,
    IReadOnlyList<XeroTaxRate> TaxRates,
    IReadOnlyList<XeroAccount> Accounts,
    DateTimeOffset ReadAtUtc)
{
    /// <summary>The schema version this build writes.</summary>
    public const int CurrentSchemaVersion = 1;
}

/// <summary>
/// Reads and caches Xero's own settings (X1, D6). Read-only towards Xero:
/// TempestOS never writes the organisation, a tax rate or an account.
/// </summary>
public interface IXeroSettingsReader
{
    /// <summary>The last cached reading for the connected tenant, or <see langword="null"/> when none — no network call.</summary>
    Task<XeroSettingsReading?> ReadCachedAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads Organisation, TaxRates and Accounts from Xero (three calls), caches the result, and returns it; on any failure the cache is left as it was and the outcome says why.</summary>
    Task<ConnectorResult<XeroSettingsReading>> RefreshAsync(CancellationToken cancellationToken = default);
}
