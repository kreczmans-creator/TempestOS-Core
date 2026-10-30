using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>B-0 review, LOW 8: the cross-discipline checks run over every discipline's calculators, Fluid Power's included.</summary>
public sealed class AllCalculatorsTests
{
    [Fact]
    public void Every_Discipline_Has_Its_Shipped_List_In_The_Cross_Discipline_Checks()
    {
        Assert.Equal(Enum.GetValues<CalcDiscipline>().Order(), AllCalculators.ByDiscipline.Keys.Order());
        Assert.Same(Tempest.Calc.FluidPower.FluidPowerCalculators.All, AllCalculators.ByDiscipline[CalcDiscipline.FluidPower]);
        Assert.Equal(AllCalculators.ByDiscipline.Values.Sum(list => list.Count), AllCalculators.All.Count);
        Assert.All(AllCalculators.ByDiscipline, pair => Assert.All(pair.Value, c => Assert.Equal(pair.Key, c.Template.Discipline)));
        Assert.Equal(AllCalculators.All.Count, AllCalculators.All.Select(c => c.Template.Id).Distinct(StringComparer.Ordinal).Count());
    }
}
