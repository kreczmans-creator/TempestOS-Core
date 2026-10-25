using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>Every electrical worksheet calculator, for static composition.</summary>
public static class ElectricalCalculators
{
    public static IReadOnlyList<ICalcCalculator> All { get; } =
    [
        new MotorSizingCalculator(),
        new EnclosureHeatCalculator(),
        new VoltageDropCalculator(),
        new LoadBudgetCalculator(),
        new FaultProtectionCalculator(),
        new ConductorSizingCalculator(),
        new PcbTraceCalculator(),
        new ContainmentCalculator(),
    ];
}
