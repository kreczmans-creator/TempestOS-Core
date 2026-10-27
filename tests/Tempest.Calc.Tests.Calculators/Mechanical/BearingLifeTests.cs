using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class BearingLifeTests
{
    private static readonly BearingLifeCalculator Bearing = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Bearing.Template);

    [Fact]
    public void Kappa_Below_0_1_Is_Refused_By_Its_Stated_Range()
    {
        // The input states its real range (0.1 to 100), so the refusal comes from the range, not from a later, narrower rule (Black B-A-19).
        var spec = Bearing.Template.FindInput("kappa")!;
        Assert.Equal(0.1, spec.HardMin);
        Assert.Contains("0.1", spec.Description, StringComparison.Ordinal);
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Of("kappa", 0.05)));
        Assert.True(r.IsBlocked);
        Assert.StartsWith("kappa", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Very_Light_Load_Gives_A_Capped_Life_With_A_Warning_Not_A_Block()
    {
        // Black B-A-19: C = 100 000 kN (the hard maximum) against 2 kN used to block "for having too much life".
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("C", 100000, "kN")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Contains(r.Warnings, w => w.RuleId == "light-load");
        Assert.Equal(1e12, r.OutputSi("L_10"), 3);
    }

    [Fact]
    public void An_Axial_Load_Above_E_Cannot_Be_Discarded_By_A_Zero_Y()
    {
        // Black B-A-01: F_r 2 kN, F_a 1.5 kN (F_a/F_r = 0.75 > e_lim 0.3) with the default Y = 0 used to PASS on P = F_r.
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("F_a", 1.5, "kN")));
        Assert.True(r.IsBlocked);
        Assert.Equal("axial-factor", r.Blocks[0].RuleId);
        Assert.StartsWith("Y:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pure_Thrust_With_A_Zero_Y_Names_Y_Not_An_Empty_Load()
    {
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("F_r", 0, "kN")).With(CalcInputValue.Quantity("F_a", 1.5, "kN")));
        Assert.True(r.IsBlocked);
        Assert.Equal("axial-factor", r.Blocks[0].RuleId);
        Assert.Contains("pure thrust", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Duty_Step_With_An_Axial_Load_Above_E_Needs_Y_Too()
    {
        var r = CalcRunner.Run(Bearing, Defaults().With(BearingLifeCalculator.Duty((2, 0, 0.5), (2, 1.5, 0.5))));
        Assert.True(r.IsBlocked);
        Assert.Equal("axial-factor", r.Blocks[0].RuleId);
        var fixedUp = CalcRunner.Run(Bearing, Defaults().With(BearingLifeCalculator.Duty((2, 0, 0.5), (2, 1.5, 0.5))).With(CalcInputValue.Of("Y", 1.2)));
        Assert.False(fixedUp.IsBlocked);
    }

    [Fact]
    public void X1_And_Y1_Give_The_Equivalent_Load_At_Or_Below_E()
    {
        // F_a/F_r = 0.2 ≤ e_lim 0.3: P = X_1 F_r + Y_1 F_a = 1 × 2 + 0.55 × 0.4 = 2.22 kN (a double-row bearing; 0.55 is illustrative).
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("F_a", 0.4, "kN")).With(CalcInputValue.Of("Y_1", 0.55)));
        Assert.False(r.IsBlocked);
        Assert.Equal(2.22, r.OutputSi("F_dyn") / 1e3, 9);
        var singleRow = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("F_a", 0.4, "kN")));
        Assert.Equal(2.0, singleRow.OutputSi("F_dyn") / 1e3, 9);
    }

    [Fact]
    public void Kappa_Above_4_Is_Treated_As_4_With_A_Warning()
    {
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Of("kappa", 6)));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "kappa-high");
    }

    [Fact]
    public void A_ISO_Above_50_Is_Refused_By_Its_Range()
    {
        var notices = CalcInputValidator.Validate(Bearing.Template, Defaults().With(CalcInputValue.Of("a_iso", 60)));
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void Heavy_Load_Warns()
    {
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("F_r", 12, "kN")));
        Assert.Contains(r.Warnings, w => w.RuleId == "heavy-load");
    }

    [Fact]
    public void No_Load_Blocks()
    {
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("F_r", 0, "kN")));
        Assert.True(r.IsBlocked);
        Assert.StartsWith("F_r:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_One_Step_Duty_Cycle_Equals_The_Steady_Load()
    {
        var steady = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("F_r", 3, "kN")).With(CalcInputValue.Quantity("F_a", 1, "kN")).With(CalcInputValue.Of("Y", 1.5)));
        var duty = CalcRunner.Run(Bearing, Defaults().With(BearingLifeCalculator.Duty((3, 1, 1))).With(CalcInputValue.Of("Y", 1.5)));
        Assert.Equal(steady.OutputSi("F_dyn"), duty.OutputSi("F_dyn"), 9);
        Assert.Equal(steady.OutputSi("L_nmh"), duty.OutputSi("L_nmh"), 6);
    }

    [Fact]
    public void Reliability_Factor_Is_One_At_90_And_Falls_With_Reliability()
    {
        Assert.Equal(1.0, CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("R_rel", 90, "%"))).OutputSi("a_1"), 9);
        Gen.Double[90, 99.9].Sample(r =>
        {
            var lower = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("R_rel", r, "%"))).OutputSi("a_1");
            var higher = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("R_rel", r + 0.05, "%"))).OutputSi("a_1");
            return higher < lower && lower <= 1 + 1e-12;
        }, iter: 40);
    }

    [Fact]
    public void Roller_Exponent_Gives_Longer_Life_Than_Ball_Above_C_Over_P_Of_One()
    {
        var ball = CalcRunner.Run(Bearing, Defaults());
        var roller = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Choice("type", BearingLifeCalculator.Roller)));
        Assert.True(roller.OutputSi("L_10") > ball.OutputSi("L_10"));
    }

    [Fact]
    public void Tabulated_Values_Not_From_A_Record_Are_Flagged()
    {
        var notices = CalcInputValidator.Validate(Bearing.Template, Defaults().With(CalcInputValue.Quantity("C", 20, "kN", CalcProvenance.Assumed("from memory"))));
        Assert.Contains(notices, n => n.RuleId == "record.C");
    }

    [Fact]
    public void Declares_Output_Ports_For_Linking()
    {
        Assert.Contains(Bearing.Template.OutputPorts!, p => p.Symbol == "L_nmh" && p.Unit == "h");
        Assert.Contains(Bearing.Template.OutputPorts!, p => p.Symbol == "F_dyn" && p.Unit == "kN");
    }
}
