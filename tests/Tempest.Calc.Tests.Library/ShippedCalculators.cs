using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>Every shipped calculator by discipline, for the checks that range over all of them. A new <see cref="CalcDiscipline"/> must be listed (a test fails until it is).</summary>
internal static class ShippedCalculators
{
    public static IReadOnlyDictionary<CalcDiscipline, IReadOnlyList<ICalcCalculator>> ByDiscipline { get; } = new Dictionary<CalcDiscipline, IReadOnlyList<ICalcCalculator>>
    {
        [CalcDiscipline.Electrical] = Tempest.Calc.Electrical.ElectricalCalculators.All,
        [CalcDiscipline.Mechanical] = Tempest.Calc.Mechanical.MechanicalCalculators.All,
        [CalcDiscipline.Structural] = Tempest.Calc.Structural.StructuralCalculators.All,
        [CalcDiscipline.BuildingServices] = Tempest.Calc.BuildingServices.BuildingServicesCalculators.All,
        [CalcDiscipline.FluidPower] = Tempest.Calc.FluidPower.FluidPowerCalculators.All,
    };

    public static IReadOnlyList<ICalcCalculator> All { get; } = [.. ByDiscipline.Values.SelectMany(list => list)];
}

/// <summary>B-0 review, LOW 8: the cross-discipline checks of this project (G-A-02) cover every discipline, Fluid Power's included.</summary>
public sealed class ShippedCalculatorsTests
{
    [Fact]
    public void Every_Discipline_Is_In_The_Cross_Discipline_Calculator_List()
    {
        Assert.Equal(Enum.GetValues<CalcDiscipline>().Order(), ShippedCalculators.ByDiscipline.Keys.Order());
        Assert.Same(Tempest.Calc.FluidPower.FluidPowerCalculators.All, ShippedCalculators.ByDiscipline[CalcDiscipline.FluidPower]);
        Assert.Equal(ShippedCalculators.ByDiscipline.Values.Sum(list => list.Count), ShippedCalculators.All.Count);
    }
}
