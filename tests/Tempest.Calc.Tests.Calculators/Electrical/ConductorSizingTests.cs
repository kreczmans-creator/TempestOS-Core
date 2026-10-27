using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>E4 behaviour. The capacities and factors used here are synthetic values invented for these tests, not taken from any table; the ratings are the typical preferred series.</summary>
public sealed class ConductorSizingTests
{
    private static readonly ConductorSizingCalculator Sizing = new();

    /// <summary>The first known-answer case: 7.5 kW three-phase, type C MCB, θ_a 35 °C with C_a 0.91, two circuits with C_g 0.77, synthetic tables.</summary>
    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Sizing.Template);

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Sizing, overrides.Aggregate(Defaults(), (set, v) => set.With(v)));

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Installation => Choice("context", ConductorSizingCalculator.ContextInstallation);

    [Fact]
    public void Demands_An_Independent_Check() => Assert.True(Sizing.Template.IndependentCheck);

    [Fact]
    public void Declares_Its_Ports_And_Record_Kinds()
    {
        Assert.Equal(["I_b", "I_n", "CSA", "I_z", "L", "T_op", "device_code"], Sizing.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.Equal(["cable.ampacity", "cable.correction-factors", "device.ratings"], Sizing.Template.RecordKinds!.Select(k => k.Kind).Order(StringComparer.Ordinal));
        Assert.All(Sizing.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number), i =>
        {
            // The usual range sits inside the hard range and is never the same as it (the sheet standard, G42).
            Assert.True(i.Min >= i.HardMin && i.Max <= i.HardMax, i.Symbol);
            Assert.True(i.Min != i.HardMin || i.Max != i.HardMax, i.Symbol);
        });
    }

    [Theory]
    [InlineData("P", 0.005, "kW")]
    [InlineData("P", 501.0, "kW")]
    [InlineData("U", 11.0, "V")]
    [InlineData("U", 1001.0, "V")]
    [InlineData("T_amb", -11.0, "degC")]
    [InlineData("T_amb", 71.0, "degC")]
    public void Quantities_Outside_Their_Range_Are_A_Hard_Fail(string symbol, double value, string unit)
    {
        var r = Run(Q(symbol, value, unit));
        Assert.True(r.IsBlocked);
        Assert.Empty(r.Outputs);
        Assert.Contains(r.Blocks, b => b.Message.StartsWith(symbol + ":", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("cos_phi", 0.4)]
    [InlineData("cos_phi", 1.01)]
    [InlineData("eta", 0.4)]
    [InlineData("N_g", 0.0)]
    [InlineData("N_g", 21.0)]
    [InlineData("C_g", 0.05)]
    [InlineData("C_a", 1.6)]
    public void Numbers_Outside_Their_Range_Are_A_Hard_Fail(string symbol, double value)
    {
        var r = Run(CalcInputValue.Of(symbol, value));
        Assert.True(r.IsBlocked);
        Assert.Empty(r.Outputs);
    }

    [Fact]
    public void An_Empty_Ampacity_Table_Blocks_With_The_Record_Hint()
    {
        var r = Run(CalcInputValue.Table("ampacity", []));
        Assert.True(r.IsBlocked);
        var block = Assert.Single(r.Blocks);
        Assert.Equal("ampacity", block.RuleId);
        Assert.Equal("ampacity: enter the current-carrying capacities for the installation method from your own licensed table (record kind cable.ampacity).", block.Message);
    }

    [Fact]
    public void No_Rating_Large_Enough_Blocks()
    {
        var r = Run(Q("I_b_in", 80, "A"));
        var block = Assert.Single(r.Blocks);
        Assert.Equal("I_n", block.RuleId);
        Assert.StartsWith("I_n:", block.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Conductor_Large_Enough_Blocks()
    {
        var r = Run(Q("I_b_in", 50, "A"), Choice("device", ConductorSizingCalculator.GgFuse));
        var block = Assert.Single(r.Blocks);
        Assert.Equal("CSA", block.RuleId);
        Assert.StartsWith("CSA:", block.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Linked_Design_Current_Replaces_The_Computed_One()
    {
        var computed = Run();
        var linked = Run(Q("I_b_in", 22, "A"), Q("P", 1, "kW"));
        Assert.Equal(22, linked.OutputSi("I_b"), 9);
        Assert.NotEqual(computed.OutputSi("I_b"), linked.OutputSi("I_b"));
        Assert.Equal(25, linked.OutputSi("I_n"), 9);
    }

    [Theory]
    [InlineData(ConductorSizingCalculator.ThreePhase, 7500 / (1.7320508075688772 * 400 * 0.85 * 0.9))]
    [InlineData(ConductorSizingCalculator.SinglePhase, 7500 / (400 * 0.85 * 0.9))]
    [InlineData(ConductorSizingCalculator.Dc, 7500 / 400.0)]
    public void The_Design_Current_Follows_The_Supply(string supply, double expected)
    {
        Assert.Equal(expected, Run(Choice("supply", supply)).OutputSi("I_b"), 9);
    }

    [Fact]
    public void A_Semi_Enclosed_Fuse_Applies_C_c()
    {
        var gg = Run(Installation, Choice("device", ConductorSizingCalculator.GgFuse));
        var semi = Run(Choice("device", ConductorSizingCalculator.SemiEnclosed));
        Assert.Equal(1, gg.OutputSi("C_c"), 12);
        Assert.Equal(0.725, semi.OutputSi("C_c"), 12);
        Assert.Equal(gg.OutputSi("I_t_req") / 0.725, semi.OutputSi("I_t_req"), 9);
        Assert.True(semi.OutputSi("CSA") >= gg.OutputSi("CSA"));
    }

    [Fact]
    public void An_Mcb_Has_The_Inherent_I2_And_An_Mccb_Needs_One_Outside_The_Installation_Context()
    {
        var mcb = Run(Q("I2", 1, "A"));
        Assert.Equal(1.45 * 16, mcb.OutputSi("I2"), 9);
        Assert.Equal(3, mcb.Checks.Count);

        // Machine wiring: an MCCB without I2 cannot be deemed to meet I2 <= 1.45 Iz (Green G-B-13).
        var withoutI2 = Run(Choice("device", ConductorSizingCalculator.Mccb));
        var block = Assert.Single(withoutI2.Blocks);
        Assert.Equal("I2", block.RuleId);
        Assert.StartsWith("I2:", block.Message, StringComparison.Ordinal);
        Assert.Null(withoutI2.FindOutput("u_I2"));

        var withI2 = Run(Choice("device", ConductorSizingCalculator.Mccb), Q("I2", 24, "A"));
        Assert.False(withI2.IsBlocked);
        Assert.DoesNotContain(withI2.Warnings, w => w.RuleId == "I2");
        Assert.Equal(3, withI2.Checks.Count);
        Assert.Equal(24 / (1.45 * (24.1 * 0.91 * 0.77)), withI2.OutputSi("u_I2"), 9);

        var tooHigh = Run(Choice("device", ConductorSizingCalculator.Mpcb), Q("I2", 40, "A"));
        Assert.Contains(tooHigh.Checks, c => c.Name.Contains("1.45", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void An_Mccb_Without_I2_Is_A_Stated_Deemed_Condition_In_The_Installation_Context()
    {
        var r = Run(Installation, Choice("device", ConductorSizingCalculator.Mccb));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "I2-deemed" && w.Message.Contains("1.45·I_n", StringComparison.Ordinal));
        Assert.Null(r.FindOutput("u_I2"));
        var check = Assert.Single(r.Checks, c => c.Name.Contains("deemed", StringComparison.Ordinal));
        Assert.True(check.Passed);
        Assert.Contains("I_n", check.Name, StringComparison.Ordinal);
        Assert.Equal(3, r.Checks.Count);
    }

    [Fact]
    public void A_Gg_Fuse_Is_Deemed_Only_Under_BS_7671_And_Needs_Its_I2_In_The_Machine_Context()
    {
        // Green G-B-02: an IEC 60269-1 gG fuse has I2 = 1.6 In, so the BS 88 'deemed' rule must not be applied to machine wiring.
        var machine = Run(Choice("device", ConductorSizingCalculator.GgFuse));
        var block = Assert.Single(machine.Blocks);
        Assert.Equal("I2", block.RuleId);
        Assert.Contains("machine-wiring context", block.Message, StringComparison.Ordinal);

        // With its datasheet I2 = 1.6 * 16 A = 25.6 A the check is made and fails: 25.6 / (1.45 * 16.88687) = 1.04546.
        var checkedRun = Run(Choice("device", ConductorSizingCalculator.GgFuse), Q("I2", 25.6, "A"));
        Assert.False(checkedRun.IsBlocked);
        Assert.Equal(25.6 / (1.45 * 24.1 * 0.91 * 0.77), checkedRun.OutputSi("u_I2"), 9);
        Assert.Contains(checkedRun.Checks, c => c.Name.Contains("1.45", StringComparison.Ordinal) && !c.Passed);

        // The BS 7671 installation context keeps the deemed condition for a BS 88 gG fuse.
        var installation = Run(Installation, Choice("device", ConductorSizingCalculator.GgFuse));
        Assert.False(installation.IsBlocked);
        Assert.Contains(installation.Checks, c => c.Name.Contains("deemed", StringComparison.Ordinal) && c.Name.Contains("BS 88", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void A_Gg_Fuse_Shows_No_I2_And_Is_A_Deemed_Condition_Under_BS_7671()
    {
        var r = Run(Installation, Choice("device", ConductorSizingCalculator.GgFuse));
        Assert.Null(r.FindOutput("I2"));
        Assert.Null(r.FindOutput("u_I2"));
        var check = Assert.Single(r.Checks, c => c.Name.Contains("deemed", StringComparison.Ordinal));
        Assert.True(check.Passed);
        Assert.Contains("433.1.201", check.Clause, StringComparison.Ordinal);
        Assert.Equal(3, r.Checks.Count);
    }

    [Theory]
    [InlineData(ConductorSizingCalculator.McbB, 1)]
    [InlineData(ConductorSizingCalculator.McbC, 2)]
    [InlineData(ConductorSizingCalculator.McbD, 3)]
    [InlineData(ConductorSizingCalculator.GgFuse, 4)]
    [InlineData(ConductorSizingCalculator.SemiEnclosed, 4)]
    [InlineData(ConductorSizingCalculator.Mccb, 4)]
    [InlineData(ConductorSizingCalculator.Mpcb, 4)]
    public void The_Device_Type_Code_Is_An_Output_For_The_Fault_Check(string device, int code)
    {
        var r = Run(Installation, Choice("device", device));
        Assert.Equal(code, r.OutputSi("device_code"), 12);
        Assert.Equal("device_code", Sizing.Template.OutputPorts!.Single(p => p.Symbol == "device_code").Symbol);
    }

    [Fact]
    public void Ampacity_Rows_In_The_Other_Conductor_System_Say_So()
    {
        // Black B-A-20: AWG selected with metric rows (and the reverse) used to read as an empty table.
        var awgWithMetric = Run(Choice("system", ConductorSizingCalculator.Awg));
        var block = Assert.Single(awgWithMetric.Blocks);
        Assert.Equal("ampacity: the rows hold cross-sections but the conductor system is AWG. Enter AWG gauge numbers, or switch the system to metric.", block.Message);

        var metricWithAwg = Run(ConductorSizingCalculator.AwgAmpacity((12, 19.4), (10, 27.8)));
        Assert.Equal("ampacity: the rows hold AWG gauge numbers but the conductor system is metric. Enter cross-sections, or switch the system to AWG.", Assert.Single(metricWithAwg.Blocks).Message);

        Assert.Equal("ampacity: enter the current-carrying capacities for the installation method from your own licensed table (record kind cable.ampacity).", Assert.Single(Run(CalcInputValue.Table("ampacity", [])).Blocks).Message);
    }

    [Fact]
    public void A_Semi_Enclosed_Fuse_Defaults_To_Twice_The_Rating_For_I2()
    {
        var r = Run(Choice("device", ConductorSizingCalculator.SemiEnclosed));
        Assert.Equal(2 * r.OutputSi("I_n"), r.OutputSi("I2"), 9);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "I2");
        Assert.Equal(3, r.Checks.Count);
        Assert.Equal(24, Run(Choice("device", ConductorSizingCalculator.SemiEnclosed), Q("I2", 24, "A")).OutputSi("I2"), 9);
    }

    [Fact]
    public void A_Fractional_Awg_Gauge_Blocks()
    {
        var r = Run(Q("I_b_in", 25, "A"), Choice("system", ConductorSizingCalculator.Awg), ConductorSizingCalculator.AwgAmpacity((10.5, 26.3)));
        var block = Assert.Single(r.Blocks);
        Assert.Equal("ampacity: AWG gauge must be a whole number.", block.Message);
    }

    [Fact]
    public void Derived_Ambient_Factor_Is_The_Square_Root_Formula_And_Labelled()
    {
        var r = Run(CalcInputValue.Switch("derive_Ca", true), Q("T_amb", 40, "degC"));
        Assert.Equal(Math.Sqrt(30.0 / 40.0), r.OutputSi("C_a"), 12);
        Assert.Contains(r.Warnings, w => w.RuleId == "C_a" && w.Message.Contains("derived approximation, so compare it with the licensed table", StringComparison.Ordinal));
        Assert.Contains(r.Steps, s => s.Symbol == "C_a" && s.Formula.Contains("derived approximation", StringComparison.Ordinal));

        var xlpe = Run(CalcInputValue.Switch("derive_Ca", true), Q("T_amb", 40, "degC"), Choice("insulation", ConductorSizingCalculator.Xlpe));
        Assert.Equal(Math.Sqrt(50.0 / 60.0), xlpe.OutputSi("C_a"), 12);

        var user = Run(CalcInputValue.Switch("derive_Ca", true), Q("T_amb", 40, "degC"), Choice("insulation", ConductorSizingCalculator.UserInsulation), Q("T_user", 105, "degC"));
        Assert.Equal(Math.Sqrt(65.0 / 75.0), user.OutputSi("C_a"), 12);

        var missing = Run(CalcInputValue.Switch("derive_Ca", true), Choice("insulation", ConductorSizingCalculator.UserInsulation));
        Assert.Equal("theta_p", Assert.Single(missing.Blocks).RuleId);

        var hot = Run(CalcInputValue.Switch("derive_Ca", true), Q("T_amb", 70, "degC"));
        Assert.True(hot.IsBlocked);
    }

    [Fact]
    public void The_Context_Changes_Citations_And_Never_Numbers()
    {
        var machine = Run(Choice("context", ConductorSizingCalculator.ContextMachine));
        var installation = Run(Choice("context", ConductorSizingCalculator.ContextInstallation));

        foreach (var symbol in new[] { "I_b", "I_n", "I_t_req", "CSA", "I_z", "u_Ib", "u_In", "u_I2" })
            Assert.Equal(machine.OutputSi(symbol), installation.OutputSi(symbol));

        Assert.NotEqual(machine.RunAssumptions[0].Statement, installation.RunAssumptions[0].Statement);
        Assert.Contains(machine.Checks, c => c.Clause!.Contains("60204-1", StringComparison.Ordinal));
        Assert.DoesNotContain(installation.Checks, c => c.Clause!.Contains("60204-1", StringComparison.Ordinal));
    }

    [Fact]
    public void Grouped_Circuits_With_No_Grouping_Factor_Warn()
    {
        Assert.Contains(Run(CalcInputValue.Of("N_g", 4), CalcInputValue.Of("C_g", 1)).Warnings, w => w.RuleId == "C_g");
        Assert.DoesNotContain(Run(CalcInputValue.Of("N_g", 4), CalcInputValue.Of("C_g", 0.58)).Warnings, w => w.RuleId == "C_g");
    }

    [Fact]
    public void An_Awg_Gauge_Gives_The_Geometric_Area()
    {
        Assert.Equal(5.261154954510373, ConductorSizingCalculator.AwgArea(10), 12);
        Assert.Equal("4/0", ConductorSizingCalculator.AwgName(-3));
        Assert.Equal("1/0", ConductorSizingCalculator.AwgName(0));

        var r = Run(Q("I_b_in", 25, "A"), Choice("system", ConductorSizingCalculator.Awg), CalcInputValue.Of("C_a", 1), CalcInputValue.Of("C_g", 1), ConductorSizingCalculator.AwgAmpacity((12, 18.4), (10, 27.3)));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(5.261154954510373, r.OutputSi("CSA") * 1e6, 9);
        Assert.Equal("AWG 10", ((Tempest.Calc.Engine.Values.TextValue)r.FindOutput("gauge")!.Value).Value);
    }

    [Fact]
    public void The_Smallest_Carrying_Row_Is_Chosen_Whatever_The_Row_Order()
    {
        var shuffled = ConductorSizingCalculator.Ampacity((10, 43.1), (2.5, 16.2), (6, 30.8), (1.5, 11.4), (4, 23.7));
        Assert.Equal(Run().OutputSi("CSA"), Run(shuffled).OutputSi("CSA"));
    }

    [Fact]
    public void More_Grouping_Never_Gives_A_Smaller_Conductor()
    {
        double Csa(double cg)
        {
            var r = Run(CalcInputValue.Of("C_g", cg));
            return r.IsBlocked ? double.PositiveInfinity : r.OutputSi("CSA");
        }

        Gen.Double[0.1, 1.0].Select(Gen.Double[0.1, 1.0]).Sample((a, b) =>
        {
            var (low, high) = a <= b ? (a, b) : (b, a);
            return Csa(low) >= Csa(high);
        }, iter: 120);
    }

    [Fact]
    public void A_Smaller_Cg_Raises_The_Required_Capacity()
    {
        Gen.Double[0.6, 1.0].Sample(cg => Run(CalcInputValue.Of("C_g", cg * 0.9)).OutputSi("I_t_req") > Run(CalcInputValue.Of("C_g", cg)).OutputSi("I_t_req"), iter: 60);
    }
}
