using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.column-buckling</c> against its specification's vectors.</summary>
public class ColumnBucklingCalculationDefinitionTests
{
    private static ColumnBucklingInput ToInput(ColumnBucklingVectors.Vector v) => new(
        SteelPin, v.EffectiveLength, v.Area, v.SecondMomentOfArea, v.YoungsModulus, v.YieldStrength, v.RobertsonConstant, v.AppliedLoad);

    private static ColumnBucklingResult Run(ColumnBucklingVectors.Vector v, CalculationContext? context = null) =>
        new ColumnBucklingCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(ColumnBucklingVectors.WorkedExamples), MemberType = typeof(ColumnBucklingVectors))]
    public void WorkedExamples_MatchTheHandCalculation(ColumnBucklingVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        Assert.Null(result.RefusalReason);
        AssertClose(v.ExpectedRadiusOfGyration, result.RadiusOfGyration, v.RelativeTolerance, "RadiusOfGyration");
        AssertClose(v.ExpectedSlenderness, result.Slenderness, v.RelativeTolerance, "Slenderness");
        AssertClose(v.ExpectedEulerStress, result.EulerStress, v.RelativeTolerance, "EulerStress");
        AssertClose(v.ExpectedLimitingSlenderness, result.LimitingSlenderness, v.RelativeTolerance, "LimitingSlenderness");
        AssertClose(v.ExpectedPerryFactor, result.PerryFactor, v.RelativeTolerance, "PerryFactor");
        AssertClose(v.ExpectedCompressiveStrength, result.CompressiveStrength, v.RelativeTolerance, "CompressiveStrength");
        AssertClose(v.ExpectedCompressionResistance, result.CompressionResistance, v.RelativeTolerance, "CompressionResistance");
        AssertClose(v.ExpectedUtilisation, result.Utilisation, v.RelativeTolerance, "Utilisation");
        Assert.Contains(SteelPin.RecordId, context.ReferencedMaterialIds);
    }

    [Theory]
    [MemberData(nameof(ColumnBucklingVectors.InvalidInputs), MemberType = typeof(ColumnBucklingVectors))]
    public void ZeroOrNegativeInputs_AreRejectedAsInvalid(ColumnBucklingVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(ColumnBucklingVectors.Refusals), MemberType = typeof(ColumnBucklingVectors))]
    public void SlendernessAbove180_IsRefusedInTheResult_NotThrown(ColumnBucklingVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(EngineeringCheckOutcome.OutsideMethodLimits, result.Outcome);
        Assert.Contains(v.ExpectedReasonFragment!, result.RefusalReason, StringComparison.Ordinal);
        Assert.True(result.Slenderness > ColumnBucklingCalculationDefinition.MaximumSlenderness);
        Assert.Null(result.CompressiveStrength);
        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied);
    }

    [Fact]
    public void AStockyStrut_ReachesItsYieldStrengthWhenTheEulerStressIsFarAbove()
    {
        // Below the limiting slenderness the Perry factor is zero and the
        // formula reduces to the lesser of yield and Euler: here, yield.
        var example = ((IEnumerable<object[]>)ColumnBucklingVectors.WorkedExamples).Select(r => (ColumnBucklingVectors.Vector)r[0]).First();
        var input = ToInput(example) with { EffectiveLength = new(200, Tempest.Core.UnitsAndQuantities.LengthUnits.Millimetre) };

        var result = new ColumnBucklingCalculationDefinition().Calculate(input, new CalculationContext());

        Assert.True(result.Slenderness < result.LimitingSlenderness);
        Assert.Equal(0.0, result.PerryFactor);
        AssertClose(example.YieldStrength.BaseValue, result.CompressiveStrength!.Value.BaseValue, 1e-9, "CompressiveStrength equals yield");
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheSameFigures()
    {
        var example = ((IEnumerable<object[]>)ColumnBucklingVectors.WorkedExamples).Select(r => (ColumnBucklingVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<ColumnBucklingInput, ColumnBucklingResult>(ColumnBucklingCalculationDefinition.Id, ToInput(example));

        AssertClose(example.ExpectedCompressionResistance, readBack.Result.CompressionResistance, example.RelativeTolerance, "CompressionResistance after read-back");
        Assert.Equal(executed.Result.Outcome, readBack.Result.Outcome);
        AssertHasIntermediate(executed, "Perry factor");
        AssertHasIntermediate(executed, "Slenderness");
    }
}
