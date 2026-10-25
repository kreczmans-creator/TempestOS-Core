using Tempest.Calc.Sheets;

namespace Tempest.Calc.FluidPower;

/// <summary>
/// Every Fluid Power worksheet calculator, for static composition (ADR-0170).
/// Empty until B-2 adds F1 (cylinder and actuator circuit), F2 (accumulator)
/// and F3 (seal groove); the assembly, its test project and the composition
/// wiring exist now so those land as additions to this list only.
/// </summary>
public static class FluidPowerCalculators
{
    public static IReadOnlyList<ICalcCalculator> All { get; } = [new AccumulatorCalculator()];
}
