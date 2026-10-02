using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.thermal-resistance-chain</c> against its specification's vectors.</summary>
public class ThermalResistanceChainCalculationDefinitionTests
{
    private static ThermalResistanceChainResult Run(ThermalResistanceChainVectors.Vector v, CalculationContext? context = null) =>
        new ThermalResistanceChainCalculationDefinition().Calculate(v.ToInput(), context ?? new CalculationContext());

    private static ThermalResistanceChainVectors.Vector Example(int index) =>
        ((IEnumerable<object[]>)ThermalResistanceChainVectors.WorkedExamples).Select(r => (ThermalResistanceChainVectors.Vector)r[0]).ElementAt(index);

    private static double Celsius(Quantity<Temperature> t) => t.ConvertTo(TemperatureUnits.DegreeCelsius).Value;

    [Theory]
    [MemberData(nameof(ThermalResistanceChainVectors.WorkedExamples), MemberType = typeof(ThermalResistanceChainVectors))]
    public void WorkedExamples_MatchTheHandCalculation(ThermalResistanceChainVectors.Vector v)
    {
        var result = Run(v);

        Assert.Equal(OutcomeFor(v.ExpectedMeetsCriteria), result.Outcome);
        Assert.Equal(v.ExpectedMeetsCriteria, result.CriterionMet);
        AssertClose(v.ExpectedTotalResistance, result.TotalThermalResistance.BaseValue, v.RelativeTolerance, "TotalThermalResistance");
        Assert.Equal(v.ExpectedRises!.Count, result.StageTemperatureRises.Count);
        Assert.Equal(v.ExpectedNodesCelsius!.Count, result.NodeTemperatures.Count);

        for (var i = 0; i < v.ExpectedRises.Count; i++)
        {
            AssertClose(v.ExpectedRises[i], result.StageTemperatureRises[i].BaseValue, v.RelativeTolerance, $"StageTemperatureRises[{i}]");
            AssertClose(v.ExpectedNodesCelsius[i], Celsius(result.NodeTemperatures[i]), v.RelativeTolerance, $"NodeTemperatures[{i}]");
        }

        AssertClose(v.ExpectedNodesCelsius[0], Celsius(result.SourceTemperature), v.RelativeTolerance, "SourceTemperature");
        AssertClose(v.ExpectedRises.Sum(), result.TotalTemperatureRise.BaseValue, v.RelativeTolerance, "TotalTemperatureRise");
        AssertClose(v.ExpectedMargin, result.ThermalMargin.BaseValue, v.RelativeTolerance, "ThermalMargin");
        AssertClose(v.ExpectedPermittedResistance, result.MaximumPermittedTotalResistance?.BaseValue, v.RelativeTolerance, "MaximumPermittedTotalResistance");
        AssertClose(v.ExpectedUtilisation, result.Utilisation, v.RelativeTolerance, "Utilisation");
    }

    [Theory]
    [MemberData(nameof(ThermalResistanceChainVectors.InvalidInputs), MemberType = typeof(ThermalResistanceChainVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(ThermalResistanceChainVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZeroPower_SitsAtAmbient_AndHasNoResistanceBudget()
    {
        var input = Example(0).ToInput() with { PowerDissipation = new(0, PowerUnits.Watt) };

        var result = new ThermalResistanceChainCalculationDefinition().Calculate(input, new CalculationContext());

        AssertClose(40.0, Celsius(result.SourceTemperature), 1e-12, "SourceTemperature");
        Assert.Null(result.MaximumPermittedTotalResistance);
        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, result.Outcome);
    }

    [Fact]
    public void TheExceededCase_RecordsAnUnsatisfiedCheck_NamingTheJunctionTemperature()
    {
        var context = new CalculationContext();
        Run(Example(2), context);

        Assert.Contains(context.ConstraintChecks, c => !c.IsSatisfied && (c.Detail ?? string.Empty).Contains("Source 140 degC", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackEveryNode()
    {
        var v = Example(0);

        var (executed, readBack) = await RoundTripAsync<ThermalResistanceChainInput, ThermalResistanceChainResult>(ThermalResistanceChainCalculationDefinition.Id, v.ToInput());

        Assert.Equal(3, readBack.Result.NodeTemperatures.Count);
        AssertClose(102.5, Celsius(readBack.Result.SourceTemperature), v.RelativeTolerance, "SourceTemperature after read-back");
        AssertClose(85.0, Celsius(readBack.Result.NodeTemperatures[2]), v.RelativeTolerance, "Heat sink temperature after read-back");
        Assert.Equal(executed.Result.Outcome, readBack.Result.Outcome);
        AssertHasIntermediate(executed, "Junction to case rise");
        AssertHasIntermediate(executed, "Maximum permitted total resistance");
    }
}
