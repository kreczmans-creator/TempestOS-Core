using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.bolt-group-eccentric-shear</c> — the worked
/// examples and edge cases of
/// <c>docs/engineering/calculations/calc.bolt-group-eccentric-shear.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class BoltGroupEccentricShearVectors
{
    /// <summary>Outcome the vector expects.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <inheritdoc cref="Computes"/>
    public const string Refused = "Refused";

    /// <summary>One bolt's position in the group's in-plane frame.</summary>
    public sealed record Point(Quantity<Length> X, Quantity<Length> Y);

    /// <summary>One vector. Expected bolt forces are in the same order as the bolts.</summary>
    public sealed record Vector(
        string Name,
        IReadOnlyList<Point> Bolts,
        Quantity<Force> LoadX,
        Quantity<Force> LoadY,
        Quantity<Length> LoadPointX,
        Quantity<Length> LoadPointY,
        Quantity<Force> AllowableShearPerBolt,
        string ExpectedOutcome,
        Quantity<Length>? ExpectedCentroidX = null,
        Quantity<Length>? ExpectedCentroidY = null,
        Quantity<Torque>? ExpectedMoment = null,
        Quantity<Area>? ExpectedUnitPolarMoment = null,
        IReadOnlyList<Quantity<Force>>? ExpectedBoltForces = null,
        int? ExpectedGoverningBoltIndex = null,
        double? ExpectedUtilisation = null,
        bool? ExpectedMeetsCriteria = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures).</summary>
        public double RelativeTolerance => 1e-5;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Force> N(double v) => new(v, ForceUnits.Newton);
    private static Quantity<Force> kN(double v) => new(v, ForceUnits.Kilonewton);
    private static Quantity<Length> mm(double v) => new(v, LengthUnits.Millimetre);
    private static Quantity<Torque> Nm(double v) => new(v, TorqueUnits.NewtonMetre);
    private static Quantity<Area> mm2(double v) => new(v, AreaUnits.SquareMillimetre);
    private static Point P(double x, double y) => new(mm(x), mm(y));
    private static Point PIn(double xMm, double yMm) => new(new Quantity<Length>(xMm / 25.4, LengthUnits.Inch), new Quantity<Length>(yMm / 25.4, LengthUnits.Inch));

    private static readonly IReadOnlyList<Point> FourBoltRectangle = [P(75, 50), P(75, -50), P(-75, 50), P(-75, -50)];
    private static readonly IReadOnlyList<Point> ThreeBoltLine = [P(0, 0), P(0, 80), P(0, 160)];

    /// <summary>The four worked examples, plus Example 1 in imperial units.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: four bolts, vertical load beside the group",
            FourBoltRectangle, N(0), kN(-20), mm(250), mm(0), kN(25),
            Computes, mm(0), mm(0), Nm(-5000), mm2(32_500),
            [N(18_239.85), N(18_239.85), N(10_095.70), N(10_095.70)], 0, 0.72959, ExpectedMeetsCriteria: true),

        new("Example 2: three bolts in a line, load beside the line",
            ThreeBoltLine, N(0), kN(-30), mm(120), mm(80), kN(20),
            Computes, mm(0), mm(80), Nm(-3600), mm2(12_800),
            [N(24_622.14), N(10_000), N(24_622.14)], 0, 1.2311, ExpectedMeetsCriteria: false),

        new("Example 3: three bolts, inclined load",
            ThreeBoltLine, kN(15), kN(-30), mm(120), mm(80), kN(40),
            Computes, mm(0), mm(80), Nm(-3600), mm2(12_800),
            [N(20_155.64), N(11_180.34), N(29_261.75)], 2, 0.73154, ExpectedMeetsCriteria: true),

        new("Example 4: load through the centroid",
            FourBoltRectangle, N(0), kN(-20), mm(0), mm(0), kN(25),
            Computes, mm(0), mm(0), Nm(0), mm2(32_500),
            [N(5000), N(5000), N(5000), N(5000)], 0, 0.2, ExpectedMeetsCriteria: true),

        new("Example 1 in imperial units",
            [PIn(75, 50), PIn(75, -50), PIn(-75, 50), PIn(-75, -50)],
            new Quantity<Force>(0, ForceUnits.PoundForce),
            new Quantity<Force>(-20_000 / 4.4482216152605, ForceUnits.PoundForce),
            new Quantity<Length>(250 / 25.4, LengthUnits.Inch), new Quantity<Length>(0, LengthUnits.Inch),
            new Quantity<Force>(25_000 / 4.4482216152605, ForceUnits.PoundForce),
            Computes, mm(0), mm(0), Nm(-5000), mm2(32_500),
            [N(18_239.85), N(18_239.85), N(10_095.70), N(10_095.70)], 0, 0.72959, ExpectedMeetsCriteria: true),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("no bolts", [], N(0), kN(-20), mm(250), mm(0), kN(25), InvalidInput, ExpectedReasonFragment: "bolt"),
        new("two bolts at the same point", [P(0, 0), P(0, 0), P(100, 0)], N(0), kN(-20), mm(250), mm(0), kN(25), InvalidInput, ExpectedReasonFragment: "coincident"),
        new("no load", FourBoltRectangle, N(0), N(0), mm(250), mm(0), kN(25), InvalidInput, ExpectedReasonFragment: "load"),
        new("zero allowable", FourBoltRectangle, N(0), kN(-20), mm(250), mm(0), N(0), InvalidInput, ExpectedReasonFragment: "Allowable"),
        new("negative allowable", FourBoltRectangle, N(0), kN(-20), mm(250), mm(0), kN(-25), InvalidInput, ExpectedReasonFragment: "Allowable"),
    ];

    /// <summary>Inputs outside the method's limits — refused in the result, never thrown.</summary>
    public static TheoryData<Vector> Refusals =>
    [
        new("one bolt with the load 100 mm from it", [P(0, 0)], N(0), kN(-20), mm(100), mm(0), kN(25), Refused, ExpectedReasonFragment: "moment"),
    ];

    /// <summary>A single bolt with the load through it is ordinary direct shear.</summary>
    public static TheoryData<Vector> SingleBoltDirectShear =>
    [
        new("one bolt, load through it", [P(0, 0)], N(0), kN(-20), mm(0), mm(0), kN(25),
            Computes, mm(0), mm(0), Nm(0), mm2(0), [N(20_000)], 0, 0.8, ExpectedMeetsCriteria: true),
    ];
}
