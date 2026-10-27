using Tempest.Calc.Engine.Units;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Calculations.Modules.Diagrams;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Shot-fix C5: three reference diagrams drew a fixed, representative pattern whatever the table held (six bolts for a four-bolt
/// example; two weld lines for a C-weld of three; a pentagon for a box girder with a void). Where the table has rows these are
/// now drawn from them: the bolt positions, the weld segments, the polygon loops with their holes. The shape is fitted into the
/// representative drawing's own box at one scale for x and y (y up, as entered), capped at the template's row limit. With no
/// usable row the representative drawing stays.
/// </summary>
public static partial class CalcDiagrams
{
    /// <summary>The variant as drawn for these inputs: <paramref name="variant"/> itself, or its geometry redrawn from the rows of the table it stands for.</summary>
    public static DiagramVariant Redraw(CalcTemplate template, CalcInputSet inputs, DiagramVariant variant)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(variant);

        return template.Id switch
        {
            BoltGroupCalculator.Id => RedrawBolts(template, inputs, variant) ?? variant,
            WeldGroupCalculator.Id => RedrawWelds(template, inputs, variant) ?? variant,
            SectionPropertiesCalculator.Id when variant.When.TryGetValue("shape", out var shape) && shape == SectionPropertiesCalculator.ShapePolygon
                => RedrawPolygon(template, inputs, variant) ?? variant,
            _ => variant,
        };
    }

    /// <summary>The box each redrawn shape is fitted into: where the representative drawing stood.</summary>
    private readonly record struct Box(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;
    }

    private static readonly Box BoltBox = new(84, 54, 186, 158);
    private static readonly Box WeldBox = new(70, 58, 200, 168);
    private static readonly Box PolygonBox = new(120, 44, 260, 192);

    private static DiagramVariant? RedrawBolts(CalcTemplate template, CalcInputSet inputs, DiagramVariant variant)
    {
        var points = Rows(template, inputs, "bolts").Select(r => (X: Mm(template, "bolts", r, "x"), Y: Mm(template, "bolts", r, "y")))
            .Where(p => p.X is not null && p.Y is not null).Select(p => (X: p.X!.Value, Y: p.Y!.Value)).ToList();

        if (points.Count == 0)
            return null;

        if (Fit(BoltBox, points) is not { } map)
            return null;

        var drawn = points.Select(p => map(p.X, p.Y)).ToList();
        var radius = Math.Clamp(0.35 * NearestSpacing(drawn), 2, 8);
        var elements = new List<DiagramElement>();

        foreach (var e in variant.Elements)
        {
            if (e.Id == "plate")
            {
                var (minX, minY, maxX, maxY) = Bounds(drawn);
                var pad = radius + 10;
                elements.Add(new DiagramPlate("plate", minX - pad, minY - pad, maxX - minX + 2 * pad, maxY - minY + 2 * pad));
                elements.AddRange(drawn.Select((p, i) => i == 0
                    ? new DiagramCircle("bolt-1", p, radius, Filled: true, "bolts", "bolts", new(135, 26), Solid: true)
                    : new DiagramCircle($"bolt-{i + 1}", p, radius, Filled: true, "bolts", Solid: true)));
                continue;
            }

            if (e.Id.StartsWith("bolt-", StringComparison.Ordinal))
                continue;

            elements.Add(e);
        }

        var caption = $"A bolt group ({Count(points.Count, "bolt")} as listed, placed to one scale) under an in-plane load at (e_x, e_y), x and y from one origin, + as drawn";
        return new DiagramVariant(caption, variant.When, elements);
    }

    private static DiagramVariant? RedrawWelds(CalcTemplate template, CalcInputSet inputs, DiagramVariant variant)
    {
        var lines = Rows(template, inputs, "welds")
            .Select(r => (X1: Mm(template, "welds", r, "x1"), Y1: Mm(template, "welds", r, "y1"), X2: Mm(template, "welds", r, "x2"), Y2: Mm(template, "welds", r, "y2")))
            .Where(l => l.X1 is not null && l.Y1 is not null && l.X2 is not null && l.Y2 is not null && (l.X1 != l.X2 || l.Y1 != l.Y2))
            .Select(l => (X1: l.X1!.Value, Y1: l.Y1!.Value, X2: l.X2!.Value, Y2: l.Y2!.Value)).ToList();

        if (lines.Count == 0)
            return null;

        if (Fit(WeldBox, [.. lines.SelectMany(l => new[] { (l.X1, l.Y1), (l.X2, l.Y2) })]) is not { } map)
            return null;

        var elements = new List<DiagramElement>();
        var placed = false;

        foreach (var e in variant.Elements)
        {
            if (e.Id.StartsWith("weld-", StringComparison.Ordinal))
            {
                if (!placed)
                {
                    elements.AddRange(lines.Select((l, i) => i == 0
                        ? new DiagramMember("weld-1", map(l.X1, l.Y1), map(l.X2, l.Y2), 6, "welds", "welds", new(135, 44))
                        : new DiagramMember($"weld-{i + 1}", map(l.X1, l.Y1), map(l.X2, l.Y2), 6, "welds")));
                    placed = true;
                }

                continue;
            }

            elements.Add(e);
        }

        var caption = $"A fillet-weld group in plan ({Count(lines.Count, "weld line")} as listed, placed to one scale), x and y from one origin, under forces and moments at (e_x, e_y), + as drawn; beside it, the weld section with leg s";
        return new DiagramVariant(caption, variant.When, elements);
    }

    private static DiagramVariant? RedrawPolygon(CalcTemplate template, CalcInputSet inputs, DiagramVariant variant)
    {
        var vertices = Rows(template, inputs, "vertices")
            .Select(r => (X: Mm(template, "vertices", r, "x"), Y: Mm(template, "vertices", r, "y"), Loop: r.Get("loop")?.Number is { } n && double.IsFinite(n) ? (int)Math.Round(n) : 1))
            .Where(v => v.X is not null && v.Y is not null).Select(v => (X: v.X!.Value, Y: v.Y!.Value, v.Loop)).ToList();
        var loops = vertices.GroupBy(v => v.Loop).OrderBy(g => g.Key).Select(g => g.Select(v => (v.X, v.Y)).ToList()).Where(l => l.Count >= 3).ToList();

        if (loops.Count == 0)
            return null;

        if (Fit(PolygonBox, [.. loops.SelectMany(l => l)]) is not { } map)
            return null;

        var elements = new List<DiagramElement>();

        for (var i = 0; i < loops.Count; i++)
        {
            var path = loops[i].Select(p => map(p.X, p.Y)).ToList();
            path.Add(path[0]);
            elements.Add(i == 0
                ? new DiagramPolyline("outline", path, InputName: "vertices", Symbol: "vertices", LabelAt: new(190, 214))
                : new DiagramPolyline($"hole-{i}", path, Thickness: 1.5, InputName: "vertices"));
        }

        // A marker on every vertex while they stay legible; a dense outline is drawn as its line alone.
        var count = loops.Sum(l => l.Count);

        if (count <= 40)
            elements.AddRange(loops.SelectMany(l => l).Select((p, i) => new DiagramCircle($"vertex-{i + 1}", map(p.X, p.Y), 3, Filled: true, "vertices", Solid: true)));

        elements.AddRange(variant.Elements.Where(e => e is DiagramLabel { Id: "shape" }));
        var caption = loops.Count == 1
            ? $"A polygon through the {count} vertices as listed, drawn to one scale"
            : $"A polygon of {count} vertices as listed: the outline and {Count(loops.Count - 1, "hole")}, drawn to one scale";
        return new DiagramVariant(caption, variant.When, elements);
    }

    /// <summary>The table's rows, at most the template's row limit for it.</summary>
    private static IReadOnlyList<CalcRow> Rows(CalcTemplate template, CalcInputSet inputs, string symbol) =>
        inputs.Find(symbol)?.Rows is { } rows && template.FindInput(symbol) is { } spec
            ? [.. rows.Where(r => r is not null).Take(spec.MaxRows)]
            : [];

    /// <summary>A length cell in mm (its own unit, else the column's), or <see langword="null"/> where it is blank, not finite or not a length.</summary>
    private static double? Mm(CalcTemplate template, string table, CalcRow row, string column)
    {
        if (row.Get(column) is not { Number: { } n } cell || !double.IsFinite(n))
            return null;

        var unitText = cell.Unit is { Length: > 0 } u ? u : template.FindInput(table)?.Columns?.FirstOrDefault(c => c.Key == column)?.DefaultUnit ?? "mm";

        if (!UnitCatalog.TryParse(unitText, out var unit, out _))
            return null;

        var mm = unit.ToSi(n) * 1e3;
        return double.IsFinite(mm) ? mm : null;
    }

    /// <summary>A span shorter than this (mm) is drawn as none: the points coincide on the drawing, and a scale over it would not be finite.</summary>
    private const double SmallestSpanMm = 1e-9;

    /// <summary>
    /// A map from model coordinates (x right, y up) into <paramref name="box"/> (y down), one scale for both, centred; or <see langword="null"/>
    /// when the points cannot be drawn to a finite scale (a span past the largest double), and the representative drawing stays. The centre is
    /// taken as min + span/2, which cannot overflow, and a span below <see cref="SmallestSpanMm"/> counts as none, so every mapped point is finite
    /// (review item 8: 1.7e308 mm gave an infinite centre, and a 1e-310 mm span an infinite scale).
    /// </summary>
    private static Func<double, double, DiagramPoint>? Fit(Box box, IReadOnlyList<(double X, double Y)> points)
    {
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var spanX = maxX - minX;
        var spanY = maxY - minY;

        if (!double.IsFinite(spanX) || !double.IsFinite(spanY))
            return null;

        spanX = spanX < SmallestSpanMm ? 0 : spanX;
        spanY = spanY < SmallestSpanMm ? 0 : spanY;
        var scale = (spanX > 0, spanY > 0) switch
        {
            (true, true) => Math.Min(box.Width / spanX, box.Height / spanY),
            (true, false) => box.Width / spanX,
            (false, true) => box.Height / spanY,
            _ => 1,
        };
        var cx = (box.Left + box.Right) / 2;
        var cy = (box.Top + box.Bottom) / 2;
        var mx = minX + spanX / 2;
        var my = minY + spanY / 2;

        if (!double.IsFinite(scale) || scale <= 0)
            return null;

        return (x, y) => new DiagramPoint(cx + (x - mx) * scale, cy - (y - my) * scale);
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(IReadOnlyList<DiagramPoint> points) =>
        (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));

    /// <summary>The smallest distance between two drawn points (the box size where there is one point or all coincide).</summary>
    private static double NearestSpacing(IReadOnlyList<DiagramPoint> points)
    {
        var best = double.PositiveInfinity;

        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                var d = Math.Sqrt(Math.Pow(points[i].X - points[j].X, 2) + Math.Pow(points[i].Y - points[j].Y, 2));

                if (d > 0 && d < best)
                    best = d;
            }
        }

        return double.IsFinite(best) ? best : 30;
    }

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
}
