using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.fillet-weld-throat-stress</c> against its specification's vectors.</summary>
public class FilletWeldThroatStressCalculationDefinitionTests
{
    private static FilletWeldThroatStressInput ToInput(FilletWeldThroatStressVectors.Vector v) => new(
        SteelPin, v.ParallelForce, v.TransverseForce, v.NormalForce, v.EffectiveLength, v.ThroatThickness, v.UltimateStrength, v.CorrelationFactor, v.PartialFactor);

    private static FilletWeldThroatStressResult Run(FilletWeldThroatStressVectors.Vector v, CalculationContext? context = null) =>
        new FilletWeldThroatStressCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(FilletWeldThroatStressVectors.WorkedExamples), MemberType = typeof(FilletWeldThroatStressVectors))]
    public void WorkedExamples_MatchTheHandCalculation(FilletWeldThroatStressVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        Assert.Null(result.RefusalReason);
        AssertClose(v.ExpectedResultantForce, result.ResultantForce, v.RelativeTolerance, "ResultantForce");
        AssertClose(v.ExpectedDesignShearStrength, result.DesignShearStrength, v.RelativeTolerance, "DesignShearStrength");
        AssertClose(v.ExpectedThroatStress, result.ThroatStress, v.RelativeTolerance, "ThroatStress");
        AssertClose(v.ExpectedWeldResistance, result.WeldResistance, v.RelativeTolerance, "WeldResistance");
        AssertClose(v.ExpectedUtilisation, result.Utilisation, v.RelativeTolerance, "Utilisation");
        AssertClose(v.ExpectedRequiredThroat, result.RequiredThroat, v.RelativeTolerance, "RequiredThroat");
        AssertClose(v.ExpectedGoverningRequiredThroat, result.GoverningRequiredThroat, v.RelativeTolerance, "GoverningRequiredThroat");
        AssertClose(v.ExpectedGoverningRequiredThroat!.Value.BaseValue * Math.Sqrt(2.0), result.RequiredLeg!.Value.BaseValue, v.RelativeTolerance, "RequiredLeg");
        Assert.Contains(SteelPin.RecordId, context.ReferencedMaterialIds);
    }

    [Theory]
    [MemberData(nameof(FilletWeldThroatStressVectors.InvalidInputs), MemberType = typeof(FilletWeldThroatStressVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(FilletWeldThroatStressVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(FilletWeldThroatStressVectors.Refusals), MemberType = typeof(FilletWeldThroatStressVectors))]
    public void WeldsBelowTheCodeMinima_AreRefusedInTheResult_NotThrown(FilletWeldThroatStressVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, result.Outcome);
        Assert.Contains(v.ExpectedReasonFragment!, result.RefusalReason, StringComparison.Ordinal);
        Assert.Null(result.ThroatStress);
        Assert.Null(result.RequiredThroat);
        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheSameFigures()
    {
        var example = ((IEnumerable<object[]>)FilletWeldThroatStressVectors.WorkedExamples).Select(r => (FilletWeldThroatStressVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<FilletWeldThroatStressInput, FilletWeldThroatStressResult>(FilletWeldThroatStressCalculationDefinition.Id, ToInput(example));

        AssertClose(example.ExpectedThroatStress, readBack.Result.ThroatStress, example.RelativeTolerance, "ThroatStress after read-back");
        Assert.Equal(executed.Result.Outcome, readBack.Result.Outcome);
        Assert.Contains(SteelPin.RecordId, executed.ReferencedMaterialIds);
        AssertHasIntermediate(executed, "Design shear strength");
        AssertHasIntermediate(executed, "Required throat");
    }
}
