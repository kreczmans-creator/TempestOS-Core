using Tempest.Calc.Library;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum A, L1: the pack templates (saved chains) this build composes in, beside the calculators
/// (<see cref="CalcCalculatorComposition"/>). Each later addendum phase adds the chains its calculators complete.
/// </summary>
public static class CalcPackTemplateComposition
{
    public static IReadOnlyList<CalcPackTemplate> All() => [];
}
