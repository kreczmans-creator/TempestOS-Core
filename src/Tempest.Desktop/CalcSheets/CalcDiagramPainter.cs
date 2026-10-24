using SkiaSharp;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Paints a worksheet's reference diagram (ADR-0158, D-41) onto a Skia canvas for the issued sheet, in the diagram's
/// own units (<see cref="CalculationDiagramSpec.Width"/> × <see cref="CalculationDiagramSpec.Height"/>): the same shapes
/// <see cref="Tempest.Desktop.Views.CalculationDiagramView"/> draws on screen, in print colours — ink, a light plate
/// shade, loads in red, dimensions in grey. The labels are not painted here: the sheet lays them out as text runs
/// (<see cref="Labels"/>), so they are embedded, searchable and selectable like the rest of the sheet.
/// </summary>
public static class CalcDiagramPainter
{
    private const float ArrowHead = 8f;

    private static readonly SKColor Ink = DocumentTemplate.Ink900;
    private static readonly SKColor Plate = new(0xE4, 0xE7, 0xEE);
    private static readonly SKColor Load = new(0xC0, 0x39, 0x2B);
    private static readonly SKColor Dimension = new(0x6B, 0x72, 0x80);
    private static readonly SKColor Paper = SKColors.White;

    /// <summary>A label of the diagram: where it sits in the diagram's units, its text, and whether it reads from the anchor rightwards (a vertical dimension) rather than centred on it.</summary>
    public readonly record struct DiagramLabelPlacement(float X, float Y, string Text, bool LeftAligned);

    /// <summary>The labels of <paramref name="reading"/>, as the screen places them.</summary>
    public static IReadOnlyList<DiagramLabelPlacement> Labels(CalculationDiagramReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        return [.. reading.Shapes.Where(s => s.Label is not null).Select(s => new DiagramLabelPlacement(
            (float)s.Element.LabelAnchor.X, (float)s.Element.LabelAnchor.Y, s.Label!, s.Element is DiagramDimension { IsVertical: true, LabelAt: null }))];
    }

    /// <summary>Paints every shape of <paramref name="reading"/> (not its labels) in the diagram's own units.</summary>
    public static void Paint(SKCanvas canvas, CalculationDiagramReading reading)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(reading);

        foreach (var shape in reading.Shapes)
            Draw(canvas, shape.Element);
    }

    private static void Draw(SKCanvas canvas, DiagramElement element)
    {
        switch (element)
        {
            case DiagramMember m:
                Line(canvas, P(m.From), P(m.To), (float)m.Thickness, Ink, SKStrokeCap.Butt);
                break;

            case DiagramPlate p:
                Fill(canvas, SKRect.Create((float)p.X, (float)p.Y, (float)p.Width, (float)p.Height), Plate, Ink, 1.5f);
                break;

            case DiagramCircle c:
            {
                var fill = c.Solid ? Ink : c.Filled ? Plate : Paper;
                using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = fill };
                canvas.DrawCircle(P(c.Centre), (float)c.Radius, paint);
                Stroke(paint, Ink, c.Filled ? 1.5f : 2.5f);
                canvas.DrawCircle(P(c.Centre), (float)c.Radius, paint);
                break;
            }

            case DiagramSupport s:
                DrawSupport(canvas, s);
                break;

            case DiagramPointLoad l:
                Arrow(canvas, P(l.Tail), P(l.Tip), Load, 2f);
                break;

            case DiagramDistributedLoad d:
            {
                var top = (float)(d.SurfaceY - d.Height);
                Line(canvas, new SKPoint((float)d.FromX, top), new SKPoint((float)d.ToX, top), 2f, Load);
                var count = Math.Max(2, (int)Math.Round((d.ToX - d.FromX) / 35) + 1);

                for (var i = 0; i < count; i++)
                {
                    var x = (float)(d.FromX + (d.ToX - d.FromX) * i / (count - 1));
                    Arrow(canvas, new SKPoint(x, top), new SKPoint(x, (float)d.SurfaceY), Load, 1.5f);
                }

                break;
            }

            case DiagramMoment mo:
                DrawMoment(canvas, mo);
                break;

            case DiagramSpring sp:
                DrawSpring(canvas, sp);
                break;

            case DiagramDimension dim:
                DrawDimension(canvas, dim);
                break;

            case DiagramPolyline pl when pl.Points.Count >= 2:
            {
                using var paint = new SKPaint { IsAntialias = true, StrokeJoin = SKStrokeJoin.Round };
                Stroke(paint, Ink, (float)pl.Thickness);
                using var dash = pl.Dashed ? SKPathEffect.CreateDash([4f * (float)pl.Thickness, 3f * (float)pl.Thickness], 0) : null;
                paint.PathEffect = dash;
                using var path = new SKPath();
                path.AddPoly([.. pl.Points.Select(P)], close: false);
                canvas.DrawPath(path, paint);
                break;
            }

            case DiagramAxes ax:
            {
                var origin = P(ax.Origin);
                Arrow(canvas, origin, new SKPoint((float)ax.XEnd, origin.Y), Ink, 1.5f);
                Arrow(canvas, origin, new SKPoint(origin.X, (float)ax.YEnd), Ink, 1.5f);
                break;
            }
        }
    }

    private static void DrawSupport(SKCanvas canvas, DiagramSupport s)
    {
        SKPoint At(double u, double v) => s.Ground switch
        {
            DiagramDirection.Up => new SKPoint((float)(s.At.X + u), (float)(s.At.Y - v)),
            DiagramDirection.Left => new SKPoint((float)(s.At.X - v), (float)(s.At.Y + u)),
            DiagramDirection.Right => new SKPoint((float)(s.At.X + v), (float)(s.At.Y + u)),
            _ => new SKPoint((float)(s.At.X + u), (float)(s.At.Y + v)),
        };

        void Hatch(double v, double from, double to)
        {
            for (var u = from; u <= to; u += 7)
                Line(canvas, At(u, v), At(u - 5, v + 7), 1f, Ink);
        }

        switch (s.Kind)
        {
            case DiagramSupportKind.Fixed:
                Line(canvas, At(-24, 0), At(24, 0), 2.5f, Ink);
                Hatch(0, -22, 24);
                break;

            case DiagramSupportKind.Pinned:
                Polygon(canvas, [At(0, 0), At(-10, 16), At(10, 16)], Plate, Ink, 1.5f);
                Line(canvas, At(-15, 16), At(15, 16), 1.5f, Ink);
                Hatch(16, -12, 15);
                break;

            case DiagramSupportKind.Roller:
            {
                Polygon(canvas, [At(0, 0), At(-10, 12), At(10, 12)], Plate, Ink, 1.5f);
                using var paint = new SKPaint { IsAntialias = true };

                foreach (var u in new[] { -5.0, 5.0 })
                {
                    paint.Style = SKPaintStyle.Fill;
                    paint.Color = Paper;
                    canvas.DrawCircle(At(u, 15), 3f, paint);
                    Stroke(paint, Ink, 1.2f);
                    canvas.DrawCircle(At(u, 15), 3f, paint);
                }

                Line(canvas, At(-15, 18), At(15, 18), 1.5f, Ink);
                break;
            }
        }
    }

    private static void DrawMoment(SKCanvas canvas, DiagramMoment m)
    {
        // A three-quarter arc opening towards the top right, its head at the end of the sweep (as on screen).
        var start = Math.PI * 0.75;
        var sweep = Math.PI * 1.5 * (m.Clockwise ? 1 : -1);
        var end = start + sweep;
        var oval = SKRect.Create((float)(m.Centre.X - m.Radius), (float)(m.Centre.Y - m.Radius), (float)(2 * m.Radius), (float)(2 * m.Radius));
        using var paint = new SKPaint { IsAntialias = true };
        Stroke(paint, Load, 2f);
        using var path = new SKPath();
        path.AddArc(oval, (float)(start * 180 / Math.PI), (float)(sweep * 180 / Math.PI));
        canvas.DrawPath(path, paint);

        var tip = new SKPoint((float)(m.Centre.X + m.Radius * Math.Cos(end)), (float)(m.Centre.Y + m.Radius * Math.Sin(end)));
        var tangent = m.Clockwise ? new SKPoint((float)-Math.Sin(end), (float)Math.Cos(end)) : new SKPoint((float)Math.Sin(end), (float)-Math.Cos(end));
        Head(canvas, tip, tangent, Load, ArrowHead);
    }

    private static void DrawSpring(SKCanvas canvas, DiagramSpring s)
    {
        var from = P(s.From);
        var to = P(s.To);
        var along = to - from;
        var length = along.Length;

        if (length < 1)
            return;

        var unit = new SKPoint(along.X / length, along.Y / length);
        var normal = new SKPoint(-unit.Y, unit.X);
        const float Lead = 8f, Amplitude = 7f;
        const int Teeth = 6;
        var points = new List<SKPoint> { from, from + Scale(unit, Lead) };
        var zig = length - 2 * Lead;

        for (var i = 0; i < Teeth; i++)
            points.Add(from + Scale(unit, Lead + zig * (i + 0.5f) / Teeth) + Scale(normal, i % 2 == 0 ? Amplitude : -Amplitude));

        points.Add(to - Scale(unit, Lead));
        points.Add(to);

        using var paint = new SKPaint { IsAntialias = true, StrokeJoin = SKStrokeJoin.Round };
        Stroke(paint, Ink, 1.8f);
        using var path = new SKPath();
        path.AddPoly([.. points], close: false);
        canvas.DrawPath(path, paint);
    }

    private static void DrawDimension(SKCanvas canvas, DiagramDimension d)
    {
        var from = P(d.From);
        var to = P(d.To);
        var lineFrom = P(d.LineFrom);
        var lineTo = P(d.LineTo);

        if (Math.Abs(d.Offset) > 0.5)
        {
            var overshoot = Math.Sign(d.Offset) * 4f;
            var extension = d.IsVertical ? new SKPoint(overshoot, 0) : new SKPoint(0, overshoot);
            Line(canvas, from, lineFrom + extension, 0.8f, Dimension);
            Line(canvas, to, lineTo + extension, 0.8f, Dimension);
        }

        Line(canvas, lineFrom, lineTo, 1f, Dimension);
        var direction = lineTo - lineFrom;
        var length = direction.Length;

        if (length < 1)
            return;

        var unit = new SKPoint(direction.X / length, direction.Y / length);
        Head(canvas, lineTo, unit, Dimension, 6f);
        Head(canvas, lineFrom, new SKPoint(-unit.X, -unit.Y), Dimension, 6f);
    }

    private static void Arrow(SKCanvas canvas, SKPoint tail, SKPoint tip, SKColor color, float thickness)
    {
        var direction = tip - tail;
        var length = direction.Length;

        if (length < 1)
            return;

        var unit = new SKPoint(direction.X / length, direction.Y / length);
        Line(canvas, tail, tip - Scale(unit, ArrowHead * 0.8f), thickness, color);
        Head(canvas, tip, unit, color, ArrowHead);
    }

    private static void Head(SKCanvas canvas, SKPoint tip, SKPoint unit, SKColor color, float size)
    {
        var normal = new SKPoint(-unit.Y, unit.X);
        var basePoint = tip - Scale(unit, size);
        Polygon(canvas, [tip, basePoint + Scale(normal, size / 2), basePoint - Scale(normal, size / 2)], color, null, 0);
    }

    private static void Line(SKCanvas canvas, SKPoint a, SKPoint b, float thickness, SKColor color, SKStrokeCap cap = SKStrokeCap.Round)
    {
        using var paint = new SKPaint { IsAntialias = true, StrokeCap = cap };
        Stroke(paint, color, thickness);
        canvas.DrawLine(a, b, paint);
    }

    private static void Fill(SKCanvas canvas, SKRect rect, SKColor fill, SKColor stroke, float thickness)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = fill };
        canvas.DrawRect(rect, paint);
        Stroke(paint, stroke, thickness);
        canvas.DrawRect(rect, paint);
    }

    private static void Polygon(SKCanvas canvas, SKPoint[] points, SKColor fill, SKColor? stroke, float thickness)
    {
        using var path = new SKPath();
        path.AddPoly(points, close: true);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = fill };
        canvas.DrawPath(path, paint);

        if (stroke is { } s && thickness > 0)
        {
            Stroke(paint, s, thickness);
            canvas.DrawPath(path, paint);
        }
    }

    private static void Stroke(SKPaint paint, SKColor color, float thickness)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.Color = color;
        paint.StrokeWidth = thickness;
    }

    private static SKPoint Scale(SKPoint v, float by) => new(v.X * by, v.Y * by);

    private static SKPoint P(DiagramPoint point) => new((float)point.X, (float)point.Y);
}
