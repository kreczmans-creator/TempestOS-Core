using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>Which surface of the cylinder wall a figure refers to.</summary>
public enum CylinderSurface
{
    /// <summary>The inner surface.</summary>
    Bore,

    /// <summary>The outer surface.</summary>
    Outer,
}

/// <summary>The inputs to one thick-walled cylinder stress calculation.</summary>
/// <param name="MaterialPin">The released material record the allowable stress was derived from.</param>
/// <param name="InnerRadius">The bore radius.</param>
/// <param name="OuterRadius">The outer radius.</param>
/// <param name="InternalPressure">The internal gauge pressure.</param>
/// <param name="ExternalPressure">The external gauge pressure.</param>
/// <param name="ClosedEnds">Whether the ends are closed, so the pressures load the wall axially.</param>
/// <param name="AllowableStress">The allowable the maximum von Mises stress is compared against.</param>
public sealed record ThickWalledCylinderInput(
    ReferencePin MaterialPin,
    Quantity<Length> InnerRadius,
    Quantity<Length> OuterRadius,
    Quantity<Pressure> InternalPressure,
    Quantity<Pressure> ExternalPressure,
    bool ClosedEnds,
    Quantity<Pressure> AllowableStress);

/// <summary>The result of one thick-walled cylinder stress calculation. This method has no refusal, so every figure is always present.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="BoreRadialStress">Radial stress at the bore.</param>
/// <param name="BoreHoopStress">Hoop stress at the bore.</param>
/// <param name="OuterRadialStress">Radial stress at the outer surface.</param>
/// <param name="OuterHoopStress">Hoop stress at the outer surface.</param>
/// <param name="AxialStress">Axial stress, uniform through the wall; zero for open ends.</param>
/// <param name="BoreVonMisesStress">Von Mises stress at the bore.</param>
/// <param name="BoreTrescaStress">Tresca stress at the bore.</param>
/// <param name="OuterVonMisesStress">Von Mises stress at the outer surface.</param>
/// <param name="OuterTrescaStress">Tresca stress at the outer surface.</param>
/// <param name="MaximumVonMisesStress">The larger von Mises stress of the two surfaces.</param>
/// <param name="MaximumVonMisesSurface">Which surface it occurs at.</param>
/// <param name="Utilisation">Maximum von Mises stress over the allowable.</param>
/// <param name="ThinWallHoopEstimate">The thin-wall hoop stress for the same pressures and mean radius, for comparison.</param>
/// <param name="LameToThinWallRatio">The larger Lame hoop stress magnitude over the thin-wall estimate magnitude.</param>
/// <param name="WallRatio">Outer radius over inner radius.</param>
/// <param name="CriterionMet">Whether the utilisation is at or below one.</param>
public sealed record ThickWalledCylinderResult(
    EngineeringCheckOutcome Outcome,
    Quantity<Pressure> BoreRadialStress,
    Quantity<Pressure> BoreHoopStress,
    Quantity<Pressure> OuterRadialStress,
    Quantity<Pressure> OuterHoopStress,
    Quantity<Pressure> AxialStress,
    Quantity<Pressure> BoreVonMisesStress,
    Quantity<Pressure> BoreTrescaStress,
    Quantity<Pressure> OuterVonMisesStress,
    Quantity<Pressure> OuterTrescaStress,
    Quantity<Pressure> MaximumVonMisesStress,
    CylinderSurface MaximumVonMisesSurface,
    double Utilisation,
    Quantity<Pressure> ThinWallHoopEstimate,
    double LameToThinWallRatio,
    double WallRatio,
    bool CriterionMet);

/// <summary>
/// Thick-walled cylinder stresses under internal and external pressure, by
/// Lame's equations, beside the thin-wall estimate. Specified in
/// <c>docs/engineering/calculations/calc.thick-walled-cylinder.md</c>.
/// </summary>
public sealed class ThickWalledCylinderCalculationDefinition : ICalculationDefinition<ThickWalledCylinderInput, ThickWalledCylinderResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.thick-walled-cylinder";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Thick-Walled Cylinder Stresses (Lame)",
        Description:
            "Radial, hoop and axial stresses at the bore and outer surface of a thick-walled cylinder under internal and "
            + "external pressure, combined as von Mises and Tresca stresses, with the thin-wall estimate for comparison.",
        Category: "Pressure Systems",
        Assumptions:
        [
            new CalculationAssumption("The cylinder is long, linear-elastic, and loaded only by uniform internal and external pressure.", "Lame's plane-strain solution."),
            new CalculationAssumption("With closed ends the pressure end load is carried uniformly by the wall as an axial stress; with open ends the axial stress is zero.", "The two standard end conditions."),
        ],
        Constraints:
        [
            new CalculationConstraint("Inner radius and allowable stress must be positive; the outer radius must exceed the inner; pressures must not be negative and not both be zero."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any constraint above is not met.</exception>
    public ThickWalledCylinderResult Calculate(ThickWalledCylinderInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var aMm = input.InnerRadius.ConvertTo(LengthUnits.Millimetre).Value;
        var bMm = input.OuterRadius.ConvertTo(LengthUnits.Millimetre).Value;
        var piMPa = input.InternalPressure.ConvertTo(PressureUnits.Megapascal).Value;
        var poMPa = input.ExternalPressure.ConvertTo(PressureUnits.Megapascal).Value;
        var allowableMPa = input.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value;

        ModuleGuards.Require(context, "Inner radius must be positive.", aMm > 0, input, nameof(input.InnerRadius));
        ModuleGuards.Require(context, "Outer radius must exceed the inner radius.", bMm > aMm, $"outer {bMm:0.###} mm, inner {aMm:0.###} mm");
        ModuleGuards.Require(context, "Internal pressure must not be negative.", piMPa >= 0, input, nameof(input.InternalPressure));
        ModuleGuards.Require(context, "External pressure must not be negative.", poMPa >= 0, input, nameof(input.ExternalPressure));
        ModuleGuards.Require(context, "At least one pressure must be non-zero.", piMPa > 0 || poMPa > 0, "no pressure");
        ModuleGuards.Require(context, "Allowable stress must be positive.", allowableMPa > 0, input, nameof(input.AllowableStress));

        context.ReferenceMaterial(input.MaterialPin.RecordId);
        context.RecordIntermediate("Material reference", input.MaterialPin.ToString());

        var a2 = aMm * aMm;
        var b2 = bMm * bMm;
        var denominator = b2 - a2;
        var constantA = (a2 * piMPa - b2 * poMPa) / denominator;
        var constantB = a2 * b2 * (piMPa - poMPa) / denominator;
        var axialMPa = input.ClosedEnds ? constantA : 0.0;

        var boreRadial = constantA - constantB / a2;
        var boreHoop = constantA + constantB / a2;
        var outerRadial = constantA - constantB / b2;
        var outerHoop = constantA + constantB / b2;

        var (boreVonMises, boreTresca) = Equivalent(boreRadial, boreHoop, axialMPa);
        var (outerVonMises, outerTresca) = Equivalent(outerRadial, outerHoop, axialMPa);
        var maximumSurface = boreVonMises >= outerVonMises ? CylinderSurface.Bore : CylinderSurface.Outer;
        var maximumVonMises = Math.Max(boreVonMises, outerVonMises);
        var utilisation = maximumVonMises / allowableMPa;
        var met = ModuleGuards.IsMet(utilisation);

        var thinWallHoop = (piMPa - poMPa) * (aMm + bMm) / 2.0 / (bMm - aMm);
        var largestHoop = Math.Max(Math.Abs(boreHoop), Math.Abs(outerHoop));
        var lameToThinWall = largestHoop / Math.Abs(thinWallHoop);
        var wallRatio = bMm / aMm;

        context.RecordIntermediate("Lame constant A", MPa(constantA));
        context.RecordIntermediate("Lame constant B (MPa.mm^2)", constantB);
        context.RecordIntermediate("Wall ratio b/a", wallRatio);
        context.RecordIntermediate("Bore hoop stress", MPa(boreHoop));
        context.RecordIntermediate("Bore von Mises stress", MPa(boreVonMises));
        context.RecordIntermediate("Outer von Mises stress", MPa(outerVonMises));
        context.RecordIntermediate("Thin-wall hoop estimate", MPa(thinWallHoop));
        context.RecordIntermediate("Utilisation", utilisation);

        context.RecordConstraintCheck(
            "Maximum von Mises stress must not exceed the allowable stress.",
            met,
            $"Maximum von Mises {maximumVonMises:0.###} MPa at the {maximumSurface} against allowable {allowableMPa:0.###} MPa; utilisation {utilisation:0.####}.");

        return new ThickWalledCylinderResult(
            ModuleGuards.OutcomeOf(met),
            MPa(boreRadial),
            MPa(boreHoop),
            MPa(outerRadial),
            MPa(outerHoop),
            MPa(axialMPa),
            MPa(boreVonMises),
            MPa(boreTresca),
            MPa(outerVonMises),
            MPa(outerTresca),
            MPa(maximumVonMises),
            maximumSurface,
            utilisation,
            MPa(thinWallHoop),
            lameToThinWall,
            wallRatio,
            met);
    }

    private static (double VonMises, double Tresca) Equivalent(double radial, double hoop, double axial)
    {
        var vonMises = Math.Sqrt(0.5 * ((hoop - radial) * (hoop - radial) + (radial - axial) * (radial - axial) + (axial - hoop) * (axial - hoop)));
        var tresca = Math.Max(Math.Abs(hoop - radial), Math.Max(Math.Abs(radial - axial), Math.Abs(axial - hoop)));
        return (vonMises, tresca);
    }

    private static Quantity<Pressure> MPa(double value) => new(value, PressureUnits.Megapascal);
}
