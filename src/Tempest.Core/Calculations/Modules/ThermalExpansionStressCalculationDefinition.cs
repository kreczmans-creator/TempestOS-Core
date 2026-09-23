using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>The inputs to one thermal expansion and restrained stress calculation.</summary>
/// <param name="MaterialPin">The released material record the modulus and expansion coefficient came from.</param>
/// <param name="Length">The bar length.</param>
/// <param name="Area">The bar section area.</param>
/// <param name="YoungsModulus">The material's own Young's modulus.</param>
/// <param name="ExpansionCoefficient">The material's own coefficient of linear thermal expansion.</param>
/// <param name="TemperatureChange">The temperature change; positive for heating.</param>
/// <param name="Gap">The clearance that must close before the restraint engages, in either direction.</param>
/// <param name="RestraintStiffness">The restraint's stiffness, or <see langword="null"/> for a rigid restraint.</param>
/// <param name="AllowableStress">The allowable the stress magnitude is compared against.</param>
public sealed record ThermalExpansionStressInput(
    ReferencePin MaterialPin,
    Quantity<Length> Length,
    Quantity<Area> Area,
    Quantity<Pressure> YoungsModulus,
    Quantity<ThermalExpansion> ExpansionCoefficient,
    Quantity<TemperatureDelta> TemperatureChange,
    Quantity<Length> Gap,
    Quantity<Stiffness>? RestraintStiffness,
    Quantity<Pressure> AllowableStress);

/// <summary>The result of one thermal expansion calculation. This method has no refusal, so every figure is always present.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="FreeMovement">The unrestrained thermal movement; positive for expansion.</param>
/// <param name="EngagedMovement">The free movement beyond the gap, signed.</param>
/// <param name="BarStiffness">The bar's axial stiffness, E A over L.</param>
/// <param name="RestraintForce">The force the restraint exerts; positive when it resists expansion.</param>
/// <param name="Stress">The axial stress; negative (compressive) on restrained heating.</param>
/// <param name="ActualMovement">The movement that actually occurs.</param>
/// <param name="Utilisation">Stress magnitude over the allowable.</param>
/// <param name="CriterionMet">Whether the utilisation is at or below one.</param>
public sealed record ThermalExpansionStressResult(
    EngineeringCheckOutcome Outcome,
    Quantity<Length> FreeMovement,
    Quantity<Length> EngagedMovement,
    Quantity<Stiffness> BarStiffness,
    Quantity<Force> RestraintForce,
    Quantity<Pressure> Stress,
    Quantity<Length> ActualMovement,
    double Utilisation,
    bool CriterionMet);

/// <summary>
/// Thermal expansion of a bar and the stress its restraint induces, with an
/// optional gap and an optional elastic restraint. Specified in
/// <c>docs/engineering/calculations/calc.thermal-expansion-stress.md</c>.
/// </summary>
public sealed class ThermalExpansionStressCalculationDefinition : ICalculationDefinition<ThermalExpansionStressInput, ThermalExpansionStressResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.thermal-expansion-stress";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Thermal Expansion and Restrained Thermal Stress",
        Description:
            "Free thermal movement of a bar, and the force and stress that develop when a rigid or elastic restraint "
            + "stops it beyond a clearance, compared against an allowable stress.",
        Category: "Thermal",
        Assumptions:
        [
            new CalculationAssumption("The expansion coefficient is constant over the temperature change and the bar stays elastic.", "Linear thermal expansion with a linear material."),
            new CalculationAssumption("The restraint engages in either direction once the clearance has closed, and the bar and the restraint act as springs in series.", "The compatibility statement of the restrained bar."),
        ],
        Constraints:
        [
            new CalculationConstraint("Length, area, Young's modulus, expansion coefficient and allowable stress must be positive; the gap must not be negative; a restraint stiffness, when given, must be positive."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any constraint above is not met.</exception>
    public ThermalExpansionStressResult Calculate(ThermalExpansionStressInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var lengthM = input.Length.BaseValue;
        var areaM2 = input.Area.BaseValue;
        var modulusPa = input.YoungsModulus.BaseValue;
        var alphaPerK = input.ExpansionCoefficient.BaseValue;
        var deltaK = input.TemperatureChange.BaseValue;
        var gapM = input.Gap.BaseValue;
        var restraintNpm = input.RestraintStiffness?.BaseValue;
        var allowablePa = input.AllowableStress.BaseValue;

        ModuleGuards.Require(context, "Length must be positive.", lengthM > 0, input, nameof(input.Length));
        ModuleGuards.Require(context, "Area must be positive.", areaM2 > 0, input, nameof(input.Area));
        ModuleGuards.Require(context, "Young's modulus must be positive.", modulusPa > 0, input, nameof(input.YoungsModulus));
        ModuleGuards.Require(context, "Expansion coefficient must be positive.", alphaPerK > 0, input, nameof(input.ExpansionCoefficient));
        ModuleGuards.Require(context, "Gap must not be negative.", gapM >= 0, input, nameof(input.Gap));
        ModuleGuards.Require(context, "Restraint stiffness must be positive when given.", restraintNpm is null || restraintNpm > 0, input, nameof(input.RestraintStiffness));
        ModuleGuards.Require(context, "Allowable stress must be positive.", allowablePa > 0, input, nameof(input.AllowableStress));

        context.ReferenceMaterial(input.MaterialPin.RecordId);
        context.RecordIntermediate("Material reference", input.MaterialPin.ToString());

        var freeM = alphaPerK * lengthM * deltaK;
        var engagedM = Math.Sign(freeM) * Math.Max(0.0, Math.Abs(freeM) - gapM);
        var barStiffnessNpm = modulusPa * areaM2 / lengthM;
        var forceN = restraintNpm is { } restraint
            ? engagedM / (1.0 / barStiffnessNpm + 1.0 / restraint)
            : engagedM * barStiffnessNpm;
        var stressPa = -forceN / areaM2;
        var actualM = freeM - forceN / barStiffnessNpm;
        var utilisation = Math.Abs(stressPa) / allowablePa;
        var met = ModuleGuards.IsMet(utilisation);

        var free = new Quantity<Length>(freeM, LengthUnits.Metre).ConvertTo(LengthUnits.Millimetre);
        var engaged = new Quantity<Length>(engagedM, LengthUnits.Metre).ConvertTo(LengthUnits.Millimetre);
        var barStiffness = new Quantity<Stiffness>(barStiffnessNpm, StiffnessUnits.NewtonPerMetre).ConvertTo(StiffnessUnits.NewtonPerMillimetre);
        var force = new Quantity<Force>(forceN, ForceUnits.Newton);
        var stress = new Quantity<Pressure>(stressPa, PressureUnits.Pascal).ConvertTo(PressureUnits.Megapascal);
        var actual = new Quantity<Length>(actualM, LengthUnits.Metre).ConvertTo(LengthUnits.Millimetre);

        context.RecordIntermediate("Free movement", free);
        context.RecordIntermediate("Gap consumed", new Quantity<Length>(Math.Min(Math.Abs(freeM), gapM), LengthUnits.Metre).ConvertTo(LengthUnits.Millimetre));
        context.RecordIntermediate("Bar stiffness", barStiffness);
        context.RecordIntermediate("Restraint", restraintNpm is { } r ? new Quantity<Stiffness>(r, StiffnessUnits.NewtonPerMetre).ConvertTo(StiffnessUnits.NewtonPerMillimetre).ToString() : "rigid");
        context.RecordIntermediate("Restraint force", force);
        context.RecordIntermediate("Stress", stress);
        context.RecordIntermediate("Utilisation", utilisation);

        context.RecordConstraintCheck(
            "Stress magnitude must not exceed the allowable stress.",
            met,
            $"Stress {stress.Value:0.###} MPa against allowable {input.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value:0.###} MPa; utilisation {utilisation:0.####}.");

        return new ThermalExpansionStressResult(
            ModuleGuards.OutcomeOf(met),
            free,
            engaged,
            barStiffness,
            force,
            stress,
            actual,
            utilisation,
            met);
    }
}
