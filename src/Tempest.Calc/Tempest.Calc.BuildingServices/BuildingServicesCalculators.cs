using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>Every building-services worksheet calculator, for static composition.</summary>
public static class BuildingServicesCalculators
{
    public static IReadOnlyList<ICalcCalculator> All { get; } =
    [
        new PipeFlowCalculator(),
        new PsychrometricsCalculator(),
        new HeatExchangerCalculator(),
        new SealedSystemCalculator(),
        new RoomHeatLossCalculator(),
        new DuctSizingCalculator(),
        new GlaserCalculator(),
        new PipeExpansionCalculator(),
        new InsulationCalculator(),
        new PlaneWallCalculator(),
    ];
}
