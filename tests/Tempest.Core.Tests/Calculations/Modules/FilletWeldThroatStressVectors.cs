using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.fillet-weld-throat-stress</c> — the worked
/// examples and edge cases of
/// <c>docs/engineering/calculations/calc.fillet-weld-throat-stress.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class FilletWeldThroatStressVectors
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
        Quantity<Force> ParallelForce,
        Quantity<Force> TransverseForce,
        Quantity<Force> NormalForce,
        Quantity<Length> EffectiveLength,
        Quantity<Length> ThroatThickness,
        Quantity<Pressure> UltimateStrength,
        double CorrelationFactor,
        double PartialFactor,
        string ExpectedOutcome,
        Quantity<Force>? ExpectedResultantForce = null,
        Quantity<Pressure>? ExpectedDesignShearStrength = null,
        Quantity<Pressure>? ExpectedThroatStress = null,
        Quantity<Force>? ExpectedWeldResistance = null,
        double? ExpectedUtilisation = null,
        Quantity<Length>? ExpectedRequiredThroat = null,
        Quantity<Length>? ExpectedGoverningRequiredThroat = null,
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
    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);

    /// <summary>The three worked examples, plus Example 1 in imperial units.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: S355 lap weld in longitudinal shear",
            kN(150), N(0), N(0), mm(200), mm(5), MPa(490), 0.9, 1.25,
            Computes, N(150_000), MPa(251.47), MPa(150.00), N(251_468), 0.59650, mm(2.9825), mm(3.0), ExpectedMeetsCriteria: true),

        new("Example 2: S235, combined parallel and transverse load",
            kN(40), kN(30), N(0), mm(120), mm(4), MPa(360), 0.8, 1.25,
            Computes, N(50_000), MPa(207.85), MPa(104.17), N(99_766), 0.50117, mm(2.0047), mm(3.0), ExpectedMeetsCriteria: true),

        new("Example 3: overloaded normal weld",
            N(0), N(0), kN(120), mm(100), mm(4), MPa(490), 0.9, 1.25,
            Computes, N(120_000), MPa(251.47), MPa(300.00), N(100_587), 1.1930, mm(4.7719), mm(4.7719), ExpectedMeetsCriteria: false),

        new("Example 1 in imperial units",
            new Quantity<Force>(150_000 / 4.4482216152605, ForceUnits.PoundForce),
            new Quantity<Force>(0, ForceUnits.PoundForce),
            new Quantity<Force>(0, ForceUnits.PoundForce),
            new Quantity<Length>(200 / 25.4, LengthUnits.Inch),
            new Quantity<Length>(5 / 25.4, LengthUnits.Inch),
            new Quantity<Pressure>(490e6 / 6894.757293168, PressureUnits.Psi), 0.9, 1.25,
            Computes, N(150_000), MPa(251.47), MPa(150.00), N(251_468), 0.59650, mm(2.9825), mm(3.0), ExpectedMeetsCriteria: true),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("no load", N(0), N(0), N(0), mm(200), mm(5), MPa(490), 0.9, 1.25, InvalidInput, ExpectedReasonFragment: "force"),
        new("zero length", kN(150), N(0), N(0), mm(0), mm(5), MPa(490), 0.9, 1.25, InvalidInput, ExpectedReasonFragment: "length"),
        new("negative throat", kN(150), N(0), N(0), mm(200), mm(-5), MPa(490), 0.9, 1.25, InvalidInput, ExpectedReasonFragment: "Throat"),
        new("zero ultimate strength", kN(150), N(0), N(0), mm(200), mm(5), MPa(0), 0.9, 1.25, InvalidInput, ExpectedReasonFragment: "Ultimate"),
        new("zero correlation factor", kN(150), N(0), N(0), mm(200), mm(5), MPa(490), 0, 1.25, InvalidInput, ExpectedReasonFragment: "Correlation"),
        new("partial factor below one", kN(150), N(0), N(0), mm(200), mm(5), MPa(490), 0.9, 0.9, InvalidInput, ExpectedReasonFragment: "Partial"),
    ];

    /// <summary>Inputs outside the method's limits — refused in the result, never thrown.</summary>
    public static TheoryData<Vector> Refusals =>
    [
        new("throat below 3 mm", kN(150), N(0), N(0), mm(200), mm(2.5), MPa(490), 0.9, 1.25, Refused, ExpectedReasonFragment: "3 mm"),
        new("length below 30 mm", kN(10), N(0), N(0), mm(20), mm(5), MPa(490), 0.9, 1.25, Refused, ExpectedReasonFragment: "30 mm"),
        new("length below six throats", kN(10), N(0), N(0), mm(35), mm(6), MPa(490), 0.9, 1.25, Refused, ExpectedReasonFragment: "six"),
    ];
}
