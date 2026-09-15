using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.lifting-lug-pin-joint</c> — the worked examples
/// and edge cases of <c>docs/engineering/calculations/calc.lifting-lug-pin-joint.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class LiftingLugPinJointVectors
{
    /// <summary>Outcome the vector expects.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <inheritdoc cref="Computes"/>
    public const string Refused = "Refused";

    /// <summary>The five stresses and utilisations a computed vector expects, in the order the specification lists them.</summary>
    public sealed record Expected(
        Quantity<Pressure> NetSectionStress, double NetSectionUtilisation,
        Quantity<Pressure> BearingStress, double BearingUtilisation,
        Quantity<Pressure> TearOutStress, double TearOutUtilisation,
        Quantity<Pressure> PinShearStress, double PinShearUtilisation,
        Quantity<Pressure> PinBendingStress, double PinBendingUtilisation,
        string GoverningCheck, bool MeetsCriteria);

    /// <summary>One vector.</summary>
    public sealed record Vector(
        string Name,
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
        Quantity<Pressure> PinAllowableShearStress,
        string ExpectedOutcome,
        Expected? ExpectedResult = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures).</summary>
        public double RelativeTolerance => 1e-5;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Force> kN(double v) => new(v, ForceUnits.Kilonewton);
    private static Quantity<Length> mm(double v) => new(v, LengthUnits.Millimetre);
    private static Quantity<Length> inch(double mmValue) => new(mmValue / 25.4, LengthUnits.Inch);
    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);
    private static Quantity<Pressure> psi(double mpa) => new(mpa * 1e6 / 6894.757293168, PressureUnits.Psi);

    private static readonly Expected Example1 = new(
        MPa(36.765), 0.24510, MPa(83.333), 0.41667, MPa(31.250), 0.34722, MPa(35.368), 0.23579, MPa(122.61), 0.49043, "PinBending", true);

    private static readonly Expected Example2 = new(
        MPa(125.00), 0.83333, MPa(320.00), 1.6000, MPa(133.33), 1.4815, MPa(122.23), 0.81487, MPa(381.36), 1.5254, "Bearing", false);

    /// <summary>The two worked examples, plus Example 1 in imperial units.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: a lug that passes",
            kN(50), mm(20), mm(100), mm(32), mm(30), mm(40), mm(12), mm(2), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150),
            Computes, Example1),

        new("Example 2: a lug that fails three ways",
            kN(120), mm(15), mm(90), mm(26), mm(25), mm(30), mm(10), mm(1), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150),
            Computes, Example2),

        new("Example 1 in imperial units",
            new Quantity<Force>(50_000 / 4.4482216152605, ForceUnits.PoundForce),
            inch(20), inch(100), inch(32), inch(30), inch(40), inch(12), inch(2), psi(150), psi(200), psi(90), psi(250), psi(150),
            Computes, Example1),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero load", kN(0), mm(20), mm(100), mm(32), mm(30), mm(40), mm(12), mm(2), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150), InvalidInput, ExpectedReasonFragment: "Load"),
        new("negative thickness", kN(50), mm(-20), mm(100), mm(32), mm(30), mm(40), mm(12), mm(2), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150), InvalidInput, ExpectedReasonFragment: "thickness"),
        new("width equal to the hole", kN(50), mm(20), mm(32), mm(32), mm(30), mm(40), mm(12), mm(2), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150), InvalidInput, ExpectedReasonFragment: "width"),
        new("pin larger than the hole", kN(50), mm(20), mm(100), mm(32), mm(33), mm(40), mm(12), mm(2), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150), InvalidInput, ExpectedReasonFragment: "hole"),
        new("zero edge distance", kN(50), mm(20), mm(100), mm(32), mm(30), mm(0), mm(12), mm(2), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150), InvalidInput, ExpectedReasonFragment: "Edge distance"),
        new("negative clearance", kN(50), mm(20), mm(100), mm(32), mm(30), mm(40), mm(12), mm(-1), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150), InvalidInput, ExpectedReasonFragment: "Clearance"),
        new("zero pin allowable", kN(50), mm(20), mm(100), mm(32), mm(30), mm(40), mm(12), mm(2), MPa(150), MPa(200), MPa(90), MPa(0), MPa(150), InvalidInput, ExpectedReasonFragment: "allowable"),
    ];

    /// <summary>Inputs outside the method's limits — refused in the result, never thrown.</summary>
    public static TheoryData<Vector> Refusals =>
    [
        // 25 mm pin in a 32 mm hole: ratio 0.78, below 0.9.
        new("loose pin", kN(50), mm(20), mm(100), mm(32), mm(25), mm(40), mm(12), mm(2), MPa(150), MPa(200), MPa(90), MPa(250), MPa(150), Refused, ExpectedReasonFragment: "pin"),
    ];
}
