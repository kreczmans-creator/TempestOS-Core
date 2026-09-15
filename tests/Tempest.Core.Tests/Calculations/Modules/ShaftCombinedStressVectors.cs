using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.shaft-combined-stress</c> — the worked examples
/// and edge cases of <c>docs/engineering/calculations/calc.shaft-combined-stress.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class ShaftCombinedStressVectors
{
    /// <summary>Outcome the vector expects.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <summary>One vector.</summary>
    public sealed record Vector(
        string Name,
        Quantity<Length> Diameter,
        Quantity<Torque> BendingMoment,
        Quantity<Torque> Torque,
        Quantity<Pressure> YieldStrength,
        double BendingStressConcentrationFactor,
        double TorsionalStressConcentrationFactor,
        double RequiredSafetyFactor,
        string ExpectedOutcome,
        Quantity<Pressure>? ExpectedBendingStress = null,
        Quantity<Pressure>? ExpectedTorsionalShearStress = null,
        Quantity<Pressure>? ExpectedMaximumShearStress = null,
        Quantity<Pressure>? ExpectedVonMisesStress = null,
        double? ExpectedTrescaSafetyFactor = null,
        double? ExpectedVonMisesSafetyFactor = null,
        bool? ExpectedMeetsCriteria = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Length> mm(double v) => new(v, LengthUnits.Millimetre);
    private static Quantity<Torque> Nm(double v) => new(v, TorqueUnits.NewtonMetre);
    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);

    /// <summary>The three worked examples, plus Example 1 in imperial units.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: 40 mm shaft",
            mm(40), Nm(500), Nm(800), MPa(350), 1, 1, 2,
            Computes, MPa(79.577), MPa(63.662), MPa(75.073), MPa(135.98), 2.3311, 2.5739, ExpectedMeetsCriteria: true),

        new("Example 2: 25 mm shaft, required factor 3",
            mm(25), Nm(120), Nm(60), MPa(250), 1, 1, 3,
            Computes, MPa(78.228), MPa(19.557), MPa(43.731), MPa(85.247), 2.8584, 2.9327, ExpectedMeetsCriteria: false),

        new("Example 3: 25 mm shaft with a shoulder fillet",
            mm(25), Nm(120), Nm(60), MPa(250), 1.6, 1.3, 1.5,
            Computes, MPa(125.16), MPa(25.424), MPa(67.549), MPa(132.69), 1.8505, 1.8842, ExpectedMeetsCriteria: true),

        new("Example 1 in imperial units",
            new Quantity<Length>(40 / 25.4, LengthUnits.Inch),
            new Quantity<Torque>(500 / 0.1129848290276167, TorqueUnits.PoundForceInch),
            new Quantity<Torque>(800 / 1.3558179483314004, TorqueUnits.PoundForceFoot),
            new Quantity<Pressure>(350e6 / 6894.757293168, PressureUnits.Psi), 1, 1, 2,
            Computes, MPa(79.577), MPa(63.662), MPa(75.073), MPa(135.98), 2.3311, 2.5739, ExpectedMeetsCriteria: true),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero diameter", mm(0), Nm(500), Nm(800), MPa(350), 1, 1, 2, InvalidInput, ExpectedReasonFragment: "Diameter"),
        new("negative moment", mm(40), Nm(-500), Nm(800), MPa(350), 1, 1, 2, InvalidInput, ExpectedReasonFragment: "moment"),
        new("negative torque", mm(40), Nm(500), Nm(-800), MPa(350), 1, 1, 2, InvalidInput, ExpectedReasonFragment: "Torque"),
        new("no load at all", mm(40), Nm(0), Nm(0), MPa(350), 1, 1, 2, InvalidInput, ExpectedReasonFragment: "both zero"),
        new("zero yield", mm(40), Nm(500), Nm(800), MPa(0), 1, 1, 2, InvalidInput, ExpectedReasonFragment: "Yield"),
        new("bending concentration factor below one", mm(40), Nm(500), Nm(800), MPa(350), 0.9, 1, 2, InvalidInput, ExpectedReasonFragment: "concentration"),
        new("required factor below one", mm(40), Nm(500), Nm(800), MPa(350), 1, 1, 0.5, InvalidInput, ExpectedReasonFragment: "Required"),
    ];
}
