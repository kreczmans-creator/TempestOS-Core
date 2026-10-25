namespace Tempest.Core.Calculations;

/// <summary>
/// `ADR-0173`: the calculations an installation withholds until a feature is switched on, and the one refusal sentence every path that would run
/// one gives. The engine consults this at its single execution choke point (<see cref="CalculationEngine"/>), so a caller that runs a definition
/// by id, whether a command, a plugin, a script or a future entry point, cannot reach a gated calculation while its feature is off. The worksheet
/// catalogue (<c>CalcCatalogue.FeatureRefusal</c>) and the Engineering Calculators service use the same wording.
/// </summary>
public static class CalculationFeatureGate
{
    /// <summary>The feature that withholds the lifting calculations (the same name the worksheet calculators' feature flag carries).</summary>
    public const string LiftingFeature = "lifting";

    /// <summary>The worksheet calculator of S8 (lifting lugs and spreader beams); it is not an engine definition, and is listed so that a definition ever registered under its id is gated too.</summary>
    public const string LiftingWorksheetId = "struct.lifting";

    /// <summary>The older lifting-lug and pin-joint engine definition.</summary>
    public const string LiftingModuleId = "calc.lifting-lug-pin-joint";

    private static readonly Dictionary<string, (string Title, string Feature)> Gated = new(StringComparer.Ordinal)
    {
        [LiftingModuleId] = ("Lifting lug and pin joint", LiftingFeature),
        [LiftingWorksheetId] = ("Lifting lugs and spreader beams", LiftingFeature),
    };

    /// <summary>The ids the engine gates, ordered (a test ties this list to the worksheet templates' own <c>FeatureFlag</c> and to the engine's registered definitions).</summary>
    public static IReadOnlyList<string> GatedIds => [.. Gated.Keys.Order(StringComparer.Ordinal)];

    /// <summary>The feature the calculation <paramref name="calculationId"/> is gated behind, or <see langword="null"/> when it is always available.</summary>
    public static string? FeatureOf(string? calculationId) =>
        calculationId is not null && Gated.TryGetValue(calculationId, out var gate) ? gate.Feature : null;

    /// <summary>
    /// Why <paramref name="calculationId"/> cannot be run now, or <see langword="null"/> when it can: it is gated and <paramref name="featureEnabled"/>
    /// is absent (nothing has said the feature is on: refused) or says the feature is off.
    /// </summary>
    public static string? RefusalFor(string? calculationId, Func<string, bool>? featureEnabled)
    {
        if (calculationId is null || !Gated.TryGetValue(calculationId, out var gate))
            return null;

        return featureEnabled is not null && featureEnabled(gate.Feature) ? null : Refusal(gate.Title, calculationId, gate.Feature);
    }

    /// <summary>
    /// Where a feature is switched on: only the machine's administrator can write it (the application installs per user, so nothing in the user's
    /// profile, the install folder, the environment or the command line counts).
    /// </summary>
    public static string EnablementSource(string feature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        var name = char.ToUpperInvariant(feature[0]) + feature[1..];
        return $"machine policy value {name}: DWORD 1 under HKLM\\SOFTWARE\\Policies\\TempestOS\\Features on Windows, or \"{name}\": true in the root-owned /etc/tempestos/features.json elsewhere";
    }

    /// <summary>The one refusal sentence for a calculation that is composed in but switched off.</summary>
    public static string Refusal(string title, string id, string feature) =>
        $"Calculator '{title}' ({id}) is switched off: its '{feature}' feature is not enabled in this installation " +
        $"({EnablementSource(feature)}). It stays off until the owner has completed its review checklist; nothing can be created, checked or issued with it while it is.";
}

/// <summary>Thrown when the engine is asked to run a calculation whose feature is switched off (`ADR-0173`); the message is <see cref="CalculationFeatureGate.Refusal"/>.</summary>
public sealed class CalculationFeatureDisabledException : CalculationException
{
    /// <summary>Initialises a new instance of the <see cref="CalculationFeatureDisabledException"/> class.</summary>
    public CalculationFeatureDisabledException(string calculationId, string message)
        : base(message)
    {
        CalculationId = calculationId;
    }

    /// <summary>Gets the calculation that was refused.</summary>
    public string CalculationId { get; }
}
