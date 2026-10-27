using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Shot-fix C5: the reference diagram (and Figure 1 of the issued PDF, which draws the same reading) follows the table it stands for.
/// The bolt group drew six bolts for a four-bolt example, the weld group two lines for a C-weld of three, and the polygon section a generic
/// pentagon for a box girder with a void. Each now draws its rows at one scale; with no rows the representative drawing stays.
/// </summary>
public sealed class CalcDiagramGeometryTests
{
    private static CalcTemplate Template(string id) => CalcCalculatorComposition.All().Single(c => c.Template.Id == id).Template;

    private static CalcInputSet Example(string discipline, string id, string slug)
    {
        var path = Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples", discipline, id, slug, "inputs.json");
        var (inputs, notices) = CalcInputJson.Read(File.ReadAllText(path));
        Assert.Empty(notices);
        return inputs;
    }

    private static CalculationDiagramReading Read(CalcTemplate template, CalcInputSet inputs) => CalcDiagramReader.Read(template, inputs)!;

    /// <summary>The drawn points are the model points (x right, y up) at one scale and a shift, y turned down: the shape is the shape entered.</summary>
    private static void AssertSameShape(IReadOnlyList<(double X, double Y)> model, IReadOnlyList<DiagramPoint> drawn)
    {
        Assert.Equal(model.Count, drawn.Count);
        double? scale = null;

        for (var i = 1; i < model.Count; i++)
        {
            var mx = model[i].X - model[0].X;
            var my = model[i].Y - model[0].Y;
            var dx = drawn[i].X - drawn[0].X;
            var dy = drawn[i].Y - drawn[0].Y;
            var length = Math.Sqrt(mx * mx + my * my);

            if (length == 0)
            {
                Assert.Equal(0, Math.Sqrt(dx * dx + dy * dy), 9);
                continue;
            }

            scale ??= Math.Sqrt(dx * dx + dy * dy) / length;
            Assert.True(scale > 0);
            Assert.Equal(mx * scale.Value, dx, 6);
            Assert.Equal(-my * scale.Value, dy, 6);
        }
    }

    private static List<(double X, double Y)> Cells(CalcInputSet inputs, string table, string x, string y) =>
        [.. inputs.Find(table)!.Rows!.Select(r => (r.Get(x)!.Number!.Value, r.Get(y)!.Number!.Value))];

    [Theory]
    [InlineData("four-bolt-bracket")]
    [InlineData("end-plate-moment")]
    public void The_Bolt_Group_Draws_The_Bolts_As_Listed(string slug)
    {
        var template = Template(BoltGroupCalculator.Id);
        var inputs = Example("mechanical", BoltGroupCalculator.Id, slug);
        var reading = Read(template, inputs);
        var bolts = reading.Shapes.Select(s => s.Element).OfType<DiagramCircle>().Where(c => c.Id.StartsWith("bolt-", StringComparison.Ordinal)).ToList();

        var model = Cells(inputs, "bolts", "x", "y");
        Assert.Equal(4, model.Count);
        AssertSameShape(model, [.. bolts.Select(b => b.Centre)]);
        Assert.DoesNotContain("six drawn", reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains("4 bolts as listed", reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("bolts = 4 rows", reading.LabelFor("bolts"));

        // The plate is drawn round the bolts, and every bolt is inside it.
        var plate = reading.Shapes.Select(s => s.Element).OfType<DiagramPlate>().Single(p => p.Id == "plate");
        Assert.All(bolts, b => Assert.InRange(b.Centre.X, plate.X, plate.X + plate.Width));
        Assert.All(bolts, b => Assert.InRange(b.Centre.Y, plate.Y, plate.Y + plate.Height));
    }

    [Fact]
    public void The_Weld_Group_Draws_The_Weld_Segments_As_Listed()
    {
        var template = Template(WeldGroupCalculator.Id);
        var inputs = Example("mechanical", WeldGroupCalculator.Id, "bracket-c-weld");
        var reading = Read(template, inputs);
        var welds = reading.Shapes.Select(s => s.Element).OfType<DiagramMember>().Where(m => m.Id.StartsWith("weld-", StringComparison.Ordinal)).ToList();

        Assert.Equal(3, welds.Count);
        var model = inputs.Find("welds")!.Rows!.SelectMany(r => new[] { (r.Get("x1")!.Number!.Value, r.Get("y1")!.Number!.Value), (r.Get("x2")!.Number!.Value, r.Get("y2")!.Number!.Value) }).ToList();
        AssertSameShape(model, [.. welds.SelectMany(w => new[] { w.From, w.To })]);
        Assert.Contains("3 weld lines as listed", reading.Variant.Caption, StringComparison.Ordinal);
        Assert.DoesNotContain("two lines drawn", reading.Variant.Caption, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Polygon_Section_Draws_Its_Outline_And_Its_Void()
    {
        var template = Template(SectionPropertiesCalculator.Id);
        var inputs = Example("structural", SectionPropertiesCalculator.Id, "fabricated-box-girder");
        var reading = Read(template, inputs);
        var loops = reading.Shapes.Select(s => s.Element).OfType<DiagramPolyline>().ToList();

        Assert.Equal(["outline", "hole-1"], loops.Select(l => l.Id));
        var rows = inputs.Find("vertices")!.Rows!;
        var model = rows.GroupBy(r => r.Get("loop")?.Number ?? 1).OrderBy(g => g.Key).Select(g => g.Select(r => (r.Get("x")!.Number!.Value, r.Get("y")!.Number!.Value)).ToList()).ToList();
        Assert.Equal(2, model.Count);

        // Each loop is closed and the two loops share one scale and one origin.
        Assert.All(loops, l => Assert.Equal(l.Points[0], l.Points[^1]));
        AssertSameShape([.. model[0], .. model[1]], [.. loops[0].Points.SkipLast(1), .. loops[1].Points.SkipLast(1)]);
        Assert.DoesNotContain("representatively", reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains("the outline and 1 hole", reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("vertices = 8 rows", reading.LabelFor("vertices"));
    }

    [Fact]
    public void With_No_Rows_The_Representative_Drawing_Stays()
    {
        var bolts = Read(Template(BoltGroupCalculator.Id), new CalcInputSet([CalcInputValue.Quantity("F_y", 10, "kN")]));
        Assert.Equal(6, bolts.Shapes.Count(s => s.Element.Id.StartsWith("bolt-", StringComparison.Ordinal)));
        Assert.Contains("six drawn", bolts.Variant.Caption, StringComparison.Ordinal);

        var welds = Read(Template(WeldGroupCalculator.Id), new CalcInputSet([CalcInputValue.Table("welds", [])]));
        Assert.Equal(2, welds.Shapes.Count(s => s.Element.Id.StartsWith("weld-", StringComparison.Ordinal)));

        var polygon = Read(Template(SectionPropertiesCalculator.Id), new CalcInputSet([CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapePolygon)]));
        Assert.Contains("representatively", polygon.Variant.Caption, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Table_Longer_Than_The_Row_Limit_Draws_No_More_Than_The_Limit()
    {
        var template = Template(BoltGroupCalculator.Id);
        var limit = template.FindInput("bolts")!.MaxRows;
        var rows = Enumerable.Range(0, limit + 25).Select(i => CalcRow.Of(("x", new CalcCell(i % 20 * 50.0, "mm")), ("y", new CalcCell(i / 20 * 50.0, "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList();
        var reading = Read(template, new CalcInputSet([CalcInputValue.Table("bolts", rows)]));
        Assert.Equal(limit, reading.Shapes.Count(s => s.Element.Id.StartsWith("bolt-", StringComparison.Ordinal)));
    }
}
