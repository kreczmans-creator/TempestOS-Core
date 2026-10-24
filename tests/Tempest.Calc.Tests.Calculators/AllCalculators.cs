using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>Every calculator under test, as the composer sees them. Phases 3–5 append their assemblies here.</summary>
public static class AllCalculators
{
    /// <summary>Each discipline's shipped list. A new <see cref="CalcDiscipline"/> must be listed here (a test fails until it is), so its calculators meet every cross-discipline check.</summary>
    public static IReadOnlyDictionary<CalcDiscipline, IReadOnlyList<ICalcCalculator>> ByDiscipline { get; } = new Dictionary<CalcDiscipline, IReadOnlyList<ICalcCalculator>>
    {
        [CalcDiscipline.Mechanical] = Tempest.Calc.Mechanical.MechanicalCalculators.All,
        [CalcDiscipline.Structural] = Tempest.Calc.Structural.StructuralCalculators.All,
        [CalcDiscipline.BuildingServices] = Tempest.Calc.BuildingServices.BuildingServicesCalculators.All,
        [CalcDiscipline.Electrical] = Tempest.Calc.Electrical.ElectricalCalculators.All,
        [CalcDiscipline.FluidPower] = Tempest.Calc.FluidPower.FluidPowerCalculators.All,
    };

    public static IReadOnlyList<ICalcCalculator> All { get; } = [.. ByDiscipline.Values.SelectMany(list => list)];

    public static TheoryData<string> Ids => [.. All.Select(c => c.Template.Id)];

    public static ICalcCalculator Get(string id) => All.Single(c => c.Template.Id == id);
}
