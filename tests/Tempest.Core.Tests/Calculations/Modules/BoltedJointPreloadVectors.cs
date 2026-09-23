using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.bolted-joint-preload</c> — the worked examples
/// and edge cases of <c>docs/engineering/calculations/calc.bolted-joint-preload.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class BoltedJointPreloadVectors
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
        Quantity<Force> Preload,
        Quantity<Force> ExternalLoad,
        Quantity<Stiffness> BoltStiffness,
        Quantity<Stiffness> MemberStiffness,
        Quantity<Area> TensileStressArea,
        Quantity<Pressure> ProofStrength,
        string ExpectedOutcome,
        double? ExpectedJointConstant = null,
        Quantity<Force>? ExpectedBoltLoad = null,
        Quantity<Force>? ExpectedClampLoad = null,
        Quantity<Force>? ExpectedSeparationLoad = null,
        double? ExpectedSeparationFactor = null,
        Quantity<Pressure>? ExpectedBoltStress = null,
        double? ExpectedYieldFactor = null,
        bool? ExpectedSeparated = null,
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
    private static Quantity<Stiffness> kNmm(double v) => new(v, StiffnessUnits.KilonewtonPerMillimetre);
    private static Quantity<Area> mm2(double v) => new(v, AreaUnits.SquareMillimetre);
    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);

    /// <summary>The three worked examples, plus Example 1 in imperial units.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: M12 class 8.8 at 75 % of proof",
            N(37_935), kN(20), kNmm(200), kNmm(600), mm2(84.3), MPa(600),
            Computes, 0.25, N(42_935), N(22_935), N(50_580), 2.5290, MPa(509.31), 2.5290, ExpectedSeparated: false, ExpectedMeetsCriteria: true),

        new("Example 2: M16 class 10.9",
            N(90_000), kN(40), kNmm(500), kNmm(2000), mm2(157), MPa(830),
            Computes, 0.20, N(98_000), N(58_000), N(112_500), 2.8125, MPa(624.20), 5.0388, ExpectedSeparated: false, ExpectedMeetsCriteria: true),

        new("Example 3: the joint separates",
            N(10_000), kN(20), kNmm(3), kNmm(7), mm2(84.3), MPa(600),
            Computes, 0.30, N(20_000), N(0), N(14_285.7), 0.71429, MPa(237.25), ExpectedYieldFactor: null, ExpectedSeparated: true, ExpectedMeetsCriteria: false),

        // Example 1 in lbf, lbf/in, ft^2 and psi.
        new("Example 1 in imperial units",
            new Quantity<Force>(37_935 / 4.4482216152605, ForceUnits.PoundForce),
            new Quantity<Force>(20_000 / 4.4482216152605, ForceUnits.PoundForce),
            new Quantity<Stiffness>(200e6 / 175.12683524647637, StiffnessUnits.PoundForcePerInch),
            new Quantity<Stiffness>(600e6 / 175.12683524647637, StiffnessUnits.PoundForcePerInch),
            new Quantity<Area>(84.3e-6 / 0.09290304, AreaUnits.SquareFoot),
            new Quantity<Pressure>(600e6 / 6894.757293168, PressureUnits.Psi),
            Computes, 0.25, N(42_935), N(22_935), N(50_580), 2.5290, MPa(509.31), 2.5290, ExpectedSeparated: false, ExpectedMeetsCriteria: true),
    ];

    /// <summary>Zero and negative inputs — each is rejected as invalid input.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero preload", N(0), kN(20), kNmm(200), kNmm(600), mm2(84.3), MPa(600), InvalidInput, ExpectedReasonFragment: "Preload"),
        new("negative external load", N(37_935), kN(-5), kNmm(200), kNmm(600), mm2(84.3), MPa(600), InvalidInput, ExpectedReasonFragment: "External load"),
        new("zero bolt stiffness", N(37_935), kN(20), kNmm(0), kNmm(600), mm2(84.3), MPa(600), InvalidInput, ExpectedReasonFragment: "Bolt stiffness"),
        new("negative member stiffness", N(37_935), kN(20), kNmm(200), kNmm(-600), mm2(84.3), MPa(600), InvalidInput, ExpectedReasonFragment: "Member stiffness"),
        new("zero stress area", N(37_935), kN(20), kNmm(200), kNmm(600), mm2(0), MPa(600), InvalidInput, ExpectedReasonFragment: "stress area"),
        new("zero proof strength", N(37_935), kN(20), kNmm(200), kNmm(600), mm2(84.3), MPa(0), InvalidInput, ExpectedReasonFragment: "Proof strength"),
    ];

    /// <summary>Inputs outside the method's limits — refused in the result, never thrown.</summary>
    public static TheoryData<Vector> Refusals =>
    [
        // Proof load of Example 1's M12 is 600 x 84.3 = 50 580 N.
        new("preload above proof load", N(60_000), kN(20), kNmm(200), kNmm(600), mm2(84.3), MPa(600), Refused, ExpectedReasonFragment: "proof load"),
        new("preload exactly at proof load", N(50_580), kN(20), kNmm(200), kNmm(600), mm2(84.3), MPa(600), Refused, ExpectedReasonFragment: "proof load"),
    ];
}
