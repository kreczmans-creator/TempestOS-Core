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

    [Fact]
    public void Cold_Side_Rise_And_Mean_Temperature_Are_Ports_For_The_Cold_Circuit()
    {
        // Balanced counterflow, NTU = 1: Q = 125.4 kW into 4180 W/K, so the cold stream rises 30 K from 20 °C and averages 35 °C.
        var rated = CalcRunner.Run(Hx, Defaults());
        Assert.Equal(30, rated.OutputSi("dT_c"), 1e-9);
        Assert.Equal(35 + 273.15, rated.OutputSi("T_c_m"), 1e-9);
        Assert.Contains(Hx.Template.OutputPorts!, p => p is { Symbol: "dT_c", Unit: "K" });
        Assert.Contains(Hx.Template.OutputPorts!, p => p is { Symbol: "T_c_m", Unit: "degC" });

        // Sizing: the rise is Q_req / (m_c c_p,c) whatever the duty, and the mean is halfway between inlet and outlet.
        Gen.Select(Gen.Double[0.5, 50], Gen.Double[0.05, 2], Gen.Double[-20, 40]).Sample((q, mc, tin) =>
        {
            var r = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Choice("mode", HeatExchangerCalculator.Sizing)).With(CalcInputValue.Quantity("Q_req", q, "kW"))
                .With(CalcInputValue.Quantity("m_c", mc, "kg/s")).With(CalcInputValue.Quantity("T_c_in", tin, "degC")).With(CalcInputValue.Quantity("m_h", 2, "kg/s")));

            if (r.IsBlocked)
                return;

            Assert.Equal(q * 1e3 / (mc * 4180), r.OutputSi("dT_c"), 1e-9);
            Assert.Equal(tin + 273.15 + r.OutputSi("dT_c") / 2, r.OutputSi("T_c_m"), 1e-9);
            Assert.Equal(r.OutputSi("T_c_out") - r.OutputSi("dT_c"), tin + 273.15, 1e-9);
        }, iter: 60);
    }

    [Fact]
    public void The_Cold_Side_Specific_Heat_Is_A_Port_So_A_Glycol_Mixture_Reaches_The_Pump_Flow()
    {
        var r = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("cp_c", 3700, "J/(kg*K)")));
        Assert.Equal(3700, r.OutputSi("cp_c"), 1e-9);
        Assert.Contains(Hx.Template.OutputPorts!, p => p is { Symbol: "cp_c", Unit: "J/(kg*K)" });

        // Air on the hot side (c_p 1006 J/kg·K): the duty needs no table, only the stream's capacity rate.
        var air = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("cp_h", 1006, "J/(kg*K)")).With(CalcInputValue.Quantity("m_h", 0.4, "kg/s")).With(CalcInputValue.Quantity("U", 60, "W/(m^2*K)")));
        Assert.False(air.IsBlocked);
        Assert.Equal(0.4 * 1006, air.Steps.Single(s => s.Symbol == "C_min").Result is Tempest.Calc.Engine.Values.ScalarValue c ? c.Si : double.NaN, 9);
    }
}
