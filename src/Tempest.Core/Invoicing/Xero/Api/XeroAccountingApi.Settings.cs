using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero.Api;

// ============================================================================
// `v0.24.0` task X1 (D6) — the settings resource: `GET Organisation`,
// `GET TaxRates`, `GET Accounts` (`docs/releases/v0.24.0/Xero Technical
// Design.md` §3, §8). Read-only: TempestOS never writes Xero's settings, and
// `XeroWriteSafetyHandler`'s write allow-list refuses any non-GET to these
// paths. The wire shapes below are what Xero answers; the reader
// (`Settings/XeroSettingsReader.cs`) maps them to the §12 contracts.
// ============================================================================

/// <summary>Xero's <c>{ "Organisations": [ … ] }</c> envelope (<c>GET Organisation</c> answers exactly one).</summary>
/// <param name="Organisations">The organisation.</param>
public sealed record XeroWireOrganisationsEnvelope([property: JsonPropertyName("Organisations")] IReadOnlyList<XeroWireOrganisation>? Organisations);

/// <summary>The organisation, as <c>GET Organisation</c> answers it — only the fields TempestOS reads.</summary>
/// <param name="OrganisationID">Xero's id for the organisation.</param>
/// <param name="Name">The display name.</param>
/// <param name="LegalName">The legal name.</param>
/// <param name="TaxNumber">The VAT number.</param>
/// <param name="RegistrationNumber">The company number.</param>
/// <param name="BaseCurrency">The base currency.</param>
/// <param name="CountryCode">The country.</param>
/// <param name="PaysTax">Whether the organisation is registered for VAT.</param>
/// <param name="IsDemoCompany">Whether this is Xero's Demo Company (D7).</param>
/// <param name="Addresses">The organisation's addresses.</param>
/// <param name="Phones">The organisation's phone numbers.</param>
/// <param name="ExternalLinks">The organisation's links (website, social).</param>
public sealed record XeroWireOrganisation(
    [property: JsonPropertyName("OrganisationID")] string? OrganisationID,
    [property: JsonPropertyName("Name")] string? Name,
    [property: JsonPropertyName("LegalName")] string? LegalName = null,
    [property: JsonPropertyName("TaxNumber")] string? TaxNumber = null,
    [property: JsonPropertyName("RegistrationNumber")] string? RegistrationNumber = null,
    [property: JsonPropertyName("BaseCurrency")] string? BaseCurrency = null,
    [property: JsonPropertyName("CountryCode")] string? CountryCode = null,
    [property: JsonPropertyName("PaysTax")] bool? PaysTax = null,
    [property: JsonPropertyName("IsDemoCompany")] bool? IsDemoCompany = null,
    [property: JsonPropertyName("Addresses")] IReadOnlyList<XeroWireAddress>? Addresses = null,
    [property: JsonPropertyName("Phones")] IReadOnlyList<XeroWirePhone>? Phones = null,
    [property: JsonPropertyName("ExternalLinks")] IReadOnlyList<XeroWireExternalLink>? ExternalLinks = null);

/// <summary>One of the organisation's addresses.</summary>
/// <param name="AddressType"><c>STREET</c> or <c>POBOX</c>.</param>
/// <param name="AddressLine1">Line 1.</param>
/// <param name="AddressLine2">Line 2.</param>
/// <param name="AddressLine3">Line 3.</param>
/// <param name="AddressLine4">Line 4.</param>
/// <param name="City">The town or city.</param>
/// <param name="Region">The region or county.</param>
/// <param name="PostalCode">The postcode.</param>
/// <param name="Country">The country.</param>
public sealed record XeroWireAddress(
    [property: JsonPropertyName("AddressType")] string? AddressType,
    [property: JsonPropertyName("AddressLine1")] string? AddressLine1 = null,
    [property: JsonPropertyName("AddressLine2")] string? AddressLine2 = null,
    [property: JsonPropertyName("AddressLine3")] string? AddressLine3 = null,
    [property: JsonPropertyName("AddressLine4")] string? AddressLine4 = null,
    [property: JsonPropertyName("City")] string? City = null,
    [property: JsonPropertyName("Region")] string? Region = null,
    [property: JsonPropertyName("PostalCode")] string? PostalCode = null,
    [property: JsonPropertyName("Country")] string? Country = null);

/// <summary>One of the organisation's phone numbers.</summary>
/// <param name="PhoneType"><c>DEFAULT</c>, <c>OFFICE</c>, <c>DDI</c>, <c>MOBILE</c> or <c>FAX</c>.</param>
/// <param name="PhoneNumber">The number.</param>
/// <param name="PhoneAreaCode">The area code, when held separately.</param>
/// <param name="PhoneCountryCode">The country code, when held separately.</param>
public sealed record XeroWirePhone(
    [property: JsonPropertyName("PhoneType")] string? PhoneType,
    [property: JsonPropertyName("PhoneNumber")] string? PhoneNumber,
    [property: JsonPropertyName("PhoneAreaCode")] string? PhoneAreaCode = null,
    [property: JsonPropertyName("PhoneCountryCode")] string? PhoneCountryCode = null);

/// <summary>One of the organisation's links.</summary>
/// <param name="LinkType"><c>Website</c>, <c>Facebook</c>, …</param>
/// <param name="Url">The address.</param>
public sealed record XeroWireExternalLink(
    [property: JsonPropertyName("LinkType")] string? LinkType,
    [property: JsonPropertyName("Url")] string? Url);

/// <summary>Xero's <c>{ "TaxRates": [ … ] }</c> envelope.</summary>
/// <param name="TaxRates">The tax rates.</param>
public sealed record XeroWireTaxRatesEnvelope([property: JsonPropertyName("TaxRates")] IReadOnlyList<XeroWireTaxRate>? TaxRates);

/// <summary>One tax rate, as <c>GET TaxRates</c> answers it.</summary>
/// <param name="TaxType">The code a line carries.</param>
/// <param name="Name">The display name.</param>
/// <param name="Status"><c>ACTIVE</c>, <c>DELETED</c>, <c>ARCHIVED</c> or <c>PENDING</c>.</param>
/// <param name="EffectiveRate">The rate, as a percentage.</param>
/// <param name="CanApplyToRevenue">Whether sales lines may use it.</param>
/// <param name="CanApplyToExpenses">Whether purchase lines may use it.</param>
/// <param name="ReportTaxType">Xero's VAT-return category.</param>
public sealed record XeroWireTaxRate(
    [property: JsonPropertyName("TaxType")] string? TaxType,
    [property: JsonPropertyName("Name")] string? Name,
    [property: JsonPropertyName("Status")] string? Status,
    [property: JsonPropertyName("EffectiveRate")] decimal? EffectiveRate = null,
    [property: JsonPropertyName("CanApplyToRevenue")] bool? CanApplyToRevenue = null,
    [property: JsonPropertyName("CanApplyToExpenses")] bool? CanApplyToExpenses = null,
    [property: JsonPropertyName("ReportTaxType")] string? ReportTaxType = null);

/// <summary>Xero's <c>{ "Accounts": [ … ] }</c> envelope.</summary>
/// <param name="Accounts">The accounts.</param>
public sealed record XeroWireAccountsEnvelope([property: JsonPropertyName("Accounts")] IReadOnlyList<XeroWireAccount>? Accounts);

/// <summary>One account in the chart of accounts, as <c>GET Accounts</c> answers it.</summary>
/// <param name="AccountID">Xero's id for the account.</param>
/// <param name="Code">The code a line carries; absent for some bank accounts.</param>
/// <param name="Name">The account's name.</param>
/// <param name="Type"><c>REVENUE</c>, <c>DIRECTCOSTS</c>, <c>OVERHEADS</c>, <c>BANK</c>, …</param>
/// <param name="Class"><c>REVENUE</c>, <c>EXPENSE</c>, <c>ASSET</c>, …</param>
/// <param name="Status"><c>ACTIVE</c>, <c>ARCHIVED</c> or <c>DELETED</c>.</param>
/// <param name="TaxType">The account's default tax type.</param>
/// <param name="BankAccountNumber">For a bank account, its number as the organisation entered it.</param>
/// <param name="CurrencyCode">For a bank account, its currency.</param>
/// <param name="UpdatedDateUTC">When it last changed (Microsoft JSON date).</param>
public sealed record XeroWireAccount(
    [property: JsonPropertyName("AccountID")] string? AccountID,
    [property: JsonPropertyName("Code")] string? Code,
    [property: JsonPropertyName("Name")] string? Name,
    [property: JsonPropertyName("Type")] string? Type,
    [property: JsonPropertyName("Class")] string? Class = null,
    [property: JsonPropertyName("Status")] string? Status = null,
    [property: JsonPropertyName("TaxType")] string? TaxType = null,
    [property: JsonPropertyName("BankAccountNumber")] string? BankAccountNumber = null,
    [property: JsonPropertyName("CurrencyCode")] string? CurrencyCode = null,
    [property: JsonPropertyName("UpdatedDateUTC")] string? UpdatedDateUTC = null);

// `v0.24.0` task X1: the settings resource (§3 "Company (X1)", §8).
public sealed partial class XeroAccountingApi
{
    /// <summary>Reads the connected organisation (<c>GET Organisation</c>).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The organisation; Unknown when Xero answered with none.</returns>
    public async Task<XeroApiResult<XeroWireOrganisation>> GetOrganisationAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<XeroWireOrganisationsEnvelope>("Organisation", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWireOrganisationsEnvelope, XeroWireOrganisation>(result);

        var organisation = result.Value!.Organisations?.FirstOrDefault();
        return organisation is null
            ? Failure<XeroWireOrganisation>(ConnectorOutcome.Unknown, result.HttpStatus, "Xero answered GET Organisation with no organisation.")
            : new XeroApiResult<XeroWireOrganisation>(ConnectorOutcome.Ok, organisation, result.HttpStatus, null, []);
    }

    /// <summary>Reads every tax rate, every status (<c>GET TaxRates</c>; not paged).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<IReadOnlyList<XeroWireTaxRate>>> GetTaxRatesAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<XeroWireTaxRatesEnvelope>("TaxRates", cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Outcome == ConnectorOutcome.Ok
            ? new XeroApiResult<IReadOnlyList<XeroWireTaxRate>>(ConnectorOutcome.Ok, result.Value!.TaxRates ?? [], result.HttpStatus, null, [])
            : Retype<XeroWireTaxRatesEnvelope, IReadOnlyList<XeroWireTaxRate>>(result);
    }

    /// <summary>
    /// Reads the chart of accounts, every status (<c>GET Accounts</c>; not
    /// paged). With <paramref name="ifModifiedSince"/>, Xero answers only the
    /// accounts changed since then (archiving one counts as a change) — the
    /// caller merges them into what it read before; a <c>304 Not
    /// Modified</c> is answered as Ok with no accounts (nothing changed).
    /// </summary>
    /// <param name="ifModifiedSince">Sent as <c>If-Modified-Since</c> when not <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<IReadOnlyList<XeroWireAccount>>> GetAccountsAsync(DateTimeOffset? ifModifiedSince = null, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<XeroWireAccountsEnvelope>("Accounts", ifModifiedSince: ifModifiedSince, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Outcome == ConnectorOutcome.Ok)
            return new XeroApiResult<IReadOnlyList<XeroWireAccount>>(ConnectorOutcome.Ok, result.Value!.Accounts ?? [], result.HttpStatus, null, []);

        return ifModifiedSince is not null && result.HttpStatus == NotModifiedStatus
            ? new XeroApiResult<IReadOnlyList<XeroWireAccount>>(ConnectorOutcome.Ok, [], result.HttpStatus, null, [])
            : Retype<XeroWireAccountsEnvelope, IReadOnlyList<XeroWireAccount>>(result);
    }

    /// <summary>HTTP 304, the answer to a conditional read when nothing changed.</summary>
    private const int NotModifiedStatus = 304;
}
