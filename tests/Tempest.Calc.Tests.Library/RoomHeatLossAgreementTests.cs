using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// B4 reuses the series-resistance arithmetic of the legacy Core module <c>calc.plane-wall-heat-transfer</c> (Addendum A, §A6):
/// the same wall, with the legacy film coefficients h₁ = 1/R_si and h₂ = 1/R_se, must give the same U-value and the same
/// heat flow through one square metre.
/// </summary>
public sealed class RoomHeatLossAgreementTests
{
    public static TheoryData<string, double, double, double, double[][]> Walls => new()
    {
        // name, Rsi, Rse, Rg (0 = none), layers as [d mm, λ]
        { "Insulated cavity wall", 0.13, 0.04, 0, [[102.5, 0.84], [100, 0.03], [100, 0.2], [13, 0.4]] },
        { "Single slab", 0.13, 0.04, 0, [[100, 0.5]] },
        { "Roof build-up with different films", 0.10, 0.04, 0, [[12.5, 0.23], [200, 0.045], [18, 0.16]] },
        { "Partition with an air gap", 0.13, 0.13, 0.18, [[12.5, 0.22], [12.5, 0.22]] },
        { "Thin film layers", 0.17, 0.04, 0, [[1, 5], [500, 0.01], [3.5, 0.9]] },
    };

    [Theory]
    [MemberData(nameof(Walls))]
    public void B4_And_The_Legacy_Plane_Wall_Module_Agree_On_U_And_Heat_Flow(string name, double rsi, double rse, double rg, double[][] layers)
    {
        // The legacy module has no air-gap input: an air gap is a 1 mm layer of conductivity 0.001/R_g, which has resistance R_g.
        var legacyLayers = layers.Select((l, i) => new PlaneWallLayer($"Layer {i + 1}", new Quantity<Length>(l[0], LengthUnits.Millimetre), new Quantity<ThermalConductivity>(l[1], ThermalConductivityUnits.WattPerMetreKelvin))).ToList();

        if (rg > 0)
            legacyLayers.Add(new PlaneWallLayer("Air gap", new Quantity<Length>(1, LengthUnits.Millimetre), new Quantity<ThermalConductivity>(0.001 / rg, ThermalConductivityUnits.WattPerMetreKelvin)));

        var legacy = new PlaneWallHeatTransferCalculationDefinition().Calculate(
            new PlaneWallHeatTransferInput(
                new Quantity<Temperature>(21, TemperatureUnits.DegreeCelsius),
                new Quantity<HeatTransferCoefficient>(1 / rsi, HeatTransferCoefficientUnits.WattPerSquareMetreKelvin),
                legacyLayers,
                new Quantity<HeatTransferCoefficient>(1 / rse, HeatTransferCoefficientUnits.WattPerSquareMetreKelvin),
                new Quantity<Temperature>(-3, TemperatureUnits.DegreeCelsius),
                new Quantity<Area>(1, AreaUnits.SquareMetre)),
            new CalculationContext());

        var inputs = new CalcInputSet(
        [
            CalcInputValue.Quantity("T_int", 21, "degC"),
            CalcInputValue.Quantity("theta_e", -3, "degC"),
            RoomHeatLossCalculator.Layers(layers.Select((l, i) => (name, $"Layer {i + 1}", l[0], l[1])).ToArray()),
            RoomHeatLossCalculator.Elements((name, 1, null, null, rsi, rse, rg > 0 ? rg : null, null, null)),
        ]);

        var result = CalcRunner.Run(new RoomHeatLossCalculator(), inputs);
        Assert.False(result.IsBlocked, string.Join("; ", result.Blocks.Select(b => b.Message)));

        var u = legacy.OverallHeatTransferCoefficient.BaseValue;
        Assert.Equal(u, result.OutputSi("U_1"), 9);
        Assert.Equal(legacy.TotalThermalResistance.BaseValue, result.OutputSi("R_1"), 9);
        Assert.Equal(legacy.HeatFlow.BaseValue, result.OutputSi("Phi_1"), 9);
    }
}
