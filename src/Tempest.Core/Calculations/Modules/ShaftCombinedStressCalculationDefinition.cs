using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>The inputs to one shaft combined stress check.</summary>
/// <param name="MaterialPin">The released material record the yield strength came from.</param>
/// <param name="Diameter">The solid shaft diameter at the section.</param>
/// <param name="BendingMoment">The bending moment at the section, as a magnitude.</param>
/// <param name="Torque">The torque at the section, as a magnitude.</param>
/// <param name="YieldStrength">The material's own yield strength.</param>
/// <param name="BendingStressConcentrationFactor">The stress concentration factor applied to the bending stress; 1 for none.</param>
/// <param name="TorsionalStressConcentrationFactor">The stress concentration factor applied to the torsional shear; 1 for none.</param>
/// <param name="RequiredSafetyFactor">The factor of safety the lower of the two theories must reach.</param>
public sealed record ShaftCombinedStressInput(
    ReferencePin MaterialPin,
    Quantity<Length> Diameter,
    Quantity<Torque> BendingMoment,
    Quantity<Torque> Torque,
    Quantity<Pressure> YieldStrength,
    double BendingStressConcentrationFactor,
    double TorsionalStressConcentrationFactor,
    double RequiredSafetyFactor);

/// <summary>The result of one shaft combined stress check. This method has no refusal, so every figure is always present.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="BendingStress">The bending stress at the surface, concentration applied.</param>
/// <param name="TorsionalShearStress">The torsional shear at the surface, concentration applied.</param>
/// <param name="MaximumShearStress">The maximum shear stress (Tresca).</param>
/// <param name="VonMisesStress">The distortion-energy equivalent stress.</param>
/// <param name="TrescaSafetyFactor">Half the yield strength over the maximum shear stress.</param>
/// <param name="VonMisesSafetyFactor">Yield strength over the von Mises stress.</param>
/// <param name="GoverningSafetyFactor">The lower of the two.</param>
/// <param name="CriterionMet">Whether the governing factor reaches the required one.</param>
public sealed record ShaftCombinedStressResult(
    EngineeringCheckOutcome Outcome,
    Quantity<Pressure> BendingStress,
    Quantity<Pressure> TorsionalShearStress,
    Quantity<Pressure> MaximumShearStress,
    Quantity<Pressure> VonMisesStress,
    double TrescaSafetyFactor,
    double VonMisesSafetyFactor,
    double GoverningSafetyFactor,
    bool CriterionMet);

/// <summary>
/// A solid round shaft under combined bending and torsion: maximum shear
/// stress and von Mises factors of safety. Specified in
/// <c>docs/engineering/calculations/calc.shaft-combined-stress.md</c>.
/// </summary>
public sealed class ShaftCombinedStressCalculationDefinition : ICalculationDefinition<ShaftCombinedStressInput, ShaftCombinedStressResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.shaft-combined-stress";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Shaft under Combined Torsion and Bending",
        Description:
            "Bending and torsional stresses in a solid round shaft combined by the maximum-shear-stress (Tresca) and "
            + "distortion-energy (von Mises) theories, each as a factor of safety against yield.",
        Category: "Machine Elements",
        Assumptions:
        [
            new CalculationAssumption("The section is solid and circular and stays elastic.", "The closed-form surface stresses of a round bar."),
            new CalculationAssumption("The loading is static; stress concentration is applied through the caller's factors.", "Fatigue is a separate calculation."),
        ],
        Constraints:
        [
            new CalculationConstraint("Diameter and yield strength must be positive; moment and torque must not be negative and not both be zero; every factor must be at least 1.0."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any constraint above is not met.</exception>
    public ShaftCombinedStressResult Calculate(ShaftCombinedStressInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var diameterMm = input.Diameter.ConvertTo(LengthUnits.Millimetre).Value;
        var momentNmm = input.BendingMoment.ConvertTo(TorqueUnits.NewtonMillimetre).Value;
        var torqueNmm = input.Torque.ConvertTo(TorqueUnits.NewtonMillimetre).Value;
        var yieldMPa = input.YieldStrength.ConvertTo(PressureUnits.Megapascal).Value;

        ModuleGuards.Require(context, "Diameter must be positive.", diameterMm > 0, $"{diameterMm:0.###} mm");
        ModuleGuards.Require(context, "Bending moment must not be negative.", momentNmm >= 0, $"{momentNmm:0.###} N.mm");
        ModuleGuards.Require(context, "Torque must not be negative.", torqueNmm >= 0, $"{torqueNmm:0.###} N.mm");
        ModuleGuards.Require(context, "Bending moment and torque must not both be zero.", momentNmm > 0 || torqueNmm > 0, "no load");
        ModuleGuards.Require(context, "Yield strength must be positive.", yieldMPa > 0, $"{yieldMPa:0.###} MPa");
        ModuleGuards.Require(context, "Bending stress concentration factor must be at least 1.0.", input.BendingStressConcentrationFactor >= 1.0, $"{input.BendingStressConcentrationFactor:0.###}");
        ModuleGuards.Require(context, "Torsional stress concentration factor must be at least 1.0.", input.TorsionalStressConcentrationFactor >= 1.0, $"{input.TorsionalStressConcentrationFactor:0.###}");
        ModuleGuards.Require(context, "Required safety factor must be at least 1.0.", input.RequiredSafetyFactor >= 1.0, $"{input.RequiredSafetyFactor:0.###}");

        context.ReferenceMaterial(input.MaterialPin.RecordId);
        context.RecordIntermediate("Material reference", input.MaterialPin.ToString());

        var piD3 = Math.PI * diameterMm * diameterMm * diameterMm;
        var bendingMPa = input.BendingStressConcentrationFactor * 32.0 * momentNmm / piD3;
        var shearMPa = input.TorsionalStressConcentrationFactor * 16.0 * torqueNmm / piD3;
        var maximumShearMPa = Math.Sqrt(bendingMPa * bendingMPa / 4.0 + shearMPa * shearMPa);
        var vonMisesMPa = Math.Sqrt(bendingMPa * bendingMPa + 3.0 * shearMPa * shearMPa);
        var trescaFactor = (yieldMPa / 2.0) / maximumShearMPa;
        var vonMisesFactor = yieldMPa / vonMisesMPa;
        var governing = Math.Min(trescaFactor, vonMisesFactor);
        var met = ModuleGuards.IsMet(input.RequiredSafetyFactor / governing);

        var bending = new Quantity<Pressure>(bendingMPa, PressureUnits.Megapascal);
        var shear = new Quantity<Pressure>(shearMPa, PressureUnits.Megapascal);
        var maximumShear = new Quantity<Pressure>(maximumShearMPa, PressureUnits.Megapascal);
        var vonMises = new Quantity<Pressure>(vonMisesMPa, PressureUnits.Megapascal);

        context.RecordIntermediate("Pi d cubed", new Quantity<SectionModulus>(piD3, SectionModulusUnits.MillimetreCubed));
        context.RecordIntermediate("Bending stress", bending);
        context.RecordIntermediate("Torsional shear stress", shear);
        context.RecordIntermediate("Maximum shear stress", maximumShear);
        context.RecordIntermediate("Von Mises stress", vonMises);
        context.RecordIntermediate("Tresca safety factor", trescaFactor);
        context.RecordIntermediate("Von Mises safety factor", vonMisesFactor);

        context.RecordConstraintCheck(
            "The governing factor of safety must reach the required factor.",
            met,
            $"Governing {governing:0.####} (Tresca {trescaFactor:0.####}, von Mises {vonMisesFactor:0.####}) against required {input.RequiredSafetyFactor:0.###}.");

        return new ShaftCombinedStressResult(
            ModuleGuards.OutcomeOf(met),
            bending,
            shear,
            maximumShear,
            vonMises,
            trescaFactor,
            vonMisesFactor,
            governing,
            met);
    }
}
