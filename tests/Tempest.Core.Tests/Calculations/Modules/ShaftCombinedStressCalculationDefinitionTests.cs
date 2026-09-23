using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.shaft-combined-stress</c> against its specification's vectors.</summary>
public class ShaftCombinedStressCalculationDefinitionTests
{
    private static ShaftCombinedStressInput ToInput(ShaftCombinedStressVectors.Vector v) => new(
        SteelPin, v.Diameter, v.BendingMoment, v.Torque, v.YieldStrength, v.BendingStressConcentrationFactor, v.TorsionalStressConcentrationFactor, v.RequiredSafetyFactor);

    private static ShaftCombinedStressResult Run(ShaftCombinedStressVectors.Vector v, CalculationContext? context = null) =>
        new ShaftCombinedStressCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(ShaftCombinedStressVectors.WorkedExamples), MemberType = typeof(ShaftCombinedStressVectors))]
    public void WorkedExamples_MatchTheHandCalculation(ShaftCombinedStressVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        AssertClose(v.ExpectedBendingStress, result.BendingStress, v.RelativeTolerance, "BendingStress");
        AssertClose(v.ExpectedTorsionalShearStress, result.TorsionalShearStress, v.RelativeTolerance, "TorsionalShearStress");
        AssertClose(v.ExpectedMaximumShearStress, result.MaximumShearStress, v.RelativeTolerance, "MaximumShearStress");
        AssertClose(v.ExpectedVonMisesStress, result.VonMisesStress, v.RelativeTolerance, "VonMisesStress");
        AssertClose(v.ExpectedTrescaSafetyFactor, result.TrescaSafetyFactor, v.RelativeTolerance, "TrescaSafetyFactor");
        AssertClose(v.ExpectedVonMisesSafetyFactor, result.VonMisesSafetyFactor, v.RelativeTolerance, "VonMisesSafetyFactor");
        Assert.Equal(Math.Min(result.TrescaSafetyFactor, result.VonMisesSafetyFactor), result.GoverningSafetyFactor);
        Assert.Contains(SteelPin.RecordId, context.ReferencedMaterialIds);
    }

    [Theory]
    [MemberData(nameof(ShaftCombinedStressVectors.InvalidInputs), MemberType = typeof(ShaftCombinedStressVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(ShaftCombinedStressVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheTrescaFactorIsNeverAboveTheVonMisesFactor_ForPureTorsion()
    {
        // Under pure shear Tresca predicts yield at S_y/2 and von Mises at
        // S_y/sqrt(3): the maximum-shear theory is the more conservative.
        var example = ((IEnumerable<object[]>)ShaftCombinedStressVectors.WorkedExamples).Select(r => (ShaftCombinedStressVectors.Vector)r[0]).First();
        var input = ToInput(example) with { BendingMoment = new(0, Tempest.Core.UnitsAndQuantities.TorqueUnits.NewtonMetre) };

        var result = new ShaftCombinedStressCalculationDefinition().Calculate(input, new CalculationContext());

        Assert.True(result.TrescaSafetyFactor < result.VonMisesSafetyFactor);
        AssertClose(Math.Sqrt(3.0) / 2.0, result.TrescaSafetyFactor / result.VonMisesSafetyFactor, 1e-9, "Tresca over von Mises for pure torsion");
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheSameFigures()
    {
        var example = ((IEnumerable<object[]>)ShaftCombinedStressVectors.WorkedExamples).Select(r => (ShaftCombinedStressVectors.Vector)r[0]).First();

        var (executed, readBack) = await RoundTripAsync<ShaftCombinedStressInput, ShaftCombinedStressResult>(ShaftCombinedStressCalculationDefinition.Id, ToInput(example));

        AssertClose(example.ExpectedVonMisesStress, readBack.Result.VonMisesStress, example.RelativeTolerance, "VonMisesStress after read-back");
        Assert.Equal(executed.Result.Outcome, readBack.Result.Outcome);
        AssertHasIntermediate(executed, "Von Mises stress");
        AssertHasIntermediate(executed, "Tresca safety factor");
    }
}
