using Tempest.Core.Settings;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// The configured "General expenses" contact (`v0.24.0` X5, build decision
/// Q3): the customer or supplier an expense's draft bill goes against when
/// the expense names no supplier of its own — for example the company's own
/// director, for a reimbursement. Held in Settings as an
/// <c>Organisation.Reference</c> (<see cref="SettingKey"/>), linked to a Xero
/// contact like any other supplier (X2), so the link stays per Xero
/// organisation (D7). Blank (the default) Blocks such a bill with the reason
/// until one is chosen.
/// </summary>
public sealed class XeroGeneralExpensesContact
{
    /// <summary>The Settings key holding the "General expenses" organisation's reference.</summary>
    public const string SettingKey = "Xero.Expenses.GeneralContact";

    /// <summary>The Settings display name.</summary>
    public const string DisplayName = "Xero — \"General expenses\" contact for expenses with no supplier";

    private readonly ISettingsProvider _settings;
    private int _definitionEnsured;

    /// <summary>Initialises a new instance of the <see cref="XeroGeneralExpensesContact"/> class.</summary>
    /// <param name="settings">Where the choice is kept.</param>
    public XeroGeneralExpensesContact(ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    /// <summary>Registers the Settings definition (default blank) unless already registered — idempotent, so this and the Settings UI may each call it.</summary>
    /// <param name="settings">The settings provider.</param>
    public static void EnsureDefinition(ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            settings.RegisterDefinition(new SettingDefinition(SettingKey, DisplayName, string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Registered already.
        }
    }

    /// <summary>The chosen organisation's reference, or <see langword="null"/> when none is chosen. No network.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _definitionEnsured, 1) == 0)
            EnsureDefinition(_settings);

        try
        {
            var value = await _settings.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (SettingNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Chooses <paramref name="organisationReference"/> as the "General expenses" contact (<see langword="null"/> or blank clears it).</summary>
    /// <param name="organisationReference">The organisation's reference.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public Task SetAsync(string? organisationReference, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _definitionEnsured, 1) == 0)
            EnsureDefinition(_settings);

        return _settings.SetValueAsync(SettingKey, organisationReference?.Trim() ?? string.Empty, cancellationToken);
    }
}
