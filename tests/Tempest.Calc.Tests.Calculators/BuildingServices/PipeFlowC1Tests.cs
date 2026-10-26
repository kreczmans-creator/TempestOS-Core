using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

/// <summary>Regression tests for the C-1 findings on bs.pipe-pump (Track P verification pack).</summary>
public sealed class PipeFlowC1Tests
{
    private static readonly PipeFlowCalculator Calc = new();

    private static CalcRunResult Run(params CalcInputValue[] values) => CalcRunner.Run(Calc, new CalcInputSet(values));

    private static CalcInputValue[] CustomWaterLike(params CalcInputValue[] more) =>
        [CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 998.2, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 1.002, "mPa*s"), .. more];

    [Fact]
    public void Impeller_Trim_Scales_The_Flow_With_D_And_The_Similar_Pump_With_D_Cubed()
    {
        // bs.pipe-pump-02: 100 m DN80 (eps 0.045, K 2, rise 10 m), 30 m3/h, curve (0,40) (20,36) (40,24) (60,4), eta 0.7, D2/D1 0.8, fluid with rho 998.2, mu 1.002 mPa.s.
        // Independent python (Colebrook, bisection on the piecewise-linear curve): trim Q ~ D, H ~ D^2: Q_duty2 32.99895 m3/h, H 14.56084 m, P 1.86648 kW;
        // geometrically similar Q ~ D^3: Q_duty2 22.99609 m3/h, H 12.21489 m, P 1.09114 kW (the product's old only law).
        CalcInputValue[] Pump(params CalcInputValue[] more) => CustomWaterLike([CalcInputValue.Quantity("Q", 30, "m^3/h"), PipeFlowCalculator.Segments(("riser", 100, 80, 0.045, 2, 0, 10, "discharge")),
            CalcInputValue.VectorOf("Q_pump", [0, 20, 40, 60], "m^3/h"), CalcInputValue.VectorOf("H_pump", [40, 36, 24, 4], "m"), CalcInputValue.Of("D_ratio", 0.8), CalcInputValue.Of("eta_pump", 0.7), .. more]);

        var trim = Run(Pump());
        Assert.Equal(32.99895, trim.OutputSi("Q_duty2") * 3600, 3);
        Assert.Equal(14.56084, trim.OutputSi("H_duty2"), 3);
        Assert.Equal(1.86648, trim.OutputSi("P_shaft2") / 1000, 4);

        var similar = Run(Pump(CalcInputValue.Choice("D_law", PipeFlowCalculator.DiameterSimilar)));
        Assert.Equal(22.99609, similar.OutputSi("Q_duty2") * 3600, 3);
        Assert.Equal(12.21489, similar.OutputSi("H_duty2"), 3);
        Assert.Equal(1.09114, similar.OutputSi("P_shaft2") / 1000, 4);
    }

    [Fact]
    public void A_Custom_Fluid_With_NPSH_Required_Gets_NPSH_Available_Or_A_Notice()
    {
        // bs.pipe-pump-01: rho 1050, mu 3 mPa.s, 30 m3/h through 10 m DN80 (eps 0.045, K 2) marked suction, z_s -8 m, NPSH_r 3 m, p_v 20 kPa absolute.
        // Independent python: h_f,suction 0.68389 m; NPSH_a = 101.325e3/(rho g) - 8 - 0.68389 - 20e3/(rho g) = -0.78594 m, so the check fails.
        CalcInputValue[] Inputs(params CalcInputValue[] more) => [CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 1050, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 3, "mPa*s"),
            CalcInputValue.Quantity("Q", 30, "m^3/h"), PipeFlowCalculator.Segments(("suction line", 10, 80, 0.045, 2, 0, 0, "suction")), CalcInputValue.Quantity("z_s", -8, "m"), CalcInputValue.Quantity("NPSH_r", 3, "m"), .. more];

        var withVapour = Run(Inputs(CalcInputValue.Quantity("p_v_custom", 20, "kPa")));
        Assert.Equal(-0.78594, withVapour.OutputSi("NPSH_a"), 4);
        Assert.False(withVapour.Passed);
        Assert.DoesNotContain(withVapour.Notices, n => n.RuleId is "npsh-custom" or "npsh-no-suction");

        var without = Run(Inputs());
        Assert.Null(without.FindOutput("NPSH_a"));
        var warning = Assert.Single(without.Warnings, w => w.RuleId == "npsh-custom");
        Assert.Contains("p_v_custom", warning.Message, StringComparison.Ordinal);

        // No NPSH_r asked: nothing to warn about.
        Assert.DoesNotContain(Run(Inputs(new CalcInputValue("NPSH_r"))).Notices, n => n.RuleId == "npsh-custom");
    }

    [Fact]
    public void NPSH_Required_With_No_Suction_Segment_Warns_That_The_Friction_Is_Left_Out()
    {
        // bs.pipe-pump-R1: water 60 C, 30 m3/h, one 60 m DN65 segment (eps 0.045, K 6), z_s -3, NPSH_r 3. Side blank (discharge): NPSH_a 5.4404 m and a pass; suction: -2.1512 m and a fail
        // (the verification script's independent figures).
        // The side column is optional: a blank cell is the usual way a user leaves it, and means discharge.
        CalcInputValue Line(string? side) => CalcInputValue.Table("segments", [CalcRow.Of([("name", new CalcCell(Text: "line")), ("L", new CalcCell(60, "m")), ("D", new CalcCell(65, "mm")), ("eps", new CalcCell(0.045, "mm")), ("K", new CalcCell(6)), .. side is null ? Array.Empty<(string, CalcCell)>() : [("side", new CalcCell(Text: side))]])]);
        CalcRunResult At(string? side) => Run(CalcInputValue.Quantity("T", 60, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), Line(side),
            CalcInputValue.Quantity("z_s", -3, "m"), CalcInputValue.Quantity("NPSH_r", 3, "m"));

        var blank = At(null);
        Assert.Equal(5.4404, blank.OutputSi("NPSH_a"), 2);
        var warning = Assert.Single(blank.Warnings, w => w.RuleId == "npsh-no-suction");
        Assert.Contains("suction", warning.Message, StringComparison.Ordinal);

        var marked = At("suction");
        Assert.Equal(-2.1512, marked.OutputSi("NPSH_a"), 2);
        Assert.False(marked.Passed);
        Assert.DoesNotContain(marked.Warnings, w => w.RuleId == "npsh-no-suction");

        // No NPSH_r entered: no warning.
        Assert.DoesNotContain(Run(CalcInputValue.Quantity("T", 60, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), Line(null), CalcInputValue.Quantity("z_s", -3, "m")).Warnings, w => w.RuleId == "npsh-no-suction");
    }

    [Fact]
    public void The_Shaft_Torque_And_Speed_Follow_The_Shifted_Duty_When_The_Speed_Ratio_Is_Not_One()
    {
        // chain.wet-heating-R1: 24 kW over 10 K, water 70 C, 60 m DN32 (eps 0.045, K 15, no rise), n_pump 2800 rpm, curve (0,6) (1,5) (2,3.5) (3,1) m3/h, eta 0.7.
        // Independent python: n2/n1 1 gives P 21.692 W (T 0.07398 N.m at 2800 rpm); n2/n1 1.2 gives Q_duty2 2.97699 m3/h, H 3.30903 m, P_shaft2 37.483 W at 3360 rpm, so T = 37.483/(3360 2 pi/60) = 0.10653 N.m.
        CalcRunResult At(double nRatio) => Run(CalcInputValue.Quantity("T", 70, "degC"), CalcInputValue.Quantity("Phi_load", 24, "kW"), CalcInputValue.Quantity("dT_load", 10, "K"), PipeFlowCalculator.Segments(("loop", 60, 32, 0.045, 15, 0, 0, "discharge")),
            CalcInputValue.VectorOf("Q_pump", [0, 1, 2, 3], "m^3/h"), CalcInputValue.VectorOf("H_pump", [6, 5, 3.5, 1], "m"), CalcInputValue.Of("n_ratio", nRatio), CalcInputValue.Of("eta_pump", 0.7), CalcInputValue.Quantity("n_pump", 2800, "rpm"));

        var rated = At(1);
        Assert.Equal(0.07398, rated.OutputSi("T_shaft"), 4);
        Assert.Equal(2800 * 2 * Math.PI / 60, rated.OutputSi("n_shaft"), 6);

        var faster = At(1.2);
        Assert.Equal(37.483, faster.OutputSi("P_shaft2"), 2);
        Assert.Equal(0.10653, faster.OutputSi("T_shaft"), 4);
        Assert.Equal(3360 * 2 * Math.PI / 60, faster.OutputSi("n_shaft"), 6);
    }
}
