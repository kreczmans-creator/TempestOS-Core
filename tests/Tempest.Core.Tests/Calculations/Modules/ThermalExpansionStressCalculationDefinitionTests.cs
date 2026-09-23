using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.thermal-expansion-stress</c> against its specification's vectors.</summary>
public class ThermalExpansionStressCalculationDefinitionTests
{
    private static ThermalExpansionStressInput ToInput(ThermalExpansionStressVectors.Vector v) => new(
        SteelPin, v.Length, v.Area, v.YoungsModulus, v.ExpansionCoefficient, v.TemperatureChange, v.Gap, v.RestraintStiffness, v.AllowableStress);

    private static ThermalExpansionStressResult Run(ThermalExpansionStressVectors.Vector v, CalculationContext? context = null) =>
        new ThermalExpansionStressCalculationDefinition().Calculate(ToInput(v), context ?? new CalculationContext());

    [Theory]
    [MemberData(nameof(ThermalExpansionStressVectors.WorkedExamples), MemberType = typeof(ThermalExpansionStressVectors))]
    public void WorkedExamples_MatchTheHandCalculation(ThermalExpansionStressVectors.Vector v)
    {
        var context = new CalculationContext();
        var result = Run(v, context);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        AssertClose(v.ExpectedFreeMovement, result.FreeMovement, v.RelativeTolerance, "FreeMovement");
        AssertClose(v.ExpectedRestraintForce, result.RestraintForce, v.RelativeTolerance, "RestraintForce");
        AssertClose(v.ExpectedStress, result.Stress, v.RelativeTolerance, "Stress");
        AssertClose(v.ExpectedActualMovement, result.ActualMovement, v.RelativeTolerance, "ActualMovement");
        Assert.Contains(SteelPin.RecordId, context.ReferencedMaterialIds);
    }

    [Theory]
    [MemberData(nameof(ThermalExpansionStressVectors.InvalidInputs), MemberType = typeof(ThermalExpansionStressVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(ThermalExpansionStressVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoTemperatureChange_ComputesZeroEverywhere()
    {
        var example = ((IEnumerable<object[]>)ThermalExpansionStressVectors.WorkedExamples).Select(r => (ThermalExpansionStressVectors.Vector)r[0]).First();
        var input = ToInput(example) with { TemperatureChange = new(0, Tempest.Core.UnitsAndQuantities.TemperatureDeltaUnits.Kelvin) };

        var result = new ThermalExpansionStressCalculationDefinition().Calculate(input, new CalculationContext());

        Assert.Equal(0.0, result.FreeMovement.BaseValue);
        Assert.Equal(0.0, result.RestraintForce.BaseValue);
        Assert.Equal(0.0, result.Stress.BaseValue);
        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, result.Outcome);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackTheSameFigures()
    {
        var example = ((IEnumerable<object[]>)ThermalExpansionStressVectors.WorkedExamples).Select(r => (ThermalExpansionStressVectors.Vector)r[0]).Skip(2).First();

        var (executed, readBack) = await RoundTripAsync<ThermalExpansionStressInput, ThermalExpansionStressResult>(ThermalExpansionStressCalculationDefinition.Id, ToInput(example));

        // Example 3, with the elastic restraint: the optional stiffness
        // survives the round trip in the retained input as well as the result.
        AssertClose(example.ExpectedRestraintForce, readBack.Result.RestraintForce, example.RelativeTolerance, "RestraintForce after read-back");
        Assert.Equal(executed.Result.Outcome, readBack.Result.Outcome);
        AssertHasIntermediate(executed, "Free movement");
        AssertHasIntermediate(executed, "Restraint");
    }
}
