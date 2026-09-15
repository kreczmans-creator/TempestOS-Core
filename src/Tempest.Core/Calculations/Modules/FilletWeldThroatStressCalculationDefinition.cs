using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>The inputs to one fillet weld throat stress check.</summary>
/// <param name="MaterialPin">The released material record of the weaker part joined, whose ultimate strength is used.</param>
/// <param name="ParallelForce">The force component along the weld axis.</param>
/// <param name="TransverseForce">The force component across the weld, in the plate plane.</param>
/// <param name="NormalForce">The force component normal to the plate.</param>
/// <param name="EffectiveLength">The weld's total effective length.</param>
/// <param name="ThroatThickness">The weld throat.</param>
/// <param name="UltimateStrength">The weaker part's ultimate strength, from the material record.</param>
/// <param name="CorrelationFactor">The correlation factor of EN 1993-1-8 for the steel grade (0.8 S235, 0.85 S275, 0.9 S355, 1.0 S420 and S460).</param>
/// <param name="PartialFactor">The partial factor for welds, 1.25 recommended.</param>
public sealed record FilletWeldThroatStressInput(
    ReferencePin MaterialPin,
    Quantity<Force> ParallelForce,
    Quantity<Force> TransverseForce,
    Quantity<Force> NormalForce,
    Quantity<Length> EffectiveLength,
    Quantity<Length> ThroatThickness,
    Quantity<Pressure> UltimateStrength,
    double CorrelationFactor,
    double PartialFactor);

/// <summary>The result of one fillet weld check. Figures are <see langword="null"/> only when the method was refused.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="RefusalReason">Why the method was refused, or <see langword="null"/>.</param>
/// <param name="ResultantForce">The resultant of the three force components.</param>
/// <param name="DesignShearStrength">The weld's design shear strength.</param>
/// <param name="ThroatStress">Resultant force over throat area.</param>
/// <param name="WeldResistance">The weld's design resistance as a force.</param>
/// <param name="Utilisation">Throat stress over design shear strength.</param>
/// <param name="RequiredThroat">The throat at which the utilisation would be exactly one.</param>
/// <param name="GoverningRequiredThroat">The required throat, or the 3 mm minimum where that governs.</param>
/// <param name="RequiredLeg">The equal-leg size for the governing required throat.</param>
/// <param name="CriterionMet">Whether the utilisation is at or below one.</param>
public sealed record FilletWeldThroatStressResult(
    EngineeringCheckOutcome Outcome,
    string? RefusalReason,
    Quantity<Force>? ResultantForce,
    Quantity<Pressure>? DesignShearStrength,
    Quantity<Pressure>? ThroatStress,
    Quantity<Force>? WeldResistance,
    double? Utilisation,
    Quantity<Length>? RequiredThroat,
    Quantity<Length>? GoverningRequiredThroat,
    Quantity<Length>? RequiredLeg,
    bool? CriterionMet);

/// <summary>
/// Fillet weld sizing and throat stress under combined load, by the
/// simplified method of EN 1993-1-8 clause 4.5.3.3. Specified in
/// <c>docs/engineering/calculations/calc.fillet-weld-throat-stress.md</c>.
/// </summary>
public sealed class FilletWeldThroatStressCalculationDefinition
    : ICalculationDefinition<FilletWeldThroatStressInput, FilletWeldThroatStressResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.fillet-weld-throat-stress";

    /// <summary>The smallest throat EN 1993-1-8 clause 4.5.2 allows, in millimetres.</summary>
    public const double MinimumThroatMm = 3.0;

    /// <summary>The smallest effective length EN 1993-1-8 clause 4.5.1 allows to be designed to carry load, in millimetres.</summary>
    public const double MinimumLengthMm = 30.0;

    /// <summary>The effective length, in throat thicknesses, below which clause 4.5.1 does not allow the weld to be designed to carry load.</summary>
    public const double MinimumLengthInThroats = 6.0;

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Fillet Weld Throat Stress",
        Description:
            "Throat stress from the resultant of the forces a fillet weld transmits, against the design shear strength "
            + "of the EN 1993-1-8 simplified method; the throat the load would need.",
        Category: "Welds",
        Assumptions:
        [
            new CalculationAssumption("The resultant of all forces per unit length is compared against one design resistance whatever its direction.", "EN 1993-1-8 clause 4.5.3.3, the simplified method."),
            new CalculationAssumption("The ultimate strength is that of the weaker part joined.", "Clause 4.5.3.2 bases the weld strength on the parent material."),
            new CalculationAssumption("The load is uniform along the effective length.", "A hand-calculation idealisation."),
        ],
        Constraints:
        [
            new CalculationConstraint("At least one force component non-zero; length, throat, ultimate strength and correlation factor positive; partial factor at least 1.0."),
            new CalculationConstraint("Throat must be at least 3 mm (clause 4.5.2; refused below it)."),
            new CalculationConstraint("Effective length must be at least the larger of 30 mm and six throat thicknesses (clause 4.5.1; refused below it)."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">No load, a non-positive length, throat, strength or correlation factor, or a partial factor below 1.0.</exception>
    public FilletWeldThroatStressResult Calculate(FilletWeldThroatStressInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var parallelN = input.ParallelForce.BaseValue;
        var transverseN = input.TransverseForce.BaseValue;
        var normalN = input.NormalForce.BaseValue;
        var lengthMm = input.EffectiveLength.ConvertTo(LengthUnits.Millimetre).Value;
        var throatMm = input.ThroatThickness.ConvertTo(LengthUnits.Millimetre).Value;
        var ultimateMPa = input.UltimateStrength.ConvertTo(PressureUnits.Megapascal).Value;

        ModuleGuards.Require(context, "At least one force component must be non-zero.", parallelN != 0 || transverseN != 0 || normalN != 0, "a zero load");
        ModuleGuards.Require(context, "Effective length must be positive.", lengthMm > 0, $"{lengthMm:0.###} mm");
        ModuleGuards.Require(context, "Throat thickness must be positive.", throatMm > 0, $"{throatMm:0.###} mm");
        ModuleGuards.Require(context, "Ultimate strength must be positive.", ultimateMPa > 0, $"{ultimateMPa:0.###} MPa");
        ModuleGuards.Require(context, "Correlation factor must be positive.", input.CorrelationFactor > 0, $"{input.CorrelationFactor:0.###}");
        ModuleGuards.Require(context, "Partial factor must be at least 1.0.", input.PartialFactor >= 1.0, $"{input.PartialFactor:0.###}");

        context.ReferenceMaterial(input.MaterialPin.RecordId);
        context.RecordIntermediate("Material reference (weaker part)", input.MaterialPin.ToString());

        if (throatMm < MinimumThroatMm)
        {
            var reason = ModuleGuards.Refuse(
                context,
                "Throat must be at least 3 mm (clause 4.5.2; refused below it).",
                $"Refused: a throat of {throatMm:0.###} mm is below the 3 mm minimum EN 1993-1-8 clause 4.5.2 allows for a fillet weld.");

            return Refused(reason);
        }

        context.RecordConstraintCheck("Throat must be at least 3 mm (clause 4.5.2; refused below it).", true, $"Throat {throatMm:0.###} mm.");

        var minimumLengthMm = Math.Max(MinimumLengthMm, MinimumLengthInThroats * throatMm);
        if (lengthMm < minimumLengthMm)
        {
            var governing = lengthMm < MinimumLengthMm
                ? "the 30 mm minimum"
                : $"six throat thicknesses ({minimumLengthMm:0.###} mm)";
            var reason = ModuleGuards.Refuse(
                context,
                "Effective length must be at least the larger of 30 mm and six throat thicknesses (clause 4.5.1; refused below it).",
                $"Refused: an effective length of {lengthMm:0.###} mm is below {governing}; EN 1993-1-8 clause 4.5.1 does not allow such a weld to be designed to carry load.");

            return Refused(reason);
        }

        context.RecordConstraintCheck(
            "Effective length must be at least the larger of 30 mm and six throat thicknesses (clause 4.5.1; refused below it).",
            true,
            $"Length {lengthMm:0.###} mm against minimum {minimumLengthMm:0.###} mm.");

        var resultantN = Math.Sqrt(parallelN * parallelN + transverseN * transverseN + normalN * normalN);
        var designShearMPa = ultimateMPa / (Math.Sqrt(3.0) * input.CorrelationFactor * input.PartialFactor);
        var throatStressMPa = resultantN / (throatMm * lengthMm);
        var resistanceN = designShearMPa * throatMm * lengthMm;
        var utilisation = throatStressMPa / designShearMPa;
        var requiredThroatMm = resultantN / (designShearMPa * lengthMm);
        var governingThroatMm = Math.Max(requiredThroatMm, MinimumThroatMm);
        var met = ModuleGuards.IsMet(utilisation);

        var resultant = new Quantity<Force>(resultantN, ForceUnits.Newton);
        var designShear = new Quantity<Pressure>(designShearMPa, PressureUnits.Megapascal);
        var throatStress = new Quantity<Pressure>(throatStressMPa, PressureUnits.Megapascal);
        var resistance = new Quantity<Force>(resistanceN, ForceUnits.Newton);

        context.RecordIntermediate("Resultant force", resultant);
        context.RecordIntermediate("Design shear strength", designShear);
        context.RecordIntermediate("Throat stress", throatStress);
        context.RecordIntermediate("Utilisation", utilisation);
        context.RecordIntermediate("Required throat", new Quantity<Length>(requiredThroatMm, LengthUnits.Millimetre));

        context.RecordConstraintCheck(
            "Throat stress must not exceed the design shear strength.",
            met,
            $"Throat stress {throatStressMPa:0.###} MPa against {designShearMPa:0.###} MPa; utilisation {utilisation:0.####}.");

        return new FilletWeldThroatStressResult(
            ModuleGuards.OutcomeOf(met),
            null,
            resultant,
            designShear,
            throatStress,
            resistance,
            utilisation,
            new Quantity<Length>(requiredThroatMm, LengthUnits.Millimetre),
            new Quantity<Length>(governingThroatMm, LengthUnits.Millimetre),
            new Quantity<Length>(governingThroatMm * Math.Sqrt(2.0), LengthUnits.Millimetre),
            met);
    }

    private static FilletWeldThroatStressResult Refused(string reason) =>
        new(EngineeringCheckOutcome.OutsideMethodLimits, reason, null, null, null, null, null, null, null, null, null);
}
