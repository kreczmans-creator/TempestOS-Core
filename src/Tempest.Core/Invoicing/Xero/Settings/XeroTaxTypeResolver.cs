using Tempest.Core.BusinessGovernance;
using Tempest.Core.Settings;

namespace Tempest.Core.Invoicing.Xero.Settings;

/// <summary>
/// The code a Xero line will carry — a tax type or an account code — or why
/// the push is Blocked (`v0.24.0` X1, design §6.8, §8). Exactly one of
/// <see cref="Code"/> and <see cref="BlockedReason"/> is set.
/// </summary>
/// <param name="Code">The code Xero holds as active and applicable; <see langword="null"/> when Blocked.</param>
/// <param name="BlockedReason">Why the line cannot be pushed, readably; <see langword="null"/> when <see cref="Code"/> is usable.</param>
public sealed record XeroCodeResolution(string? Code, string? BlockedReason)
{
    /// <summary>Whether the push must be Blocked.</summary>
    public bool IsBlocked => BlockedReason is not null;

    /// <summary>A usable code.</summary>
    /// <param name="code">The code.</param>
    public static XeroCodeResolution Usable(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new(code, null);
    }

    /// <summary>A Blocked line, with the reason.</summary>
    /// <param name="reason">Why.</param>
    public static XeroCodeResolution Blocked(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(null, reason);
    }
}

/// <summary>
/// Chooses the Xero tax type for a line's <see cref="VatRate"/> and checks
/// it against Xero's own tax rates (`v0.24.0` X1, D6; design §8 "Tax
/// types"), so a line can never carry a tax type Xero lacks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which code.</b> Settings holds one tax type per rate and side
/// (<see cref="SettingKey"/>), defaulting to <see cref="VatRateTaxTypeMapping"/>'s
/// UK codes; an empty value also means the default. A non-UK organisation
/// chooses from <see cref="Choices"/> (the reading's active, applicable
/// rates) in Settings (U1).
/// </para>
/// <para>
/// <b>Checked against the last reading</b> (offline included,
/// design §6.8): the code must exist, be <c>ACTIVE</c>, and apply to the
/// side (<see cref="VatRateTaxTypeMapping.FindTaxTypeProblem"/>). With no
/// reading at all the line is Blocked — nothing is sent half-formed.
/// </para>
/// </remarks>
public sealed class XeroTaxTypeResolver
{
    /// <summary>The prefix of every tax-type Settings key.</summary>
    public const string SettingKeyPrefix = "Xero.TaxType.";

    private readonly IXeroSettingsReader _reader;
    private readonly ISettingsProvider _settings;
    private int _definitionsEnsured;

    /// <summary>Initialises a new instance of the <see cref="XeroTaxTypeResolver"/> class.</summary>
    /// <param name="reader">The X1 settings reader (its cached reading is used; no network).</param>
    /// <param name="settings">Where the chosen tax types are kept.</param>
    public XeroTaxTypeResolver(IXeroSettingsReader reader, ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(settings);

        _reader = reader;
        _settings = settings;
    }

    /// <summary>The Settings key holding the tax type for <paramref name="rate"/> on <paramref name="direction"/> lines (for example <c>Xero.TaxType.Sales.Standard</c>).</summary>
    /// <param name="direction">Sales or purchases.</param>
    /// <param name="rate">The VAT rate.</param>
    public static string SettingKey(VatTaxDirection direction, VatRate rate) => $"{SettingKeyPrefix}{direction}.{rate}";

    /// <summary>
    /// Registers a Settings definition for every rate and side (default:
    /// <see cref="VatRateTaxTypeMapping"/>'s code) unless already registered —
    /// idempotent, so the resolver and the Settings UI may each call it.
    /// </summary>
    /// <param name="settings">The settings provider.</param>
    public static void EnsureDefinitions(ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var direction in Enum.GetValues<VatTaxDirection>())
        {
            foreach (var rate in Enum.GetValues<VatRate>())
            {
                VatRateTaxTypeMapping.TryMap(rate, direction, out var defaultTaxType);
                try
                {
                    settings.RegisterDefinition(new SettingDefinition(
                        SettingKey(direction, rate),
                        $"Xero tax type for {rate} VAT on {(direction == VatTaxDirection.Sales ? "sales" : "purchases")}",
                        defaultTaxType ?? string.Empty));
                }
                catch (DuplicateSettingDefinitionException)
                {
                    // Registered already.
                }
            }
        }
    }

    /// <summary>
    /// The tax type a <paramref name="direction"/> line at
    /// <paramref name="rate"/> carries — the Settings choice checked against
    /// the cached reading — or why it is Blocked. No network call.
    /// </summary>
    /// <param name="rate">The line's VAT rate.</param>
    /// <param name="direction">Sales or purchases.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<XeroCodeResolution> ResolveAsync(VatRate rate, VatTaxDirection direction, CancellationToken cancellationToken = default)
    {
        var chosen = await ReadChoiceAsync(rate, direction, cancellationToken).ConfigureAwait(false);
        var reading = await _reader.ReadCachedAsync(cancellationToken).ConfigureAwait(false);

        return Resolve(rate, direction, reading, chosen);
    }

    /// <summary>
    /// Pure: the tax type for <paramref name="rate"/> on a
    /// <paramref name="direction"/> line — <paramref name="chosenTaxType"/>
    /// when given, else <see cref="VatRateTaxTypeMapping"/>'s default —
    /// checked against <paramref name="reading"/>.
    /// </summary>
    /// <param name="rate">The line's VAT rate.</param>
    /// <param name="direction">Sales or purchases.</param>
    /// <param name="reading">The last reading of Xero; <see langword="null"/> Blocks.</param>
    /// <param name="chosenTaxType">The Settings choice; <see langword="null"/> or blank for the default.</param>
    public static XeroCodeResolution Resolve(VatRate rate, VatTaxDirection direction, XeroSettingsReading? reading, string? chosenTaxType = null)
    {
        var taxType = string.IsNullOrWhiteSpace(chosenTaxType)
            ? VatRateTaxTypeMapping.TryMap(rate, direction, out var mapped) ? mapped : null
            : chosenTaxType.Trim();

        if (taxType is null)
            return XeroCodeResolution.Blocked($"VAT rate '{rate}' has no Xero tax type; choose one in Settings.");

        if (reading is null)
            return XeroCodeResolution.Blocked("Xero's tax rates have not been read yet; refresh Xero in Settings.");

        var problem = VatRateTaxTypeMapping.FindTaxTypeProblem(taxType, direction, reading.TaxRates);
        if (problem is not null)
            return XeroCodeResolution.Blocked(problem);

        // Xero's own spelling of the code, whatever case Settings held it in.
        var held = reading.TaxRates.First(r => string.Equals(r.TaxType, taxType, StringComparison.OrdinalIgnoreCase));
        return XeroCodeResolution.Usable(held.TaxType);
    }

    /// <summary>The tax rates Settings may offer for <paramref name="direction"/> lines: <c>ACTIVE</c> and applicable to the side, in Xero's order.</summary>
    /// <param name="reading">The last reading of Xero.</param>
    /// <param name="direction">Sales or purchases.</param>
    public static IReadOnlyList<XeroTaxRate> Choices(XeroSettingsReading reading, VatTaxDirection direction)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return [.. reading.TaxRates.Where(r => VatRateTaxTypeMapping.FindTaxTypeProblem(r.TaxType, direction, [r]) is null)];
    }

    private async Task<string?> ReadChoiceAsync(VatRate rate, VatTaxDirection direction, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(rate) || !Enum.IsDefined(direction))
            return null;

        if (Interlocked.Exchange(ref _definitionsEnsured, 1) == 0)
            EnsureDefinitions(_settings);

        try
        {
            return await _settings.GetValueAsync(SettingKey(direction, rate), cancellationToken).ConfigureAwait(false);
        }
        catch (SettingNotFoundException)
        {
            // A provider this resolver never ensured (replaced at run time): the default.
            return null;
        }
    }
}
