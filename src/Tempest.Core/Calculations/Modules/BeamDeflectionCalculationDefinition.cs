using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>How the beam is supported.</summary>
public enum BeamSupport
{
    /// <summary>Pinned at both ends.</summary>
    SimplySupported,

    /// <summary>Fixed at one end, free at the other.</summary>
    Cantilever,
}

/// <summary>How the beam is loaded.</summary>
public enum BeamLoading
{
    /// <summary>A single point load: at midspan of a simply supported beam, at the free end of a cantilever.</summary>
    PointLoad,

    /// <summary>A uniformly distributed load over the whole span, entered as its total.</summary>
    UniformlyDistributed,
}

/// <summary>The inputs to one beam bending and deflection check.</summary>
/// <param name="MaterialPin">The released material record the modulus came from (see <see cref="MaterialPropertyReader"/>).</param>
/// <param name="Support">Simply supported or cantilever.</param>
/// <param name="Loading">Point load or uniformly distributed.</param>
/// <param name="Load">The point load, or the total of the distributed load.</param>
/// <param name="Span">The span, or the cantilever length.</param>
/// <param name="YoungsModulus">The material's own Young's modulus.</param>
/// <param name="SecondMomentOfArea">The section's second moment of area about the bending axis.</param>
/// <param name="ExtremeFibreDistance">Neutral axis to the most stressed fibre.</param>
/// <param name="AllowableBendingStress">The allowable the maximum bending stress is compared against.</param>
/// <param name="DeflectionLimit">The limit the maximum deflection is compared against.</param>
public sealed record BeamDeflectionInput(
    ReferencePin MaterialPin,
    BeamSupport Support,
    BeamLoading Loading,
    Quantity<Force> Load,
    Quantity<Length> Span,
    Quantity<Pressure> YoungsModulus,
    Quantity<SecondMomentOfArea> SecondMomentOfArea,
    Quantity<Length> ExtremeFibreDistance,
    Quantity<Pressure> AllowableBendingStress,
    Quantity<Length> DeflectionLimit);

/// <summary>The result of one beam bending and deflection check. Every figure is <see langword="null"/> only when the method was refused.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="RefusalReason">Why the method was refused, or <see langword="null"/>.</param>
/// <param name="SpanToDepthRatio">Span over twice the extreme fibre distance, the ratio the refusal is decided on.</param>
/// <param name="MaximumMoment">The maximum bending moment.</param>
/// <param name="MaximumBendingStress">The elastic bending stress at the extreme fibre.</param>
/// <param name="MaximumDeflection">The maximum deflection.</param>
/// <param name="StressUtilisation">Maximum bending stress over the allowable.</param>
/// <param name="DeflectionUtilisation">Maximum deflection over the limit.</param>
/// <param name="StressCriterionMet">Whether the stress utilisation is at or below one.</param>
/// <param name="DeflectionCriterionMet">Whether the deflection utilisation is at or below one.</param>
public sealed record BeamDeflectionResult(
    EngineeringCheckOutcome Outcome,
    string? RefusalReason,
    double SpanToDepthRatio,
    Quantity<Torque>? MaximumMoment,
    Quantity<Pressure>? MaximumBendingStress,
    Quantity<Length>? MaximumDeflection,
    double? StressUtilisation,
    double? DeflectionUtilisation,
    bool? StressCriterionMet,
    bool? DeflectionCriterionMet);

/// <summary>
/// Beam bending and deflection: the Euler-Bernoulli closed forms for a
/// simply supported or cantilever beam under a point load or a uniformly
/// distributed load. Specified in
/// <c>docs/engineering/calculations/calc.beam-deflection.md</c>.
/// </summary>
public sealed class BeamDeflectionCalculationDefinition : ICalculationDefinition<BeamDeflectionInput, BeamDeflectionResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.beam-deflection";

    /// <summary>The span-to-depth ratio below which the method is refused, shear deflection no longer being negligible.</summary>
    public const double MinimumSpanToDepthRatio = 10.0;

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Beam Bending and Deflection",
        Description:
            "Maximum moment, bending stress and deflection of a simply supported or cantilever beam under a point load "
            + "or a uniformly distributed load (Euler-Bernoulli), each compared against its limit.",
        Category: "Structural",
        Assumptions:
        [
            new CalculationAssumption("The beam is linear-elastic, prismatic, and plane sections stay plane.", "Euler-Bernoulli beam theory."),
            new CalculationAssumption("A point load acts at midspan (simply supported) or at the free end (cantilever); a distributed load covers the whole span.", "The positions of maximum moment and maximum deflection for the tabulated cases."),
            new CalculationAssumption("The section depth is twice the extreme fibre distance, for the span-to-depth check.", "True of a doubly symmetric section."),
        ],
        Constraints:
        [
            new CalculationConstraint("Load, span, Young's modulus, second moment of area, extreme fibre distance, allowable stress and deflection limit must all be positive."),
            new CalculationConstraint("Span-to-depth ratio must be at least 10 (method limit; refused below it)."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any input that must be positive is zero or negative.</exception>
    public BeamDeflectionResult Calculate(BeamDeflectionInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var loadN = input.Load.BaseValue;
        var spanM = input.Span.BaseValue;
        var modulusPa = input.YoungsModulus.BaseValue;
        var secondMomentM4 = input.SecondMomentOfArea.BaseValue;
        var fibreM = input.ExtremeFibreDistance.BaseValue;
        var allowablePa = input.AllowableBendingStress.BaseValue;
        var limitM = input.DeflectionLimit.BaseValue;

        ModuleGuards.Require(context, "Load must be positive.", loadN > 0, $"{loadN:0.###} N");
        ModuleGuards.Require(context, "Span must be positive.", spanM > 0, $"{spanM:0.######} m");
        ModuleGuards.Require(context, "Young's modulus must be positive.", modulusPa > 0, $"{modulusPa:0.###} Pa");
        ModuleGuards.Require(context, "Second moment of area must be positive.", secondMomentM4 > 0, $"{secondMomentM4:E3} m^4");
        ModuleGuards.Require(context, "Extreme fibre distance must be positive.", fibreM > 0, $"{fibreM:0.######} m");
        ModuleGuards.Require(context, "Allowable bending stress must be positive.", allowablePa > 0, $"{allowablePa:0.###} Pa");
        ModuleGuards.Require(context, "Deflection limit must be positive.", limitM > 0, $"{limitM:0.######} m");

        context.ReferenceMaterial(input.MaterialPin.RecordId);
        context.RecordIntermediate("Material reference", input.MaterialPin.ToString());
        context.RecordIntermediate("Case", $"{input.Support}, {input.Loading}");

        var spanToDepth = spanM / (2.0 * fibreM);
        context.RecordIntermediate("Span-to-depth ratio", spanToDepth);

        if (spanToDepth < MinimumSpanToDepthRatio)
        {
            var reason = ModuleGuards.Refuse(
                context,
                "Span-to-depth ratio must be at least 10 (method limit; refused below it).",
                $"Refused: the span-to-depth ratio L/(2c) is {spanToDepth:0.###}, below {MinimumSpanToDepthRatio}. Shear deflection is "
                + "no longer negligible there and Euler-Bernoulli theory under-predicts the deflection; use a method that "
                + "includes shear deformation.");

            return new BeamDeflectionResult(EngineeringCheckOutcome.OutsideMethodLimits, reason, spanToDepth, null, null, null, null, null, null, null);
        }

        var (momentCoefficient, deflectionCoefficient) = (input.Support, input.Loading) switch
        {
            (BeamSupport.SimplySupported, BeamLoading.PointLoad) => (1.0 / 4.0, 1.0 / 48.0),
            (BeamSupport.SimplySupported, BeamLoading.UniformlyDistributed) => (1.0 / 8.0, 5.0 / 384.0),
            (BeamSupport.Cantilever, BeamLoading.PointLoad) => (1.0, 1.0 / 3.0),
            (BeamSupport.Cantilever, BeamLoading.UniformlyDistributed) => (1.0 / 2.0, 1.0 / 8.0),
            _ => throw new CalculationInputInvalidException($"Unknown beam case {input.Support}/{input.Loading}."),
        };

        var momentNm = momentCoefficient * loadN * spanM;
        var stressPa = momentNm * fibreM / secondMomentM4;
        var deflectionM = deflectionCoefficient * loadN * spanM * spanM * spanM / (modulusPa * secondMomentM4);

        var stressUtilisation = stressPa / allowablePa;
        var deflectionUtilisation = deflectionM / limitM;
        var stressMet = ModuleGuards.IsMet(stressUtilisation);
        var deflectionMet = ModuleGuards.IsMet(deflectionUtilisation);

        var moment = new Quantity<Torque>(momentNm, TorqueUnits.NewtonMetre);
        var stress = new Quantity<Pressure>(stressPa, PressureUnits.Pascal).ConvertTo(PressureUnits.Megapascal);
        var deflection = new Quantity<Length>(deflectionM, LengthUnits.Metre).ConvertTo(LengthUnits.Millimetre);

        context.RecordIntermediate("Maximum bending moment", moment);
        context.RecordIntermediate("Maximum bending stress", stress);
        context.RecordIntermediate("Maximum deflection", deflection);
        context.RecordIntermediate("Stress utilisation", stressUtilisation);
        context.RecordIntermediate("Deflection utilisation", deflectionUtilisation);

        context.RecordConstraintCheck(
            "Maximum bending stress must not exceed the allowable bending stress.",
            stressMet,
            $"Stress {stress.Value:0.###} MPa against allowable {input.AllowableBendingStress.ConvertTo(PressureUnits.Megapascal).Value:0.###} MPa; utilisation {stressUtilisation:0.####}.");
        context.RecordConstraintCheck(
            "Maximum deflection must not exceed the deflection limit.",
            deflectionMet,
            $"Deflection {deflection.Value:0.####} mm against limit {input.DeflectionLimit.ConvertTo(LengthUnits.Millimetre).Value:0.####} mm; utilisation {deflectionUtilisation:0.####}.");

        return new BeamDeflectionResult(
            ModuleGuards.OutcomeOf(stressMet && deflectionMet),
            null,
            spanToDepth,
            moment,
            stress,
            deflection,
            stressUtilisation,
            deflectionUtilisation,
            stressMet,
            deflectionMet);
    }
}
