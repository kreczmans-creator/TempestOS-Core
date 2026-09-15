using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.bearing-rating-life</c> against its specification's vectors.</summary>
public class BearingRatingLifeCalculationDefinitionTests
{
    private const string Designation = "6208";

    private static BearingRatingLifeInput ToInput(BearingRatingLifeVectors.Vector v) => new(
        null, Designation, Enum.Parse<RollingBearingType>(v.BearingType), v.BasicDynamicLoadRating, v.RadialLoad, v.AxialLoad,
        v.RadialFactor, v.AxialFactor, v.Speed, v.ReliabilityFactor, v.RequiredLife);

    private static BearingRatingLifeResult Run(BearingRatingLifeVectors.Vector v, CalculationContext? context = null) =>
        new BearingRatingLifeCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(BearingRatingLifeVectors.WorkedExamples), MemberType = typeof(BearingRatingLifeVectors))]
    public void WorkedExamples_MatchTheHandCalculation(BearingRatingLifeVectors.Vector v)
    {
        var result = Run(v);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        Assert.Null(result.RefusalReason);
        AssertClose(v.ExpectedEquivalentLoad, result.EquivalentDynamicLoad, v.RelativeTolerance, "EquivalentDynamicLoad");
        AssertClose(v.ExpectedLoadRatio, result.LoadRatio, v.RelativeTolerance, "LoadRatio");
        AssertClose(v.ExpectedBasicRatingLifeMillionRevolutions, result.BasicRatingLifeMillionRevolutions, v.RelativeTolerance, "BasicRatingLifeMillionRevolutions");
        AssertClose(v.ExpectedBasicRatingLife, result.BasicRatingLife, v.RelativeTolerance, "BasicRatingLife");
        AssertClose(v.ExpectedModifiedRatingLifeMillionRevolutions, result.ModifiedRatingLifeMillionRevolutions, v.RelativeTolerance, "ModifiedRatingLifeMillionRevolutions");
        AssertClose(v.ExpectedModifiedRatingLife, result.ModifiedRatingLife, v.RelativeTolerance, "ModifiedRatingLife");
        Assert.Equal("h", result.BasicRatingLife!.Value.Unit.Symbol);
    }

    [Theory]
    [MemberData(nameof(BearingRatingLifeVectors.InvalidInputs), MemberType = typeof(BearingRatingLifeVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(BearingRatingLifeVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(BearingRatingLifeVectors.Refusals), MemberType = typeof(BearingRatingLifeVectors))]
    public void ALoadAboveHalfTheRating_IsRefusedInTheResult_NotThrown(BearingRatingLifeVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, result.Outcome);
        Assert.Contains(v.ExpectedReasonFragment!, result.RefusalReason, StringComparison.Ordinal);
        Assert.True(result.LoadRatio > BearingRatingLifeCalculationDefinition.MaximumLoadRatio);
        Assert.Null(result.BasicRatingLife);
        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied);
    }

    [Fact]
    public void NoRequiredLife_MeansNoCriterion_AndTheOutcomeMeetsIt()
    {
        var example = ((IEnumerable<object[]>)BearingRatingLifeVectors.WorkedExamples).Select(r => (BearingRatingLifeVectors.Vector)r[0]).Skip(1).First();
        var input = ToInput(example) with { RequiredLife = new(0, Tempest.Core.UnitsAndQuantities.DurationUnits.Hour) };

        var result = new BearingRatingLifeCalculationDefinition().Calculate(input, new CalculationContext());

        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, result.Outcome);
        Assert.True(result.CriterionMet);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheSameFigures()
    {
        var example = ((IEnumerable<object[]>)BearingRatingLifeVectors.WorkedExamples).Select(r => (BearingRatingLifeVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<BearingRatingLifeInput, BearingRatingLifeResult>(BearingRatingLifeCalculationDefinition.Id, ToInput(example));

        AssertClose(example.ExpectedBasicRatingLife, readBack.Result.BasicRatingLife, example.RelativeTolerance, "BasicRatingLife after read-back");
        Assert.Equal(executed.Result.Outcome, readBack.Result.Outcome);
        AssertHasIntermediate(executed, "Load ratio P/C");
        AssertHasIntermediate(executed, "Bearing designation");
    }
}
