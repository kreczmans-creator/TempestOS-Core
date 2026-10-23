using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>Every calculator under test, as the composer sees them. Phases 3–5 append their assemblies here.</summary>
public static class AllCalculators
{
    public static IReadOnlyList<ICalcCalculator> All { get; } =
    [
        .. Tempest.Calc.Mechanical.MechanicalCalculators.All,
        .. Tempest.Calc.Structural.StructuralCalculators.All,
    ];

    public static TheoryData<string> Ids => [.. All.Select(c => c.Template.Id)];

    public static ICalcCalculator Get(string id) => All.Single(c => c.Template.Id == id);
}
