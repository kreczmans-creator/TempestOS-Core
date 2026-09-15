using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.bearing-rating-life</c> — the worked examples
/// and edge cases of <c>docs/engineering/calculations/calc.bearing-rating-life.md</c>,
/// as data. Every expected figure was derived by hand.
/// </summary>
public static class BearingRatingLifeVectors
{
    /// <summary>Outcome the vector expects.</summary>
    public const string Computes = "Computes";

    /// <inheritdoc cref="Computes"/>
    public const string InvalidInput = "InvalidInput";

    /// <inheritdoc cref="Computes"/>
    public const string Refused = "Refused";

    /// <summary>One vector. Lives in millions of revolutions are plain numbers; lives in hours are durations.</summary>
    public sealed record Vector(
        string Name,
        string BearingType,
        Quantity<Force> BasicDynamicLoadRating,
        Quantity<Force> RadialLoad,
        Quantity<Force> AxialLoad,
        double RadialFactor,
        double AxialFactor,
        Quantity<RotationalSpeed> Speed,
        double ReliabilityFactor,
        Quantity<Duration> RequiredLife,
        string ExpectedOutcome,
        Quantity<Force>? ExpectedEquivalentLoad = null,
        double? ExpectedLoadRatio = null,
        double? ExpectedBasicRatingLifeMillionRevolutions = null,
        Quantity<Duration>? ExpectedBasicRatingLife = null,
        double? ExpectedModifiedRatingLifeMillionRevolutions = null,
        Quantity<Duration>? ExpectedModifiedRatingLife = null,
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
    private static Quantity<RotationalSpeed> rpm(double v) => new(v, RotationalSpeedUnits.RevolutionPerMinute);
    private static Quantity<Duration> h(double v) => new(v, DurationUnits.Hour);

    /// <summary>The three worked examples, plus Example 1 in imperial units and radians per second.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: deep-groove ball bearing, pure radial load",
            "Ball", kN(35.1), kN(4), N(0), 1, 0, rpm(1500), 1, h(5000),
            Computes, N(4000), 0.11396, 675.68, h(7507.6), 675.68, h(7507.6), ExpectedMeetsCriteria: true),

        new("Example 2: cylindrical roller bearing",
            "Roller", kN(78), kN(12), N(0), 1, 0, rpm(3000), 1, h(4000),
            Computes, N(12_000), 0.15385, 512.52, h(2847.3), 512.52, h(2847.3), ExpectedMeetsCriteria: false),

        new("Example 3: ball bearing, combined load, 95 % reliability",
            "Ball", kN(25), kN(3), kN(1.5), 0.56, 1.5, rpm(1000), 0.64, h(2000),
            Computes, N(3930), 0.15720, 257.42, h(4290.3), 164.75, h(2745.8), ExpectedMeetsCriteria: true),

        new("Example 1 in imperial units and radians per second",
            "Ball",
            new Quantity<Force>(35_100 / 4.4482216152605, ForceUnits.PoundForce),
            new Quantity<Force>(4_000 / 4.4482216152605, ForceUnits.PoundForce),
            new Quantity<Force>(0, ForceUnits.PoundForce), 1, 0,
            new Quantity<RotationalSpeed>(1500 * 2 * Math.PI / 60, RotationalSpeedUnits.RadianPerSecond), 1,
            new Quantity<Duration>(5000 * 3600, DurationUnits.Second),
            Computes, N(4000), 0.11396, 675.68, h(7507.6), 675.68, h(7507.6), ExpectedMeetsCriteria: true),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("zero rating", "Ball", kN(0), kN(4), N(0), 1, 0, rpm(1500), 1, h(5000), InvalidInput, ExpectedReasonFragment: "rating"),
        new("negative radial load", "Ball", kN(35.1), kN(-4), N(0), 1, 0, rpm(1500), 1, h(5000), InvalidInput, ExpectedReasonFragment: "Radial load"),
        new("unloaded bearing", "Ball", kN(35.1), N(0), N(0), 1, 0, rpm(1500), 1, h(5000), InvalidInput, ExpectedReasonFragment: "Equivalent"),
        new("zero speed", "Ball", kN(35.1), kN(4), N(0), 1, 0, rpm(0), 1, h(5000), InvalidInput, ExpectedReasonFragment: "Speed"),
        new("reliability factor above one", "Ball", kN(35.1), kN(4), N(0), 1, 0, rpm(1500), 1.2, h(5000), InvalidInput, ExpectedReasonFragment: "Reliability"),
        new("negative required life", "Ball", kN(35.1), kN(4), N(0), 1, 0, rpm(1500), 1, h(-1), InvalidInput, ExpectedReasonFragment: "Required life"),
    ];

    /// <summary>Inputs outside the method's limits — refused in the result, never thrown.</summary>
    public static TheoryData<Vector> Refusals =>
    [
        // 20 kN on a 35.1 kN rating: P/C = 0.57.
        new("equivalent load above half the rating", "Ball", kN(35.1), kN(20), N(0), 1, 0, rpm(1500), 1, h(5000), Refused, ExpectedReasonFragment: "half"),
    ];
}
