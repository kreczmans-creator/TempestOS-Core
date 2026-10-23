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
    ];
}
