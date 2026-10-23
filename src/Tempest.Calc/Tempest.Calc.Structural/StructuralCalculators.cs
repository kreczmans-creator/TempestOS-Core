using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>Every structural worksheet calculator, for static composition.</summary>
public static class StructuralCalculators
{
    public static IReadOnlyList<ICalcCalculator> All { get; } =
    [
        new SectionPropertiesCalculator(),
        new FrameCalculator(),
        new BeamFormulasCalculator(),
        new MemberChecksCalculator(),
    ];
}
