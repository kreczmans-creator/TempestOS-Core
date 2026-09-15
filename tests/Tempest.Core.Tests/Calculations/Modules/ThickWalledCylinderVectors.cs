using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.thick-walled-cylinder</c> — the worked examples
/// and edge cases of <c>docs/engineering/calculations/calc.thick-walled-cylinder.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class ThickWalledCylinderVectors
{
    /// <summary>Outcome the vector expects.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <summary>The stresses a computed vector expects.</summary>
    public sealed record Expected(
        Quantity<Pressure> BoreRadial, Quantity<Pressure> BoreHoop,
        Quantity<Pressure> OuterRadial, Quantity<Pressure> OuterHoop,
        Quantity<Pressure> Axial,
        Quantity<Pressure> BoreVonMises, Quantity<Pressure> BoreTresca, Quantity<Pressure> OuterVonMises,
        Quantity<Pressure> MaximumVonMises, double Utilisation,
        Quantity<Pressure> ThinWallHoopEstimate, bool MeetsCriteria);

    /// <summary>One vector.</summary>
    public sealed record Vector(
        string Name,
        Quantity<Length> InnerRadius,
        Quantity<Length> OuterRadius,
        Quantity<Pressure> InternalPressure,
        Quantity<Pressure> ExternalPressure,
        bool ClosedEnds,
        Quantity<Pressure> AllowableStress,
        string ExpectedOutcome,
        Expected? ExpectedResult = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Length> mm(double v) => new(v, LengthUnits.Millimetre);
    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);

    private static readonly Expected Example1 = new(
        MPa(-50.000), MPa(83.333), MPa(0), MPa(33.333), MPa(16.667),
        MPa(115.47), MPa(133.33), MPa(28.868), MPa(115.47), 0.57735, MPa(75.000), true);

    private static readonly Expected Example2 = new(
        MPa(0), MPa(-72.000), MPa(-20.000), MPa(-52.000), MPa(-36.000),
        MPa(62.354), MPa(72.000), MPa(27.713), MPa(62.354), 0.62354, MPa(-50.000), true);

    private static readonly Expected Example3 = new(
        MPa(-50.000), MPa(83.333), MPa(0), MPa(33.333), MPa(0),
        MPa(116.67), MPa(133.33), MPa(33.333), MPa(116.67), 0.58333, MPa(75.000), true);

    /// <summary>The three worked examples, plus Example 1 in inches, psi and bar.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: internal pressure, closed ends", mm(50), mm(100), MPa(50), MPa(0), true, MPa(200), Computes, Example1),
        new("Example 2: external pressure, closed ends", mm(40), mm(60), MPa(0), MPa(20), true, MPa(100), Computes, Example2),
        new("Example 3: internal pressure, open ends", mm(50), mm(100), MPa(50), MPa(0), false, MPa(200), Computes, Example3),
        new("Example 1 in inches, psi and bar",
            new Quantity<Length>(50 / 25.4, LengthUnits.Inch), new Quantity<Length>(100 / 25.4, LengthUnits.Inch),
            new Quantity<Pressure>(50e6 / 6894.757293168, PressureUnits.Psi), new Quantity<Pressure>(0, PressureUnits.Bar), true,
            new Quantity<Pressure>(2000, PressureUnits.Bar),
            Computes, Example1),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero inner radius", mm(0), mm(100), MPa(50), MPa(0), true, MPa(200), InvalidInput, ExpectedReasonFragment: "Inner radius"),
        new("outer radius not larger than inner", mm(50), mm(50), MPa(50), MPa(0), true, MPa(200), InvalidInput, ExpectedReasonFragment: "Outer radius"),
        new("negative internal pressure", mm(50), mm(100), MPa(-1), MPa(0), true, MPa(200), InvalidInput, ExpectedReasonFragment: "pressure"),
        new("no pressure", mm(50), mm(100), MPa(0), MPa(0), true, MPa(200), InvalidInput, ExpectedReasonFragment: "pressure"),
        new("zero allowable", mm(50), mm(100), MPa(50), MPa(0), true, MPa(0), InvalidInput, ExpectedReasonFragment: "Allowable"),
    ];
}
