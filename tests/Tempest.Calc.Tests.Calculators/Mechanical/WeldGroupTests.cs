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

    [Fact]
    public void A_Single_Straight_Weld_Blocks_As_Collinear()
    {
        var r = CalcRunner.Run(Weld, Defaults().With(WeldGroupCalculator.Welds((0, 0, 0, 100))));
        Assert.True(r.IsBlocked);
        Assert.Equal("collinear", r.Blocks[0].RuleId);
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
