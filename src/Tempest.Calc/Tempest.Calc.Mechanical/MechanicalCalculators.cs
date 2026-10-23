using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>Every mechanical worksheet calculator, for static composition.</summary>
public static class MechanicalCalculators
{
    public static IReadOnlyList<ICalcCalculator> All { get; } =
    [
        new ToleranceStackCalculator(),
    ];
}
