using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.tolerance-stack</c> against its specification's vectors.</summary>
public class ToleranceStackCalculationDefinitionTests
{
    private static ToleranceStackResult Run(ToleranceStackVectors.Vector v, CalculationContext? context = null) =>
        new ToleranceStackCalculationDefinition().Calculate(v.ToInput(), context ?? new CalculationContext());

    private static ToleranceStackVectors.Vector Example(int index) =>
        ((IEnumerable<object[]>)ToleranceStackVectors.WorkedExamples).Select(r => (ToleranceStackVectors.Vector)r[0]).ElementAt(index);

    private static void Close(ToleranceStackVectors.Vector v, double? expected, Quantity<Length>? actual, string what)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        AssertClose(v.Q(expected.Value), actual, v.RelativeTolerance, what);
    }

    [Theory]
    [MemberData(nameof(ToleranceStackVectors.WorkedExamples), MemberType = typeof(ToleranceStackVectors))]
    public void WorkedExamples_MatchTheHandCalculation(ToleranceStackVectors.Vector v)
    {
        var result = Run(v);

        Assert.Equal(v.ExpectedOutcome, result.Outcome);
        Assert.Equal(v.Parts.Count, result.ContributorCount);
        Close(v, v.ExpectedNominal, result.NominalResult, "NominalResult");
        Close(v, v.ExpectedMean, result.MeanResult, "MeanResult");
        Close(v, v.ExpectedWorstTolerance, result.WorstCaseTolerance, "WorstCaseTolerance");
        Close(v, v.ExpectedWorstMinimum, result.WorstCaseMinimum, "WorstCaseMinimum");
        Close(v, v.ExpectedWorstMaximum, result.WorstCaseMaximum, "WorstCaseMaximum");
        Close(v, v.ExpectedMinimumMargin, result.WorstCaseMinimumMargin, "WorstCaseMinimumMargin");
        Close(v, v.ExpectedMaximumMargin, result.WorstCaseMaximumMargin, "WorstCaseMaximumMargin");
        Close(v, v.ExpectedRss, result.RootSumSquareTolerance, "RootSumSquareTolerance");
        Close(v, v.ExpectedSigma, result.ResultStandardDeviation, "ResultStandardDeviation");

        if (v.ExpectedRss is { } rss)
        {
            Close(v, v.ExpectedMean - rss, result.RootSumSquareMinimum, "RootSumSquareMinimum");
            Close(v, v.ExpectedMean + rss, result.RootSumSquareMaximum, "RootSumSquareMaximum");
        }

        if (v.ExpectedCpk is { } cpk)
            AssertClose(cpk, result.ProcessCapabilityIndex, v.RelativeTolerance, "ProcessCapabilityIndex");
        else
            Assert.Null(result.ProcessCapabilityIndex);

        if (v.ExpectedPpm is { } ppm)
            AssertClose(ppm, result.PredictedPartsPerMillionOutside, v.RelativeTolerance, "PredictedPartsPerMillionOutside");
        else
            Assert.Null(result.PredictedPartsPerMillionOutside);
    }

    [Fact]
    public void WithNoStatisticalBasis_NoRootSumSquareFigureIsProduced_AndTheResultSaysWhy()
    {
        var context = new CalculationContext();
        var result = Run(Example(0), context);

        Assert.Null(result.RootSumSquareTolerance);
        Assert.Null(result.ResultStandardDeviation);
        Assert.Null(result.ProcessCapabilityIndex);
        Assert.Contains("no statistical basis was stated", result.StatisticalNote, StringComparison.Ordinal);
        Assert.Contains(context.IntermediateResults, i => i.Name == "Root-sum-square tolerance" && (i.Value as string ?? string.Empty).Contains("not computed", StringComparison.Ordinal));
    }

    [Fact]
    public void TheVerdictComesFromTheWorstCase_EvenWhereTheStatisticalLimitsSitInside()
    {
        // Example 4: the RSS limits (0.5294 to 0.6906 mm) predict 12 787 ppm
        // below 0.55 mm, but the worst case (0.48 mm) is below it outright:
        // the verdict is the worst case's, whatever the prediction.
        var v = Example(3);
        var result = Run(v);

        Assert.Equal(EngineeringCheckOutcome.DoesNotMeetCriteria, result.Outcome);
        Assert.True(result.RootSumSquareMaximum!.Value.BaseValue < v.Q(0.80).BaseValue);
        Assert.Contains(ToleranceStackVectors.Basis, result.StatisticalNote, StringComparison.Ordinal);
    }

    [Theory]
    // Exactly on a limit in decimal, a few parts in a quintillion over it in
    // binary: the v0.16.0 suite's own regression, carried over.
    [InlineData(50.0, 0.10, 20.0, 0.05, 29.85, 30.15)]
    [InlineData(25.4, 0.05, 25.4, 0.05, -0.10, 0.10)]
    public void AStackExactlyOnItsLimit_Passes_RatherThanFailingOnARoundingArtefact(double n1, double t1, double n2, double t2, double lo, double hi)
    {
        var mm = LengthUnits.Millimetre;
        var input = new ToleranceStackInput(
            [
                new ToleranceStackContributor("A", ToleranceDirection.Adds, new(n1, mm), new(t1, mm), new(-t1, mm)),
                new ToleranceStackContributor("B", ToleranceDirection.Subtracts, new(n2, mm), new(t2, mm), new(-t2, mm)),
            ],
            new(lo, mm), new(hi, mm), 3, null);

        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, new ToleranceStackCalculationDefinition().Calculate(input, new CalculationContext()).Outcome);

        // A micrometre over is far beyond the slack, and still fails.
        var over = input with { MaximumResult = new(hi - 0.001, mm) };
        Assert.Equal(EngineeringCheckOutcome.DoesNotMeetCriteria, new ToleranceStackCalculationDefinition().Calculate(over, new CalculationContext()).Outcome);
    }

    [Theory]
    [MemberData(nameof(ToleranceStackVectors.InvalidInputs), MemberType = typeof(ToleranceStackVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(ToleranceStackVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    [InlineData(double.NaN)]
    public void ANonPositiveSigmaLevel_IsRejected(double sigma)
    {
        var input = Example(2).ToInput() with { SigmaPerTolerance = sigma };

        var refused = Assert.Throws<CalculationInputInvalidException>(() => new ToleranceStackCalculationDefinition().Calculate(input, new CalculationContext()));

        Assert.Contains("Sigma per tolerance", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(1.0, 0.15865525393145707)]
    [InlineData(3.0, 0.0013498980316301)]
    [InlineData(-2.0, 0.9772498680518208)]
    public void TheNormalTail_MatchesTheTabulatedValues(double z, double expected) =>
        AssertClose(expected, ToleranceStackCalculationDefinition.NormalUpperTail(z), 2e-7, $"Q({z})");

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackEveryFigure()
    {
        var v = Example(2);

        var (executed, readBack) = await RoundTripAsync<ToleranceStackInput, ToleranceStackResult>(ToleranceStackCalculationDefinition.Id, v.ToInput());

        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, readBack.Result.Outcome);
        AssertClose(v.Q(0.48), readBack.Result.WorstCaseMinimum, v.RelativeTolerance, "WorstCaseMinimum after read-back");
        AssertClose(v.Q(v.ExpectedRss!.Value), readBack.Result.RootSumSquareTolerance, v.RelativeTolerance, "RootSumSquareTolerance after read-back");
        AssertClose(v.ExpectedCpk, readBack.Result.ProcessCapabilityIndex, v.RelativeTolerance, "C_pk after read-back");
        AssertHasIntermediate(executed, "Worst-case limits");
        AssertHasIntermediate(executed, "Statistical basis");
        AssertHasIntermediate(executed, "Contributor Housing bore depth");
    }
}
