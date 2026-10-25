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

    /// <summary>The Parker numbers with no operating-temperature spread (T_min = T_max, so the gas-law values are exact), overrides applied in order.</summary>
    private static CalcInputSet With(params CalcInputValue[] overrides) => overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Accumulator.Template).With(CalcInputValue.Quantity("T_min", 50, "degC")), (set, value) => set.With(value));

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

        foreach (var (symbol, value, unit) in new[] { ("n", 1.5, ""), ("n", 0.9, ""), ("dV", 0.0, "L"), ("dV", 2000.0, "L"), ("p_2", 800.0, "bar"), ("p_0", -1.0, "bar"), ("f_rg", 2.0, ""), ("f_rg", 0.95, ""), ("T_min", -50.0, "degC"), ("T_min", 200.0, "degC"), ("PS", -1.0, "bar"), ("V_vessel", 0.0, "L"), ("T_max", 200.0, "degC"), ("r_0", 1.5, ""), ("r_p", 25.0, "") })
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

    private static string Pssr(CalcRunResult r) => Assert.IsType<TextValue>(r.FindOutput("pssr")!.Value).Value;

    [Fact]
    public void The_Bar_Litre_Product_Is_The_Vessels_PS_Times_Its_Volume_And_Reads_Against_250()
    {
        // PS is the vessel's own maximum allowable pressure, not the working pressure: 230 bar gauge on the selected 32 L.
        var big = Run(Sizes(10, 20, 32, 50), Bars("PS", 230));
        Assert.Equal(230 * 32, big.OutputSi("bar_litres") / (Bar * Litre), 9);
        Assert.Contains("at or above 250 bar·L", Pssr(big), StringComparison.Ordinal);

        // A volume entered for the vessel replaces the selected size.
        var entered = Run(Sizes(10, 20, 32, 50), Bars("PS", 230), CalcInputValue.Quantity("V_vessel", 40, "L"));
        Assert.Equal(230 * 40, entered.OutputSi("bar_litres") / (Bar * Litre), 9);

        var small = Run(CalcInputValue.Quantity("dV", 0.5, "L"), Bars("p_0", 40), Bars("p_1", 50), Bars("p_2", 100), Bars("PS", 110), CalcInputValue.Quantity("V_vessel", 2, "L"));
        Assert.Equal(110 * 2, small.OutputSi("bar_litres") / (Bar * Litre), 9);
        Assert.Contains("is below 250 bar·L", Pssr(small), StringComparison.Ordinal);
        Assert.DoesNotContain(small.Checks, c => c.Name.Contains("PSSR", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Vessel_Rated_Above_The_Working_Pressure_Can_Lose_The_Exception_The_Working_Pressure_Would_Allow()
    {
        // p2 = 100 bar gauge with 2.4 L is 240 bar·L at the working pressure; a relief valve at 110 bar makes it 264.
        void Check(CalcRunResult r, double barLitres, string text)
        {
            Assert.Equal(barLitres, r.OutputSi("bar_litres") / (Bar * Litre), 9);
            Assert.Contains(text, Pssr(r), StringComparison.Ordinal);
        }

        var gauge = new[] { CalcInputValue.Quantity("dV", 0.5, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge), Bars("p_0", 40), Bars("p_1", 50), Bars("p_2", 100), CalcInputValue.Quantity("V_vessel", 2.4, "L") };
        Check(Run([.. gauge, Bars("PS", 100)]), 240, "is below 250 bar·L");
        Check(Run([.. gauge, Bars("PS", 110)]), 264, "at or above 250 bar·L");
    }

    [Fact]
    public void Without_PS_Or_Without_A_Volume_The_Indication_Is_Not_Available_And_Never_Reads_Below_250()
    {
        const string unavailable = "indication not available — enter the vessel's PS and volume";

        foreach (var r in new[] { Run(), Run(Sizes(10, 20, 32, 50)), Run(Bars("PS", 230)), Run(CalcInputValue.Quantity("V_vessel", 40, "L")), Run(Sizes(10, 20)) })
        {
            Assert.StartsWith(unavailable, Pssr(r), StringComparison.Ordinal);
            Assert.DoesNotContain("below 250", Pssr(r), StringComparison.Ordinal);
            Assert.Null(r.FindOutput("bar_litres"));
        }
    }

    [Fact]
    public void PS_Below_The_Working_Pressure_Or_A_Vessel_Smaller_Than_The_Gas_Volume_Blocks_And_Names_The_Input()
    {
        var lowPs = Run(Bars("PS", 150));
        Assert.True(lowPs.IsBlocked);
        Assert.StartsWith("PS:", lowPs.Blocks[0].Message, StringComparison.Ordinal);

        // p2 = 210 bar absolute is 208.98675 bar gauge: PS at exactly that is accepted, a hair below is not.
        Assert.False(Run(Bars("PS", 210 - 1.01325), CalcInputValue.Quantity("V_vessel", 40, "L")).IsBlocked);
        Assert.True(Run(Bars("PS", 208), CalcInputValue.Quantity("V_vessel", 40, "L")).IsBlocked);

        var small = Run(Bars("PS", 230), CalcInputValue.Quantity("V_vessel", 20, "L"));
        Assert.True(small.IsBlocked);
        Assert.StartsWith("V_vessel:", small.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Gas_Volume_Is_Scaled_By_The_Temperature_Range_And_The_Ratio_Rule_Uses_The_Cold_Pre_Charge()
    {
        var flat = Run();
        var ranged = Run(CalcInputValue.Quantity("T_min", 20, "degC"));
        var factor = 323.15 / 293.15;
        Assert.Equal(factor, ranged.OutputSi("V0") / flat.OutputSi("V0"), 12);
        Assert.Equal(1.0, flat.OutputSi("f_T"), 12);
        Assert.Equal(factor, ranged.OutputSi("f_T"), 12);

        // p0 at T_min is the pre-charge over the same factor; the ratio utilisation follows it, the p0/p1 rule stays at T_max.
        Assert.Equal(90 * Bar / factor, ranged.OutputSi("p0_min"), 3);
        Assert.Equal(210 / (90 / factor) / 4, ranged.OutputSi("u_rp"), 12);
        Assert.True(ranged.OutputSi("u_rp") > flat.OutputSi("u_rp"));
        Assert.Equal(flat.OutputSi("u_r0"), ranged.OutputSi("u_r0"), 12);

        // A cold limit that pushes p2/p0 over the rule fails the ratio check although the warm pre-charge passes it.
        var cold = Run(Bars("p_0", 50), Bars("p_2", 190), CalcInputValue.Quantity("T_min", -40, "degC"));
        Assert.Contains(cold.Checks, c => c.Name.StartsWith("Pressure ratio within", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(Run(Bars("p_0", 50), Bars("p_2", 190)).Checks, c => c.Name.StartsWith("Pressure ratio within", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void The_Sized_Gas_Still_Delivers_The_Liquid_Volume_At_The_Minimum_Temperature()
    {
        // Property (isothermal): the pre-charge falls to p0·T_min/T_max when cold; V0 scaled by T_max/T_min then delivers exactly ΔV between p1 and p2.
        Gen.Select(Gen.Double[0.5, 100], Gen.Double[20, 300], Gen.Double[0.3, 0.9], Gen.Double[1.1, 3.5], Gen.Double[-40, 50], Gen.Double[50, 150]).Sample((dv, p1, f0, f2, tMin, tMax) =>
        {
            var (p0, p2) = (p1 * f0, p1 * f2);

            if (p2 > 700)
                return true;

            var r = Run(CalcInputValue.Quantity("dV", dv, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", p0), Bars("p_1", p1), Bars("p_2", p2), CalcInputValue.Quantity("T_min", tMin, "degC"), CalcInputValue.Quantity("T_max", tMax, "degC"));
            var v0 = r.OutputSi("V0") / Litre;
            var cold = p0 * (tMin + 273.15) / (tMax + 273.15);
            var delivered = v0 * (cold / p1 - cold / p2);
            return Math.Abs(delivered - dv) <= 1e-9 * dv && Math.Abs(r.OutputSi("p0_min") / Bar - cold) <= 1e-9 * cold;
        }, iter: 80);
    }

    [Fact]
    public void A_Minimum_Temperature_Above_The_Maximum_Blocks()
    {
        var r = Run(CalcInputValue.Quantity("T_min", 60, "degC"));
        Assert.True(r.IsBlocked);
        Assert.StartsWith("T_min:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Rules_Record_Says_The_Ratios_Are_Applied_To_Absolute_Pressures()
    {
        var kind = Accumulator.Template.RecordKinds!.Single(k => k.Kind == "accumulator.rules");
        Assert.Contains("ABSOLUTE", kind.Guidance, StringComparison.Ordinal);
        Assert.Contains("gauge or absolute", kind.Guidance, StringComparison.Ordinal);

        // The same Parker numbers read as gauge fail the p0 <= 0.9 p1 rule by 0.11 %; read as absolute they sit exactly on it.
        Assert.Equal(1.0, Run().OutputSi("u_r0"), 9);
        Assert.Equal(1.00111454, Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge)).OutputSi("u_r0"), 8);
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

        // Nitrogen's compressibility factor exceeds 1 at high pressure: a factor below 1 (a smaller accumulator) is refused.
        Assert.Equal(1.0, Accumulator.Template.Inputs.Single(i => i.Symbol == "f_rg").HardMin);
        Assert.Contains(CalcInputValidator.Validate(Accumulator.Template, With(CalcInputValue.Of("f_rg", 0.95))), n => n.Severity == CalcNoticeSeverity.Block);
        Assert.DoesNotContain(CalcInputValidator.Validate(Accumulator.Template, With(CalcInputValue.Of("f_rg", 1.0))), n => n.Severity == CalcNoticeSeverity.Block);
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
        // bar_litres exists once the vessel's PS and a volume are given; V0 and p0 always.
        var r = Run(Bars("PS", 230), CalcInputValue.Quantity("V_vessel", 40, "L"));
        Assert.Equal(["V0", "p0", "bar_litres"], Accumulator.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.All(Accumulator.Template.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));
        Assert.NotNull(Run().FindOutput("V0"));
        Assert.NotNull(Run().FindOutput("p0"));
        Assert.DoesNotContain(r.Checks, c => c.Name.Contains("PD 5500", StringComparison.Ordinal) || c.Name.Contains("EN 13445", StringComparison.Ordinal) || c.Name.Contains("ASME", StringComparison.Ordinal));
        Assert.Contains("Not a pressure-vessel code check", Accumulator.Template.Scope, StringComparison.Ordinal);
    }
}
