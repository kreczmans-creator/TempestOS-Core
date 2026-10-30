using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class PlaneWallTests
{
    private static readonly PlaneWallCalculator Wall = new();

    private static CalcInputSet Inputs(params (string, double, double)[] layers) =>
        CalcTemplateHasher.DefaultInputs(Wall.Template).With(PlaneWallCalculator.Layers(layers));

    private static CalcInputSet NoFilms(params (string, double, double)[] layers) =>
        new([.. Inputs(layers).Values.Where(v => v.Symbol is not ("h_1" or "h_2"))]);

    [Fact]
    public void The_Drops_Add_Up_To_The_Temperature_Difference()
    {
        Gen.Select(Gen.Double[1, 500], Gen.Double[0.01, 5], Gen.Double[-30, 60]).Sample((t, k, t1) =>
        {
            var r = CalcRunner.Run(Wall, Inputs(("a", t, k), ("b", 2 * t, k / 3)).With(CalcInputValue.Quantity("T_1", t1, "degC")));
            var q = r.OutputSi("Phi");
            var cold = r.OutputSi("T_s2");
            return Math.Abs(cold - q * r.OutputSi("R_h2") - 263.15) < 1e-9 * Math.Max(1, Math.Abs(cold));
        }, iter: 50);
    }

    [Fact]
    public void Without_Films_The_End_Surfaces_Are_The_Given_Temperatures()
    {
        var r = CalcRunner.Run(Wall, NoFilms(("brick", 200, 0.72), ("insulation", 50, 0.04)));
        Assert.False(r.IsBlocked);
        Assert.Null(r.FindOutput("R_h1"));
        Assert.Equal(293.15, r.OutputSi("T_s0"), 9);
        Assert.Equal(263.15, r.OutputSi("T_s2"), 9);
    }

    [Fact]
    public void U_Value_Does_Not_Depend_On_The_Temperatures_And_Flow_Is_Linear_In_The_Difference()
    {
        var a = CalcRunner.Run(Wall, Inputs(("glass", 8, 0.78)).With(CalcInputValue.Quantity("T_1", 20, "degC")).With(CalcInputValue.Quantity("T_2", 0, "degC")));
        var b = CalcRunner.Run(Wall, Inputs(("glass", 8, 0.78)).With(CalcInputValue.Quantity("T_1", 40, "degC")).With(CalcInputValue.Quantity("T_2", 0, "degC")));
        Assert.Equal(a.OutputSi("U_wall"), b.OutputSi("U_wall"), 12);
        Assert.Equal(2 * a.OutputSi("Phi"), b.OutputSi("Phi"), 9);
    }

    [Fact]
    public void Reversed_Flow_Is_Negative_And_Warned()
    {
        var r = CalcRunner.Run(Wall, Inputs(("glass", 8, 0.78)).With(CalcInputValue.Quantity("T_1", 0, "degC")).With(CalcInputValue.Quantity("T_2", 20, "degC")));
        Assert.True(r.OutputSi("Phi") < 0);
        Assert.Contains(r.Warnings, w => w.RuleId == "reversed");
    }

    [Fact]
    public void No_Layers_Blocks()
    {
        var r = CalcRunner.Run(Wall, CalcTemplateHasher.DefaultInputs(Wall.Template).With(CalcInputValue.Table("layers", [])));
        Assert.Contains(r.Blocks, b => b.RuleId == "no-layers");
    }
}
