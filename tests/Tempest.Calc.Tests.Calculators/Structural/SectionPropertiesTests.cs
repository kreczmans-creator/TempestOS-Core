using CsCheck;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Calc.Structural.Geometry;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class SectionPropertiesTests
{
    private static readonly SectionPropertiesCalculator Calc = new();

    private static CalcRunResult Run(params CalcInputValue[] values) => CalcRunner.Run(Calc, new CalcInputSet(values));

    [Fact]
    public void Polygon_Rectangle_Matches_Closed_Form()
    {
        Gen.Select(Gen.Double[1, 500], Gen.Double[1, 500]).Sample((b, h) =>
        {
            var p = PolygonSection.Compute(Shapes.Rectangle(b / 1e3, h / 1e3));
            var bm = b / 1e3;
            var hm = h / 1e3;
            Assert.Equal(bm * hm, p.A, bm * hm * 1e-12);
            Assert.Equal(bm * hm * hm * hm / 12, p.Ixx, p.Ixx * 1e-9);
            Assert.Equal(hm * bm * bm * bm / 12, p.Iyy, p.Iyy * 1e-9);
            Assert.Equal(bm * hm * hm / 4, p.WplX, p.WplX * 1e-9);
            Assert.Equal(hm * bm * bm / 4, p.WplY, p.WplY * 1e-9);
            Assert.Equal(0, p.Ixy, p.Ixx * 1e-9);
        }, iter: 200);
    }

    [Fact]
    public void Rotation_Preserves_Area_And_Principal_Values()
    {
        Gen.Select(Gen.Double[10, 300], Gen.Double[10, 300], Gen.Double[2, 9], Gen.Double[-3, 3]).Sample((h, b, t, angle) =>
        {
            var loops = Shapes.Angle(h / 1e3, b / 1e3, t / 1e3, 0);
            var p0 = PolygonSection.Compute(loops);
            var p1 = PolygonSection.Compute(loops.Select(l => l.Transform(0.05, -0.02, angle)).ToList());
            Assert.Equal(p0.A, p1.A, p0.A * 1e-9);
            Assert.Equal(p0.I1, p1.I1, p0.I1 * 1e-9);
            Assert.Equal(p0.I2, p1.I2, p0.I1 * 1e-9);
            Assert.Equal(p0.Ixx + p0.Iyy, p1.Ixx + p1.Iyy, p0.I1 * 1e-9);
        }, iter: 100);
    }

    [Fact]
    public void Composite_Of_Two_Rectangles_Equals_One_Rectangle()
    {
        var one = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeRectangle), CalcInputValue.Quantity("b", 100, "mm"), CalcInputValue.Quantity("h", 200, "mm"));
        var two = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeComposite), SectionPropertiesCalculator.Parts(
            (SectionPropertiesCalculator.ShapeRectangle, 100, 120, 0, 0, 0, 0, 60, 0, "add"),
            (SectionPropertiesCalculator.ShapeRectangle, 100, 80, 0, 0, 0, 0, 160, 0, "add")));
        Assert.False(two.IsBlocked, string.Join("; ", two.Blocks.Select(b => b.Message)));

        foreach (var symbol in new[] { "A", "I_xx", "I_yy", "W_el_x", "W_pl_x", "W_pl_y", "i_x" })
            Assert.Equal(one.OutputSi(symbol), two.OutputSi(symbol), one.OutputSi(symbol) * 1e-9);
    }

    [Fact]
    public void Subtracted_Part_Makes_A_Hole()
    {
        var tube = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeHollowRectangle), CalcInputValue.Quantity("b", 100, "mm"), CalcInputValue.Quantity("h", 150, "mm"), CalcInputValue.Quantity("t", 10, "mm"), CalcInputValue.Quantity("r", 0, "mm"), CalcInputValue.Quantity("r_i", 0, "mm"));
        var composite = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeComposite), SectionPropertiesCalculator.Parts(
            (SectionPropertiesCalculator.ShapeRectangle, 100, 150, 0, 0, 0, 0, 0, 0, "add"),
            (SectionPropertiesCalculator.ShapeRectangle, 80, 130, 0, 0, 0, 0, 0, 0, "subtract")));
        Assert.Equal(tube.OutputSi("A"), composite.OutputSi("A"), tube.OutputSi("A") * 1e-9);
        Assert.Equal(tube.OutputSi("I_xx"), composite.OutputSi("I_xx"), tube.OutputSi("I_xx") * 1e-9);
        Assert.Equal(tube.OutputSi("W_pl_x"), composite.OutputSi("W_pl_x"), tube.OutputSi("W_pl_x") * 1e-9);
    }

    [Fact]
    public void Polygon_With_Hole_Matches_The_Tube()
    {
        var hole = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapePolygon), SectionPropertiesCalculator.Polygon(
            ("mm", [(-50, -75), (50, -75), (50, 75), (-50, 75)]),
            ("mm", [(-40, -65), (40, -65), (40, 65), (-40, 65)])));
        Assert.False(hole.IsBlocked);
        Assert.Equal((100 * 150 - 80 * 130) * 1e-6, hole.OutputSi("A"), 1e-15);
        var ixx = (100.0 * 150 * 150 * 150 - 80.0 * 130 * 130 * 130) / 12 * 1e-12;
        Assert.Equal(ixx, hole.OutputSi("I_xx"), ixx * 1e-9);
    }

    [Fact]
    public void Missing_Dimension_Blocks_With_The_Symbol()
    {
        var r = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeTube), CalcInputValue.Quantity("t", 5, "mm"));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.StartsWith("d:", StringComparison.Ordinal));
    }

    [Fact]
    public void Impossible_Geometry_Blocks()
    {
        Assert.True(Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeTube), CalcInputValue.Quantity("d", 20, "mm"), CalcInputValue.Quantity("t", 10, "mm")).IsBlocked);
        Assert.True(Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeI), CalcInputValue.Quantity("h", 100, "mm"), CalcInputValue.Quantity("b", 50, "mm"), CalcInputValue.Quantity("t", 60, "mm"), CalcInputValue.Quantity("t_f", 10, "mm"), CalcInputValue.Quantity("r", 0, "mm")).IsBlocked);
        Assert.True(Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapePolygon), SectionPropertiesCalculator.Polygon(("mm", [(0, 0), (10, 0), (20, 0)]))).IsBlocked);
        Assert.True(Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapePolygon), SectionPropertiesCalculator.Polygon(("mm", [(0, 0), (10, 0)]))).IsBlocked);
        Assert.True(Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeComposite)).IsBlocked);
    }

    [Fact]
    public void Hole_Larger_Than_Outline_Blocks()
    {
        var r = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapePolygon), SectionPropertiesCalculator.Polygon(
            ("mm", [(0, 0), (10, 0), (10, 10), (0, 10)]),
            ("mm", [(-5, -5), (20, -5), (20, 20), (-5, 20)])));
        Assert.True(r.IsBlocked);
    }

    [Fact]
    public void Polygon_And_Parametric_Angle_Agree()
    {
        var shape = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeAngle), CalcInputValue.Quantity("h", 150, "mm"), CalcInputValue.Quantity("b", 90, "mm"), CalcInputValue.Quantity("t", 12, "mm"), CalcInputValue.Quantity("r", 0, "mm"));
        var poly = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapePolygon), SectionPropertiesCalculator.Polygon(("mm", [(0, 0), (90, 0), (90, 12), (12, 12), (12, 150), (0, 150)])));

        foreach (var symbol in new[] { "A", "x_c", "y_c", "I_xx", "I_yy", "I_xy", "I_1", "I_2", "theta", "W_pl_x" })
            Assert.Equal(shape.OutputSi(symbol), poly.OutputSi(symbol), Math.Abs(shape.OutputSi(symbol)) * 1e-9 + 1e-15);

        Assert.Contains(poly.Notices, n => n.RuleId == "no-torsion");
        Assert.Null(poly.FindOutput("J"));
        Assert.NotNull(shape.FindOutput("J"));
    }

    [Fact]
    public void Principal_Axes_Of_An_Angle_Are_Rotated()
    {
        var r = Run(CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapeAngle), CalcInputValue.Quantity("h", 100, "mm"), CalcInputValue.Quantity("b", 100, "mm"), CalcInputValue.Quantity("t", 10, "mm"), CalcInputValue.Quantity("r", 0, "mm"));
        var theta = r.OutputSi("theta");
        Assert.Equal(Math.PI / 4, Math.Abs(theta), 6);
        Assert.True(r.OutputSi("I_1") > r.OutputSi("I_xx"));
        Assert.True(r.OutputSi("I_2") < r.OutputSi("I_yy"));
    }

    [Fact]
    public void Vertex_Cap_Is_Enforced()
    {
        var many = Enumerable.Range(0, SectionLoop.MaxVertices + 1).Select(i => new Point2(Math.Cos(i * 1e-4), Math.Sin(i * 1e-4))).ToList();
        Assert.Throws<ArgumentException>(() => new SectionLoop(many, false));
    }

    [Fact]
    public void Torsion_Of_A_Square_Matches_The_Tabulated_Factor()
    {
        // Roark Table 10.1: K = 0.1406 a⁴ for a square.
        Assert.Equal(0.1406, Torsion.Rectangle(1, 1), 4);
        Assert.Equal(Torsion.Rectangle(2, 1), Torsion.Rectangle(1, 2), 15);
    }
}
