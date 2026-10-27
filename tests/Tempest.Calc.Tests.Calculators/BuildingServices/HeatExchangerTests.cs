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
    public void A_Cold_Side_Inlet_Below_The_Air_Dew_Point_Warns_That_The_Duty_Is_Sensible_Only()
    {
        // chain.cooling-01: 35 degC air at 60 % RH has a dew point of 26.07 degC (python3, Magnus b = 17.62, c = 243.12: gamma = ln 0.6 + 17.62 x 35/(243.12 + 35); 243.12 gamma/(17.62 - gamma) = 26.072; the
        // finding quotes 26.07), and the example coil's water enters at 22 degC: the coil surface condenses, the sensible-only duty misses the latent load and no notice was given.
        var set = Defaults().With(CalcInputValue.Quantity("T_c_in", 22, "degC")).With(CalcInputValue.Quantity("T_dew", 26.07, "degC"));
        var r = CalcRunner.Run(Hx, set);
        Assert.Contains(r.Warnings, w => w.RuleId == "condensation");
        Assert.DoesNotContain(CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("T_c_in", 28, "degC")).With(CalcInputValue.Quantity("T_dew", 26.07, "degC"))).Warnings, w => w.RuleId == "condensation");
        Assert.DoesNotContain(CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("T_c_in", 22, "degC"))).Warnings, w => w.RuleId == "condensation");
    }

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
            Assert.Equal(r.OutputSi("Phi"), r.Steps.Single(s => s.Symbol == "Q_lmtd").Result is Tempest.Calc.Engine.Values.ScalarValue v ? v.Si : double.NaN, 6);
        }
    }

    [Fact]
    public void Sizing_Then_Rating_Round_Trips()
    {
        var sized = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Choice("mode", HeatExchangerCalculator.Sizing)).With(CalcInputValue.Quantity("Q_req", 100, "kW")));
        var area = sized.OutputSi("A_req");
        var rated = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("A", area, "m^2")));
        Assert.Equal(100e3, rated.OutputSi("Phi"), 3);
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
        var clean = CalcRunner.Run(Hx, Defaults()).OutputSi("Phi");
        var fouled = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("R_f", 0.0005, "m^2*K/W"))).OutputSi("Phi");
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
        var air = CalcRunner.Run(Hx, Defaults().With(CalcInputValue.Quantity("cp_h", 1006, "J/(kg*K)")).With(CalcInputValue.Quantity("m_h", 0.4, "kg/s")).With(CalcInputValue.Quantity("U_clean", 60, "W/(m^2*K)")));
        Assert.False(air.IsBlocked);
        Assert.Equal(0.4 * 1006, air.Steps.Single(s => s.Symbol == "C_min").Result is Tempest.Calc.Engine.Values.ScalarValue c ? c.Si : double.NaN, 9);
    }

    private static CalcInputSet Isothermal(string side) => Defaults().With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Isothermal)).With(CalcInputValue.Choice("const_side", side));

    [Fact]
    public void A_Stream_At_Constant_Temperature_Never_Sets_C_Min_And_Leaves_At_Its_Inlet_Temperature()
    {
        // Black B-A-02 / Green G-B-07: with the hot side held at constant temperature (condensing vapour), the cold stream is C_min whatever the hot
        // stream's own m·c_p. The old code took min(C_h, C_c) and changed the constant stream's temperature too.
        var small = CalcRunner.Run(Hx, Isothermal(HeatExchangerCalculator.HotConstant).With(CalcInputValue.Quantity("m_h", 0.5, "kg/s")).With(CalcInputValue.Quantity("m_c", 2, "kg/s")));
        var large = CalcRunner.Run(Hx, Isothermal(HeatExchangerCalculator.HotConstant).With(CalcInputValue.Quantity("m_h", 50, "kg/s")).With(CalcInputValue.Quantity("m_c", 2, "kg/s")));
        Assert.Equal(small.OutputSi("Phi"), large.OutputSi("Phi"), 9);
        Assert.Equal(0.5, small.OutputSi("NTU"), 9);                                   // UA 4180 W/K over C_c 8360 W/K
        Assert.Equal(197.36422, small.OutputSi("Phi") / 1e3, 4);                          // (1 − e^−0.5) × 8360 × 60 K (python3)
        Assert.Equal(80, small.OutputSi("T_h_out") - 273.15, 9);
        Assert.Equal(small.OutputSi("Phi"), small.OutputSi("Q_lmtd"), 3);
        Assert.DoesNotContain(small.Warnings, w => w.RuleId == "lmtd-mismatch");
    }

    [Fact]
    public void A_Cold_Side_At_Constant_Temperature_Leaves_At_Its_Inlet_And_The_Hot_Stream_Falls()
    {
        var r = CalcRunner.Run(Hx, Isothermal(HeatExchangerCalculator.ColdConstant));
        Assert.Equal(20, r.OutputSi("T_c_out") - 273.15, 9);
        Assert.Equal(0, r.OutputSi("dT_c"), 12);
        Assert.Equal(80 - 0.6321205588 * 60, r.OutputSi("T_h_out") - 273.15, 6);
        Assert.Equal(r.OutputSi("Phi"), r.OutputSi("Q_lmtd"), 3);
    }

    [Fact]
    public void The_Epsilon_Ntu_And_Lmtd_Duties_Agree_For_Every_Arrangement_And_Constant_Side()
    {
        Gen.Select(Gen.Double[0.05, 3], Gen.Double[0.1, 5], Gen.Double[0.1, 5], Gen.Int[0, 5]).Sample((area, mh, mc, kind) =>
        {
            var set = Defaults().With(CalcInputValue.Quantity("A", area, "m^2")).With(CalcInputValue.Quantity("m_h", mh, "kg/s")).With(CalcInputValue.Quantity("m_c", mc, "kg/s"));
            set = kind switch
            {
                0 => set.With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Counterflow)),
                1 => set.With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Parallel)),
                2 => set.With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.ShellTube)),
                3 => set.With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Isothermal)).With(CalcInputValue.Choice("const_side", HeatExchangerCalculator.HotConstant)),
                4 => set.With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Isothermal)).With(CalcInputValue.Choice("const_side", HeatExchangerCalculator.ColdConstant)),
                _ => set.With(CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Crossflow)),
            };

            var r = CalcRunner.Run(Hx, set);
            return r.IsBlocked || r.FindOutput("Q_lmtd") is null || Math.Abs(r.OutputSi("Q_lmtd") - r.OutputSi("Phi")) < 1e-6 * r.OutputSi("Phi");
        }, iter: 150);
    }
}
