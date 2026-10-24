using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>Every mechanical worksheet calculator, for static composition.</summary>
public static class MechanicalCalculators
{
    public static IReadOnlyList<ICalcCalculator> All { get; } =
    [
        new ToleranceStackCalculator(),
        new Iso286FitCalculator(),
        new PressFitCalculator(),
        new ThickCylinderCalculator(),
        new BoltedJointCalculator(),
        new BoltGroupCalculator(),
        new BearingLifeCalculator(),
        new WeldGroupCalculator(),
        new ShaftCalculator(),
    ];
}
