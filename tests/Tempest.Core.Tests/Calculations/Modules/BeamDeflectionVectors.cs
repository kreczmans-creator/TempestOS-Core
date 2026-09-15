using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.beam-deflection</c> — the worked examples and
/// edge cases of <c>docs/engineering/calculations/calc.beam-deflection.md</c>,
/// as data. Every expected figure was derived by hand (see the document),
/// never read out of the implementation.
/// </summary>
public static class BeamDeflectionVectors
{
    /// <summary>Outcome the vector expects: computes, invalid input, or a method-limit refusal.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <inheritdoc cref="Computes"/>
    public const string Refused = "Refused";

    /// <summary>One vector. Support and loading are the definition's enum member names.</summary>
    public sealed record Vector(
        string Name,
        string Support,
        string Loading,
        Quantity<Force> Load,
        Quantity<Length> Span,
        Quantity<Pressure> YoungsModulus,
        Quantity<SecondMomentOfArea> SecondMomentOfArea,
        Quantity<Length> ExtremeFibreDistance,
        Quantity<Pressure> AllowableBendingStress,
        Quantity<Length> DeflectionLimit,
        string ExpectedOutcome,
        Quantity<Torque>? ExpectedMaximumMoment = null,
        Quantity<Pressure>? ExpectedBendingStress = null,
        Quantity<Length>? ExpectedDeflection = null,
        bool? ExpectedMeetsCriteria = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Force> N(double v) => new(v, ForceUnits.Newton);
    private static Quantity<Force> kN(double v) => new(v, ForceUnits.Kilonewton);
    private static Quantity<Length> mm(double v) => new(v, LengthUnits.Millimetre);
    private static Quantity<Length> m(double v) => new(v, LengthUnits.Metre);
    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);
    private static Quantity<Pressure> GPa(double v) => new(v, PressureUnits.Gigapascal);
    private static Quantity<SecondMomentOfArea> mm4(double v) => new(v, SecondMomentOfAreaUnits.MillimetreToTheFourth);
    private static Quantity<Torque> Nm(double v) => new(v, TorqueUnits.NewtonMetre);

    /// <summary>The four worked examples of the specification, plus Example 1 restated in imperial units.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: simply supported, central point load",
            "SimplySupported", "PointLoad", kN(10), m(2), GPa(210), mm4(2.0e6), mm(50), MPa(165), mm(8),
            Computes, Nm(5000), MPa(125.00), mm(3.9683), ExpectedMeetsCriteria: true),

        new("Example 2: cantilever, uniformly distributed load",
            "Cantilever", "UniformlyDistributed", kN(6), m(1.5), GPa(70), mm4(1.0e6), mm(40), MPa(150), mm(20),
            Computes, Nm(4500), MPa(180.00), mm(36.161), ExpectedMeetsCriteria: false),

        new("Example 3: simply supported, uniformly distributed load",
            "SimplySupported", "UniformlyDistributed", kN(20), m(4), GPa(210), mm4(1.943e7), mm(100), MPa(235), mm(16),
            Computes, Nm(10000), MPa(51.467), mm(4.0847), ExpectedMeetsCriteria: true),

        new("Example 4: cantilever, end point load, stress exactly on the allowable",
            "Cantilever", "PointLoad", kN(2), mm(500), GPa(200), mm4(5.0e4), mm(12.5), MPa(250), mm(10),
            Computes, Nm(1000), MPa(250.00), mm(8.3333), ExpectedMeetsCriteria: true),

        // The same physics as Example 1 in inches, pound-force and psi:
        // 10 kN = 2248.089 lbf, 2 m = 78.740 in, 210 GPa = 30 457 924.9 psi,
        // 2.0e6 mm^4 = 4.80505 in^4, 50 mm = 1.9685 in.
        new("Example 1 in imperial units",
            "SimplySupported", "PointLoad",
            new Quantity<Force>(10_000 / 4.4482216152605, ForceUnits.PoundForce),
            new Quantity<Length>(2 / 0.0254, LengthUnits.Inch),
            new Quantity<Pressure>(210e9 / 6894.757293168, PressureUnits.Psi),
            new Quantity<SecondMomentOfArea>(2.0e-6 / 4.162314256e-7, SecondMomentOfAreaUnits.InchToTheFourth),
            new Quantity<Length>(0.05 / 0.0254, LengthUnits.Inch),
            new Quantity<Pressure>(165e6 / 6894.757293168, PressureUnits.Psi),
            new Quantity<Length>(0.008 / 0.0254, LengthUnits.Inch),
            Computes, Nm(5000), MPa(125.00), mm(3.9683), ExpectedMeetsCriteria: true),
    ];

    /// <summary>Zero and negative inputs — each is rejected as invalid input.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero load", "SimplySupported", "PointLoad", N(0), m(2), GPa(210), mm4(2.0e6), mm(50), MPa(165), mm(8), InvalidInput, ExpectedReasonFragment: "Load"),
        new("negative load", "SimplySupported", "PointLoad", kN(-10), m(2), GPa(210), mm4(2.0e6), mm(50), MPa(165), mm(8), InvalidInput, ExpectedReasonFragment: "Load"),
        new("zero span", "SimplySupported", "PointLoad", kN(10), m(0), GPa(210), mm4(2.0e6), mm(50), MPa(165), mm(8), InvalidInput, ExpectedReasonFragment: "Span"),
        new("zero modulus", "SimplySupported", "PointLoad", kN(10), m(2), GPa(0), mm4(2.0e6), mm(50), MPa(165), mm(8), InvalidInput, ExpectedReasonFragment: "modulus"),
        new("negative second moment", "Cantilever", "UniformlyDistributed", kN(10), m(2), GPa(210), mm4(-1), mm(50), MPa(165), mm(8), InvalidInput, ExpectedReasonFragment: "Second moment"),
        new("zero extreme fibre distance", "Cantilever", "PointLoad", kN(10), m(2), GPa(210), mm4(2.0e6), mm(0), MPa(165), mm(8), InvalidInput, ExpectedReasonFragment: "fibre"),
        new("zero allowable stress", "SimplySupported", "PointLoad", kN(10), m(2), GPa(210), mm4(2.0e6), mm(50), MPa(0), mm(8), InvalidInput, ExpectedReasonFragment: "Allowable"),
        new("negative deflection limit", "SimplySupported", "PointLoad", kN(10), m(2), GPa(210), mm4(2.0e6), mm(50), MPa(165), mm(-8), InvalidInput, ExpectedReasonFragment: "Deflection limit"),
    ];

    /// <summary>Inputs outside the method's limits — refused in the result, never thrown.</summary>
    public static TheoryData<Vector> Refusals =>
    [
        // Example 1's section over a 200 mm span: L / (2c) = 2, far below 10.
        new("stubby beam, span-to-depth ratio 2", "SimplySupported", "PointLoad", kN(10), mm(200), GPa(210), mm4(2.0e6), mm(50), MPa(165), mm(8),
            Refused, ExpectedReasonFragment: "span-to-depth"),
        // Exactly 9.99: still refused; 10 is the first accepted ratio.
        new("span-to-depth ratio just under 10", "Cantilever", "PointLoad", kN(1), mm(999), GPa(210), mm4(2.0e6), mm(50), MPa(165), mm(8),
            Refused, ExpectedReasonFragment: "span-to-depth"),
    ];
}
