using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.fatigue-miner</c> against its specification's vectors.</summary>
public class FatigueMinerCalculationDefinitionTests
{
    private const string Curve = "EN 1993-1-9 detail category 71";

    private static FatigueMinerInput ToInput(FatigueMinerVectors.Vector v) => new(
        null, Curve, v.ReferenceStressRange, v.ReferenceCycles, v.Slope, v.EnduranceLimit,
        v.Blocks.Select(b => new FatigueLoadBlock(b.StressRange, b.Cycles)).ToList());

    private static FatigueMinerResult Run(FatigueMinerVectors.Vector v, CalculationContext? context = null) =>
        new FatigueMinerCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(FatigueMinerVectors.WorkedExamples), MemberType = typeof(FatigueMinerVectors))]
    public void WorkedExamples_MatchTheHandCalculation(FatigueMinerVectors.Vector v)
    {
        var result = Run(v);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        Assert.Null(result.RefusalReason);
        Assert.NotNull(result.BlockLives);
        Assert.NotNull(result.BlockDamages);
        Assert.Equal(v.ExpectedLives!.Count, result.BlockLives.Count);

        for (var i = 0; i < v.ExpectedLives.Count; i++)
        {
            if (v.ExpectedLives[i] is { } life)
                AssertClose(life, result.BlockLives[i], v.RelativeTolerance, $"BlockLives[{i}]");
            else
                Assert.Null(result.BlockLives[i]);

            AssertClose(v.ExpectedDamages![i], result.BlockDamages[i], v.RelativeTolerance, $"BlockDamages[{i}]");
        }

        AssertClose(v.ExpectedTotalDamage, result.TotalDamage, v.RelativeTolerance, "TotalDamage");
        AssertClose(v.ExpectedRepetitionsToFailure, result.RepetitionsToFailure, v.RelativeTolerance, "RepetitionsToFailure");
    }

    [Theory]
    [MemberData(nameof(FatigueMinerVectors.InvalidInputs), MemberType = typeof(FatigueMinerVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(FatigueMinerVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(FatigueMinerVectors.Refusals), MemberType = typeof(FatigueMinerVectors))]
    public void ALowCycleBlock_IsRefusedInTheResult_NotThrown(FatigueMinerVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, result.Outcome);
        Assert.Contains(v.ExpectedReasonFragment!, result.RefusalReason, StringComparison.Ordinal);
        Assert.Null(result.TotalDamage);
        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied);
    }

    [Fact]
    public void ASpectrumEntirelyBelowTheEnduranceLimit_AccruesNoDamage_AndHasUnboundedRepetitions()
    {
        var example = ((IEnumerable<object[]>)FatigueMinerVectors.WorkedExamples).Select(r => (FatigueMinerVectors.Vector)r[0]).Skip(1).First();
        var input = ToInput(example) with { Blocks = [new FatigueLoadBlock(new(50, Tempest.Core.UnitsAndQuantities.PressureUnits.Megapascal), 1e9)] };

        var result = new FatigueMinerCalculationDefinition().Calculate(input, new CalculationContext());

        Assert.Equal(0.0, result.TotalDamage);
        Assert.Null(result.RepetitionsToFailure);
        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, result.Outcome);
    }

    [Fact]
    public void NeitherAMaterialPinNorACurveReference_IsRejected()
    {
        var example = ((IEnumerable<object[]>)FatigueMinerVectors.WorkedExamples).Select(r => (FatigueMinerVectors.Vector)r[0]).First();
        var input = ToInput(example) with { CurveReference = "" };

        var refused = Assert.Throws<CalculationInputInvalidException>(() => new FatigueMinerCalculationDefinition().Calculate(input, new CalculationContext()));

        Assert.Contains("curve reference", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackEveryBlock()
    {
        var example = ((IEnumerable<object[]>)FatigueMinerVectors.WorkedExamples).Select(r => (FatigueMinerVectors.Vector)r[0]).Skip(1).First();

        var (executed, readBack) = await RoundTripAsync<FatigueMinerInput, FatigueMinerResult>(FatigueMinerCalculationDefinition.Id, ToInput(example));

        Assert.NotNull(readBack.Result.BlockLives);
        Assert.Equal(3, readBack.Result.BlockLives.Count);
        Assert.Null(readBack.Result.BlockLives[2]);
        AssertClose(example.ExpectedTotalDamage, readBack.Result.TotalDamage, example.RelativeTolerance, "TotalDamage after read-back");
        Assert.Equal(executed.Result.Outcome, readBack.Result.Outcome);
        AssertHasIntermediate(executed, "Block damages");
        AssertHasIntermediate(executed, "S-N curve reference");
    }
}
