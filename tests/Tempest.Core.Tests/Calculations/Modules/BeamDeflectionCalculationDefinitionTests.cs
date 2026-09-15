using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.beam-deflection</c> against its specification's vectors.</summary>
public class BeamDeflectionCalculationDefinitionTests
{
    private static BeamDeflectionInput ToInput(BeamDeflectionVectors.Vector v) => new(
        SteelPin,
        Enum.Parse<BeamSupport>(v.Support),
        Enum.Parse<BeamLoading>(v.Loading),
        v.Load, v.Span, v.YoungsModulus, v.SecondMomentOfArea, v.ExtremeFibreDistance, v.AllowableBendingStress, v.DeflectionLimit);

    private static BeamDeflectionResult Run(BeamDeflectionVectors.Vector v, CalculationContext? context = null) =>
        new BeamDeflectionCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(BeamDeflectionVectors.WorkedExamples), MemberType = typeof(BeamDeflectionVectors))]
    public void WorkedExamples_MatchTheHandCalculation(BeamDeflectionVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        Assert.Null(result.RefusalReason);
        AssertClose(v.ExpectedMaximumMoment, result.MaximumMoment, v.RelativeTolerance, "MaximumMoment");
        AssertClose(v.ExpectedBendingStress, result.MaximumBendingStress, v.RelativeTolerance, "MaximumBendingStress");
        AssertClose(v.ExpectedDeflection, result.MaximumDeflection, v.RelativeTolerance, "MaximumDeflection");
        Assert.Equal(v.ExpectedMeetsCriteria, result.StressCriterionMet & result.DeflectionCriterionMet);

        // The material the modulus came from is on the record, and the
        // figures are typed quantities in engineering units.
        Assert.Contains(SteelPin.RecordId, context.ReferencedMaterialIds);
        Assert.Equal("MPa", result.MaximumBendingStress!.Value.Unit.Symbol);
        Assert.Equal("mm", result.MaximumDeflection!.Value.Unit.Symbol);
    }

    [Theory]
    [MemberData(nameof(BeamDeflectionVectors.InvalidInputs), MemberType = typeof(BeamDeflectionVectors))]
    public void ZeroOrNegativeInputs_AreRejectedAsInvalid(BeamDeflectionVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(BeamDeflectionVectors.Refusals), MemberType = typeof(BeamDeflectionVectors))]
    public void InputsOutsideTheMethodLimits_AreRefusedInTheResult_NotThrown(BeamDeflectionVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, result.Outcome);
        Assert.Contains(v.ExpectedReasonFragment!, result.RefusalReason, StringComparison.Ordinal);
        Assert.Null(result.MaximumMoment);
        Assert.Null(result.MaximumDeflection);
        Assert.True(result.SpanToDepthRatio < BeamDeflectionCalculationDefinition.MinimumSpanToDepthRatio);
        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheSameFigures()
    {
        var example = ((IEnumerable<object[]>)BeamDeflectionVectors.WorkedExamples).Select(r => (BeamDeflectionVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<BeamDeflectionInput, BeamDeflectionResult>(BeamDeflectionCalculationDefinition.Id, ToInput(example));

        AssertClose(example.ExpectedDeflection, readBack.Result.MaximumDeflection, example.RelativeTolerance, "MaximumDeflection after read-back");
        Assert.Equal(executed.Result.Outcome, readBack.Result.Outcome);
        Assert.Contains(SteelPin.RecordId, executed.ReferencedMaterialIds);
        AssertHasIntermediate(executed, "Maximum bending moment");
        AssertHasIntermediate(executed, "Span-to-depth ratio");
    }

    [Fact]
    public void TheInputRecordIsDimensioned_SoNoBareDoubleCanBePassedWhereAQuantityBelongs()
    {
        var parameters = typeof(BeamDeflectionInput).GetConstructors().Single().GetParameters();

        Assert.Equal(typeof(Quantity<Force>), parameters.Single(p => p.Name == "Load").ParameterType);
        Assert.Equal(typeof(Quantity<Length>), parameters.Single(p => p.Name == "Span").ParameterType);
        Assert.Equal(typeof(Quantity<Pressure>), parameters.Single(p => p.Name == "YoungsModulus").ParameterType);
        Assert.Equal(typeof(Quantity<SecondMomentOfArea>), parameters.Single(p => p.Name == "SecondMomentOfArea").ParameterType);
    }
}
