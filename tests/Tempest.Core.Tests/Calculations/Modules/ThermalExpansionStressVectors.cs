using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.thermal-expansion-stress</c> — the worked
/// examples and edge cases of
/// <c>docs/engineering/calculations/calc.thermal-expansion-stress.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class ThermalExpansionStressVectors
{
    /// <summary>Outcome the vector expects.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <summary>One vector. A null restraint stiffness is a rigid restraint.</summary>
    public sealed record Vector(
        string Name,
        Quantity<Length> Length,
        Quantity<Area> Area,
        Quantity<Pressure> YoungsModulus,
        Quantity<ThermalExpansion> ExpansionCoefficient,
        Quantity<TemperatureDelta> TemperatureChange,
        Quantity<Length> Gap,
        Quantity<Stiffness>? RestraintStiffness,
        Quantity<Pressure> AllowableStress,
        string ExpectedOutcome,
        Quantity<Length>? ExpectedFreeMovement = null,
        Quantity<Force>? ExpectedRestraintForce = null,
        Quantity<Pressure>? ExpectedStress = null,
        Quantity<Length>? ExpectedActualMovement = null,
        bool? ExpectedMeetsCriteria = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Length> mm(double v) => new(v, LengthUnits.Millimetre);
    private static Quantity<Length> m(double v) => new(v, LengthUnits.Metre);
    private static Quantity<Area> mm2(double v) => new(v, AreaUnits.SquareMillimetre);
    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);
    private static Quantity<Pressure> GPa(double v) => new(v, PressureUnits.Gigapascal);
    private static Quantity<ThermalExpansion> perK(double v) => new(v, ThermalExpansionUnits.PerKelvin);
    private static Quantity<TemperatureDelta> K(double v) => new(v, TemperatureDeltaUnits.Kelvin);
    private static Quantity<Force> N(double v) => new(v, ForceUnits.Newton);
    private static Quantity<Stiffness> kNmm(double v) => new(v, StiffnessUnits.KilonewtonPerMillimetre);

    /// <summary>The five worked examples, plus Example 1 in imperial units.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: rigid walls, no gap, +60 K",
            m(2), mm2(500), GPa(200), perK(12e-6), K(60), mm(0), null, MPa(150),
            Computes, mm(1.4400), N(72_000), MPa(-144.00), mm(0), ExpectedMeetsCriteria: true),

        new("Example 2: rigid walls, 0.5 mm gap, +60 K",
            m(2), mm2(500), GPa(200), perK(12e-6), K(60), mm(0.5), null, MPa(150),
            Computes, mm(1.4400), N(47_000), MPa(-94.000), mm(0.50000), ExpectedMeetsCriteria: true),

        new("Example 3: elastic restraint 100 kN/mm, no gap, +60 K",
            m(2), mm2(500), GPa(200), perK(12e-6), K(60), mm(0), kNmm(100), MPa(150),
            Computes, mm(1.4400), N(48_000), MPa(-96.000), mm(0.48000), ExpectedMeetsCriteria: true),

        new("Example 4: cooling 40 K, rigid, no gap",
            m(2), mm2(500), GPa(200), perK(12e-6), K(-40), mm(0), null, MPa(150),
            Computes, mm(-0.96000), N(-48_000), MPa(96.000), mm(0), ExpectedMeetsCriteria: true),

        new("Example 5: +10 K against a 0.5 mm gap",
            m(2), mm2(500), GPa(200), perK(12e-6), K(10), mm(0.5), null, MPa(150),
            Computes, mm(0.24000), N(0), MPa(0), mm(0.24000), ExpectedMeetsCriteria: true),

        // 12e-6 /K = 6.6667 uin/(in.degF); 60 K = 108 degF of change.
        new("Example 1 in imperial units",
            new Quantity<Length>(2 / 0.0254, LengthUnits.Inch),
            new Quantity<Area>(500e-6 / 0.09290304, AreaUnits.SquareFoot),
            new Quantity<Pressure>(200e9 / 6894.757293168, PressureUnits.Psi),
            new Quantity<ThermalExpansion>(12e-6 / 1.8e-6, ThermalExpansionUnits.MicroinchPerInchDegreeFahrenheit),
            new Quantity<TemperatureDelta>(108, TemperatureDeltaUnits.FahrenheitDegree),
            new Quantity<Length>(0, LengthUnits.Inch), null,
            new Quantity<Pressure>(150e6 / 6894.757293168, PressureUnits.Psi),
            Computes, mm(1.4400), N(72_000), MPa(-144.00), mm(0), ExpectedMeetsCriteria: true),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero length", m(0), mm2(500), GPa(200), perK(12e-6), K(60), mm(0), null, MPa(150), InvalidInput, ExpectedReasonFragment: "Length"),
        new("negative area", m(2), mm2(-500), GPa(200), perK(12e-6), K(60), mm(0), null, MPa(150), InvalidInput, ExpectedReasonFragment: "Area"),
        new("zero modulus", m(2), mm2(500), GPa(0), perK(12e-6), K(60), mm(0), null, MPa(150), InvalidInput, ExpectedReasonFragment: "modulus"),
        new("zero expansion coefficient", m(2), mm2(500), GPa(200), perK(0), K(60), mm(0), null, MPa(150), InvalidInput, ExpectedReasonFragment: "coefficient"),
        new("negative gap", m(2), mm2(500), GPa(200), perK(12e-6), K(60), mm(-0.1), null, MPa(150), InvalidInput, ExpectedReasonFragment: "Gap"),
        new("zero restraint stiffness", m(2), mm2(500), GPa(200), perK(12e-6), K(60), mm(0), kNmm(0), MPa(150), InvalidInput, ExpectedReasonFragment: "stiffness"),
        new("zero allowable", m(2), mm2(500), GPa(200), perK(12e-6), K(60), mm(0), null, MPa(0), InvalidInput, ExpectedReasonFragment: "Allowable"),
    ];
}
