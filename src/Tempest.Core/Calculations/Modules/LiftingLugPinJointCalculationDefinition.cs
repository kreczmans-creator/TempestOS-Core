using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>The five checks a lifting lug and its pin are put through.</summary>
public enum LugCheck
{
    /// <summary>Tension across the net section through the hole.</summary>
    NetSection,

    /// <summary>Bearing of the pin on the hole.</summary>
    Bearing,

    /// <summary>Shear tear-out of the lug beyond the hole, on two planes.</summary>
    TearOut,

    /// <summary>The pin in double shear.</summary>
    PinShear,

    /// <summary>The pin in bending between the cheek plates.</summary>
    PinBending,
}

/// <summary>The inputs to one lifting lug and pin joint check.</summary>
/// <param name="LugMaterialPin">The released material record the lug allowables were derived from.</param>
/// <param name="PinMaterialPin">The released material record the pin allowables were derived from.</param>
/// <param name="Load">The load through the pin.</param>
/// <param name="LugThickness">The lug plate thickness.</param>
/// <param name="LugWidth">The lug width across the hole.</param>
/// <param name="HoleDiameter">The hole diameter.</param>
/// <param name="PinDiameter">The pin diameter.</param>
/// <param name="EdgeDistance">Hole edge to lug end, in the load direction.</param>
/// <param name="CheekPlateThickness">The thickness of each cheek plate the pin bears on.</param>
/// <param name="Clearance">The gap between the lug face and each cheek plate.</param>
/// <param name="AllowableTensileStress">The lug allowable in tension.</param>
/// <param name="AllowableBearingStress">The lug allowable in bearing.</param>
/// <param name="AllowableShearStress">The lug allowable in shear.</param>
/// <param name="PinAllowableBendingStress">The pin allowable in bending.</param>
/// <param name="PinAllowableShearStress">The pin allowable in shear.</param>
public sealed record LiftingLugPinJointInput(
    ReferencePin LugMaterialPin,
    ReferencePin PinMaterialPin,
    Quantity<Force> Load,
    Quantity<Length> LugThickness,
    Quantity<Length> LugWidth,
    Quantity<Length> HoleDiameter,
    Quantity<Length> PinDiameter,
    Quantity<Length> EdgeDistance,
    Quantity<Length> CheekPlateThickness,
    Quantity<Length> Clearance,
    Quantity<Pressure> AllowableTensileStress,
    Quantity<Pressure> AllowableBearingStress,
    Quantity<Pressure> AllowableShearStress,
    Quantity<Pressure> PinAllowableBendingStress,
    Quantity<Pressure> PinAllowableShearStress);

/// <summary>The result of one lifting lug check. Figures are <see langword="null"/> only when the method was refused.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="RefusalReason">Why the method was refused, or <see langword="null"/>.</param>
/// <param name="PinToHoleRatio">Pin diameter over hole diameter, the ratio the refusal is decided on.</param>
/// <param name="NetSectionStress">Load over net section area.</param>
/// <param name="NetSectionUtilisation">Net section stress over its allowable.</param>
/// <param name="BearingStress">Load over projected bearing area.</param>
/// <param name="BearingUtilisation">Bearing stress over its allowable.</param>
/// <param name="TearOutStress">Load over the two shear planes beyond the hole.</param>
/// <param name="TearOutUtilisation">Tear-out stress over its allowable.</param>
/// <param name="PinShearStress">Load over the pin's two shear areas.</param>
/// <param name="PinShearUtilisation">Pin shear stress over its allowable.</param>
/// <param name="PinBendingMoment">The pin's bending moment at its centre.</param>
/// <param name="PinBendingStress">Pin bending moment over the pin's section modulus.</param>
/// <param name="PinBendingUtilisation">Pin bending stress over its allowable.</param>
/// <param name="GoverningCheck">The check with the highest utilisation.</param>
/// <param name="GoverningUtilisation">That utilisation.</param>
public sealed record LiftingLugPinJointResult(
    EngineeringCheckOutcome Outcome,
    string? RefusalReason,
    double PinToHoleRatio,
    Quantity<Pressure>? NetSectionStress,
    double? NetSectionUtilisation,
    Quantity<Pressure>? BearingStress,
    double? BearingUtilisation,
    Quantity<Pressure>? TearOutStress,
    double? TearOutUtilisation,
    Quantity<Pressure>? PinShearStress,
    double? PinShearUtilisation,
    Quantity<Torque>? PinBendingMoment,
    Quantity<Pressure>? PinBendingStress,
    double? PinBendingUtilisation,
    LugCheck? GoverningCheck,
    double? GoverningUtilisation);

/// <summary>
/// A lifting lug and its pin: net section, bearing, tear-out, pin shear and
/// pin bending. Specified in
/// <c>docs/engineering/calculations/calc.lifting-lug-pin-joint.md</c>.
/// </summary>
public sealed class LiftingLugPinJointCalculationDefinition : ICalculationDefinition<LiftingLugPinJointInput, LiftingLugPinJointResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.lifting-lug-pin-joint";

    /// <summary>The pin-to-hole diameter ratio below which the uniform-bearing idealisation is refused.</summary>
    public const double MinimumPinToHoleRatio = 0.9;

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Lifting Lug and Pin Joint",
        Description:
            "Net-section tension, pin bearing and shear tear-out of a lifting lug, and double shear and bending of its pin, "
            + "each against its allowable; the governing check reported.",
        Category: "Lifting",
        Assumptions:
        [
            new CalculationAssumption("Bearing is uniform over the projected pin area, and the tear-out planes are two parallel planes the length of the edge distance.", "The classical hand check; a close-fitting pin."),
            new CalculationAssumption("The pin is loaded uniformly by the lug and supported uniformly by two cheek plates, giving M = (P/2)(t/4 + g + ts/2).", "The usual pin bending idealisation."),
            new CalculationAssumption("Allowable stresses are supplied by the caller from the material record and the design category in force.", "This module applies no design factor of its own."),
        ],
        Constraints:
        [
            new CalculationConstraint("Load, thicknesses, diameters, edge distance and allowables must be positive; clearance must not be negative; the width must exceed the hole and the pin must fit it."),
            new CalculationConstraint("Pin diameter must be at least 0.9 of the hole diameter (method limit; refused below it)."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any input that must be positive is zero or negative, the clearance is negative, the width does not exceed the hole, or the pin does not fit the hole.</exception>
    public LiftingLugPinJointResult Calculate(LiftingLugPinJointInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var loadN = input.Load.BaseValue;
        var tMm = input.LugThickness.ConvertTo(LengthUnits.Millimetre).Value;
        var widthMm = input.LugWidth.ConvertTo(LengthUnits.Millimetre).Value;
        var holeMm = input.HoleDiameter.ConvertTo(LengthUnits.Millimetre).Value;
        var pinMm = input.PinDiameter.ConvertTo(LengthUnits.Millimetre).Value;
        var edgeMm = input.EdgeDistance.ConvertTo(LengthUnits.Millimetre).Value;
        var cheekMm = input.CheekPlateThickness.ConvertTo(LengthUnits.Millimetre).Value;
        var clearanceMm = input.Clearance.ConvertTo(LengthUnits.Millimetre).Value;
        var tensileMPa = input.AllowableTensileStress.ConvertTo(PressureUnits.Megapascal).Value;
        var bearingMPa = input.AllowableBearingStress.ConvertTo(PressureUnits.Megapascal).Value;
        var shearMPa = input.AllowableShearStress.ConvertTo(PressureUnits.Megapascal).Value;
        var pinBendingMPa = input.PinAllowableBendingStress.ConvertTo(PressureUnits.Megapascal).Value;
        var pinShearMPa = input.PinAllowableShearStress.ConvertTo(PressureUnits.Megapascal).Value;

        ModuleGuards.Require(context, "Load must be positive.", loadN > 0, $"{loadN:0.###} N");
        ModuleGuards.Require(context, "Lug thickness must be positive.", tMm > 0, $"{tMm:0.###} mm");
        ModuleGuards.Require(context, "Hole diameter must be positive.", holeMm > 0, $"{holeMm:0.###} mm");
        ModuleGuards.Require(context, "Lug width must exceed the hole diameter.", widthMm > holeMm, $"width {widthMm:0.###} mm, hole {holeMm:0.###} mm");
        ModuleGuards.Require(context, "Pin diameter must be positive.", pinMm > 0, $"{pinMm:0.###} mm");
        // With the framework's slack: a pin stated as exactly the hole size in
        // another unit must not be refused as "does not fit" by a rounding artefact.
        ModuleGuards.Require(context, "Pin diameter must not exceed the hole diameter.", pinMm <= holeMm * (1.0 + ModuleGuards.AcceptanceRelativeTolerance), $"pin {pinMm:0.###} mm, hole {holeMm:0.###} mm");
        ModuleGuards.Require(context, "Edge distance must be positive.", edgeMm > 0, $"{edgeMm:0.###} mm");
        ModuleGuards.Require(context, "Cheek plate thickness must be positive.", cheekMm > 0, $"{cheekMm:0.###} mm");
        ModuleGuards.Require(context, "Clearance must not be negative.", clearanceMm >= 0, $"{clearanceMm:0.###} mm");
        ModuleGuards.Require(context, "Allowable tensile stress must be positive.", tensileMPa > 0, $"{tensileMPa:0.###} MPa");
        ModuleGuards.Require(context, "Allowable bearing stress must be positive.", bearingMPa > 0, $"{bearingMPa:0.###} MPa");
        ModuleGuards.Require(context, "Allowable shear stress must be positive.", shearMPa > 0, $"{shearMPa:0.###} MPa");
        ModuleGuards.Require(context, "Pin allowable bending stress must be positive.", pinBendingMPa > 0, $"{pinBendingMPa:0.###} MPa");
        ModuleGuards.Require(context, "Pin allowable shear stress must be positive.", pinShearMPa > 0, $"{pinShearMPa:0.###} MPa");

        context.ReferenceMaterial(input.LugMaterialPin.RecordId);
        context.ReferenceMaterial(input.PinMaterialPin.RecordId);
        context.RecordIntermediate("Lug material reference", input.LugMaterialPin.ToString());
        context.RecordIntermediate("Pin material reference", input.PinMaterialPin.ToString());

        var pinToHole = pinMm / holeMm;
        context.RecordIntermediate("Pin-to-hole ratio", pinToHole);

        // With the framework's slack: a pin at exactly 0.9 of the hole, stated in
        // another unit, must not be refused by a rounding artefact.
        if (pinToHole < MinimumPinToHoleRatio * (1.0 - ModuleGuards.AcceptanceRelativeTolerance))
        {
            var reason = ModuleGuards.Refuse(
                context,
                "Pin diameter must be at least 0.9 of the hole diameter (method limit; refused below it).",
                $"Refused: the pin fills only {pinToHole:0.###} of the hole diameter, below {MinimumPinToHoleRatio}. The uniform-bearing "
                + "idealisation assumes a close-fitting pin; a looser fit concentrates bearing over a narrow contact.");

            return new LiftingLugPinJointResult(
                EngineeringCheckOutcome.OutsideMethodLimits, reason, pinToHole,
                null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        var netAreaMm2 = (widthMm - holeMm) * tMm;
        var bearingAreaMm2 = pinMm * tMm;
        var tearOutAreaMm2 = 2.0 * edgeMm * tMm;
        var pinShearAreaMm2 = 2.0 * Math.PI * pinMm * pinMm / 4.0;
        var pinMomentNmm = (loadN / 2.0) * (tMm / 4.0 + clearanceMm + cheekMm / 2.0);
        var pinSectionModulusMm3 = Math.PI * pinMm * pinMm * pinMm / 32.0;

        var netStress = loadN / netAreaMm2;
        var bearingStress = loadN / bearingAreaMm2;
        var tearOutStress = loadN / tearOutAreaMm2;
        var pinShearStress = loadN / pinShearAreaMm2;
        var pinBendingStress = pinMomentNmm / pinSectionModulusMm3;

        var checks = new (LugCheck Check, double Stress, double Allowable, string Description)[]
        {
            (LugCheck.NetSection, netStress, tensileMPa, "Net-section tensile stress must not exceed the allowable tensile stress."),
            (LugCheck.Bearing, bearingStress, bearingMPa, "Bearing stress must not exceed the allowable bearing stress."),
            (LugCheck.TearOut, tearOutStress, shearMPa, "Tear-out shear stress must not exceed the allowable shear stress."),
            (LugCheck.PinShear, pinShearStress, pinShearMPa, "Pin shear stress must not exceed the pin allowable shear stress."),
            (LugCheck.PinBending, pinBendingStress, pinBendingMPa, "Pin bending stress must not exceed the pin allowable bending stress."),
        };

        context.RecordIntermediate("Net section area", new Quantity<Area>(netAreaMm2, AreaUnits.SquareMillimetre));
        context.RecordIntermediate("Bearing area", new Quantity<Area>(bearingAreaMm2, AreaUnits.SquareMillimetre));
        context.RecordIntermediate("Tear-out shear area", new Quantity<Area>(tearOutAreaMm2, AreaUnits.SquareMillimetre));
        context.RecordIntermediate("Pin shear area", new Quantity<Area>(pinShearAreaMm2, AreaUnits.SquareMillimetre));
        context.RecordIntermediate("Pin bending moment", new Quantity<Torque>(pinMomentNmm, TorqueUnits.NewtonMillimetre));
        context.RecordIntermediate("Pin section modulus", new Quantity<SectionModulus>(pinSectionModulusMm3, SectionModulusUnits.MillimetreCubed));

        var everyMet = true;
        var governing = checks[0].Check;
        var governingUtilisation = double.NegativeInfinity;
        var utilisations = new double[checks.Length];

        for (var i = 0; i < checks.Length; i++)
        {
            var (check, stress, allowable, description) = checks[i];
            var utilisation = stress / allowable;
            utilisations[i] = utilisation;
            var met = ModuleGuards.IsMet(utilisation);
            everyMet &= met;

            context.RecordIntermediate($"{check} utilisation", utilisation);
            context.RecordConstraintCheck(description, met, $"{stress:0.###} MPa against {allowable:0.###} MPa; utilisation {utilisation:0.####}.");

            if (utilisation > governingUtilisation)
            {
                governingUtilisation = utilisation;
                governing = check;
            }
        }

        context.RecordIntermediate("Governing check", governing.ToString());

        return new LiftingLugPinJointResult(
            ModuleGuards.OutcomeOf(everyMet),
            null,
            pinToHole,
            MPa(netStress), utilisations[0],
            MPa(bearingStress), utilisations[1],
            MPa(tearOutStress), utilisations[2],
            MPa(pinShearStress), utilisations[3],
            new Quantity<Torque>(pinMomentNmm, TorqueUnits.NewtonMillimetre),
            MPa(pinBendingStress), utilisations[4],
            governing,
            governingUtilisation);
    }

    private static Quantity<Pressure> MPa(double value) => new(value, PressureUnits.Megapascal);
}
