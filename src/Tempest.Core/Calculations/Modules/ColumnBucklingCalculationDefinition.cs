using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>The inputs to one column buckling check.</summary>
/// <param name="MaterialPin">The released material record the modulus and yield strength came from.</param>
/// <param name="EffectiveLength">The strut's effective length.</param>
/// <param name="Area">The section area.</param>
/// <param name="SecondMomentOfArea">The second moment of area about the weaker axis.</param>
/// <param name="YoungsModulus">The material's own Young's modulus (205 GPa for steel in BS 5950).</param>
/// <param name="YieldStrength">The material's own design (yield) strength.</param>
/// <param name="RobertsonConstant">The Robertson constant selecting the strut curve: 2.0 (a), 3.5 (b), 5.5 (c), 8.0 (d).</param>
/// <param name="AppliedLoad">The axial compression the resistance is compared against.</param>
public sealed record ColumnBucklingInput(
    ReferencePin MaterialPin,
    Quantity<Length> EffectiveLength,
    Quantity<Area> Area,
    Quantity<SecondMomentOfArea> SecondMomentOfArea,
    Quantity<Pressure> YoungsModulus,
    Quantity<Pressure> YieldStrength,
    double RobertsonConstant,
    Quantity<Force> AppliedLoad);

/// <summary>The result of one column buckling check. The radius of gyration and slenderness are always reported; every other figure is <see langword="null"/> only when the method was refused.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="RefusalReason">Why the method was refused, or <see langword="null"/>.</param>
/// <param name="RadiusOfGyration">Square root of I over A.</param>
/// <param name="Slenderness">Effective length over radius of gyration.</param>
/// <param name="EulerStress">The elastic critical stress.</param>
/// <param name="EulerLoad">The elastic critical load.</param>
/// <param name="LimitingSlenderness">The slenderness below which the Perry factor is zero.</param>
/// <param name="PerryFactor">The imperfection factor.</param>
/// <param name="CompressiveStrength">The Perry-Robertson compressive strength.</param>
/// <param name="CompressionResistance">Compressive strength times area.</param>
/// <param name="Utilisation">Applied load over compression resistance.</param>
/// <param name="CriterionMet">Whether the utilisation is at or below one.</param>
public sealed record ColumnBucklingResult(
    EngineeringCheckOutcome Outcome,
    string? RefusalReason,
    Quantity<Length> RadiusOfGyration,
    double Slenderness,
    Quantity<Pressure>? EulerStress,
    Quantity<Force>? EulerLoad,
    double? LimitingSlenderness,
    double? PerryFactor,
    Quantity<Pressure>? CompressiveStrength,
    Quantity<Force>? CompressionResistance,
    double? Utilisation,
    bool? CriterionMet);

/// <summary>
/// Column buckling: the Euler critical stress corrected by the
/// Perry-Robertson formula in the form of BS 5950-1 Annex C. Specified in
/// <c>docs/engineering/calculations/calc.column-buckling.md</c>.
/// </summary>
public sealed class ColumnBucklingCalculationDefinition : ICalculationDefinition<ColumnBucklingInput, ColumnBucklingResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.column-buckling";

    /// <summary>The slenderness above which BS 5950-1 clause 4.7.3.2 does not allow a compression member; the method is refused beyond it.</summary>
    public const double MaximumSlenderness = 180.0;

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Column Buckling (Perry-Robertson)",
        Description:
            "Compressive strength and resistance of a strut from its slenderness, by Euler's critical stress with the "
            + "Perry-Robertson imperfection correction (BS 5950-1 Annex C form), against an applied axial load.",
        Category: "Structural",
        Assumptions:
        [
            new CalculationAssumption("The strut is straight but for the imperfection the Perry factor represents, pin-ended over its effective length, and buckles about the axis the second moment of area is given for.", "The Perry-Robertson idealisation."),
            new CalculationAssumption("The Perry factor grows linearly with slenderness beyond the limiting slenderness, at a rate set by the Robertson constant.", "BS 5950-1 Annex C.2."),
        ],
        Constraints:
        [
            new CalculationConstraint("Effective length, area, second moment of area, Young's modulus, yield strength and Robertson constant must be positive; the applied load must not be negative."),
            new CalculationConstraint("Slenderness must not exceed 180 (BS 5950-1 clause 4.7.3.2; refused above it)."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any input that must be positive is zero or negative, or the applied load is negative.</exception>
    public ColumnBucklingResult Calculate(ColumnBucklingInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var lengthMm = input.EffectiveLength.ConvertTo(LengthUnits.Millimetre).Value;
        var areaMm2 = input.Area.ConvertTo(AreaUnits.SquareMillimetre).Value;
        var secondMomentMm4 = input.SecondMomentOfArea.ConvertTo(SecondMomentOfAreaUnits.MillimetreToTheFourth).Value;
        var modulusMPa = input.YoungsModulus.ConvertTo(PressureUnits.Megapascal).Value;
        var yieldMPa = input.YieldStrength.ConvertTo(PressureUnits.Megapascal).Value;
        var loadN = input.AppliedLoad.BaseValue;

        ModuleGuards.Require(context, "Effective length must be positive.", lengthMm > 0, $"{lengthMm:0.###} mm");
        ModuleGuards.Require(context, "Area must be positive.", areaMm2 > 0, $"{areaMm2:0.###} mm^2");
        ModuleGuards.Require(context, "Second moment of area must be positive.", secondMomentMm4 > 0, $"{secondMomentMm4:0.###} mm^4");
        ModuleGuards.Require(context, "Young's modulus must be positive.", modulusMPa > 0, $"{modulusMPa:0.###} MPa");
        ModuleGuards.Require(context, "Yield strength must be positive.", yieldMPa > 0, $"{yieldMPa:0.###} MPa");
        ModuleGuards.Require(context, "Robertson constant must be positive.", input.RobertsonConstant > 0, $"{input.RobertsonConstant:0.###}");
        ModuleGuards.Require(context, "Applied load must not be negative.", loadN >= 0, $"{loadN:0.###} N");

        context.ReferenceMaterial(input.MaterialPin.RecordId);
        context.RecordIntermediate("Material reference", input.MaterialPin.ToString());

        var radiusMm = Math.Sqrt(secondMomentMm4 / areaMm2);
        var slenderness = lengthMm / radiusMm;
        var radius = new Quantity<Length>(radiusMm, LengthUnits.Millimetre);

        context.RecordIntermediate("Radius of gyration", radius);
        context.RecordIntermediate("Slenderness", slenderness);

        if (slenderness > MaximumSlenderness)
        {
            var reason = ModuleGuards.Refuse(
                context,
                "Slenderness must not exceed 180 (BS 5950-1 clause 4.7.3.2; refused above it).",
                $"Refused: the slenderness of {slenderness:0.###} exceeds 180, the limit BS 5950-1 clause 4.7.3.2 sets for a member "
                + "resisting loads other than wind; the strut curves are not applied beyond it.");

            return new ColumnBucklingResult(EngineeringCheckOutcome.OutsideMethodLimits, reason, radius, slenderness, null, null, null, null, null, null, null, null);
        }

        var eulerMPa = Math.PI * Math.PI * modulusMPa / (slenderness * slenderness);
        var limitingSlenderness = 0.2 * Math.Sqrt(Math.PI * Math.PI * modulusMPa / yieldMPa);
        var perryFactor = Math.Max(0.0, 0.001 * input.RobertsonConstant * (slenderness - limitingSlenderness));
        var phi = (yieldMPa + (perryFactor + 1.0) * eulerMPa) / 2.0;
        var strengthMPa = eulerMPa * yieldMPa / (phi + Math.Sqrt(phi * phi - eulerMPa * yieldMPa));
        var resistanceN = strengthMPa * areaMm2;
        var utilisation = loadN / resistanceN;
        var met = ModuleGuards.IsMet(utilisation);

        var eulerStress = new Quantity<Pressure>(eulerMPa, PressureUnits.Megapascal);
        var strength = new Quantity<Pressure>(strengthMPa, PressureUnits.Megapascal);
        var resistance = new Quantity<Force>(resistanceN, ForceUnits.Newton);

        context.RecordIntermediate("Euler stress", eulerStress);
        context.RecordIntermediate("Limiting slenderness", limitingSlenderness);
        context.RecordIntermediate("Perry factor", perryFactor);
        context.RecordIntermediate("Phi", phi);
        context.RecordIntermediate("Compressive strength", strength);
        context.RecordIntermediate("Utilisation", utilisation);

        context.RecordConstraintCheck(
            "Applied load must not exceed the compression resistance.",
            met,
            $"Applied {loadN:0.###} N against resistance {resistanceN:0.###} N; utilisation {utilisation:0.####}.");

        return new ColumnBucklingResult(
            ModuleGuards.OutcomeOf(met),
            null,
            radius,
            slenderness,
            eulerStress,
            new Quantity<Force>(eulerMPa * areaMm2, ForceUnits.Newton),
            limitingSlenderness,
            perryFactor,
            strength,
            resistance,
            utilisation,
            met);
    }
}
