using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.bolted-joint-preload</c> against its specification's vectors.</summary>
public class BoltedJointPreloadCalculationDefinitionTests
{
    private static BoltedJointPreloadInput ToInput(BoltedJointPreloadVectors.Vector v) => new(
        null, FastenerGrade, v.Preload, v.ExternalLoad, v.BoltStiffness, v.MemberStiffness, v.TensileStressArea, v.ProofStrength);

    private static BoltedJointPreloadResult Run(BoltedJointPreloadVectors.Vector v, CalculationContext? context = null) =>
        new BoltedJointPreloadCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(BoltedJointPreloadVectors.WorkedExamples), MemberType = typeof(BoltedJointPreloadVectors))]
    public void WorkedExamples_MatchTheHandCalculation(BoltedJointPreloadVectors.Vector v)
    {
        var result = Run(v);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        Assert.Null(result.RefusalReason);
        AssertClose(v.ExpectedJointConstant, result.JointConstant, v.RelativeTolerance, "JointConstant");
        AssertClose(v.ExpectedBoltLoad, result.BoltLoad, v.RelativeTolerance, "BoltLoad");
        AssertClose(v.ExpectedClampLoad, result.ClampLoad, v.RelativeTolerance, "ClampLoad");
        AssertClose(v.ExpectedSeparationLoad, result.SeparationLoad, v.RelativeTolerance, "SeparationLoad");
        AssertClose(v.ExpectedSeparationFactor, result.SeparationLoadFactor, v.RelativeTolerance, "SeparationLoadFactor");
        AssertClose(v.ExpectedBoltStress, result.BoltStress, v.RelativeTolerance, "BoltStress");
        Assert.Equal(v.ExpectedSeparated, result.IsSeparated);

        if (v.ExpectedYieldFactor is { } yieldFactor)
            AssertClose(yieldFactor, result.YieldLoadFactor, v.RelativeTolerance, "YieldLoadFactor");
        else
            Assert.Null(result.YieldLoadFactor);
    }

    [Theory]
    [MemberData(nameof(BoltedJointPreloadVectors.InvalidInputs), MemberType = typeof(BoltedJointPreloadVectors))]
    public void ZeroOrNegativeInputs_AreRejectedAsInvalid(BoltedJointPreloadVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(BoltedJointPreloadVectors.Refusals), MemberType = typeof(BoltedJointPreloadVectors))]
    public void PreloadAtOrAboveProof_IsRefusedInTheResult_NotThrown(BoltedJointPreloadVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, result.Outcome);
        Assert.Contains(v.ExpectedReasonFragment!, result.RefusalReason, StringComparison.Ordinal);
        Assert.True(result.PreloadToProofRatio >= 1.0);
        Assert.Null(result.BoltLoad);
        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied);
    }

    [Fact]
    public void NoExternalLoad_LeavesTheLoadFactorsUnbounded_ReportedAsNull()
    {
        var example = ((IEnumerable<object[]>)BoltedJointPreloadVectors.WorkedExamples).Select(r => (BoltedJointPreloadVectors.Vector)r[0]).First();
        var input = ToInput(example) with { ExternalLoad = new(0, Tempest.Core.UnitsAndQuantities.ForceUnits.Newton) };

        var result = new BoltedJointPreloadCalculationDefinition().Calculate(input, new CalculationContext());

        Assert.Null(result.SeparationLoadFactor);
        Assert.Null(result.YieldLoadFactor);
        Assert.False(result.IsSeparated);
        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, result.Outcome);
        AssertClose(example.Preload.BaseValue, result.BoltLoad!.Value.BaseValue, 1e-12, "BoltLoad equals the preload");
    }

    [Fact]
    public void BlankFastenerGrade_IsRejected()
    {
        var example = ((IEnumerable<object[]>)BoltedJointPreloadVectors.WorkedExamples).Select(r => (BoltedJointPreloadVectors.Vector)r[0]).First();
        var input = ToInput(example) with { FastenerGrade = " " };

        var refused = Assert.Throws<CalculationInputInvalidException>(() => new BoltedJointPreloadCalculationDefinition().Calculate(input, new CalculationContext()));

        Assert.Contains("Fastener grade", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheSameFigures()
    {
        var example = ((IEnumerable<object[]>)BoltedJointPreloadVectors.WorkedExamples).Select(r => (BoltedJointPreloadVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<BoltedJointPreloadInput, BoltedJointPreloadResult>(BoltedJointPreloadCalculationDefinition.Id, ToInput(example));

        AssertClose(example.ExpectedBoltLoad, readBack.Result.BoltLoad, example.RelativeTolerance, "BoltLoad after read-back");
        Assert.Equal(executed.Result.IsSeparated, readBack.Result.IsSeparated);
        AssertHasIntermediate(executed, "Joint constant");
        AssertHasIntermediate(executed, "Fastener grade");
    }
}
