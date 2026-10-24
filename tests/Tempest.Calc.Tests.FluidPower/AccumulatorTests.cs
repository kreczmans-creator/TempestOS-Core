using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.FluidPower;

/// <summary>F2 — accumulator sizing through the shared gas-law kernel: behaviour beyond the known-answer cases.</summary>
public sealed class AccumulatorTests
{
    private static readonly AccumulatorCalculator Accumulator = new();

    private const double Bar = 1e5;
    private const double Litre = 1e-3;

    /// <summary>The Parker numbers read as absolute bar, the first known-answer case, with overrides applied in order.</summary>
    private static CalcInputSet With(params CalcInputValue[] overrides) => overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Accumulator.Template), (set, value) => set.With(value));

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Accumulator, With(overrides));

    private static CalcInputValue Bars(string symbol, double value) => CalcInputValue.Quantity(symbol, value, "bar");

    private static CalcInputValue Sizes(params double[] litres) => CalcInputValue.Table("sizes", litres.Select(v => CalcRow.Of(("V", new CalcCell(v, "L")))).ToList());

    private static double V0(double dv, double p0, double p1, double p2, double n) => dv / (Math.Pow(p0 / p1, 1 / n) - Math.Pow(p0 / p2, 1 / n));

    [Fact]
    public void The_Parker_Numbers_Give_29_7_Litres_As_Absolute_And_29_8_As_Gauge()
    {
        Assert.Equal(14 / (0.9 - 90.0 / 210), Run().OutputSi("V0") / Litre, 9);
        var gauge = Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge));
        Assert.Equal(V0(14, 91.01325, 101.01325, 211.01325, 1), gauge.OutputSi("V0") / Litre, 9);
    }

    [Fact]
    public void A_Fast_Discharge_Needs_A_Larger_Volume_Than_A_Slow_One()
    {
        var slow = Run(CalcInputValue.Of("n", 1)).OutputSi("V0");
        var fast = Run(CalcInputValue.Of("n", 1.4)).OutputSi("V0");
        Assert.True(fast > slow);
        Assert.Equal(V0(14, 90, 100, 210, 1.4), fast / Litre, 9);
    }

    [Fact]
    public void The_Gas_Volume_Delivers_Exactly_The_Requested_Liquid_Volume()
    {
        // Property: the gas expands from V0·(p0/p2)^(1/n) at p2 to V0·(p0/p1)^(1/n) at p1; the difference is ΔV, for any valid pressures and exponent.
        Gen.Select(Gen.Double[0.5, 100], Gen.Double[20, 300], Gen.Double[0.3, 0.9], Gen.Double[1.1, 3.5], Gen.Double[1, 1.4]).Sample((dv, p1, f0, f2, n) =>
        {
            var (p0, p2) = (p1 * f0, p1 * f2);

            if (p2 > 700)
                return true;

            var r = Run(CalcInputValue.Quantity("dV", dv, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", p0), Bars("p_1", p1), Bars("p_2", p2), CalcInputValue.Of("n", n));
            var v0 = r.OutputSi("V0") / Litre;
            var delivered = v0 * Math.Pow(p0 / p1, 1 / n) - v0 * Math.Pow(p0 / p2, 1 / n);
            return Math.Abs(delivered - dv) <= 1e-9 * dv;
        }, iter: 80);
    }

    [Fact]
    public void The_Pre_Charge_Follows_The_Absolute_Temperature_And_Is_Set_As_Gauge_When_Entered_As_Gauge()
    {
        var same = Run(CalcInputValue.Quantity("T_max", 50, "degC"), CalcInputValue.Quantity("T_c", 50, "degC"));
        Assert.Equal(90 * Bar, same.OutputSi("p0"), 3);

        var cold = Run(CalcInputValue.Quantity("T_max", 80, "degC"), CalcInputValue.Quantity("T_c", 20, "degC"));
        Assert.Equal(90 * 293.15 / 353.15 * Bar, cold.OutputSi("p0"), 3);

        var gauge = Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge), CalcInputValue.Quantity("T_max", 80, "degC"), CalcInputValue.Quantity("T_c", 20, "degC"));
        Assert.Equal(gauge.OutputSi("p0") - 1.01325 * Bar, gauge.OutputSi("p0_set"), 3);
    }

    [Fact]
    public void The_Pre_Charge_Rules_Are_Records_And_Are_Checked_Pass_Or_Fail()
    {
        Assert.True(Run().Passed);

        var high = Run(Bars("p_0", 95));
        Assert.Contains(high.Checks, c => c.Name.StartsWith("Pre-charge within", StringComparison.Ordinal) && !c.Passed);

        var steep = Run(Bars("p_0", 40), Bars("p_2", 200));
        Assert.Contains(steep.Checks, c => c.Name.StartsWith("Pressure ratio within", StringComparison.Ordinal) && !c.Passed);

        // The limits are inputs a record fills: a manufacturer allowing p0 up to p1 and a ratio of 6 passes both.
        Assert.True(Run(Bars("p_0", 95), CalcInputValue.Of("r_0", 1)).Passed);
        Assert.True(Run(Bars("p_0", 40), Bars("p_2", 200), CalcInputValue.Of("r_p", 6)).Passed);
        Assert.Contains(Accumulator.Template.RecordKinds!, k => k.Kind == "accumulator.rules" && k.Symbols.Contains("r_0") && k.Symbols.Contains("r_p"));
    }

    [Fact]
    public void Pressures_Out_Of_Order_Block_And_Name_The_Input()
    {
        var above = Run(Bars("p_0", 105));
        Assert.True(above.IsBlocked);
        Assert.StartsWith("p_0:", above.Blocks[0].Message, StringComparison.Ordinal);

        var inverted = Run(Bars("p_2", 90));
        Assert.True(inverted.IsBlocked);
        Assert.StartsWith("p_2:", inverted.Blocks[0].Message, StringComparison.Ordinal);

        var zero = Run(Bars("p_0", 0));
        Assert.True(zero.IsBlocked);
        Assert.StartsWith("p_0:", zero.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_Input_Outside_Its_Valid_Range_Is_A_Hard_Fail()
    {
        Assert.All(Accumulator.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number), spec =>
        {
            Assert.Equal(spec.Min, spec.HardMin);
            Assert.Equal(spec.Max, spec.HardMax);
        });

        foreach (var (symbol, value, unit) in new[] { ("n", 1.5, ""), ("n", 0.9, ""), ("dV", 0.0, "L"), ("dV", 2000.0, "L"), ("p_2", 800.0, "bar"), ("p_0", -1.0, "bar"), ("f_rg", 2.0, ""), ("T_max", 200.0, "degC"), ("r_0", 1.5, ""), ("r_p", 25.0, "") })
        {
            var value1 = unit.Length > 0 ? CalcInputValue.Quantity(symbol, value, unit) : CalcInputValue.Of(symbol, value);
            Assert.Contains(CalcInputValidator.Validate(Accumulator.Template, With(value1)), n => n.Severity == CalcNoticeSeverity.Block);
        }
    }

    [Fact]
    public void The_Next_Size_Is_The_Smallest_Listed_Size_Not_Below_The_Requirement()
    {
        Assert.Equal(32, Run(Sizes(50, 10, 32, 20)).OutputSi("V_sel") / Litre, 9);
        Assert.Equal(29.696969696969695 / 32, Run(Sizes(10, 20, 32, 50)).OutputSi("u_V"), 9);

        var exact = Run(Sizes(10, 29.696969696969695, 50));
        Assert.Equal(29.696969696969695, exact.OutputSi("V_sel") / Litre, 9);

        Assert.Null(Run().FindOutput("V_sel"));
    }

    [Fact]
    public void A_Size_List_That_Is_Too_Small_Warns_And_Fails_And_Selects_Nothing()
    {
        var r = Run(Sizes(10, 20));
        Assert.Contains(r.Warnings, w => w.RuleId == "size");
        Assert.Null(r.FindOutput("V_sel"));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("A listed size covers", StringComparison.Ordinal) && !c.Passed);
        Assert.False(r.Passed);
    }

    [Fact]
    public void The_Bar_Litre_Product_Uses_The_Gauge_Pressure_And_The_Selected_Size_And_Reads_Against_250()
    {
        var big = Run(Sizes(10, 20, 32, 50));
        Assert.Equal((210 - 1.01325) * 32, big.OutputSi("bar_litres") / (Bar * Litre), 9);
        Assert.Contains("at or above 250 bar·L", Assert.IsType<TextValue>(big.FindOutput("pssr")!.Value).Value, StringComparison.Ordinal);

        var small = Run(CalcInputValue.Quantity("dV", 0.5, "L"), Bars("p_0", 40), Bars("p_1", 50), Bars("p_2", 100));
        Assert.Equal((100 - 1.01325) * 1.25, small.OutputSi("bar_litres") / (Bar * Litre), 9);
        Assert.Contains("is below 250 bar·L", Assert.IsType<TextValue>(small.FindOutput("pssr")!.Value).Value, StringComparison.Ordinal);
        Assert.DoesNotContain(small.Checks, c => c.Name.Contains("PSSR", StringComparison.Ordinal));
    }

    [Fact]
    public void Above_200_Bar_The_Sheet_Warns_And_The_Real_Gas_Factor_Scales_The_Volume()
    {
        Assert.Contains(Run().Warnings, w => w.RuleId == "realgas");
        Assert.DoesNotContain(Run(Bars("p_0", 60), Bars("p_1", 80), Bars("p_2", 150)).Warnings, w => w.RuleId == "realgas");

        var ideal = Run().OutputSi("V0");
        var real = Run(CalcInputValue.Of("f_rg", 1.08));
        Assert.Equal(1.08, real.OutputSi("V0") / ideal, 12);
        Assert.Contains(real.Warnings, w => w.RuleId == "realgas" && w.Message.Contains("your real-gas factor", StringComparison.Ordinal));
    }

    [Fact]
    public void Shock_And_Emergency_Duties_Are_Marked_And_Warn_On_A_Slow_Exponent()
    {
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId is "shock" or "exponent");

        var shock = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeShock));
        Assert.Contains(shock.Warnings, w => w.RuleId == "shock");
        Assert.Contains(shock.Warnings, w => w.RuleId == "exponent");
        Assert.Contains(shock.RunAssumptions, a => a.Statement.Contains("screening", StringComparison.Ordinal));

        var emergency = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Of("n", 1.4));
        Assert.DoesNotContain(emergency.Warnings, w => w.RuleId == "exponent");
        Assert.Contains(emergency.RunAssumptions, a => a.Statement.Contains("Emergency stroke", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Ports_Are_Outputs_And_No_Pressure_Vessel_Code_Check_Is_Made()
    {
        var r = Run();
        Assert.Equal(["V0", "p0", "bar_litres"], Accumulator.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.All(Accumulator.Template.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));
        Assert.DoesNotContain(r.Checks, c => c.Name.Contains("PD 5500", StringComparison.Ordinal) || c.Name.Contains("EN 13445", StringComparison.Ordinal) || c.Name.Contains("ASME", StringComparison.Ordinal));
        Assert.Contains("Not a pressure-vessel code check", Accumulator.Template.Scope, StringComparison.Ordinal);
    }
}
