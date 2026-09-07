using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.ReferenceData;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations;

/// <summary>What an independent checker computed, by their own means.</summary>
/// <remarks>
/// <para>
/// <b>These numbers must not come from the calculation.</b> A verification
/// that re-runs the production formula proves the formula is
/// self-consistent and nothing else. The point of this record is that
/// somebody worked the problem separately — on paper, in a spreadsheet, or
/// from a textbook — and is stating what they got.
/// </para>
/// <para>
/// The platform cannot enforce that, because it cannot see where a number
/// came from. What it can do is make the claim explicit and require the
/// basis to be described, so a reviewer can judge whether the check was
/// genuinely independent.
/// </para>
/// </remarks>
/// <param name="Basis">How the independent figures were obtained, in the checker's own words. Required.</param>
/// <param name="StressMargin">The margin of safety the checker computed.</param>
/// <param name="Mass">The mass the checker computed.</param>
/// <param name="RelativeTolerance">How closely the figures must agree to count as confirming.</param>
public sealed record IndependentCheck(
    string Basis,
    double StressMargin,
    Quantity<Mass> Mass,
    double RelativeTolerance = 1e-6);

/// <summary>
/// Writes an executed bracket check into the engineering artefacts that
/// already exist for it.
/// </summary>
/// <remarks>
/// <para>
/// The bracket already has a calculation pack (<c>TDE-CPK-001</c>) and a
/// verification artefact (<c>TDE-VER-001</c>), both seeded deliberately
/// incomplete: the pack recorded a method and a pinned material basis but
/// no result, because no load had been established, and the artefact
/// reported <see cref="VerificationStanding.NotPerformed"/> because there
/// was nothing to verify. This fills them in from a real execution.
/// </para>
/// <para>
/// <b>No new reporting framework.</b> Everything written here goes into
/// fields the P05 structures already declare —
/// <see cref="CalculationPack.Inputs"/>,
/// <see cref="CalculationPack.Outputs"/>,
/// <see cref="CalculationPack.ExecutionRecordIds"/>,
/// <see cref="VerificationArtefact.Result"/>. The pack was designed to hold
/// exactly this; it had simply never been given it.
/// </para>
/// </remarks>
public sealed class BracketEngineeringRecordService
{
    private readonly ICalculationPackCatalog _packs;
    private readonly IVerificationArtefactCatalog _artefacts;

    /// <summary>Initialises a new instance of the <see cref="BracketEngineeringRecordService"/> class.</summary>
    /// <param name="packs">The calculation pack library.</param>
    /// <param name="artefacts">The verification artefact library.</param>
    public BracketEngineeringRecordService(
        ICalculationPackCatalog packs,
        IVerificationArtefactCatalog artefacts)
    {
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(artefacts);

        _packs = packs;
        _artefacts = artefacts;
    }

    /// <summary>Writes an executed check into its calculation pack.</summary>
    /// <param name="packRecordId">The pack to complete.</param>
    /// <param name="check">The executed, governed check.</param>
    /// <param name="cancellationToken">A token observed while writing.</param>
    /// <returns>The revised pack.</returns>
    /// <exception cref="ArgumentException"><paramref name="check"/> was refused and has no result to record.</exception>
    public async Task<IReferenceRecord<CalculationPack>> RecordCalculationAsync(
        string packRecordId,
        GovernedBracketCheck check,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packRecordId);
        ArgumentNullException.ThrowIfNull(check);

        if (!check.WasPerformed)
            throw new ArgumentException($"The check was refused ({check.Refusal}) and has no result to record.", nameof(check));

        var pack = await _packs.FindAsync(packRecordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceData.ReferenceRecordNotFoundException(_packs.LibraryName, packRecordId);

        var result = check.Result!;
        var record = check.Record!;

        var inputs = new List<CalculationInput>
        {
            new("IN-MATERIAL",
                "Material minimum 0.2% proof stress",
                Format(result.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value, "MPa"),
                SourcePin: result.MaterialPin,
                SourceDescription: "Taken from the pinned material record's own YieldStrength property.",
                Dimension: nameof(Pressure)),
            new("IN-LOAD",
                "Design load on the bracket",
                Format(check.Request.AppliedLoad.ConvertTo(ForceUnits.Newton).Value, "N"),
                SourceDescription: "Supplied with the check request.",
                Dimension: nameof(Force)),
            new("IN-AREA",
                "Minimum cross-sectional area",
                Format(check.Request.SectionArea.ConvertTo(AreaUnits.SquareMillimetre).Value, "mm2"),
                SourceDescription: "Supplied with the check request.",
                Dimension: nameof(Area)),
            new("IN-LENGTH",
                "Member length, for the mass estimate",
                Format(check.Request.MemberLength.ConvertTo(LengthUnits.Millimetre).Value, "mm"),
                SourceDescription: "Supplied with the check request.",
                Dimension: nameof(Length)),
            new("IN-DENSITY",
                "Material density",
                Format(result.Density.ConvertTo(MassDensityUnits.KilogramPerCubicMetre).Value, "kg/m3"),
                SourcePin: result.MaterialPin,
                SourceDescription: "Taken from the pinned material record's own Density property.",
                Dimension: nameof(MassDensity)),
        };

        var outputs = new List<CalculationOutput>
        {
            new("OUT-STRESS",
                "Direct stress on the minimum section",
                Format(result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, "MPa"),
                Dimension: nameof(Pressure),
                Interpretation: "Applied stress, F / A."),
            new("OUT-MARGIN",
                "Margin of safety against proof stress",
                result.StressMargin.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
                AcceptanceCriterion: "Margin greater than or equal to zero against the specified minimum proof stress.",
                Interpretation: result.StressCriterionMet
                    ? "Meets the stress criterion. Meeting a criterion is not design approval."
                    : "Does not meet the stress criterion."),
            new("OUT-MASS",
                "Estimated member mass",
                Format(result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, "kg"),
                Dimension: nameof(Mass),
                AcceptanceCriterion: $"At or below {Format(result.MassLimit.ConvertTo(MassUnits.Kilogram).Value, "kg")}.",
                Interpretation: result.MassCriterionMet
                    ? "Meets the mass criterion."
                    : "Does not meet the mass criterion."),
        };

        return await _packs.ReviseAsync(
            packRecordId,
            pack.Definition with
            {
                Inputs = inputs,
                Outputs = outputs,

                // The engine's own record id, so the pack points at the
                // execution rather than restating it.
                ExecutionRecordIds = [.. pack.Definition.ExecutionRecordIds, record.Id],
                Limitations =
                [
                    .. pack.Definition.Limitations.Where(l => !l.StartsWith("No result.", StringComparison.Ordinal)),
                    $"Executed by calculation '{record.CalculationId}'. Does not consider bending, buckling, "
                    + "stress concentration, bearing at the fixing holes, or fatigue.",
                ],
            },
            pack.Provenance,
            $"Completed from calculation record '{record.Id}'.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records an independent check of an executed calculation against its
    /// verification artefact.
    /// </summary>
    /// <param name="artefactRecordId">The verification artefact to complete.</param>
    /// <param name="check">The executed, governed check being verified.</param>
    /// <param name="independent">What an independent checker computed, and how.</param>
    /// <param name="verifierPrincipalId">Who performed the independent check.</param>
    /// <param name="performedOn">When they performed it.</param>
    /// <param name="calculationPackReference">
    /// The calculation pack this verification relates to, where there is
    /// one. Supplied by the caller rather than assumed: this service has no
    /// way to know which pack an artefact belongs to, and naming one it
    /// guessed would put a wrong cross-reference on an engineering record.
    /// </param>
    /// <param name="cancellationToken">A token observed while writing.</param>
    /// <returns>The revised artefact.</returns>
    public async Task<IReferenceRecord<VerificationArtefact>> RecordVerificationAsync(
        string artefactRecordId,
        GovernedBracketCheck check,
        IndependentCheck independent,
        string verifierPrincipalId,
        DateOnly performedOn,
        string? calculationPackReference = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artefactRecordId);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(independent);
        ArgumentException.ThrowIfNullOrWhiteSpace(independent.Basis);
        ArgumentException.ThrowIfNullOrWhiteSpace(verifierPrincipalId);

        if (!check.WasPerformed)
            throw new ArgumentException($"The check was refused ({check.Refusal}) and cannot be verified.", nameof(check));

        var artefact = await _artefacts.FindAsync(artefactRecordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceData.ReferenceRecordNotFoundException(_artefacts.LibraryName, artefactRecordId);

        var result = check.Result!;

        var marginAgrees = Agrees(result.StressMargin, independent.StressMargin, independent.RelativeTolerance);
        var massAgrees = Agrees(
            result.EstimatedMass.BaseValue, independent.Mass.BaseValue, independent.RelativeTolerance);

        var standing = marginAgrees && massAgrees
            ? VerificationStanding.Passed
            : VerificationStanding.Failed;

        var summary =
            $"Independent check {(standing == VerificationStanding.Passed ? "agrees with" : "DISAGREES with")} "
            + $"the calculation. Basis: {independent.Basis} "
            + $"Margin: calculated {result.StressMargin:0.######}, independently {independent.StressMargin:0.######} "
            + $"({(marginAgrees ? "agrees" : "differs")}). "
            + $"Mass: calculated {result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value:0.######} kg, "
            + $"independently {independent.Mass.ConvertTo(MassUnits.Kilogram).Value:0.######} kg "
            + $"({(massAgrees ? "agrees" : "differs")}).";

        return await _artefacts.ReviseAsync(
            artefactRecordId,
            artefact.Definition with
            {
                Result = new VerificationResult(
                    standing,
                    summary,
                    PerformedByPrincipalId: verifierPrincipalId,
                    PerformedOn: performedOn,
                    VerificationRecordId: check.Record!.Id,
                    CalculationPackReference: calculationPackReference),
                SourcePins = [result.MaterialPin],
            },
            artefact.Provenance,
            $"Independent verification recorded by '{verifierPrincipalId}': {standing}.",
            cancellationToken).ConfigureAwait(false);
    }

    private static bool Agrees(double calculated, double independent, double relativeTolerance)
    {
        var scale = Math.Max(Math.Abs(calculated), Math.Abs(independent));

        // Near zero a relative comparison is meaningless, so fall back to an
        // absolute one at the same scale.
        return scale < 1e-12
            ? Math.Abs(calculated - independent) < relativeTolerance
            : Math.Abs(calculated - independent) / scale <= relativeTolerance;
    }

    private static string Format(double value, string unit) =>
        $"{value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} {unit}";
}
