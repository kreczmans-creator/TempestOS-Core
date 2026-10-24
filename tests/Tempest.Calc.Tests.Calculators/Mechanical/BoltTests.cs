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

    private static CalcInputValue BoltTable(params (double X, double Y)[] points) =>
        CalcInputValue.Table("bolts", points.Select(p => CalcRow.Of(("x", new CalcCell(p.X, "mm")), ("y", new CalcCell(p.Y, "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList());

    private static CalcInputSet BendingOnly(params (double X, double Y)[] points) =>
        CalcTemplateHasher.DefaultInputs(Group.Template).With(BoltTable(points)).With(CalcInputValue.Quantity("F_y", 0, "kN")).With(CalcInputValue.Quantity("e_x", 0, "mm")).With(CalcInputValue.Quantity("e_y", 0, "mm"))
            .With(CalcInputValue.Quantity("V_R", 1000, "kN")).With(CalcInputValue.Quantity("T_R", 1000, "kN"));

    [Fact]
    public void An_Unsymmetric_Pattern_Uses_The_Product_Moment_In_Bending()
    {
        // Green G-A-01 (shared kernel): bolts at (0,0), (120,0), (60,80), (0,160), A = 84.3 mm², M_x = 12 kN·m. Moment equilibrium of
        // t = A(a·x' + b·y') solved in python3 gives 62.5 kN at the top bolt; the symmetric-axes form gave 68.18 kN.
        var r = CalcRunner.Run(Group, BendingOnly((0, 0), (120, 0), (60, 80), (0, 160)).With(CalcInputValue.Quantity("M_x", 12, "kN*m")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(62.5, r.OutputSi("t_max") / 1e3, 6);
    }

    [Fact]
    public void Bolts_On_One_Line_Cannot_Resist_A_Moment_About_That_Line()
    {
        // Bolts on the y axis: M_y (about y) has nothing to bear on. It used to be dropped silently.
        var about = CalcRunner.Run(Group, BendingOnly((0, 0), (0, 100), (0, 200)).With(CalcInputValue.Quantity("M_y", 5, "kN*m")));
        Assert.True(about.IsBlocked);
        Assert.Equal("collinear", about.Blocks[0].RuleId);

        // M_x bends the line: t = M y'/(A Σ y'²) × A, y' = 100 mm, Σ y'² = 20000: 10e6 × 100/20000 = 50 kN.
        var across = CalcRunner.Run(Group, BendingOnly((0, 0), (0, 100), (0, 200)).With(CalcInputValue.Quantity("M_x", 10, "kN*m")));
        Assert.False(across.IsBlocked, string.Join("; ", across.Blocks.Select(b => b.Message)));
        Assert.Equal(50, across.OutputSi("t_max") / 1e3, 6);
    }
}
