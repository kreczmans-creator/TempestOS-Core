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
public sealed class OrganisationIdentitySettings
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this state is stored under.</summary>
    public const string SettingKey = "Desktop.OrganisationIdentity";

    private readonly SettingsDocument<OrganisationIdentityDto> _document;

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

    /// <summary>The flat, immutable snapshot a document renderer reads at render time — the "renderer reads a flat model, draws it" discipline every document model in this codebase already follows.</summary>
    public OrganisationIdentity ToIdentity() => new(
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

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>The plain, JSON-serializable shape this class persists.</summary>
    private sealed record OrganisationIdentityDto(
        string LegalName, string CompanyNumber, string Website, string AddressLine1, string AddressLine2, string Email, string Phone,
        string? BankSortCode = null, string? BankAccountNumber = null, string? BankAccountName = null, string? BankIban = null);
}
