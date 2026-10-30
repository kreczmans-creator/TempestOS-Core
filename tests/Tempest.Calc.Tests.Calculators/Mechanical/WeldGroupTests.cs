using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class WeldGroupTests
{
    private static readonly WeldGroupCalculator Weld = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Weld.Template);

    [Fact]
    public void Line_Kernel_Matches_The_Closed_Forms()
    {
        Gen.Select(Gen.Double[10, 500], Gen.Double[10, 500]).Sample((b, d) =>
        {
            var box = GroupGeometry.OfLines([(0, 0, b, 0), (b, 0, b, d), (b, d, 0, d), (0, d, 0, 0)]);
            var two = GroupGeometry.OfLines([(0, 0, 0, d), (b, 0, b, d)]);
            return Math.Abs(box.J - Math.Pow(b + d, 3) / 6) < 1e-6 * box.J && Math.Abs(two.J - d * (3 * b * b + d * d) / 6) < 1e-6 * two.J;
        }, iter: 50);
    }

    [Fact]
    public void Point_Kernel_Matches_The_Bolt_Group_Sums()
    {
        var g = GroupGeometry.OfPoints([(0, 0, 1), (100, 0, 1), (100, 100, 1), (0, 100, 1)]);
        Assert.Equal(50, g.Xc, 12);
        Assert.Equal(4 * 50 * 50 * 2, g.J, 9);
    }

    [Fact]
    public void Splitting_A_Weld_Changes_Nothing()
    {
        var whole = CalcRunner.Run(Weld, Defaults());
        var split = CalcRunner.Run(Weld, Defaults().With(WeldGroupCalculator.Welds((0, 0, 0, 40), (0, 40, 0, 100), (50, 0, 50, 100))));
        Assert.Equal(whole.OutputSi("J_u"), split.OutputSi("J_u"), 12);
        Assert.Equal(whole.OutputSi("f_r_max"), split.OutputSi("f_r_max"), 6);
    }

    [Fact]
    public void Force_Per_Length_Scales_Linearly_With_The_Load()
    {
        var once = CalcRunner.Run(Weld, Defaults()).OutputSi("f_r_max");
        var twice = CalcRunner.Run(Weld, Defaults().With(CalcInputValue.Quantity("F_y", -40, "kN"))).OutputSi("f_r_max");
        Assert.Equal(2 * once, twice, 6);
    }

    private static CalcInputSet NoInPlaneLoad() => Defaults().With(CalcInputValue.Quantity("F_y", 0, "kN"));

    [Fact]
    public void An_Unsymmetric_L_Group_Uses_The_Product_Moment_In_Bending()
    {
        // Green G-A-01: two 100 mm welds meeting at the origin, M_x = 1 kN·m. Solving the moment equilibrium of f = a·x' + b·y' gives 450 N/mm
        // (the symmetric-axes form gave 360); a 150 × 50 L gives 1500 (it gave 1292). python3, independent of the code.
        var equal = CalcRunner.Run(Weld, NoInPlaneLoad().With(WeldGroupCalculator.Welds((0, 0, 100, 0), (0, 0, 0, 100))).With(CalcInputValue.Quantity("M_x", 1, "kN*m")));
        Assert.False(equal.IsBlocked);
        Assert.Equal(450, equal.OutputSi("f_r_max") / 1e3, 6);
        Assert.Equal(-125000, equal.OutputSi("I_uxy") * 1e9, 3);
        var unequal = CalcRunner.Run(Weld, NoInPlaneLoad().With(WeldGroupCalculator.Welds((0, 0, 150, 0), (0, 0, 0, 50))).With(CalcInputValue.Quantity("M_x", 1, "kN*m")));
        Assert.Equal(1500, unequal.OutputSi("f_r_max") / 1e3, 6);
    }

    [Fact]
    public void A_Symmetric_Group_Is_Unchanged_By_The_Product_Moment()
    {
        var r = CalcRunner.Run(Weld, NoInPlaneLoad().With(WeldGroupCalculator.Welds((0, -50, 100, -50), (0, 50, 100, 50))).With(CalcInputValue.Quantity("M_x", 5, "kN*m")));
        Assert.Equal(0, r.OutputSi("I_uxy"), 9);
        Assert.Equal(500, r.OutputSi("f_r_max") / 1e3, 6);
    }

    [Fact]
    public void A_Single_Weld_Loaded_Along_Its_Axis_Through_Its_Centroid_Is_Direct_Shear()
    {
        // Black B-A-15: no bending and no torsion, so nothing is missing; 10 kN over 100 mm is 100 N/mm.
        var r = CalcRunner.Run(Weld, NoInPlaneLoad().With(WeldGroupCalculator.Welds((0, 0, 0, 100))).With(CalcInputValue.Quantity("F_y", -10, "kN")).With(CalcInputValue.Quantity("e_x", 0, "mm")).With(CalcInputValue.Quantity("e_y", 50, "mm")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(100, r.OutputSi("f_r_max") / 1e3, 6);
    }

    [Fact]
    public void A_Single_Weld_Resists_Bending_Across_Its_Length_But_Not_About_Itself()
    {
        var across = CalcRunner.Run(Weld, NoInPlaneLoad().With(WeldGroupCalculator.Welds((0, 0, 0, 100))).With(CalcInputValue.Quantity("M_x", 1, "kN*m")));
        Assert.False(across.IsBlocked);
        Assert.Equal(600, across.OutputSi("f_r_max") / 1e3, 6);

        var about = CalcRunner.Run(Weld, NoInPlaneLoad().With(WeldGroupCalculator.Welds((0, 0, 0, 100))).With(CalcInputValue.Quantity("M_y", 1, "kN*m")));
        Assert.True(about.IsBlocked);
        Assert.Equal("collinear", about.Blocks[0].RuleId);
    }

    [Fact]
    public void An_Inclined_Single_Weld_Resists_Only_The_Moment_That_Bends_It()
    {
        // Line (0,0)-(60,80), l = 100: u = (0.6, 0.8), J_u = l³/12. The moment (0.8, 0.6) kN·m is the one it resists: 1e6 × 50/83 333.3 = 600 N/mm.
        var resisted = CalcRunner.Run(Weld, NoInPlaneLoad().With(WeldGroupCalculator.Welds((0, 0, 60, 80))).With(CalcInputValue.Quantity("M_x", 0.8, "kN*m")).With(CalcInputValue.Quantity("M_y", 0.6, "kN*m")));
        Assert.False(resisted.IsBlocked, string.Join("; ", resisted.Blocks.Select(b => b.Message)));
        Assert.Equal(600, resisted.OutputSi("f_r_max") / 1e3, 6);

        var unresisted = CalcRunner.Run(Weld, NoInPlaneLoad().With(WeldGroupCalculator.Welds((0, 0, 60, 80))).With(CalcInputValue.Quantity("M_x", 0.6, "kN*m")).With(CalcInputValue.Quantity("M_y", -0.8, "kN*m")));
        Assert.True(unresisted.IsBlocked);
        Assert.Equal("collinear", unresisted.Blocks[0].RuleId);
    }

    [Fact]
    public void Leg_Outside_3_To_25_Mm_Is_Refused()
    {
        Assert.Contains(CalcInputValidator.Validate(Weld.Template, Defaults().With(CalcInputValue.Quantity("s", 30, "mm"))), n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void Required_Leg_Gives_Utilisation_One()
    {
        var r = CalcRunner.Run(Weld, Defaults());
        var exact = CalcRunner.Run(Weld, Defaults().With(CalcInputValue.Quantity("s", r.OutputSi("s_req") * 1e3, "mm")));
        Assert.Equal(1.0, exact.OutputSi("u"), 9);
    }

    [Fact]
    public void An_Eccentric_Out_Of_Plane_Force_Adds_Its_Moment()
    {
        // Opus review finding 7: two horizontal welds at y = ±50 mm, F_z = 10 kN at e_y = 50 mm: 50 + 50 N/mm.
        var r = CalcRunner.Run(Weld, Defaults().With(WeldGroupCalculator.Welds((0, -50, 100, -50), (0, 50, 100, 50)))
            .With(CalcInputValue.Quantity("F_y", 0, "kN")).With(CalcInputValue.Quantity("F_z", 10, "kN"))
            .With(CalcInputValue.Quantity("e_x", 50, "mm")).With(CalcInputValue.Quantity("e_y", 50, "mm")));
        Assert.Equal(100e3, r.OutputSi("f_r_max"), 3);
    }
}
