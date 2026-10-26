using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class PipeFlowTests
{
    private static readonly PipeFlowCalculator Calc = new();

    private static CalcRunResult Run(params CalcInputValue[] values) => CalcRunner.Run(Calc, new CalcInputSet(values));

    [Fact]
    public void Fitting_Losses_Below_Re_4000_Are_Flagged_As_Understated()
    {
        // L-h: K is a turbulent-flow coefficient and rises at low Re; the fittings term is used as entered, with a warning.
        CalcRunResult At(double qh, double k) => Run(CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 870, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 30, "mPa*s"),
            CalcInputValue.Quantity("Q", qh, "m^3/h"), PipeFlowCalculator.Segments(("line", 12, 20, 0.045, k, 0, 0, "discharge")));

        // 4.5376 m3/h in 20 mm is Re 2327 (transitional): K = 6 warns, K = 0 does not.
        var transitional = At(4.53764705882353, 6);
        Assert.Contains(transitional.Notices, n => n.RuleId == "transition");
        var warning = Assert.Single(transitional.Notices, n => n.RuleId == "fittings");
        Assert.Contains("line", warning.Message, StringComparison.Ordinal);
        Assert.Contains("K = 6", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(At(4.53764705882353, 0).Notices, n => n.RuleId == "fittings");

        // Laminar flow too; turbulent flow (Re well above 4000) does not warn.
        Assert.Contains(At(1, 6).Notices, n => n.RuleId == "fittings");
        Assert.DoesNotContain(At(40, 6).Notices, n => n.RuleId == "fittings" || n.RuleId == "laminar" || n.RuleId == "transition");

        // The loss itself is unchanged: the fittings term K·ρ·v²/2 is added as entered.
        var v = 4.53764705882353 / 3600 / (Math.PI * 0.02 * 0.02 / 4);
        Assert.Equal(At(4.53764705882353, 0).OutputSi("dp_total") + 6 * 870 * v * v / 2, transitional.OutputSi("dp_total"), 1e-3);
        Assert.Equal("1.9.0", Calc.Template.Version);
    }

    [Fact]
    public void Water_Correlations_Hit_Reference_Points()
    {
        Assert.Equal(999.97, Water.Density(4), 0.05);
        Assert.Equal(998.2, Water.Density(20), 0.1);
        Assert.Equal(958.4, Water.Density(100), 0.2);
        Assert.Equal(1.002e-3, Water.Viscosity(20), 1.002e-5);
        Assert.Equal(0.547e-3, Water.Viscosity(50), 0.547e-5);
        Assert.Equal(2339, Water.VapourPressure(20), 10.0);
        Assert.Equal(101325, Water.VapourPressure(100), 600.0);
    }

    [Fact]
    public void Colebrook_Satisfies_Its_Own_Equation_And_Swamee_Jain_Is_Close()
    {
        Gen.Select(Gen.Double[4000, 1e8], Gen.Double[1e-6, 0.05]).Sample((re, rr) =>
        {
            var (f, _) = Friction.Colebrook(re, rr);
            var lhs = 1 / Math.Sqrt(f);
            var rhs = -2 * Math.Log10(rr / 3.7 + 2.51 / (re * Math.Sqrt(f)));
            Assert.Equal(lhs, rhs, lhs * 1e-9);

            if (rr <= 1e-2 && re >= 5e3)
                Assert.Equal(f, Friction.SwameeJain(re, rr), f * 0.03);
        }, iter: 300);
    }

    [Fact]
    public void Laminar_Loss_Is_Hagen_Poiseuille()
    {
        Gen.Select(Gen.Double[0.1, 5], Gen.Double[10, 200], Gen.Double[0.001, 1]).Sample((qh, dmm, mu) =>
        {
            var r = Run(CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 900, "kg/m^3"), CalcInputValue.Quantity("mu_custom", mu * 1e3, "mPa*s"),
                CalcInputValue.Quantity("Q", qh, "m^3/h"), PipeFlowCalculator.Segments(("s", 10, dmm, 0, 0, 0, 0, "discharge")));
            Assert.False(r.IsBlocked);
            var d = dmm / 1e3;
            var v = qh / 3600 / (Math.PI * d * d / 4);
            var re = 900 * v * d / mu;

            if (re < 2300)
            {
                Assert.Contains(r.Notices, n => n.RuleId == "laminar");
                Assert.Equal(32 * mu * 10 * v / (d * d), r.OutputSi("dp_total"), 32 * mu * 10 * v / (d * d) * 1e-9);
            }
        }, iter: 100);
    }

    [Fact]
    public void Series_Segments_Add_And_Fittings_Count()
    {
        var one = Run(CalcInputValue.Quantity("Q", 20, "m^3/h"), PipeFlowCalculator.Segments(("a", 50, 80, 0.045, 4, 10, 3, "discharge")));
        var two = Run(CalcInputValue.Quantity("Q", 20, "m^3/h"), PipeFlowCalculator.Segments(("a", 25, 80, 0.045, 2, 5, 1, "discharge"), ("b", 25, 80, 0.045, 2, 5, 2, "suction")));
        Assert.Equal(one.OutputSi("h_f_total"), two.OutputSi("h_f_total"), one.OutputSi("h_f_total") * 1e-9);
        Assert.Equal(one.OutputSi("H_static"), two.OutputSi("H_static"), 1e-12);
        Assert.True(two.OutputSi("NPSH_a") < one.OutputSi("NPSH_a"));
    }

    [Fact]
    public void Affinity_Laws_Scale_The_Duty_Point()
    {
        var baseInputs = Calc.Template.TestCases[2].Inputs.Where(v => v.Symbol is not ("n_ratio" or "D_ratio")).ToList();
        var r = CalcRunner.Run(Calc, new CalcInputSet([.. baseInputs, CalcInputValue.Of("n_ratio", 1.2), CalcInputValue.Of("D_ratio", 1)]));
        Assert.False(r.IsBlocked);
        Assert.True(r.OutputSi("Q_duty2") > r.OutputSi("Q_duty"));
        Assert.True(r.OutputSi("H_duty2") > r.OutputSi("H_duty"));
        var same = CalcRunner.Run(Calc, new CalcInputSet([.. baseInputs, CalcInputValue.Of("n_ratio", 1), CalcInputValue.Of("D_ratio", 1)]));
        Assert.Null(same.FindOutput("Q_duty2"));
    }

    [Fact]
    public void Pump_Below_System_Warns_Without_A_Duty_Point()
    {
        var r = Run(CalcInputValue.Quantity("Q", 30, "m^3/h"), PipeFlowCalculator.Segments(("riser", 60, 80, 0.045, 8, 0, 50, "discharge")),
            CalcInputValue.VectorOf("Q_pump", [0, 20, 40], "m^3/h"), CalcInputValue.VectorOf("H_pump", [40, 36, 24], "m"));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Notices, n => n.RuleId == "no-duty");
        Assert.False(r.Passed);
    }

    [Fact]
    public void Bad_Inputs_Block_With_The_Symbol()
    {
        static void Expect(CalcRunResult r, Func<CalcNotice, bool> match) => Assert.True(r.Blocks.Any(match), "blocks: " + string.Join(" | ", r.Blocks.Select(b => b.RuleId + ": " + b.Message)));
        Expect(Run(CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("Q", 1, "m^3/h"), PipeFlowCalculator.Segments(("s", 1, 50, 0, 0, 0, 0, "discharge"))), b => b.Message.StartsWith("rho_custom", StringComparison.Ordinal));
        Expect(Run(CalcInputValue.Quantity("T", 150, "degC"), CalcInputValue.Quantity("Q", 1, "m^3/h"), PipeFlowCalculator.Segments(("s", 1, 50, 0, 0, 0, 0, "discharge"))), b => b.Message.StartsWith("T:", StringComparison.Ordinal));
        Expect(Run(CalcInputValue.Quantity("Q", 1, "m^3/h")), b => b.Message.StartsWith("segments", StringComparison.Ordinal));
        Expect(Run(CalcInputValue.Quantity("Q", 1, "m^3/h"), PipeFlowCalculator.Segments(("s", 1, 50, 0, 0, 0, 0, "discharge")), CalcInputValue.VectorOf("Q_pump", [10, 5], "m^3/h"), CalcInputValue.VectorOf("H_pump", [1, 2], "m")), b => b.RuleId == "pump-curve");
    }

    [Fact]
    public void Velocity_Limit_Warns_And_Fails_The_Check()
    {
        var r = Run(CalcInputValue.Quantity("Q", 100, "m^3/h"), PipeFlowCalculator.Segments(("s", 10, 50, 0.045, 0, 0, 0, "discharge")), CalcInputValue.Quantity("v_max", 3, "m/s"));
        Assert.Contains(r.Notices, n => n.RuleId == "velocity");
        Assert.False(r.Passed);
    }

    private static CalcInputValue[] Loop(params CalcInputValue[] extra) =>
    [
        CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidWater), CalcInputValue.Quantity("T", 70, "degC"),
        PipeFlowCalculator.Segments(("loop", 40, 28, 0.045, 6, 0, 2, "discharge")), CalcInputValue.Quantity("h_out", 0, "m"), .. extra,
    ];

    [Fact]
    public void A_Heat_Load_Gives_The_Design_Flow_Phi_Over_Rho_Cp_DeltaT()
    {
        Gen.Select(Gen.Double[100, 1e6], Gen.Double[2, 40], Gen.Double[30, 90]).Sample((phi, dt, temp) =>
        {
            var r = Run([.. Loop(CalcInputValue.Quantity("Phi_load", phi, "W"), CalcInputValue.Quantity("dT_load", dt, "K"), CalcInputValue.Quantity("cp_load", 4186, "J/(kg*K)")), CalcInputValue.Quantity("T", temp, "degC")]);
            Assert.False(r.IsBlocked);
            var expected = phi / (Water.Density(temp) * 4186 * dt);
            Assert.Equal(expected, r.OutputSi("Q_design"), expected * 1e-9);

            // The same flow typed in gives the same hydraulics.
            var typed = Run([.. Loop(CalcInputValue.Quantity("Q", expected * 3600, "m^3/h")), CalcInputValue.Quantity("T", temp, "degC")]);
            Assert.Equal(typed.OutputSi("P_hyd"), r.OutputSi("P_hyd"), Math.Abs(typed.OutputSi("P_hyd")) * 1e-9 + 1e-15);
        }, iter: 60);
    }

    [Fact]
    public void A_Heat_Load_Needs_Its_Temperature_Difference_And_A_Flow_Is_Needed_Without_One()
    {
        var phiOnly = Run(Loop(CalcInputValue.Quantity("Phi_load", 1000, "W")));
        Assert.True(phiOnly.IsBlocked);
        Assert.Contains(phiOnly.Blocks, b => b.RuleId == "heat-flow" && b.Message.StartsWith("Phi_load:", StringComparison.Ordinal));

        var dtOnly = Run(Loop(CalcInputValue.Quantity("dT_load", 10, "K")));
        Assert.True(dtOnly.IsBlocked);

        var neither = Run(Loop());
        Assert.True(neither.IsBlocked);
        Assert.Contains(neither.Blocks, b => b.RuleId == "heat-flow" && b.Message.StartsWith("Q:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Flow_Typed_Beside_A_Heat_Load_Is_Ignored_With_A_Notice()
    {
        var r = Run(Loop(CalcInputValue.Quantity("Q", 50, "m^3/h"), CalcInputValue.Quantity("Phi_load", 1000, "W"), CalcInputValue.Quantity("dT_load", 10, "K")));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Notices, n => n.RuleId == "heat-flow-q");
        Assert.True(r.OutputSi("Q_design") < 1e-4);
    }

    [Fact]
    public void Shaft_Torque_Is_Hydraulic_Power_Over_Efficiency_And_Angular_Speed_And_Needs_A_Pump_Speed()
    {
        Gen.Select(Gen.Double[0.5, 50], Gen.Double[300, 5000], Gen.Double[0.3, 0.9]).Sample((qh, rpm, eta) =>
        {
            var r = Run(Loop(CalcInputValue.Quantity("Q", qh, "m^3/h"), CalcInputValue.Quantity("n_pump", rpm, "rpm"), CalcInputValue.Of("eta_pump", eta)));
            Assert.False(r.IsBlocked);
            var shaft = r.OutputSi("P_hyd") / eta;
            Assert.Equal(shaft, r.OutputSi("P_shaft_design"), shaft * 1e-9);
            var torque = shaft / (rpm * 2 * Math.PI / 60);
            Assert.Equal(torque, r.OutputSi("T_shaft"), torque * 1e-9);
        }, iter: 60);

        var without = Run(Loop(CalcInputValue.Quantity("Q", 5, "m^3/h")));
        Assert.DoesNotContain(without.Outputs, o => o.Symbol == "T_shaft");
    }

    [Fact]
    public void Water_Without_A_Specific_Heat_Records_The_Assumed_Constant_And_A_Custom_Fluid_Needs_One()
    {
        var water = Run(Loop(CalcInputValue.Quantity("Phi_load", 12, "kW"), CalcInputValue.Quantity("dT_load", 20, "K")));
        Assert.False(water.IsBlocked);
        Assert.Equal(12000 / (Water.Density(70) * 4186 * 20), water.OutputSi("Q_design"), 1e-12);
        var step = Assert.Single(water.Steps, st => st.Symbol == "c_p");
        Assert.Contains("c_p of water 4186 J/(kg·K) (assumed)", step.Formula, StringComparison.Ordinal);
        Assert.Contains(water.RunAssumptions, a => a.Statement.Contains("4186", StringComparison.Ordinal));
        Assert.DoesNotContain(water.Steps, st => st.Symbol == "cp_load");

        var given = Run(Loop(CalcInputValue.Quantity("Phi_load", 12, "kW"), CalcInputValue.Quantity("dT_load", 20, "K"), CalcInputValue.Quantity("cp_load", 4000, "J/(kg*K)")));
        Assert.Equal(12000 / (Water.Density(70) * 4000 * 20), given.OutputSi("Q_design"), 1e-12);
        Assert.DoesNotContain(given.RunAssumptions, a => a.Statement.Contains("4186", StringComparison.Ordinal));

        var custom = Run(CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 1050, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 3, "mPa*s"),
            CalcInputValue.Quantity("Phi_load", 12, "kW"), CalcInputValue.Quantity("dT_load", 20, "K"), CalcInputValue.Quantity("T", 60, "degC"),
            PipeFlowCalculator.Segments(("loop", 40, 28, 0.045, 0, 0, 0, "discharge")));
        Assert.True(custom.IsBlocked);
        Assert.Contains(custom.Blocks, b => b.RuleId == "cp_load" && b.Message.StartsWith("cp_load:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Heat_Load_Without_A_Temperature_Warns()
    {
        CalcInputValue[] noTemperature = [CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidWater), PipeFlowCalculator.Segments(("loop", 40, 28, 0.045, 0, 0, 0, "discharge")), CalcInputValue.Quantity("h_out", 0, "m")];

        var heat = Run([.. noTemperature, CalcInputValue.Quantity("Phi_load", 12, "kW"), CalcInputValue.Quantity("dT_load", 20, "K")]);
        Assert.False(heat.IsBlocked);
        Assert.Contains(heat.Notices, n => n.RuleId == "heat-t" && n.Message.StartsWith("T:", StringComparison.Ordinal));

        // Without a heat load the template default applies with the validator's usual notice, as before.
        var flow = Run([.. noTemperature, CalcInputValue.Quantity("Q", 5, "m^3/h")]);
        Assert.False(flow.IsBlocked);
        Assert.DoesNotContain(flow.Notices, n => n.RuleId == "heat-t");
        Assert.Contains(flow.Notices, n => n.RuleId == "input.T");

        var entered = Run(Loop(CalcInputValue.Quantity("Phi_load", 12, "kW"), CalcInputValue.Quantity("dT_load", 20, "K")));
        Assert.DoesNotContain(entered.Notices, n => n.RuleId == "heat-t");
    }

    private static CalcInputValue[] PumpCurve() =>
        [CalcInputValue.VectorOf("Q_pump", [0, 20, 40, 60], "m^3/h"), CalcInputValue.VectorOf("H_pump", [40, 36, 24, 4], "m")];

    [Fact]
    public void Shaft_Torque_Covers_The_Larger_Of_The_Design_Flow_And_Duty_Point_Shaft_Power()
    {
        Gen.Double[8, 58].Sample(qh =>
        {
            var r = Run(Loop([.. PumpCurve(), CalcInputValue.Quantity("Q", qh, "m^3/h"), CalcInputValue.Quantity("n_pump", 1450, "rpm"), CalcInputValue.Of("eta_pump", 0.65)]));
            Assert.False(r.IsBlocked);
            var design = r.OutputSi("P_shaft_design");
            var duty = r.OutputSi("P_shaft");
            var larger = Math.Max(design, duty);
            Assert.Equal(larger, r.OutputSi("P_shaft_sel"), larger * 1e-9);
            Assert.Equal(larger / (1450 * 2 * Math.PI / 60), r.OutputSi("T_shaft"), larger / (1450 * 2 * Math.PI / 60) * 1e-9);
            Assert.Equal(1450, r.OutputSi("n_shaft") * 60 / (2 * Math.PI), 1e-6);
        }, iter: 60);

        // A small design flow against this curve runs far out on it: the duty point governs, and the sheet says so.
        var small = Run(Loop([.. PumpCurve(), CalcInputValue.Quantity("Q", 2, "m^3/h"), CalcInputValue.Quantity("n_pump", 1450, "rpm")]));
        Assert.True(small.OutputSi("P_shaft") > small.OutputSi("P_shaft_design"));
        var large = Run(Loop([.. PumpCurve(), CalcInputValue.Quantity("Q", 40, "m^3/h"), CalcInputValue.Quantity("n_pump", 1450, "rpm")]));
        Assert.True(large.OutputSi("P_shaft_design") > large.OutputSi("P_shaft"));
        Assert.Equal(large.OutputSi("P_shaft_design"), large.OutputSi("P_shaft_sel"), 1e-9);
        var step = Assert.Single(small.Steps, st => st.Symbol == "T_shaft");
        Assert.Contains("larger of the design-flow and duty-point", step.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Pump_Efficiency_Is_Entered_Once_Whether_Or_Not_There_Is_A_Pump_Curve()
    {
        foreach (var extra in new[] { PumpCurve(), [] })
        {
            var r = Run(Loop([.. extra, CalcInputValue.Quantity("Q", 20, "m^3/h"), CalcInputValue.Quantity("n_pump", 1450, "rpm")]));
            Assert.Single(r.Steps, st => st.Symbol == "eta_pump");
        }
    }

    [Fact]
    public void A_Closed_Circuit_Has_No_Static_Lift_And_Reports_The_Water_It_Holds()
    {
        // Black B-A-11 / Green G-B-10: the wet-heating and cooling loops entered a 3 m rise on a closed flow-and-return loop and the pump "lifted" it.
        var segments = PipeFlowCalculator.Segments(("loop", 60, 80, 0.045, 8, 0, 10, "discharge"));
        var open = Run(CalcInputValue.Quantity("Q", 30, "m^3/h"), segments);
        var closed = Run(CalcInputValue.Quantity("Q", 30, "m^3/h"), segments, CalcInputValue.Choice("circuit", PipeFlowCalculator.ClosedCircuit));
        Assert.Equal(10, open.OutputSi("H_static"), 12);
        Assert.Equal(0, closed.OutputSi("H_static"), 12);
        Assert.Equal(open.OutputSi("h_f_total"), closed.OutputSi("h_f_total"), 12);
        Assert.Equal(closed.OutputSi("h_f_total"), closed.OutputSi("H_sys"), 12);
        Assert.Contains(closed.RunAssumptions, a => a.Statement.Contains("closed", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Water_Held_In_The_Pipes_Is_A_Port_For_The_Sealed_System()
    {
        // Green G-B-17: B6's pipework volume was a 20 L default against 24.6 L of pipe.
        Assert.Contains(Calc.Template.OutputPorts!, p => p.Symbol == "V_pipe" && p.Unit == "L");
        var r = Run(CalcInputValue.Quantity("Q", 1, "m^3/h"), PipeFlowCalculator.Segments(("a", 40, 28, 0.045, 0, 0, 0, "discharge"), ("b", 10, 20, 0.045, 0, 0, 0, "discharge")));
        var expected = (Math.PI / 4 * 0.028 * 0.028 * 40 + Math.PI / 4 * 0.020 * 0.020 * 10) * 1e3;
        Assert.Equal(expected, r.OutputSi("V_pipe") * 1e3, 9);
    }

    private static CalcRow ColdRow(string name, double l, double d, double? od) =>
        CalcRow.Of([("name", new CalcCell(Text: name)), ("L", new CalcCell(l, "m")), ("D", new CalcCell(d, "mm")), ("eps", new CalcCell(0.0015, "mm")), ("K", new CalcCell(0)), ("L_eq", new CalcCell(0, "m")), ("dz", new CalcCell(0, "m")), ("side", new CalcCell(Text: "discharge")), .. od is { } o ? [("OD", new CalcCell(o, "mm"))] : Array.Empty<(string, CalcCell)>()]);

    private static CalcInputValue Cold(double? od, double d = 51.6) =>
        CalcInputValue.Table("segments", [ColdRow("chilled water flow", 60, d, od), ColdRow("branch", 10, 30, od is null ? null : 34)]);

    [Fact]
    public void The_Outside_Diameter_Of_The_First_Segment_And_The_Line_Temperature_Are_Ports_For_The_Insulation()
    {
        // 1.4.0: an optional OD column of the segments table; OD_1 is the first segment's (the insulation's inside diameter). 1.5.0: T_line, the design temperature of the insulated line.
        Assert.Contains(Calc.Template.OutputPorts!, p => p is { Symbol: "OD_1", Unit: "mm" });
        Assert.Contains(Calc.Template.OutputPorts!, p => p is { Symbol: "T_line", Unit: "degC" });
        Assert.DoesNotContain(Calc.Template.OutputPorts!, p => p.Symbol == "T_fluid");
        Assert.Contains(Calc.Template.FindInput("segments")!.Columns!, c => c is { Key: "OD", IsOptional: true });
        Assert.True(Calc.Template.FindInput("T_line")!.IsOptional);

        var r = Run(CalcInputValue.Quantity("T", 9, "degC"), CalcInputValue.Quantity("T_line", 6, "degC"), CalcInputValue.Quantity("Q", 12, "m^3/h"), Cold(54));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(0.054, r.OutputSi("OD_1"), 12);

        // The line temperature is the COLDEST temperature entered for the line (the 6 °C flow), not the 9 °C mean the properties are taken at.
        Assert.Equal(6 + 273.15, r.OutputSi("T_line"), 9);
        Assert.Equal("degC", ((Tempest.Calc.Engine.Values.ScalarValue)r.FindOutput("T_line")!.Value).DisplayUnit.Symbol);
        Assert.Equal("mm", ((Tempest.Calc.Engine.Values.ScalarValue)r.FindOutput("OD_1")!.Value).DisplayUnit.Symbol);
        Assert.Equal(Run(CalcInputValue.Quantity("T", 9, "degC"), CalcInputValue.Quantity("Q", 12, "m^3/h"), Cold(54)).OutputSi("dp_total"), r.OutputSi("dp_total"), 12);

        // The OD changes nothing the sheet worked out before.
        var without = Run(CalcInputValue.Quantity("T", 9, "degC"), CalcInputValue.Quantity("T_line", 6, "degC"), CalcInputValue.Quantity("Q", 12, "m^3/h"), Cold(null));
        Assert.Null(without.FindOutput("OD_1"));
        Assert.Equal(without.OutputSi("dp_total"), r.OutputSi("dp_total"), 12);
        Assert.Equal(without.OutputSi("V_pipe"), r.OutputSi("V_pipe"), 12);
    }

    [Fact]
    public void Without_T_Line_The_Port_Is_The_Entered_T_And_With_Neither_It_Is_Not_Offered_So_The_Default_20_C_Never_Travels()
    {
        var onlyT = Run(CalcInputValue.Quantity("T", 9, "degC"), CalcInputValue.Quantity("Q", 12, "m^3/h"), Cold(54));
        Assert.Equal(9 + 273.15, onlyT.OutputSi("T_line"), 9);

        var neither = Run(CalcInputValue.Quantity("Q", 12, "m^3/h"), Cold(54));
        Assert.False(neither.IsBlocked);
        Assert.Null(neither.FindOutput("T_line"));
        Assert.Equal(54e-3, neither.OutputSi("OD_1"), 12);

        // A custom fluid takes its properties from rho and mu, not from T: T_line is still the design temperature of the line.
        var custom = Run(CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom), CalcInputValue.Quantity("rho_custom", 1000, "kg/m^3"), CalcInputValue.Quantity("mu_custom", 1.5, "mPa*s"),
            CalcInputValue.Quantity("T_line", -5, "degC"), CalcInputValue.Quantity("Q", 12, "m^3/h"), Cold(54));
        Assert.False(custom.IsBlocked, string.Join("; ", custom.Blocks.Select(b => b.Message)));
        Assert.Equal(-5 + 273.15, custom.OutputSi("T_line"), 9);
    }

    [Fact]
    public void An_Outside_Diameter_Not_Larger_Than_The_Inner_Diameter_Blocks_Naming_The_Segment()
    {
        var r = Run(CalcInputValue.Quantity("Q", 12, "m^3/h"), Cold(51.6));
        Assert.True(r.IsBlocked);
        var block = Assert.Single(r.Blocks, b => b.Message.StartsWith("segments:", StringComparison.Ordinal));
        Assert.Contains("segment 1", block.Message, StringComparison.Ordinal);
        Assert.Contains("chilled water flow", block.Message, StringComparison.Ordinal);
    }
}
