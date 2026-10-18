using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary><c>calc.plane-wall-heat-transfer</c> against its specification's vectors.</summary>
public class PlaneWallHeatTransferCalculationDefinitionTests
{
    private static PlaneWallHeatTransferResult Run(PlaneWallHeatTransferVectors.Vector v) =>
        new PlaneWallHeatTransferCalculationDefinition().Calculate(v.ToInput(), new CalculationContext());

    private static PlaneWallHeatTransferVectors.Vector Example(int index) =>
        ((IEnumerable<object[]>)PlaneWallHeatTransferVectors.WorkedExamples).Select(r => (PlaneWallHeatTransferVectors.Vector)r[0]).ElementAt(index);

    private static double Celsius(Quantity<Temperature> t) => t.ConvertTo(TemperatureUnits.DegreeCelsius).Value;

    [Theory]
    [MemberData(nameof(PlaneWallHeatTransferVectors.WorkedExamples), MemberType = typeof(PlaneWallHeatTransferVectors))]
    public void WorkedExamples_MatchTheHandCalculation(PlaneWallHeatTransferVectors.Vector v)
    {
        var result = Run(v);

        Assert.Equal(v.ExpectedResistances!.Count, result.Resistances.Count);
        Assert.Equal(result.Resistances.Count, result.ResistanceNames.Count);
        Assert.Equal(result.Resistances.Count, result.TemperatureDrops.Count);
        for (var i = 0; i < v.ExpectedResistances.Count; i++)
            AssertClose(v.ExpectedResistances[i], result.Resistances[i].BaseValue, v.RelativeTolerance, $"Resistances[{i}]");

        AssertClose(v.ExpectedTotalResistance, result.TotalThermalResistance.BaseValue, v.RelativeTolerance, "TotalThermalResistance");
        AssertClose(v.ExpectedU, result.OverallHeatTransferCoefficient.BaseValue, v.RelativeTolerance, "OverallHeatTransferCoefficient");
        AssertClose(v.ExpectedHeatFlow, result.HeatFlow.BaseValue, v.RelativeTolerance, "HeatFlow");
        AssertClose(v.ExpectedFlux, result.HeatFlux.BaseValue, v.RelativeTolerance, "HeatFlux");

        // One surface per layer boundary: the hot surface, each interface, the cold surface.
        Assert.Equal(v.Layers.Count + 1, result.SurfaceTemperatures.Count);
        for (var i = 0; i < v.ExpectedSurfacesCelsius!.Count; i++)
            AssertClose(v.ExpectedSurfacesCelsius[i], Celsius(result.SurfaceTemperatures[i]), v.RelativeTolerance, $"SurfaceTemperatures[{i}]");

        // The drops add up to the whole temperature difference.
        AssertClose(v.Hot.BaseValue - v.Cold.BaseValue, result.TemperatureDrops.Sum(d => d.BaseValue), 1e-9, "Sum of drops");
    }

    [Theory]
    [MemberData(nameof(PlaneWallHeatTransferVectors.InvalidInputs), MemberType = typeof(PlaneWallHeatTransferVectors))]
    public void MalformedInputs_AreRejectedAsInvalid(PlaneWallHeatTransferVectors.Vector v)
    {
        var refused = Assert.Throws<CalculationInputInvalidException>(() => Run(v));

        Assert.Contains(v.ExpectedReasonFragment!, refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AReversedTemperatureDifference_ReversesTheHeatFlow()
    {
        var example = Example(0);
        var reversed = example.ToInput() with { HotSideTemperature = example.Cold, ColdSideTemperature = example.Hot };

        var result = new PlaneWallHeatTransferCalculationDefinition().Calculate(reversed, new CalculationContext());

        AssertClose(-266.161, result.HeatFlow.BaseValue, example.RelativeTolerance, "HeatFlow");
    }

    [Fact]
    public void TheFilmsAreNamedInTheCircuit_AroundTheLayers()
    {
        var result = Run(Example(1));

        Assert.Equal(["Hot-side convection film", "Outer glass", "Air gap", "Inner glass", "Cold-side convection film"], result.ResistanceNames);
    }

    [Fact]
    public async Task ExecutesThroughTheEngine_AndReadsBackEveryTemperature()
    {
        var v = Example(1);

        var (executed, readBack) = await RoundTripAsync<PlaneWallHeatTransferInput, PlaneWallHeatTransferResult>(PlaneWallHeatTransferCalculationDefinition.Id, v.ToInput());

        Assert.Equal(4, readBack.Result.SurfaceTemperatures.Count);
        AssertClose(14.229, Celsius(readBack.Result.SurfaceTemperatures[0]), v.RelativeTolerance, "Inner surface after read-back");
        AssertClose(69.2478, readBack.Result.HeatFlow.BaseValue, v.RelativeTolerance, "HeatFlow after read-back");
        Assert.Equal(5, readBack.Result.ResistanceNames.Count);
        AssertHasIntermediate(executed, "Air gap resistance");
        AssertHasIntermediate(executed, "Overall heat transfer coefficient");
    }
}
