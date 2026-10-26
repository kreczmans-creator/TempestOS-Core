using Tempest.Calc.Sheets;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// `v0.26.0` (ADR-0165): the static list of every worksheet calculator this
/// build composes in. Static by design (§7.6): nothing is discovered from
/// a path; a calculator assembly is a project reference and its
/// calculators are named here. Phases 2–5 append to this list.
/// </summary>
public static class CalcCalculatorComposition
{
    public static IReadOnlyList<ICalcCalculator> All() =>
    [
        .. Tempest.Calc.Mechanical.MechanicalCalculators.All,
        .. Tempest.Calc.Structural.StructuralCalculators.All,
        .. Tempest.Calc.BuildingServices.BuildingServicesCalculators.All,
        .. Tempest.Calc.Electrical.ElectricalCalculators.All,
        .. Tempest.Calc.FluidPower.FluidPowerCalculators.All,
    ];

    /// <summary>
    /// Addendum C, C-6: the cable-capacity and device-rating inputs the demonstration project's motor-supply cable steps are given. They are the electrical
    /// calculator's own synthetic test tables (invented values, not from any regulation or manufacturer); no build ships real ones.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> DemoCableTables() =>
    [
        Tempest.Calc.Electrical.ConductorSizingCalculator.SyntheticAmpacity(),
        Tempest.Calc.Electrical.ConductorSizingCalculator.TypicalRatings(),
    ];
}
