using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.tolerance-stack</c> — the worked examples and
/// edge cases of <c>docs/engineering/calculations/calc.tolerance-stack.md</c>,
/// as data. Every expected figure was derived by hand; lengths are in the
/// vector's own unit (inches for Example 1, millimetres otherwise).
/// </summary>
public static class ToleranceStackVectors
{
    /// <summary>The basis stated wherever a vector wants the statistical figures.</summary>
    public const string Basis = "All processes capable, centred and independent; each half-band is 3 sigma";

    /// <summary>One contributor, as the vector states it.</summary>
    public sealed record Part(string Name, ToleranceDirection Direction, double Nominal, double Upper, double Lower);

    /// <summary>One vector. Expected lengths are in <see cref="Unit"/>; a null expectation is "not computed".</summary>
    public sealed record Vector(
        string Name,
        Unit<Length> Unit,
        IReadOnlyList<Part> Parts,
        double? Minimum,
        double? Maximum,
        string? StatisticalBasis,
        EngineeringCheckOutcome? ExpectedOutcome = null,
        double? ExpectedNominal = null,
        double? ExpectedMean = null,
        double? ExpectedWorstTolerance = null,
        double? ExpectedWorstMinimum = null,
        double? ExpectedWorstMaximum = null,
        double? ExpectedMinimumMargin = null,
        double? ExpectedMaximumMargin = null,
        double? ExpectedRss = null,
        double? ExpectedSigma = null,
        double? ExpectedCpk = null,
        double? ExpectedPpm = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <summary>The vector as the definition's input.</summary>
        public ToleranceStackInput ToInput() => new(
            Parts.Select(p => new ToleranceStackContributor(p.Name, p.Direction, Q(p.Nominal), Q(p.Upper), Q(p.Lower))).ToList(),
            Minimum is { } lo ? Q(lo) : null,
            Maximum is { } hi ? Q(hi) : null,
            3.0,
            StatisticalBasis);

        /// <summary>A length in the vector's unit.</summary>
        public Quantity<Length> Q(double value) => new(value, Unit);

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static readonly Unit<Length> In = LengthUnits.Inch;
    private static readonly Unit<Length> Mm = LengthUnits.Millimetre;

    private static Part Add(string name, double nominal, double upper, double lower) => new(name, ToleranceDirection.Adds, nominal, upper, lower);
    private static Part Sub(string name, double nominal, double upper, double lower) => new(name, ToleranceDirection.Subtracts, nominal, upper, lower);

    /// <summary>Shigley's shouldered screw: w = a − b − c − d, gap at least 0.003 in.</summary>
    private static readonly Part[] Screw =
    [
        Add("a shoulder length", 1.750, 0.003, -0.003),
        Sub("b sleeve", 0.750, 0.001, -0.001),
        Sub("c washer", 0.120, 0.005, -0.005),
        Sub("d sleeve", 0.875, 0.001, -0.001),
    ];

    /// <summary>The asymmetric bearing-housing stack.</summary>
    private static readonly Part[] Housing =
    [
        Add("Housing bore depth", 40, 0.10, 0),
        Sub("Bearing width", 20, 0, -0.12),
        Sub("Spacer", 19.5, 0.02, -0.02),
    ];

    /// <summary>The worked examples.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: shouldered-screw gap, worst case (Shigley)", In, Screw, 0.003, null, null,
            EngineeringCheckOutcome.DoesNotMeetCriteria, 0.005, 0.005, 0.010, -0.005, 0.015, -0.008, null),

        new("Example 2: shouldered-screw gap, statistical", In, Screw, 0.003, null, Basis,
            EngineeringCheckOutcome.DoesNotMeetCriteria, 0.005, 0.005, 0.010, -0.005, 0.015, -0.008, null,
            ExpectedRss: 0.006, ExpectedSigma: 0.002, ExpectedCpk: 1.0 / 3.0, ExpectedPpm: 158_655.25),

        new("Example 3: asymmetric deviations, both limits met", Mm, Housing, 0.40, 0.80, Basis,
            EngineeringCheckOutcome.MeetsCriteria, 0.5, 0.61, 0.13, 0.48, 0.74, 0.08, 0.06,
            ExpectedRss: 0.080622577, ExpectedSigma: 0.026874192, ExpectedCpk: 2.3566600, ExpectedPpm: 7.7755e-7),

        new("Example 4: worst case fails, statistical prediction given", Mm, Housing, 0.55, 0.80, Basis,
            EngineeringCheckOutcome.DoesNotMeetCriteria, 0.5, 0.61, 0.13, 0.48, 0.74, -0.07, 0.06,
            ExpectedRss: 0.080622577, ExpectedSigma: 0.026874192, ExpectedCpk: 0.74420841, ExpectedPpm: 12_786.83),

        new("Example 3 with no requirement: computed, no verdict", Mm, Housing, null, null, Basis,
            null, 0.5, 0.61, 0.13, 0.48, 0.74, null, null, ExpectedRss: 0.080622577, ExpectedSigma: 0.026874192),

        new("Example 1 in millimetres", Mm,
            Screw.Select(p => p with { Nominal = p.Nominal * 25.4, Upper = p.Upper * 25.4, Lower = p.Lower * 25.4 }).ToArray(),
            0.003 * 25.4, null, null,
            EngineeringCheckOutcome.DoesNotMeetCriteria, 0.127, 0.127, 0.254, -0.127, 0.381, -0.2032, null),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("no contributors", Mm, [], null, null, null, ExpectedReasonFragment: "contributor"),
        new("unnamed contributor", Mm, [Add(" ", 10, 0.1, -0.1)], null, null, null, ExpectedReasonFragment: "named"),
        new("lower deviation above upper", Mm, [Add("A", 10, -0.1, 0.1)], null, null, null, ExpectedReasonFragment: "lower deviation"),
        new("minimum above maximum", Mm, Housing, 0.9, 0.4, null, ExpectedReasonFragment: "minimum result"),
    ];
}
