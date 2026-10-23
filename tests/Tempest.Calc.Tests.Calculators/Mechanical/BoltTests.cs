using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class BoltTests
{
    private static readonly BoltedJointCalculator Joint = new();
    private static readonly BoltGroupCalculator Group = new();

    private static CalcInputSet JointInputs() => CalcTemplateHasher.DefaultInputs(Joint.Template);

    [Fact]
    public void Nut_Factor_And_Friction_Methods_Agree_For_A_Matching_K()
    {
        var friction = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Choice("method", "friction")));
        var fm = friction.OutputSi("F_M");
        var k = 93.0 / (fm * 0.012);
        var nut = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Choice("method", "nut factor")).With(CalcInputValue.Of("K", k)));
        Assert.Equal(fm, nut.OutputSi("F_M"), 3);
    }

    [Fact]
    public void Scatter_Reduces_Residual_Clamp_Not_Maximum_Bolt_Load()
    {
        var tight = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Of("alpha_A", 1.0)));
        var loose = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Of("alpha_A", 2.0)));
        Assert.True(loose.OutputSi("F_KR") < tight.OutputSi("F_KR"));
        Assert.Equal(tight.OutputSi("F_S_max"), loose.OutputSi("F_S_max"), 9);
    }

    [Fact]
    public void Separation_Is_Reported_Under_A_Large_Working_Load()
    {
        var r = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", 60, "kN")));
        Assert.Contains(r.Warnings, w => w.RuleId == "separation");
        Assert.Contains(r.Checks, c => c.Name == "No separation" && !c.Passed);
        Assert.False(r.Passed);
    }

    [Fact]
    public void Separation_Load_Factor_Is_Where_The_Residual_Clamp_Reaches_Zero()
    {
        // ADR-0167: the legacy joint-diagram load factor n_0 = F_A,sep / F_A, checked by re-running at F_A,sep.
        var r = CalcRunner.Run(Joint, JointInputs());
        var sep = r.OutputSi("F_A_sep");
        Assert.Equal(sep / 10_000, r.OutputSi("n_0"), 9);
        var atSep = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", sep / 1000, "kN")));
        Assert.True(Math.Abs(atSep.OutputSi("F_KR")) < 1e-6 * r.OutputSi("F_M"), $"F_KR at F_A,sep is {atSep.OutputSi("F_KR")} N");
    }

    [Fact]
    public void Yield_Load_Factor_Is_Where_The_Bolt_Load_Reaches_The_Proof_Load()
    {
        var r = CalcRunner.Run(Joint, JointInputs());
        var nL = r.OutputSi("n_L");
        Assert.True(nL > 1);
        var atYield = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", 10 * nL, "kN")));
        Assert.Equal(640e6 * r.OutputSi("A_s"), atYield.OutputSi("F_S_max"), 6);
    }

    [Fact]
    public void Load_Factors_Are_Omitted_Without_A_Working_Load()
    {
        var r = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", 0, "kN")));
        Assert.False(r.IsBlocked);
        Assert.Null(r.FindOutput("n_0"));
        Assert.Null(r.FindOutput("n_L"));
        Assert.NotNull(r.FindOutput("F_A_sep"));
    }

    [Fact]
    public void Load_Factor_Lies_Between_Zero_And_The_Introduction_Factor()
    {
        Gen.Select(Gen.Double[0, 1], Gen.Double[10, 200], Gen.Double[6, 30]).Sample((n, lk, d) =>
        {
            var inputs = JointInputs().With(CalcInputValue.Of("n", n)).With(CalcInputValue.Quantity("l_k", lk, "mm")).With(CalcInputValue.Quantity("d", d, "mm")).With(CalcInputValue.Quantity("P", d / 8, "mm")).With(CalcInputValue.Quantity("d_h", d + 1, "mm")).With(CalcInputValue.Quantity("d_w", 1.5 * d, "mm"));
            var r = CalcRunner.Run(Joint, inputs);
            return r.IsBlocked || (r.OutputSi("Phi") >= 0 && r.OutputSi("Phi") <= n + 1e-12);
        }, iter: 60);
    }

    [Fact]
    public void Hole_Geometry_Blocks_Plainly()
    {
        var r = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("d_h", 11, "mm")));
        Assert.True(r.IsBlocked);
        Assert.StartsWith("d_h:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    private static CalcInputValue Bolts(params (double X, double Y)[] points) =>
        CalcInputValue.Table("bolts", points.Select(p => CalcRow.Of(("x", new CalcCell(p.X, "mm")), ("y", new CalcCell(p.Y, "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList());

    private static CalcInputSet GroupInputs(CalcInputValue bolts) => CalcTemplateHasher.DefaultInputs(Group.Template).With(bolts);

    [Fact]
    public void Symmetric_Pattern_Under_Central_Load_Shares_Shear_Equally()
    {
        var r = CalcRunner.Run(Group, GroupInputs(Bolts((0, 0), (100, 0), (100, 100), (0, 100))).With(CalcInputValue.Quantity("e_x", 50, "mm")).With(CalcInputValue.Quantity("e_y", 50, "mm")));
        Assert.Equal(0, r.OutputSi("T"), 9);
        Assert.Equal(12.5e3, r.OutputSi("v_max"), 6);
    }

    [Fact]
    public void Prying_Raises_Tension_By_The_Lever_Ratio()
    {
        var baseline = GroupInputs(Bolts((0, -100), (100, -100), (0, 100), (100, 100))).With(CalcInputValue.Quantity("M_x", 20, "kN*m")).With(CalcInputValue.Quantity("F_y", 0, "kN"));
        var without = CalcRunner.Run(Group, baseline);
        var with = CalcRunner.Run(Group, baseline.With(CalcInputValue.Quantity("a", 30, "mm")).With(CalcInputValue.Quantity("b", 15, "mm")));
        Assert.Equal(1.5 * without.OutputSi("t_max"), with.OutputSi("t_max"), 6);
    }

    [Fact]
    public void One_Bolt_Or_Coincident_Bolts_Block()
    {
        Assert.Contains(CalcRunner.Run(Group, GroupInputs(Bolts((0, 0)))).Blocks, b => b.RuleId == "few-bolts");
        Assert.Contains(CalcRunner.Run(Group, GroupInputs(Bolts((0, 0), (0, 0)))).Blocks, b => b.RuleId == "coincident");
    }

    [Fact]
    public void Property_Shear_Scales_Linearly_With_Load()
    {
        Gen.Select(Gen.Double[1, 500], Gen.Double[-300, 300]).Sample((f, e) =>
        {
            var bolts = Bolts((0, 0), (80, 0), (80, 120), (0, 120));
            var a = CalcRunner.Run(Group, GroupInputs(bolts).With(CalcInputValue.Quantity("F_y", -f, "kN")).With(CalcInputValue.Quantity("e_x", e, "mm")));
            var b = CalcRunner.Run(Group, GroupInputs(bolts).With(CalcInputValue.Quantity("F_y", -2 * f, "kN")).With(CalcInputValue.Quantity("e_x", e, "mm")));
            return Math.Abs(b.OutputSi("v_max") - 2 * a.OutputSi("v_max")) < 1e-6 * b.OutputSi("v_max");
        }, iter: 40);
    }
}
