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
}
