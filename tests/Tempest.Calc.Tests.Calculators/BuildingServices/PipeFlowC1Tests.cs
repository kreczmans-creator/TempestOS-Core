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
    public void A_Custom_Fluid_With_NPSH_Required_Gets_NPSH_Available_Or_Is_Refused()
    {
        // bs.pipe-pump-01: rho 1050, mu 3 mPa.s, 30 m3/h through 10 m DN80 (eps 0.045, K 2) marked suction, z_s -8 m, NPSH_r 3 m, p_v 20 kPa absolute.
        // Independent python: h_f,suction 0.68389 m; NPSH_a = 101.325e3/(rho g) - 8 - 0.68389 - 20e3/(rho g) = -0.78594 m, so the check fails.
        CalcInputValue[] Inputs(params CalcInputValue[] more) => [CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 1050, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 3, "mPa*s"),
            CalcInputValue.Quantity("Q", 30, "m^3/h"), PipeFlowCalculator.Segments(("suction line", 10, 80, 0.045, 2, 0, 0, "suction")), CalcInputValue.Quantity("z_s", -8, "m"), CalcInputValue.Quantity("NPSH_r", 3, "m"), .. more];

        var withVapour = Run(Inputs(CalcInputValue.Quantity("p_v_custom", 20, "kPa")));
        Assert.Equal(-0.78594, withVapour.OutputSi("NPSH_a"), 4);
        Assert.False(withVapour.Passed);
        Assert.DoesNotContain(withVapour.Notices, n => n.RuleId is "npsh-custom" or "npsh-no-suction");

        // F2: with no vapour pressure there is no NPSH_a and no NPSH check, and the sheet warned and passed; it is now refused.
        var without = Run(Inputs());
        Assert.Null(without.FindOutput("NPSH_a"));
        Assert.True(without.IsBlocked);
        var block = Assert.Single(without.Blocks);
        Assert.Equal("npsh-custom", block.RuleId);
        Assert.StartsWith("p_v_custom:", block.Message, StringComparison.Ordinal);
        Assert.Empty(CalcMessageShape.Problems(block.Message));
        Assert.NotEqual(true, without.Passed);

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
        CalcRunResult At(string? side) => Run(CalcInputValue.Quantity("T_fluid", 60, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), Line(side),
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
        Assert.DoesNotContain(Run(CalcInputValue.Quantity("T_fluid", 60, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), Line(null), CalcInputValue.Quantity("z_s", -3, "m")).Warnings, w => w.RuleId == "npsh-no-suction");
    }

    [Fact]
    public void The_Shaft_Torque_And_Speed_Follow_The_Shifted_Duty_When_The_Speed_Ratio_Is_Not_One()
    {
        // chain.wet-heating-R1: 24 kW over 10 K, water 70 C, 60 m DN32 (eps 0.045, K 15, no rise), n_pump 2800 rpm, curve (0,6) (1,5) (2,3.5) (3,1) m3/h, eta 0.7.
        // Independent python: n2/n1 1 gives P 21.692 W (T 0.07398 N.m at 2800 rpm); n2/n1 1.2 gives Q_duty2 2.97699 m3/h, H 3.30903 m, P_shaft2 37.483 W at 3360 rpm, so T = 37.483/(3360 2 pi/60) = 0.10653 N.m.
        CalcRunResult At(double nRatio) => Run(CalcInputValue.Quantity("T_fluid", 70, "degC"), CalcInputValue.Quantity("Phi_load", 24, "kW"), CalcInputValue.Quantity("dT_load", 10, "K"), PipeFlowCalculator.Segments(("loop", 60, 32, 0.045, 15, 0, 0, "discharge")),
            CalcInputValue.VectorOf("Q_pump", [0, 1, 2, 3], "m^3/h"), CalcInputValue.VectorOf("H_pump", [6, 5, 3.5, 1], "m"), CalcInputValue.Of("n_ratio", nRatio), CalcInputValue.Of("eta_pump", 0.7), CalcInputValue.Quantity("n_pump", 2800, "rpm"));

        var rated = At(1);
        Assert.Equal(0.07398, rated.OutputSi("T_shaft"), 4);
        Assert.Equal(2800 * 2 * Math.PI / 60, rated.OutputSi("n_shaft"), 6);

        var faster = At(1.2);
        Assert.Equal(37.483, faster.OutputSi("P_shaft2"), 2);
        Assert.Equal(0.10653, faster.OutputSi("T_shaft"), 4);
        Assert.Equal(3360 * 2 * Math.PI / 60, faster.OutputSi("n_shaft"), 6);
    }

    private static CalcRunResult HeatLoad(double hOut, double? cp = null)
    {
        var inputs = new List<CalcInputValue>
        {
            CalcInputValue.Quantity("T_fluid", 40, "degC"), CalcInputValue.Quantity("Phi_load", 24, "kW"), CalcInputValue.Quantity("dT_load", 10, "K"),
            PipeFlowCalculator.Segments(("loop", 40, 28, 0.045, 6, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", hOut, "m"),
        };

        if (cp is { } value)
            inputs.Add(CalcInputValue.Quantity("cp_load", value, "J/(kg*K)"));

        return Run([.. inputs]);
    }

    [Fact]
    public void A_Heat_Load_Sheet_With_No_Outlet_Head_Warns_That_The_Plant_Loss_Is_Missing()
    {
        // chain.wet-heating-03 and chain.cooling-02: the pack's pump head was pipe friction alone (H_sys 3.002 m of which 0.002 m friction in the reference case); the boiler, coil, valves and emitters add their own loss.
        // Chain probe: h_out 1 m raises T_shaft 3.7 times with no notice either way.
        Assert.Contains(HeatLoad(0).Warnings, w => w.RuleId == "plant-loss");
        Assert.DoesNotContain(HeatLoad(2).Warnings, w => w.RuleId == "plant-loss");
        // A flow entered directly is not a heat-load sheet.
        Assert.DoesNotContain(Run(CalcInputValue.Quantity("Q", 1, "m^3/h"), PipeFlowCalculator.Segments(("loop", 40, 28, 0.045, 6, 0, 0, "discharge"))).Warnings, w => w.RuleId == "plant-loss");
    }

    [Fact]
    public void A_Heat_Load_With_A_Glycol_Specific_Heat_On_Water_Warns_That_The_Fluid_Is_Not_Water()
    {
        // chain.cooling-03 and chain.wet-heating-06: cp 3600 reached B1 as c_p only, the density and viscosity stayed water's: Q 3.3 % high, friction about 20 % low (0.6606 m against 0.7894 m for rho 1030, mu 3.5 mPa.s).
        Assert.Contains(HeatLoad(1, 3600).Warnings, w => w.RuleId == "fluid-cp");
        Assert.DoesNotContain(HeatLoad(1, 4186).Warnings, w => w.RuleId == "fluid-cp");
        Assert.DoesNotContain(HeatLoad(1, 4200).Warnings, w => w.RuleId == "fluid-cp");
        Assert.DoesNotContain(Run(CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 1030, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 3.5, "mPa*s"), CalcInputValue.Quantity("T_fluid", 40, "degC"),
            CalcInputValue.Quantity("Phi_load", 24, "kW"), CalcInputValue.Quantity("dT_load", 10, "K"), CalcInputValue.Quantity("cp_load", 3600, "J/(kg*K)"), PipeFlowCalculator.Segments(("loop", 40, 28, 0.045, 6, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", 1, "m")).Warnings, w => w.RuleId == "fluid-cp");
    }

    [Fact]
    public void Laminar_Friction_Scales_With_Q_In_The_System_Curve()
    {
        // bs.pipe-pump-03 (probe W): oil rho 900, mu 200 mPa.s, 100 m DN50, rise 2 m, 5 m3/h (Re 159), pump (0,30) (5,27) (10,22) (15,14) (20,4) m3/h and m.
        // python3: h_f(5 m3/h) = 20.517 m and h_f proportional to Q: duty 5.8784 m3/h, H 26.1216 m; the Q^2 law gave 5.4674 m3/h (7 % low) and 5.5 % less shaft power.
        var r = Run(CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 900, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 200, "mPa*s"),
            CalcInputValue.Quantity("Q", 5, "m^3/h"), PipeFlowCalculator.Segments(("oil", 100, 50, 0.045, 0, 0, 2, "discharge")),
            CalcInputValue.VectorOf("Q_pump", [0, 5, 10, 15, 20], "m^3/h"), CalcInputValue.VectorOf("H_pump", [30, 27, 22, 14, 4], "m"));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(20.517, r.OutputSi("h_f_total"), 2);
        Assert.Equal(5.8784, r.OutputSi("Q_duty") * 3600, 3);
        Assert.Equal(26.1216, r.OutputSi("H_duty"), 3);
        Assert.Equal(1.0585, r.OutputSi("Q_duty") * 3600 * r.OutputSi("H_duty") / (5.467442 * (2 + 20.517025 * 5.467442 * 5.467442 / 25)), 2);
    }

    [Fact]
    public void A_Pump_Curve_That_Crosses_The_System_Curve_Twice_Reports_The_Stable_Crossing_And_Lists_Both()
    {
        // bs.pipe-pump-04 (probe R): DN80 100 m (eps 0.045, K 2), rise 10 m, 30 m3/h, water 20 C, pump (0,8) (10,14) (40,18) (70,2). The curve crosses at 3.41 m3/h (the pump curve rising: unstable)
        // and 41.48 m3/h (falling: stable); the test of the two curve ends saw no crossing and failed the duty check. python3 scan: 3.4147 and 41.4835 m3/h.
        var r = Run(CalcInputValue.Quantity("T_fluid", 20, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), PipeFlowCalculator.Segments(("riser", 100, 80, 0.045, 2, 0, 10, "discharge")),
            CalcInputValue.VectorOf("Q_pump", [0, 10, 40, 70], "m^3/h"), CalcInputValue.VectorOf("H_pump", [8, 14, 18, 2], "m"));
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "no-duty");
        Assert.Equal(41.48, r.OutputSi("Q_duty") * 3600, 1);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "multi-duty");
        Assert.Contains("3.41", warning.Message, StringComparison.Ordinal);
        Assert.Contains("unstable", warning.Message, StringComparison.Ordinal);
        Assert.Contains(r.Checks, k => k.Name.StartsWith("Duty point against", StringComparison.Ordinal) && k.Passed);
    }

    [Fact]
    public void A_Crossing_Only_On_A_Rising_Part_Of_The_Pump_Curve_Fails_The_Stable_Duty_Check()
    {
        // F2: the same DN80 100 m line, rise 10 m, 15 m3/h, water 20 C, pump (0,8) (10,14) (20,18): the pump curve crosses the system curve once, at about 3.41 m3/h, where the pump curve
        // rises (python3 scan of the example above: 3.4147 m3/h, and the pump stays above the system curve to its end at 20 m3/h). The sheet warned that the point is unstable and passed.
        var r = Run(CalcInputValue.Quantity("T_fluid", 20, "degC"), CalcInputValue.Quantity("Q", 15, "m^3/h"), PipeFlowCalculator.Segments(("riser", 100, 80, 0.045, 2, 0, 10, "discharge")),
            CalcInputValue.VectorOf("Q_pump", [0, 10, 20], "m^3/h"), CalcInputValue.VectorOf("H_pump", [8, 14, 18], "m"));
        Assert.Equal(3.41, r.OutputSi("Q_duty") * 3600, 1);
        Assert.Contains(r.Warnings, w => w.RuleId == "duty-unstable");
        var check = Assert.Single(r.Checks, k => k.Name.StartsWith("Duty point against a stable crossing", StringComparison.Ordinal));
        Assert.False(check.Passed);
        Assert.False(r.Passed);

        // A falling crossing (the other example) keeps passing: no stable-duty check is added.
        var stable = Run(CalcInputValue.Quantity("T_fluid", 20, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"), PipeFlowCalculator.Segments(("riser", 100, 80, 0.045, 2, 0, 10, "discharge")),
            CalcInputValue.VectorOf("Q_pump", [0, 10, 40, 70], "m^3/h"), CalcInputValue.VectorOf("H_pump", [8, 14, 18, 2], "m"));
        Assert.DoesNotContain(stable.Checks, k => k.Name.StartsWith("Duty point against a stable crossing", StringComparison.Ordinal));
        Assert.Contains(Calc.Template.ApplicabilityRules, x => x.Id == "duty-unstable" && x.Effect == CalcRuleEffect.Unconservative);
    }

    [Fact]
    public void The_Default_Efficiency_For_A_Small_Pump_Warns()
    {
        // bs.pipe-pump-C2: the default eta 0.7 for a small circulator (shaft power well under 1 kW) lowers the torque handed to the motor; at eta 0.2 it is 3.5 times higher.
        CalcRunResult At(double eta) => Run(CalcInputValue.Quantity("Q", 1, "m^3/h"), PipeFlowCalculator.Segments(("loop", 20, 20, 0.045, 2, 0, 0, "discharge")), CalcInputValue.Quantity("n_pump", 2800, "rpm"), CalcInputValue.Of("eta_pump", eta));
        var d = At(0.7);
        Assert.Contains(d.Warnings, w => w.RuleId == "eta-default");
        Assert.Equal(3.5, At(0.2).OutputSi("T_shaft") / d.OutputSi("T_shaft"), 6);
        Assert.DoesNotContain(At(0.2).Warnings, w => w.RuleId == "eta-default");

        // A pump of over 1 kW: no warning.
        Assert.DoesNotContain(Run(CalcInputValue.Quantity("Q", 100, "m^3/h"), PipeFlowCalculator.Segments(("main", 100, 100, 0.045, 5, 0, 30, "discharge")), CalcInputValue.Quantity("n_pump", 2900, "rpm")).Warnings, w => w.RuleId == "eta-default");
    }

    [Fact]
    public void NPSH_Available_Is_Taken_At_The_Larger_Of_The_Design_And_The_Duty_Flow()
    {
        // bs.pipe-pump-R2: water 60 C, design 30 m3/h, 10 m DN65 suction (K 4), 100 m DN65 discharge (K 6), lift 3 m, pump (0,30) (20,28) (40,22) (60,10): duty 38.54 m3/h. NPSH_a at the design flow
        // 3.21 m passed against 3 m; at the duty flow the suction friction is higher: NPSH_a 1.76 m (python3, water properties from the same correlations) and the check fails.
        CalcInputValue[] Sheet(params CalcInputValue[] more) => [CalcInputValue.Quantity("T_fluid", 60, "degC"), CalcInputValue.Quantity("Q", 30, "m^3/h"),
            PipeFlowCalculator.Segments(("suction", 10, 65, 0.045, 4, 0, 0, "suction"), ("discharge", 100, 65, 0.045, 6, 0, 0, "discharge")), CalcInputValue.Quantity("z_s", -3, "m"), CalcInputValue.Quantity("NPSH_r", 3, "m"), .. more];
        var noCurve = Run(Sheet());
        Assert.Equal(3.2102, noCurve.OutputSi("NPSH_a"), 2);

        var curve = Run(Sheet(CalcInputValue.VectorOf("Q_pump", [0, 20, 40, 60], "m^3/h"), CalcInputValue.VectorOf("H_pump", [30, 28, 22, 10], "m")));
        Assert.Equal(38.54, curve.OutputSi("Q_duty") * 3600, 1);
        Assert.Equal(1.76, curve.OutputSi("NPSH_a"), 1);
        Assert.Contains(curve.Checks, k => k.Name.StartsWith("NPSH required", StringComparison.Ordinal) && !k.Passed);
    }
}
