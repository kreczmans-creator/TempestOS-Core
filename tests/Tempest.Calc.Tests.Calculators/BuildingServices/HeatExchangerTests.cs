using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class HeatExchangerTests
{
    private static readonly HeatExchangerCalculator Hx = new();

    private static readonly string[] Arrangements = [HeatExchangerCalculator.Counterflow, HeatExchangerCalculator.Parallel, HeatExchangerCalculator.ShellTube, HeatExchangerCalculator.Crossflow, HeatExchangerCalculator.Isothermal];

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Hx.Template);

    [Fact]
    public void Inverse_Ntu_Recovers_The_Forward_Relation_For_Every_Arrangement()
    {
        Gen.Select(Gen.Int[0, Arrangements.Length - 1], Gen.Double[0.05, 5], Gen.Double[0, 1]).Sample((i, ntu, cr) =>
        {
            var arrangement = Arrangements[i];
            var eps = HeatExchangerCalculator.Effectiveness(arrangement, ntu, arrangement == HeatExchangerCalculator.Isothermal ? 0 : cr);
            var back = HeatExchangerCalculator.InverseNtu(arrangement, eps, arrangement == HeatExchangerCalculator.Isothermal ? 0 : cr);
            return Math.Abs(back - ntu) < 1e-6 * Math.Max(1, ntu) || (arrangement == HeatExchangerCalculator.Parallel && 1 - eps * (1 + cr) < 1e-9);
        }, iter: 200);
    }

    [Fact]
    public void Counterflow_Is_The_Most_Effective_Arrangement()
    {
        Gen.Select(Gen.Double[0.05, 5], Gen.Double[0.01, 1]).Sample((ntu, cr) =>
        {
            var counter = HeatExchangerCalculator.Effectiveness(HeatExchangerCalculator.Counterflow, ntu, cr);
            return Arrangements.Skip(1).Where(a => a != HeatExchangerCalculator.Isothermal).All(a => HeatExchangerCalculator.Effectiveness(a, ntu, cr) <= counter + 1e-9);
        }, iter: 100);
    }

    [Fact]
    public void Lmtd_And_Ntu_Methods_Give_The_Same_Duty()
    {
        foreach (var arrangement in new[] { HeatExchangerCalculator.Counterflow, HeatExchangerCalculator.Parallel, HeatExchangerCalculator.ShellTube })
        {
            var r = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Choice("arrangement", arrangement)).With(CalcInputValue.Quantity("m_c", 1.7, "kg/s")));
            Assert.Equal(r.OutputSi("Q"), r.Steps.Single(s => s.Symbol == "Q_lmtd").Result is Tempest.Calc.Engine.Values.ScalarValue v ? v.Si : double.NaN, 6);
        }
    }

    [Fact]
    public void Sizing_Then_Rating_Round_Trips()
    {
        var sized = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Choice("mode", HeatExchangerCalculator.Sizing)).With(CalcInputValue.Quantity("Q_req", 100, "kW")));
        var area = sized.OutputSi("A_req");
        var rated = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("A", area, "m^2")));
        Assert.Equal(100e3, rated.OutputSi("Q"), 3);
    }

    [Fact]
    public void Unattainable_Duty_Blocks()
    {
        var r = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Choice("mode", HeatExchangerCalculator.Sizing)).With(CalcInputValue.Quantity("Q_req", 300, "kW")));
        Assert.True(r.IsBlocked);
        Assert.Equal("duty-unattainable", r.Blocks[0].RuleId);
    }

    [Fact]
    public void Cold_Inlet_Above_Hot_Inlet_Blocks()
    {
        var r = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("T_c_in", 90, "degC")));
        Assert.True(r.IsBlocked);
        Assert.StartsWith("T_h_in:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fouling_Reduces_Duty()
    {
        var clean = CalcRunner.Run(Hx, Defaults()).OutputSi("Q");
        var fouled = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("R_f", 0.0005, "m^2*K/W"))).OutputSi("Q");
        Assert.True(fouled < clean);
    }

    [Fact]
    public void Crossflow_Is_Flagged_Approximate()
    {
        var r = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Crossflow)));
        Assert.Contains(r.Warnings, w => w.RuleId == "crossflow-approx");
    }
}
