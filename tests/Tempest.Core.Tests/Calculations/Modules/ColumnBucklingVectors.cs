using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.column-buckling</c> — the worked examples and
/// edge cases of <c>docs/engineering/calculations/calc.column-buckling.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class ColumnBucklingVectors
{
    /// <summary>Outcome the vector expects.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <inheritdoc cref="Computes"/>
    public const string Refused = "Refused";

    /// <summary>One vector.</summary>
    public sealed record Vector(
        string Name,
        Quantity<Length> EffectiveLength,
        Quantity<Area> Area,
        Quantity<SecondMomentOfArea> SecondMomentOfArea,
        Quantity<Pressure> YoungsModulus,
        Quantity<Pressure> YieldStrength,
        double RobertsonConstant,
        Quantity<Force> AppliedLoad,
        string ExpectedOutcome,
        Quantity<Length>? ExpectedRadiusOfGyration = null,
        double? ExpectedSlenderness = null,
        Quantity<Pressure>? ExpectedEulerStress = null,
        double? ExpectedLimitingSlenderness = null,
        double? ExpectedPerryFactor = null,
        Quantity<Pressure>? ExpectedCompressiveStrength = null,
        Quantity<Force>? ExpectedCompressionResistance = null,
        double? ExpectedUtilisation = null,
        bool? ExpectedMeetsCriteria = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Force> kN(double v) => new(v, ForceUnits.Kilonewton);
    private static Quantity<Length> mm(double v) => new(v, LengthUnits.Millimetre);
    private static Quantity<Length> m(double v) => new(v, LengthUnits.Metre);
    private static Quantity<Area> mm2(double v) => new(v, AreaUnits.SquareMillimetre);
    private static Quantity<SecondMomentOfArea> mm4(double v) => new(v, SecondMomentOfAreaUnits.MillimetreToTheFourth);
    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);
    private static Quantity<Pressure> GPa(double v) => new(v, PressureUnits.Gigapascal);

    /// <summary>The three worked examples, plus Example 1 in imperial units.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: curve c, intermediate slenderness",
            m(3), mm2(2000), mm4(2.0e6), GPa(205), MPa(275), 5.5, kN(150),
            Computes, mm(31.623), 94.868, MPa(224.81), 17.155, 0.42742, MPa(133.78), kN(267.56), 0.56062, ExpectedMeetsCriteria: true),

        new("Example 2: slender, curve b, S355",
            m(1.5), mm2(1200), mm4(1.2e5), GPa(205), MPa(355), 3.5, kN(100),
            Computes, mm(10.000), 150.00, MPa(89.923), 15.099, 0.47215, MPa(77.973), kN(93.567), 1.0688, ExpectedMeetsCriteria: false),

        new("Example 3: stocky, curve c",
            m(1), mm2(2000), mm4(2.0e6), GPa(205), MPa(275), 5.5, kN(300),
            Computes, mm(31.623), 31.623, MPa(2023.3), 17.155, 0.079573, MPa(252.09), kN(504.17), 0.59504, ExpectedMeetsCriteria: true),

        new("Example 1 in imperial units",
            new Quantity<Length>(3 / 0.0254, LengthUnits.Inch),
            new Quantity<Area>(2000e-6 / 0.09290304, AreaUnits.SquareFoot),
            new Quantity<SecondMomentOfArea>(2.0e-6 / 4.162314256e-7, SecondMomentOfAreaUnits.InchToTheFourth),
            new Quantity<Pressure>(205e9 / 6894.757293168, PressureUnits.Psi),
            new Quantity<Pressure>(275e6 / 6894.757293168, PressureUnits.Psi), 5.5,
            new Quantity<Force>(150_000 / 4.4482216152605, ForceUnits.PoundForce),
            Computes, mm(31.623), 94.868, MPa(224.81), 17.155, 0.42742, MPa(133.78), kN(267.56), 0.56062, ExpectedMeetsCriteria: true),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero length", m(0), mm2(2000), mm4(2.0e6), GPa(205), MPa(275), 5.5, kN(150), InvalidInput, ExpectedReasonFragment: "length"),
        new("zero area", m(3), mm2(0), mm4(2.0e6), GPa(205), MPa(275), 5.5, kN(150), InvalidInput, ExpectedReasonFragment: "Area"),
        new("negative second moment", m(3), mm2(2000), mm4(-2.0e6), GPa(205), MPa(275), 5.5, kN(150), InvalidInput, ExpectedReasonFragment: "Second moment"),
        new("zero modulus", m(3), mm2(2000), mm4(2.0e6), GPa(0), MPa(275), 5.5, kN(150), InvalidInput, ExpectedReasonFragment: "modulus"),
        new("zero yield", m(3), mm2(2000), mm4(2.0e6), GPa(205), MPa(0), 5.5, kN(150), InvalidInput, ExpectedReasonFragment: "Yield"),
        new("zero Robertson constant", m(3), mm2(2000), mm4(2.0e6), GPa(205), MPa(275), 0, kN(150), InvalidInput, ExpectedReasonFragment: "Robertson"),
        new("negative load", m(3), mm2(2000), mm4(2.0e6), GPa(205), MPa(275), 5.5, kN(-150), InvalidInput, ExpectedReasonFragment: "load"),
    ];

    /// <summary>Inputs outside the method's limits — refused in the result, never thrown.</summary>
    public static TheoryData<Vector> Refusals =>
    [
        // Example 1's section at 5.75 m: lambda = 181.83.
        new("slenderness above 180", m(5.75), mm2(2000), mm4(2.0e6), GPa(205), MPa(275), 5.5, kN(150), Refused, ExpectedReasonFragment: "180"),
    ];
}
