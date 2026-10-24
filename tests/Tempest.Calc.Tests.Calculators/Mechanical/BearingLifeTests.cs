using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class BearingLifeTests
{
    private static readonly BearingLifeCalculator Bearing = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Bearing.Template);

    [Fact]
    public void Kappa_Below_0_1_Is_A_Hard_Fail()
    {
        var r = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Of("kappa", 0.05)));
        Assert.True(r.IsBlocked);
        Assert.Equal("kappa-low", r.Blocks[0].RuleId);
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
        Assert.StartsWith("F_r/F_a:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_One_Step_Duty_Cycle_Equals_The_Steady_Load()
    {
        var steady = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("F_r", 3, "kN")).With(CalcInputValue.Quantity("F_a", 1, "kN")).With(CalcInputValue.Of("Y", 1.5)));
        var duty = CalcRunner.Run(Bearing, Defaults().With(BearingLifeCalculator.Duty((3, 1, 1))).With(CalcInputValue.Of("Y", 1.5)));
        Assert.Equal(steady.OutputSi("P"), duty.OutputSi("P"), 9);
        Assert.Equal(steady.OutputSi("L_nmh"), duty.OutputSi("L_nmh"), 6);
    }

    [Fact]
    public void Reliability_Factor_Is_One_At_90_And_Falls_With_Reliability()
    {
        Assert.Equal(1.0, CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("R", 90, "%"))).OutputSi("a_1"), 9);
        Gen.Double[90, 99.9].Sample(r =>
        {
            var lower = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("R", r, "%"))).OutputSi("a_1");
            var higher = CalcRunner.Run(Bearing, Defaults().With(CalcInputValue.Quantity("R", r + 0.05, "%"))).OutputSi("a_1");
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
        Assert.Contains(Bearing.Template.OutputPorts!, p => p.Symbol == "P" && p.Unit == "kN");
    }
}
