using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

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
    public void A_Linked_Pressure_Warns_When_The_Pressures_Are_Entered_As_Absolute()
    {
        // L-g: the cylinder's supply pressure p (gauge) linked to p_2 is read on p_basis; Absolute would take it one atmosphere low.
        var link = CalcProvenance.Link(Guid.NewGuid(), "p", "A");
        var linked = CalcInputValue.Quantity("p_2", 160, "bar", link);

        var absolute = Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), linked);
        var warning = Assert.Single(absolute.Warnings, w => w.RuleId == "link-basis");
        Assert.StartsWith("p_2:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("bar low", warning.Message, StringComparison.Ordinal);
        Assert.Contains("p_basis", warning.Message, StringComparison.Ordinal);

        // Gauge (the default the pack uses), or a typed value, is consistent and silent; so is a link to another pressure only when it is on the gauge basis.
        Assert.DoesNotContain(Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge), linked).Warnings, w => w.RuleId == "link-basis");
        Assert.DoesNotContain(Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_2", 160)).Warnings, w => w.RuleId == "link-basis");

        // Every pressure input is covered.
        var all = Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), CalcInputValue.Quantity("p_0", 90, "bar", link), CalcInputValue.Quantity("p_1", 100, "bar", link), linked);
        Assert.Equal(3, all.Warnings.Count(w => w.RuleId == "link-basis"));
        Assert.Contains(Accumulator.Template.ApplicabilityRules, r => r.Id == "link-basis");
        Assert.Equal("1.5.0", Accumulator.Template.Version);
    }

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

            // Above the block pressure the ideal gas is refused (fp.accumulator-01), so a real-gas factor is entered there and divided out again.
            var frg = p2 > AccumulatorCalculator.RealGasBlockBar ? 1.5 : 1.0;
            var r = Run(CalcInputValue.Quantity("dV", dv, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", p0), Bars("p_1", p1), Bars("p_2", p2), CalcInputValue.Of("n", n), CalcInputValue.Of("f_rg", frg));
            var v0 = r.OutputSi("V0") / Litre / frg;
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

        foreach (var (symbol, value, unit) in new[] { ("n", 1.5, ""), ("n", 0.9, ""), ("dV", 0.0, "L"), ("dV", 2000.0, "L"), ("p_2", 800.0, "bar"), ("p_0", -1.0, "bar"), ("f_rg", 3.1, ""), ("f_rg", 0.95, ""), ("T_min", -50.0, "degC"), ("T_min", 200.0, "degC"), ("PS", -1.0, "bar"), ("V_vessel", 0.0, "L"), ("T_max", 200.0, "degC"), ("r_0", 1.5, ""), ("r_p", 25.0, "") })
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

            var frg = p2 > AccumulatorCalculator.RealGasBlockBar ? 1.5 : 1.0;
            var r = Run(CalcInputValue.Quantity("dV", dv, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", p0), Bars("p_1", p1), Bars("p_2", p2), CalcInputValue.Quantity("T_min", tMin, "degC"), CalcInputValue.Quantity("T_max", tMax, "degC"), CalcInputValue.Of("f_rg", frg));
            var v0 = r.OutputSi("V0") / Litre / frg;
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
        Assert.DoesNotContain(Run(Bars("p_0", 40), Bars("p_1", 50), Bars("p_2", 80)).Warnings, w => w.RuleId == "realgas");

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
    public void The_Real_Gas_Factor_Takes_The_Value_A_High_Pressure_Window_Needs_Not_Only_Up_To_One_And_A_Half()
    {
        // fp.accumulator-01: reference-EOS nitrogen at 20 °C needs 2.007 times the ideal volume for a 600/650/700 bar abs window; the factor was capped at 1.5, so no valid input gave a safe V0.
        var spec = Accumulator.Template.Inputs.Single(i => i.Symbol == "f_rg");
        Assert.True(spec.HardMax >= 2.007, $"the f_rg limit {spec.HardMax} must admit the 2.007 a 600 to 700 bar window needs");
        var window = new[] { CalcInputValue.Quantity("dV", 5, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 600), Bars("p_1", 650), Bars("p_2", 700) };

        var ideal = Run(window);
        Assert.True(ideal.IsBlocked, "f_rg left at 1 above 300 bar absolute is refused (fp.accumulator-01 remainder)");
        var real = Run([.. window, CalcInputValue.Of("f_rg", 2.007)]);
        Assert.DoesNotContain(CalcInputValidator.Validate(Accumulator.Template, With([.. window, CalcInputValue.Of("f_rg", 2.007)])), n => n.Severity == CalcNoticeSeverity.Block);
        Assert.False(real.IsBlocked);
        Assert.Equal(5 / (600.0 / 650 - 600.0 / 700) * 2.007, real.OutputSi("V0") / Litre, 9);

        // The ideal volume above 300 bar is refused, and the message points at the factor.
        Assert.Contains(ideal.Blocks, b => b.RuleId == "realgas-ideal" && b.Message.Contains("f_rg", StringComparison.Ordinal));
        Assert.Contains(real.Warnings, w => w.RuleId == "realgas" && w.Message.Contains("your real-gas factor", StringComparison.Ordinal));
        Assert.Contains(CalcInputValidator.Validate(Accumulator.Template, With(CalcInputValue.Of("f_rg", 3.1))), n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void An_Ideal_Gas_Volume_Above_300_Bar_Absolute_Is_Refused_Until_The_Real_Gas_Factor_Is_Entered()
    {
        // fp.accumulator-01 remainder: at 450/500/700 bar abs reference-EOS nitrogen needs 1.7457 times the ideal volume (CoolProp, isothermal 20 degC), so f_rg left at 1 sized
        // an accumulator about half as big as the duty needs, under a warning only. 10 L between 450 and 500 bar abs, pre-charge 450: ideal V0 = 10/(450/500 - 450/700) = 38.889 L, real 38.889 x 1.7457 = 67.89 L (python3).
        var window = new[] { CalcInputValue.Quantity("dV", 10, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 450), Bars("p_1", 500), Bars("p_2", 700) };
        var ideal = Run(window);
        var block = Assert.Single(ideal.Blocks);
        Assert.Equal("realgas-ideal", block.RuleId);
        Assert.StartsWith("f_rg:", block.Message, StringComparison.Ordinal);
        Assert.Contains("700 bar absolute", block.Message, StringComparison.Ordinal);
        Assert.Contains("Enter the real-gas factor", block.Message, StringComparison.Ordinal);
        Assert.Null(ideal.FindOutput("V0"));
        Assert.Contains(Accumulator.Template.ApplicabilityRules, r => r.Id == "realgas-ideal" && r.Severity == CalcNoticeSeverity.Block);

        var real = Run([.. window, CalcInputValue.Of("f_rg", 1.7457)]);
        Assert.False(real.IsBlocked);
        Assert.Equal(10 / (450.0 / 500 - 450.0 / 700) * 1.7457, real.OutputSi("V0") / Litre, 9);
    }

    [Fact]
    public void The_Block_Starts_Above_300_Bar_Absolute_And_Between_100_And_300_The_Ideal_Gas_Is_Only_Warned()
    {
        // 300 bar abs exactly is not above the threshold; 300.5 is. The Parker reference case (90/100/210 bar abs, f_rg 1; real factor 1.0535, CoolProp) stays a warning.
        Assert.False(Run(CalcInputValue.Quantity("dV", 10, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 250), Bars("p_1", 270), Bars("p_2", 300)).IsBlocked);
        Assert.True(Run(CalcInputValue.Quantity("dV", 10, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 250), Bars("p_1", 270), Bars("p_2", 300.5)).IsBlocked);
        Assert.False(Run().IsBlocked);
        Assert.Contains(Run().Warnings, w => w.RuleId == "realgas");

        // The block is on absolute pressure: 300 bar gauge is 301.01 abs and blocks, 298 bar gauge does not.
        Assert.True(Run(CalcInputValue.Quantity("dV", 10, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge), Bars("p_0", 250), Bars("p_1", 270), Bars("p_2", 300)).IsBlocked);
        Assert.False(Run(CalcInputValue.Quantity("dV", 10, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge), Bars("p_0", 250), Bars("p_1", 270), Bars("p_2", 298)).IsBlocked);
    }

    [Fact]
    public void A_Narrow_Window_Between_100_And_200_Bar_Warns_That_The_Ideal_Gas_Volume_Is_Too_Small()
    {
        // fp.accumulator-02: 10 L between 170/190/200 bar abs gave 223.53 L with no real-gas notice (the warning started above 200 bar abs); the reference EOS needs 1.134 times that.
        var narrow = Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 170), Bars("p_1", 190), Bars("p_2", 200), CalcInputValue.Quantity("dV", 10, "L"));
        Assert.Equal(10 / (170.0 / 190 - 170.0 / 200), narrow.OutputSi("V0") / Litre, 9);
        Assert.Equal(223.529, narrow.OutputSi("V0") / Litre, 3);
        Assert.Contains(narrow.Warnings, w => w.RuleId == "realgas" && w.Message.Contains("real-gas factor is left at 1", StringComparison.Ordinal));
        var window = Assert.Single(narrow.Warnings, w => w.RuleId == "realgas-window");
        Assert.Contains("larger than the compressibility factor", window.Message, StringComparison.Ordinal);

        // A factor entered silences the left-at-1 notice (the window notice stays: the factor must be the bracket ratio, not Z at p_2) and below 100 bar nothing is said.
        var entered = Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 170), Bars("p_1", 190), Bars("p_2", 200), CalcInputValue.Quantity("dV", 10, "L"), CalcInputValue.Of("f_rg", 1.134));
        Assert.DoesNotContain(entered.Warnings, w => w.RuleId == "realgas");
        Assert.Contains(entered.Warnings, w => w.RuleId == "realgas-window");
        Assert.Equal(1.134, entered.OutputSi("V0") / narrow.OutputSi("V0"), 12);
        var low = Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 50), Bars("p_1", 60), Bars("p_2", 90));
        Assert.DoesNotContain(low.Warnings, w => w.RuleId is "realgas" or "realgas-window");
        Assert.Contains(Accumulator.Template.ApplicabilityRules, r => r.Id == "realgas-window");
    }

    [Fact]
    public void An_Exponent_Above_One_Warns_From_50_Bar_That_Real_Nitrogen_Is_Stiffer_Than_The_Ideal_Gas()
    {
        // fp.accumulator-R2: 10 L, 90/100/200 bar abs, n 1.4 gave 27.61 L and no notice; real nitrogen (effective exponent about 1.5 to 2.1) needs 13 to 24 % more.
        var adiabatic = Run(CalcInputValue.Quantity("dV", 10, "L"), CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSame), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 90), Bars("p_1", 100), Bars("p_2", 200), CalcInputValue.Of("n", 1.4));
        Assert.Equal(V0(10, 90, 100, 200, 1.4), adiabatic.OutputSi("V0") / Litre, 9);
        Assert.Equal(27.61, adiabatic.OutputSi("V0") / Litre, 2);
        var warning = Assert.Single(adiabatic.Warnings, w => w.RuleId == "exponent-real");
        Assert.Contains("stiffer", warning.Message, StringComparison.Ordinal);
        Assert.Contains("real-gas factor", warning.Message, StringComparison.Ordinal);

        // With your factor entered the warning says it is applied on top; an isothermal discharge, or a low pressure, is not warned.
        var withFactor = Run(CalcInputValue.Quantity("dV", 10, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 90), Bars("p_1", 100), Bars("p_2", 200), CalcInputValue.Of("n", 1.4), CalcInputValue.Of("f_rg", 1.144));
        Assert.Contains("applied on top", Assert.Single(withFactor.Warnings, w => w.RuleId == "exponent-real").Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 90), Bars("p_1", 100), Bars("p_2", 200), CalcInputValue.Of("n", 1)).Warnings, w => w.RuleId == "exponent-real");
        Assert.DoesNotContain(Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 20), Bars("p_1", 25), Bars("p_2", 45), CalcInputValue.Of("n", 1.4)).Warnings, w => w.RuleId == "exponent-real");
    }

    [Fact]
    public void An_Emergency_Stroke_Fails_When_The_Lowest_Pressure_Is_Below_What_The_Push_Load_Needs()
    {
        // chain.hydraulic-power-unit-02: nothing tied p_1 to the push load; a 100 kN push needs 134.0 bar at the 100 mm bore (100 kN/(78.54 cm² × 0.95)) and p_1 stayed 120 bar with every check green.
        var emergency = new[] { CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge), CalcInputValue.Quantity("dV", 3.927, "L"), Bars("p_0", 105), Bars("p_1", 120), Bars("p_2", 160), CalcInputValue.Of("n", 1.4), CalcInputValue.Quantity("T_min", 10, "degC") };
        var needed = 100e3 / (Math.PI * 0.1 * 0.1 / 4 * 0.95) / Bar;
        Assert.Equal(134.0, needed, 1);

        var weak = Run([.. emergency, Bars("p_load", needed)]);
        Assert.Equal(needed / 120, weak.OutputSi("u_load"), 9);
        Assert.Contains(weak.Checks, c => c.Name.StartsWith("Lowest working pressure p_1 carries the load", StringComparison.Ordinal) && !c.Passed);
        Assert.False(weak.Passed);

        var enough = Run([.. emergency, Bars("p_load", 107.2)]);
        Assert.Contains(enough.Checks, c => c.Name.StartsWith("Lowest working pressure p_1 carries the load", StringComparison.Ordinal) && c.Passed);

        // Absolute basis: p_load is a gauge pressure, p_1 is compared on the same gauge basis (p_1 abs − p_atm).
        var absolute = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 106.01325), Bars("p_1", 121.01325), Bars("p_2", 161.01325), CalcInputValue.Of("n", 1.4), Bars("p_load", 120));
        Assert.Equal(1.0, absolute.OutputSi("u_load"), 9);

        // No load pressure entered: an emergency stroke says so; other duties stay silent.
        Assert.Contains(Run(emergency).Warnings, w => w.RuleId == "load-pressure");
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "load-pressure");
    }

    private static double Held(double dv, double p0, double p1, double p2, double n) => dv * (p2 / p0) / (Math.Pow(p2 / p1, 1 / n) - 1);

    [Fact]
    public void An_Emergency_Stroke_Is_Charged_Slowly_And_Discharged_At_N_By_Default()
    {
        // python3: 14 × (210/90) / ((210/100)^(1/1.4) − 1) = 46.743039671582224 L, against 36.69278925358671 L with one exponent both ways.
        var emergency = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Of("n", 1.4));
        Assert.Equal(46.743039671582224, emergency.OutputSi("V0") / Litre, 9);
        Assert.Equal(Held(14, 90, 100, 210, 1.4), emergency.OutputSi("V0") / Litre, 9);
        Assert.DoesNotContain(emergency.Warnings, w => w.RuleId == "process");
        Assert.Contains("charged slowly to p_2", Assert.IsType<TextValue>(emergency.FindOutput("process")!.Value).Value, StringComparison.Ordinal);

        // The gas held at p2 is V0·p0/p2 and expands at n to V1 = V2 (p2/p1)^(1/n): the stroke is V1 − V2 = 14 L.
        var held = emergency.OutputSi("V0") * 90 / 210;
        Assert.Equal(14, (held * Math.Pow(210.0 / 100, 1 / 1.4) - held) / Litre, 9);

        // The pre-charge rules do not depend on the process.
        Assert.Equal(1.0, emergency.OutputSi("u_r0"), 9);
        Assert.Equal(210.0 / 90 / 4, emergency.OutputSi("u_rp"), 9);
    }

    [Fact]
    public void The_Emergency_Stroke_Default_Can_Be_Set_To_One_Exponent_Both_Ways_And_Warns()
    {
        var same = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSame), CalcInputValue.Of("n", 1.4));
        Assert.Equal(36.69278925358671, same.OutputSi("V0") / Litre, 9);
        var warning = Assert.Single(same.Warnings, w => w.RuleId == "process");
        Assert.Contains("held at p_2", warning.Message, StringComparison.Ordinal);
        Assert.Contains(Accumulator.Template.ApplicabilityRules, r => r.Id == "process");

        // The explicit slow charge is the same as the emergency default.
        var explicitSlow = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSlowCharge), CalcInputValue.Of("n", 1.4));
        var byDuty = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Of("n", 1.4));
        Assert.Equal(byDuty.OutputSi("V0"), explicitSlow.OutputSi("V0"), 15);
    }

    [Fact]
    public void Energy_Storage_Keeps_One_Exponent_By_Default_And_Offers_The_Slow_Charge_As_A_Choice()
    {
        // The existing behaviour: the energy-storage default is the one-exponent relation, unchanged.
        Assert.Equal(V0(14, 90, 100, 210, 1.4), Run(CalcInputValue.Of("n", 1.4)).OutputSi("V0") / Litre, 9);
        Assert.Equal(V0(14, 90, 100, 210, 1.4), Run(CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSame), CalcInputValue.Of("n", 1.4)).OutputSi("V0") / Litre, 9);
        Assert.Equal(AccumulatorCalculator.ProcessByDuty, Accumulator.Template.Inputs.Single(i => i.Symbol == "process").DefaultChoice);
        Assert.Equal([AccumulatorCalculator.ProcessByDuty, AccumulatorCalculator.ProcessSame, AccumulatorCalculator.ProcessSlowCharge], Accumulator.Template.Inputs.Single(i => i.Symbol == "process").Choices);

        // A held energy-storage accumulator, charged slowly and discharged fast.
        var slow = Run(CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSlowCharge), CalcInputValue.Of("n", 1.4));
        Assert.Equal(46.743039671582224, slow.OutputSi("V0") / Litre, 9);
        Assert.DoesNotContain(slow.Warnings, w => w.RuleId == "process");

        // Isothermal discharge: both processes are the same relation.
        Assert.Equal(14 / (0.9 - 90.0 / 210), Run(CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSlowCharge), CalcInputValue.Of("n", 1)).OutputSi("V0") / Litre, 9);
    }

    [Fact]
    public void The_Shock_Duty_Uses_One_Exponent_Whatever_Process_Is_Chosen()
    {
        var shock = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeShock), CalcInputValue.Of("n", 1.4));
        var chosen = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeShock), CalcInputValue.Choice("process", AccumulatorCalculator.ProcessSlowCharge), CalcInputValue.Of("n", 1.4));
        Assert.Equal(V0(14, 90, 100, 210, 1.4), shock.OutputSi("V0") / Litre, 9);
        Assert.Equal(shock.OutputSi("V0"), chosen.OutputSi("V0"), 15);
        Assert.DoesNotContain(shock.Warnings, w => w.RuleId == "process");
        Assert.Contains(chosen.Warnings, w => w.RuleId == "process" && w.Message.Contains("not applied", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Held_Volume_Carries_The_Temperature_And_Real_Gas_Factors()
    {
        // python3: hydraulic power unit F2 inputs (105/120/160 bar gauge, ΔV 3.92699 L, n 1.4, 10 to 50 °C): 30.080873024534817 L (26.695181275084906 L with one exponent).
        var pa = 1.01325;
        var f = (50 + 273.15) / (10 + 273.15);
        var r = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge), CalcInputValue.Quantity("dV", 3.926990816987242, "L"), Bars("p_0", 105), Bars("p_1", 120), Bars("p_2", 160), CalcInputValue.Of("n", 1.4), CalcInputValue.Of("f_rg", 1.05), CalcInputValue.Quantity("T_min", 10, "degC"));
        Assert.Equal(1.05 * f * Held(3.926990816987242, 105 + pa, 120 + pa, 160 + pa, 1.4), r.OutputSi("V0") / Litre, 9);
        Assert.Equal(1.05 * 30.080873024534817, r.OutputSi("V0") / Litre, 9);
    }

    [Fact]
    public void Property_A_Held_Accumulator_Delivers_Exactly_The_Requested_Liquid_Volume()
    {
        Gen.Select(Gen.Double[0.5, 100], Gen.Double[20, 300], Gen.Double[0.3, 0.9], Gen.Double[1.1, 3.5], Gen.Double[1, 1.4]).Sample((dv, p1, f0, f2, n) =>
        {
            var (p0, p2) = (p1 * f0, p1 * f2);

            if (p2 > 700)
                return true;

            var frg = p2 > AccumulatorCalculator.RealGasBlockBar ? 1.5 : 1.0;
            var r = Run(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency), CalcInputValue.Quantity("dV", dv, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", p0), Bars("p_1", p1), Bars("p_2", p2), CalcInputValue.Of("n", n), CalcInputValue.Of("f_rg", frg));
            var v0 = r.OutputSi("V0") / Litre / frg;
            var held = v0 * p0 / p2;
            var delivered = held * Math.Pow(p2 / p1, 1 / n) - held;
            return Math.Abs(delivered - dv) <= 1e-8 * dv;
        });
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

    [Fact]
    public void Black_B_B_05_An_Absolute_Pressure_Below_The_Atmosphere_Is_Blocked_As_A_Gauge_Value_Typed_As_Absolute()
    {
        // 0.5 bar is a vacuum, not a pre-charge: on the Absolute basis it can only be a gauge number typed in the wrong mode.
        var absolute = Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 0.5), Bars("p_1", 100), Bars("p_2", 210));
        Assert.True(absolute.IsBlocked);
        var block = Assert.Single(absolute.Blocks, b => b.RuleId == "basis");
        Assert.StartsWith("p_0:", block.Message, StringComparison.Ordinal);
        Assert.Contains("bar a is below the atmospheric pressure", block.Message, StringComparison.Ordinal);
        Assert.Contains("p_basis to Gauge", block.Message, StringComparison.Ordinal);
        Assert.Empty(absolute.Outputs);
        Assert.Contains(Accumulator.Template.ApplicabilityRules, r => r.Id == "basis" && r.Severity == CalcNoticeSeverity.Block);

        // The same number on the Gauge basis is a real (low) pre-charge, and an absolute pressure at or above the atmosphere is untouched.
        Assert.DoesNotContain(Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Gauge), Bars("p_0", 0.5), Bars("p_1", 100), Bars("p_2", 210)).Blocks, b => b.RuleId == "basis");
        Assert.DoesNotContain(Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 1.01325), Bars("p_1", 2), Bars("p_2", 5)).Blocks, b => b.RuleId == "basis");
        Assert.False(Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_0", 4), Bars("p_1", 5), Bars("p_2", 10)).IsBlocked);

        // The atmosphere entered is the limit, not a fixed 1.01325 bar.
        Assert.Contains(Run(CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), Bars("p_atm", 1.1), Bars("p_0", 1.05), Bars("p_1", 2), Bars("p_2", 5)).Blocks, b => b.RuleId == "basis");
    }

    [Fact]
    public void Yellow_Y_B_06_Every_Pressure_On_The_Sheet_Prints_Its_Basis()
    {
        foreach (var (basis, label) in new[] { (AccumulatorCalculator.Absolute, "bar a"), (AccumulatorCalculator.Gauge, "bar g") })
        {
            var inputs = With(CalcInputValue.Choice("p_basis", basis), Bars("PS", 230));
            var result = CalcRunner.Run(Accumulator, inputs);
            var sheet = CalcSheetBuilder.Build(Accumulator.Template, inputs, result, GoldenFiles.FixedHeader(Accumulator.Template.Id, "F2"), GoldenFiles.FixedFooter(Accumulator.Template, 0, 0));

            // Inputs: the pressures that follow the basis chosen, the atmosphere absolute and the vessel PS gauge.
            string UnitOf(string symbol) => sheet.Inputs.Single(i => i.Symbol == symbol).Unit;
            Assert.All(new[] { "p_0", "p_1", "p_2" }, symbol => Assert.Equal(label, UnitOf(symbol)));
            Assert.Equal("bar a", UnitOf("p_atm"));
            Assert.Equal("bar g", UnitOf("PS"));
            Assert.EndsWith("700 bar " + label[^1], sheet.Inputs.Single(i => i.Symbol == "p_0").ValidRange, StringComparison.Ordinal);

            // No pressure input prints a bare "bar".
            Assert.DoesNotContain(sheet.Inputs, i => i.Unit == "bar");

            // Working and Results: absolute and gauge pressures say which they are, and no step prints a bare " bar" for a pressure.
            string Shown(string symbol) => sheet.Working.Single(w => w.Symbol == symbol).ResultText;
            Assert.EndsWith(" bar a", Shown("pa_0"), StringComparison.Ordinal);
            Assert.EndsWith(" bar a", Shown("pa_2"), StringComparison.Ordinal);
            Assert.EndsWith(" bar g", Shown("p2_g"), StringComparison.Ordinal);
            Assert.EndsWith(" bar a", Shown("p0"), StringComparison.Ordinal);
            Assert.EndsWith(" bar a", Shown("p0_min"), StringComparison.Ordinal);
            Assert.EndsWith(" " + label, Shown("p0_set"), StringComparison.Ordinal);
            Assert.All(sheet.Outputs.Where(o => o.Symbol is "p0" or "p0_min" or "p0_set"), o => Assert.Matches(@" bar [ag]$", o.ValueText));
            Assert.DoesNotContain(sheet.Working, w => w.ResultText.EndsWith(" bar", StringComparison.Ordinal));
        }
    }
}
