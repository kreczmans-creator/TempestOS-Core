using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Logging;
using Tempest.Core.Settings;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop;

/// <summary>
/// Settings → Organisation identity (`WP 20.10G`, PO finding D4): the
/// fields <see cref="DocumentTemplate"/>'s own footer shows on every
/// exported document — persisted through the identical
/// <see cref="ISettingsProvider"/> substrate <see cref="UserSettings"/>
/// already uses, under its own, sibling key. Every field pre-fills from
/// <see cref="OrganisationIdentity.TempestDefaults"/> until a user
/// changes it — never a constant baked into a renderer.
/// </summary>
/// <remarks>
/// <b>Company details from Xero (`v0.24.0` U1, design §8 "PDF identity").</b>
/// When a reading of Xero exists (<see cref="UseXeroCompanyDetails"/>,
/// <see cref="LoadXeroCompanyDetailsAsync"/>), <see cref="ToIdentity"/> prints
/// Xero's legal name, company number, VAT number, address, phone, website and
/// bank account instead of the values typed here; without one (offline first
/// run, or another accounting connector) the values typed here are used, as
/// before. The typed values are never overwritten by a reading.
/// </remarks>
public sealed class OrganisationIdentitySettings
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this state is stored under.</summary>
    public const string SettingKey = "Desktop.OrganisationIdentity";

    private readonly SettingsDocument<OrganisationIdentityDto> _document;
    private XeroSource? _xero;

    /// <summary>Initialises a new instance of the <see cref="OrganisationIdentitySettings"/> class, every field at the Tempest defaults.</summary>
    public OrganisationIdentitySettings(ISettingsProvider settingsProvider, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);

        _document = new SettingsDocument<OrganisationIdentityDto>(settingsProvider, SettingKey, "Organisation Identity", logger);

        var defaults = OrganisationIdentity.TempestDefaults;
        LegalName = defaults.LegalName;
        CompanyNumber = defaults.CompanyNumber ?? string.Empty;
        Website = defaults.Website ?? string.Empty;
        AddressLine1 = defaults.AddressLine1 ?? string.Empty;
        AddressLine2 = defaults.AddressLine2 ?? string.Empty;
        Email = defaults.Email ?? string.Empty;
        Phone = defaults.Phone ?? string.Empty;
        BankSortCode = defaults.BankSortCode ?? string.Empty;
        BankAccountNumber = defaults.BankAccountNumber ?? string.Empty;
        BankAccountName = defaults.BankAccountName ?? string.Empty;
        BankIban = defaults.BankIban ?? string.Empty;
    }

    /// <summary>Gets or sets the organisation's own registered name.</summary>
    public string LegalName { get; set; }

    /// <summary>Gets or sets the companies-register number.</summary>
    public string CompanyNumber { get; set; }

    /// <summary>Gets or sets the public website.</summary>
    public string Website { get; set; }

    /// <summary>Gets or sets the first address line.</summary>
    public string AddressLine1 { get; set; }

    /// <summary>Gets or sets the second address line.</summary>
    public string AddressLine2 { get; set; }

    /// <summary>Gets or sets a contact email.</summary>
    public string Email { get; set; }

    /// <summary>Gets or sets a contact phone number.</summary>
    public string Phone { get; set; }

    /// <summary>Gets or sets the bank account's own sort code — the invoice's own "Payment details" section (`WP 21.2A`).</summary>
    public string BankSortCode { get; set; }

    /// <summary>Gets or sets the bank account number.</summary>
    public string BankAccountNumber { get; set; }

    /// <summary>Gets or sets the account holder's own name, where it differs from <see cref="LegalName"/> enough to state separately.</summary>
    public string BankAccountName { get; set; }

    /// <summary>Gets or sets the IBAN, for an international client.</summary>
    public string BankIban { get; set; }

    /// <summary>The company details from the last reading of Xero that documents print, or <see langword="null"/> when there is none and the values typed here are used.</summary>
    public XeroCompanyDetails? XeroCompanyDetails => Volatile.Read(ref _xero)?.Details;

    /// <summary>The base currency (ISO 4217) of the organisation <see cref="XeroCompanyDetails"/> came from, used to choose the bank account printed; <see langword="null"/> when unknown.</summary>
    public string? XeroBaseCurrency => Volatile.Read(ref _xero)?.BaseCurrency;

    /// <summary>
    /// The flat, immutable snapshot a document renderer reads at render time — the "renderer reads a flat model, draws it" discipline every document model in this codebase already follows.
    /// The company details from Xero when a reading exists (<see cref="XeroCompanyDetails"/>, overlaid by <see cref="Overlay"/>); otherwise <see cref="ToSettingsIdentity"/>.
    /// </summary>
    public OrganisationIdentity ToIdentity()
    {
        var local = ToSettingsIdentity();
        return Volatile.Read(ref _xero) is { } xero ? Overlay(local, xero.Details, xero.BaseCurrency) : local;
    }

    /// <summary>The identity made of the values typed in Settings → Organisation alone, ignoring any reading of Xero — the offline fallback.</summary>
    public OrganisationIdentity ToSettingsIdentity() => new(
        LegalName: string.IsNullOrWhiteSpace(LegalName) ? OrganisationIdentity.TempestDefaults.LegalName : LegalName,
        CompanyNumber: NullIfBlank(CompanyNumber),
        Website: NullIfBlank(Website),
        AddressLine1: NullIfBlank(AddressLine1),
        AddressLine2: NullIfBlank(AddressLine2),
        Email: NullIfBlank(Email),
        Phone: NullIfBlank(Phone),
        BankSortCode: NullIfBlank(BankSortCode),
        BankAccountNumber: NullIfBlank(BankAccountNumber),
        BankAccountName: NullIfBlank(BankAccountName),
        BankIban: NullIfBlank(BankIban));

    /// <summary>
    /// Uses <paramref name="details"/> — the company details from a reading
    /// of Xero — for every document rendered from now on;
    /// <see langword="null"/> goes back to the values typed in Settings.
    /// Safe to call from any thread.
    /// </summary>
    /// <param name="details">The company details from Xero, or <see langword="null"/>.</param>
    public void UseXeroCompanyDetails(XeroCompanyDetails? details) => UseXeroCompanyDetails(details, baseCurrency: null);

    /// <summary>
    /// As <see cref="UseXeroCompanyDetails(XeroCompanyDetails?)"/>, with the
    /// organisation's base currency, so the bank account printed is one in
    /// that currency when Xero holds one (<see cref="Overlay(OrganisationIdentity, XeroCompanyDetails, string?)"/>).
    /// </summary>
    /// <param name="details">The company details from Xero, or <see langword="null"/>.</param>
    /// <param name="baseCurrency">The organisation's base currency (ISO 4217), or <see langword="null"/> when unknown.</param>
    public void UseXeroCompanyDetails(XeroCompanyDetails? details, string? baseCurrency) =>
        Volatile.Write(ref _xero, details is null ? null : new XeroSource(details, NullIfBlank(baseCurrency)));

    /// <summary>Uses the company details and base currency of <paramref name="reading"/>; <see langword="null"/> goes back to the values typed in Settings.</summary>
    /// <param name="reading">A reading of Xero, or <see langword="null"/>.</param>
    /// <returns>The company details now used, or <see langword="null"/> when the typed values are.</returns>
    public XeroCompanyDetails? UseXeroReading(XeroSettingsReading? reading)
    {
        var details = reading is null ? null : XeroCompanyDetails.From(reading);
        UseXeroCompanyDetails(details, reading?.Organisation.BaseCurrency);
        return details;
    }

    /// <summary>
    /// Reads the last cached reading of Xero (no network call) and uses its
    /// company details (<see cref="UseXeroCompanyDetails"/>); with no reading
    /// for the connected organisation, documents use the values typed in
    /// Settings. A reader that fails leaves the current choice as it was.
    /// </summary>
    /// <param name="reader">The X1 settings reader.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The company details now used, or <see langword="null"/> when the typed values are.</returns>
    public async Task<XeroCompanyDetails?> LoadXeroCompanyDetailsAsync(IXeroSettingsReader reader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var reading = await reader.ReadCachedAsync(cancellationToken).ConfigureAwait(false);
        return UseXeroReading(reading);
    }

    /// <summary>
    /// Pure: <paramref name="local"/> with the company details from Xero in
    /// place (design §8): Xero's legal name (else its name), company number,
    /// VAT number, address (first line, then the rest joined), phone and
    /// website — each as Xero holds it, absent when Xero holds none, so a
    /// typed value never mixes into another organisation's details. Email is
    /// not held by Xero, so the typed one stays. The bank details are Xero's
    /// first bank account with a number in the organisation's base currency
    /// (<paramref name="baseCurrency"/>), else its first bank account with a
    /// number in any currency (payee: the company's name; the
    /// number verbatim, sort code included as Xero holds it); when Xero holds
    /// no bank account the typed bank details stay.
    /// </summary>
    /// <param name="local">The identity typed in Settings.</param>
    /// <param name="xero">The company details from Xero.</param>
    public static OrganisationIdentity Overlay(OrganisationIdentity local, XeroCompanyDetails xero) => Overlay(local, xero, baseCurrency: null);

    /// <inheritdoc cref="Overlay(OrganisationIdentity, XeroCompanyDetails)"/>
    /// <param name="local">The identity typed in Settings.</param>
    /// <param name="xero">The company details from Xero.</param>
    /// <param name="baseCurrency">The organisation's base currency (ISO 4217); a bank account in it is preferred. <see langword="null"/> when unknown.</param>
    public static OrganisationIdentity Overlay(OrganisationIdentity local, XeroCompanyDetails xero, string? baseCurrency)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(xero);

        var name = string.IsNullOrWhiteSpace(xero.Name) ? local.LegalName : xero.Name.Trim();
        var lines = xero.AddressLines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
        var numbered = xero.BankAccounts.Where(b => !string.IsNullOrWhiteSpace(b.BankAccountNumber)).ToList();
        var bank = (string.IsNullOrWhiteSpace(baseCurrency)
                ? null
                : numbered.FirstOrDefault(b => string.Equals(b.CurrencyCode?.Trim(), baseCurrency.Trim(), StringComparison.OrdinalIgnoreCase)))
            ?? numbered.FirstOrDefault();

        var identity = local with
        {
            LegalName = name,
            CompanyNumber = NullIfBlank(xero.RegistrationNumber),
            VatNumber = NullIfBlank(xero.VatNumber),
            AddressLine1 = lines.Count > 0 ? lines[0] : null,
            AddressLine2 = lines.Count > 1 ? string.Join(", ", lines.Skip(1)) : null,
            Phone = NullIfBlank(xero.Phone),
            Website = NullIfBlank(xero.Website),
        };

        return bank is null
            ? identity
            : identity with { BankAccountName = name, BankSortCode = null, BankAccountNumber = bank.BankAccountNumber!.Trim(), BankIban = null };
    }

    /// <summary>Writes the current state via <see cref="ISettingsProvider.SetValueAsync"/>.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var dto = new OrganisationIdentityDto(
            LegalName, CompanyNumber, Website, AddressLine1, AddressLine2, Email, Phone,
            BankSortCode, BankAccountNumber, BankAccountName, BankIban);
        await _document.SaveAsync(dto, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads persisted state via <see cref="ISettingsProvider.GetValueAsync"/>. A missing/first-run value leaves every property at the Tempest defaults — never an exception.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var dto = await _document.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (dto is null)
            return;

        LegalName = dto.LegalName;
        CompanyNumber = dto.CompanyNumber;
        Website = dto.Website;
        AddressLine1 = dto.AddressLine1;
        AddressLine2 = dto.AddressLine2;
        Email = dto.Email;
        Phone = dto.Phone;
        // `WP 21.2A`: a document saved before this Work Package carries no
        // bank fields at all — the DTO's own JSON deserialisation leaves a
        // missing property at its type's default (`null` for `string?` on
        // the wire, coerced to `string.Empty` here so this class's own
        // fields, like every other one, are never null).
        BankSortCode = dto.BankSortCode ?? string.Empty;
        BankAccountNumber = dto.BankAccountNumber ?? string.Empty;
        BankAccountName = dto.BankAccountName ?? string.Empty;
        BankIban = dto.BankIban ?? string.Empty;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>The company details from a reading of Xero and its organisation's base currency, swapped together.</summary>
    private sealed record XeroSource(XeroCompanyDetails Details, string? BaseCurrency);

    /// <summary>The plain, JSON-serializable shape this class persists.</summary>
    private sealed record OrganisationIdentityDto(
        string LegalName, string CompanyNumber, string Website, string AddressLine1, string AddressLine2, string Email, string Phone,
        string? BankSortCode = null, string? BankAccountNumber = null, string? BankAccountName = null, string? BankIban = null);
}
