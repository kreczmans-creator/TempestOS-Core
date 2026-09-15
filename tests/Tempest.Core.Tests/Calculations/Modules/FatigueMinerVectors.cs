using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.fatigue-miner</c> — the worked examples and
/// edge cases of <c>docs/engineering/calculations/calc.fatigue-miner.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class FatigueMinerVectors
{
    /// <summary>Outcome the vector expects.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <inheritdoc cref="Computes"/>
    public const string Refused = "Refused";

    /// <summary>One block of the loading spectrum.</summary>
    public sealed record Block(Quantity<Pressure> StressRange, double Cycles);

    /// <summary>One vector. A null expected life means unbounded (below the endurance limit).</summary>
    public sealed record Vector(
        string Name,
        Quantity<Pressure> ReferenceStressRange,
        double ReferenceCycles,
        double Slope,
        Quantity<Pressure>? EnduranceLimit,
        IReadOnlyList<Block> Blocks,
        string ExpectedOutcome,
        IReadOnlyList<double?>? ExpectedLives = null,
        IReadOnlyList<double>? ExpectedDamages = null,
        double? ExpectedTotalDamage = null,
        double? ExpectedRepetitionsToFailure = null,
        bool? ExpectedMeetsCriteria = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Pressure> MPa(double v) => new(v, PressureUnits.Megapascal);
    private static Quantity<Pressure> psi(double mpa) => new(mpa * 1e6 / 6894.757293168, PressureUnits.Psi);
    private static Block B(double mpa, double cycles) => new(MPa(mpa), cycles);

    /// <summary>The two worked examples, plus Example 1 in psi.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: detail category 71, no cut-off",
            MPa(71), 2e6, 3, null, [B(100, 1e5), B(60, 5e5), B(40, 2e6)],
            Computes, [715_822, 3_313_991, 11_184_719], [0.13970, 0.15088, 0.17882], 0.46939, 2.1304, ExpectedMeetsCriteria: true),

        new("Example 2: slope 5 with an endurance limit, damage above one",
            MPa(150), 1e6, 5, MPa(60), [B(120, 2e6), B(90, 1e7), B(50, 1e7)],
            Computes, [3_051_758, 12_860_082, null], [0.65536, 0.77760, 0], 1.4330, 0.69786, ExpectedMeetsCriteria: false),

        new("Example 1 in psi",
            psi(71), 2e6, 3, null, [new Block(psi(100), 1e5), new Block(psi(60), 5e5), new Block(psi(40), 2e6)],
            Computes, [715_822, 3_313_991, 11_184_719], [0.13970, 0.15088, 0.17882], 0.46939, 2.1304, ExpectedMeetsCriteria: true),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero reference range", MPa(0), 2e6, 3, null, [B(100, 1e5)], InvalidInput, ExpectedReasonFragment: "Reference stress"),
        new("zero reference cycles", MPa(71), 0, 3, null, [B(100, 1e5)], InvalidInput, ExpectedReasonFragment: "Reference cycles"),
        new("negative slope", MPa(71), 2e6, -3, null, [B(100, 1e5)], InvalidInput, ExpectedReasonFragment: "Slope"),
        new("zero endurance limit", MPa(71), 2e6, 3, MPa(0), [B(100, 1e5)], InvalidInput, ExpectedReasonFragment: "Endurance"),
        new("no blocks", MPa(71), 2e6, 3, null, [], InvalidInput, ExpectedReasonFragment: "block"),
        new("block with zero range", MPa(71), 2e6, 3, null, [B(0, 1e5)], InvalidInput, ExpectedReasonFragment: "range"),
        new("block with negative cycles", MPa(71), 2e6, 3, null, [B(100, -1)], InvalidInput, ExpectedReasonFragment: "cycles"),
    ];

    /// <summary>Inputs outside the method's limits — refused in the result, never thrown.</summary>
    public static TheoryData<Vector> Refusals =>
    [
        // 500 MPa on category 71: N = 2e6 x (71/500)^3 = 5 727 cycles, below 10^4.
        new("low-cycle block", MPa(71), 2e6, 3, null, [B(100, 1e5), B(500, 10)], Refused, ExpectedReasonFragment: "low-cycle"),
    ];
}
