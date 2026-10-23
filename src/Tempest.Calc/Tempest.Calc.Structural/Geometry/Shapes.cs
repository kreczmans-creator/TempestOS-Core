namespace Tempest.Calc.Structural.Geometry;

/// <summary>Builds the outline loops of the parametric shapes (SI metres). Fillets and corner radii are discretised arcs; a full circle uses <see cref="CircleSegments"/> segments.</summary>
public static class Shapes
{
    public const int CircleSegments = 360;
    public const int ArcSegmentsPerQuarter = 24;

    public static IReadOnlyList<SectionLoop> Rectangle(double b, double h) =>
        [new SectionLoop([new(-b / 2, -h / 2), new(b / 2, -h / 2), new(b / 2, h / 2), new(-b / 2, h / 2)], false)];

    public static IReadOnlyList<SectionLoop> Circle(double d) => [new SectionLoop(CirclePoints(d / 2), false)];

    public static IReadOnlyList<SectionLoop> Tube(double d, double t) =>
        [new SectionLoop(CirclePoints(d / 2), false), new SectionLoop(CirclePoints(d / 2 - t), true)];

    /// <summary>A hollow rectangle b × h, wall t, outer corner radius r_o and inner radius r_i (0 for sharp corners).</summary>
    public static IReadOnlyList<SectionLoop> HollowRectangle(double b, double h, double t, double ro, double ri) =>
        [new SectionLoop(RoundedRectangle(b, h, ro), false), new SectionLoop(RoundedRectangle(b - 2 * t, h - 2 * t, ri), true)];

    /// <summary>A doubly symmetric I-section, depth h, width b, web t_w, flange t_f, root radius r; centred on its centroid.</summary>
    public static IReadOnlyList<SectionLoop> ISection(double h, double b, double tw, double tf, double r)
    {
        var p = new PathBuilder();
        p.Add(-b / 2, -h / 2).Add(b / 2, -h / 2).Add(b / 2, -h / 2 + tf).Add(tw / 2 + r, -h / 2 + tf);
        p.Arc(tw / 2 + r, -h / 2 + tf + r, r, -Math.PI / 2, -Math.PI);
        p.Add(tw / 2, h / 2 - tf - r);
        p.Arc(tw / 2 + r, h / 2 - tf - r, r, Math.PI, Math.PI / 2);
        p.Add(b / 2, h / 2 - tf).Add(b / 2, h / 2).Add(-b / 2, h / 2).Add(-b / 2, h / 2 - tf).Add(-tw / 2 - r, h / 2 - tf);
        p.Arc(-tw / 2 - r, h / 2 - tf - r, r, Math.PI / 2, 0);
        p.Add(-tw / 2, -h / 2 + tf + r);
        p.Arc(-tw / 2 - r, -h / 2 + tf + r, r, 0, -Math.PI / 2);
        p.Add(-b / 2, -h / 2 + tf);
        return [new SectionLoop(p.Points, false)];
    }

    /// <summary>A channel with its web on the left (x = 0 … t_w), flanges to the right, depth h, width b, root radius r.</summary>
    public static IReadOnlyList<SectionLoop> Channel(double h, double b, double tw, double tf, double r)
    {
        var p = new PathBuilder();
        p.Add(0, 0).Add(b, 0).Add(b, tf).Add(tw + r, tf);
        p.Arc(tw + r, tf + r, r, -Math.PI / 2, -Math.PI);
        p.Add(tw, h - tf - r);
        p.Arc(tw + r, h - tf - r, r, Math.PI, Math.PI / 2);
        p.Add(b, h - tf).Add(b, h).Add(0, h);
        return [new SectionLoop(p.Points, false)];
    }

    /// <summary>A tee with its flange on top (y = h − t_f … h) and its stem down to y = 0, centred on x = 0.</summary>
    public static IReadOnlyList<SectionLoop> Tee(double h, double b, double tw, double tf, double r)
    {
        var p = new PathBuilder();
        p.Add(-tw / 2, 0).Add(tw / 2, 0).Add(tw / 2, h - tf - r);
        p.Arc(tw / 2 + r, h - tf - r, r, Math.PI, Math.PI / 2);
        p.Add(b / 2, h - tf).Add(b / 2, h).Add(-b / 2, h).Add(-b / 2, h - tf).Add(-tw / 2 - r, h - tf);
        p.Arc(-tw / 2 - r, h - tf - r, r, Math.PI / 2, 0);
        p.Add(-tw / 2, h - tf - r);
        return [new SectionLoop(p.Points, false)];
    }

    /// <summary>An angle with its vertical leg of length h along x = 0 … t and its horizontal leg of length b along y = 0 … t, root radius r; the heel at the origin.</summary>
    public static IReadOnlyList<SectionLoop> Angle(double h, double b, double t, double r)
    {
        var p = new PathBuilder();
        p.Add(0, 0).Add(b, 0).Add(b, t).Add(t + r, t);
        p.Arc(t + r, t + r, r, -Math.PI / 2, -Math.PI);
        p.Add(t, h).Add(0, h);
        return [new SectionLoop(p.Points, false)];
    }

    private static List<Point2> CirclePoints(double radius)
    {
        var pts = new List<Point2>(CircleSegments);

        for (var i = 0; i < CircleSegments; i++)
        {
            var (s, c) = Math.SinCos(2 * Math.PI * i / CircleSegments);
            pts.Add(new Point2(radius * c, radius * s));
        }

        return pts;
    }

    private static List<Point2> RoundedRectangle(double b, double h, double r)
    {
        r = Math.Max(0, Math.Min(r, Math.Min(b, h) / 2));
        var p = new PathBuilder();
        p.Add(-b / 2 + r, -h / 2).Add(b / 2 - r, -h / 2);
        p.Arc(b / 2 - r, -h / 2 + r, r, -Math.PI / 2, 0);
        p.Add(b / 2, h / 2 - r);
        p.Arc(b / 2 - r, h / 2 - r, r, 0, Math.PI / 2);
        p.Add(-b / 2 + r, h / 2);
        p.Arc(-b / 2 + r, h / 2 - r, r, Math.PI / 2, Math.PI);
        p.Add(-b / 2, -h / 2 + r);
        p.Arc(-b / 2 + r, -h / 2 + r, r, Math.PI, 3 * Math.PI / 2);
        return p.Points;
    }

    private sealed class PathBuilder
    {
        public List<Point2> Points { get; } = [];

        public PathBuilder Add(double x, double y)
        {
            var p = new Point2(x, y);

            if (Points.Count == 0 || Distance(Points[^1], p) > 1e-12)
                Points.Add(p);

            return this;
        }

        /// <summary>Points on a circular arc from angle a0 to a1 (radians, either direction), including both ends; nothing when the radius is zero.</summary>
        public PathBuilder Arc(double cx, double cy, double r, double a0, double a1)
        {
            if (r <= 0)
                return this;

            var n = Math.Max(1, (int)Math.Ceiling(Math.Abs(a1 - a0) / (Math.PI / 2) * ArcSegmentsPerQuarter));

            for (var i = 0; i <= n; i++)
            {
                var a = a0 + (a1 - a0) * i / n;
                var (s, c) = Math.SinCos(a);
                Add(cx + r * c, cy + r * s);
            }

            return this;
        }

        private static double Distance(Point2 a, Point2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}

/// <summary>Torsion constants: exact for solid rectangles (series), circles and tubes; thin-walled for closed and open sections.</summary>
public static class Torsion
{
    private const int SeriesTerms = 25;

    /// <summary>Solid rectangle a × b by the Saint-Venant series (a ≥ b after ordering).</summary>
    public static double Rectangle(double a, double b)
    {
        if (b > a)
            (a, b) = (b, a);

        double sum = 0;

        for (var n = 1; n < 2 * SeriesTerms; n += 2)
            sum += Math.Tanh(n * Math.PI * a / (2 * b)) / Math.Pow(n, 5);

        return a * b * b * b / 3 * (1 - 192 / Math.Pow(Math.PI, 5) * (b / a) * sum);
    }

    public static double Circle(double d) => Math.PI * Math.Pow(d, 4) / 32;

    public static double Tube(double d, double t) => Math.PI * (Math.Pow(d, 4) - Math.Pow(d - 2 * t, 4)) / 32;

    /// <summary>Closed thin-walled single cell: Bredt's 4 A_m² t / p_m plus the wall's own p_m t³ / 3, with the mid-line enclosed area and perimeter.</summary>
    public static double ClosedThinWalled(double areaMid, double perimeterMid, double t) =>
        4 * areaMid * areaMid * t / perimeterMid + perimeterMid * t * t * t / 3;

    /// <summary>Open thin-walled: Σ b_i t_i³ / 3 over the plates.</summary>
    public static double OpenThinWalled(IEnumerable<(double Length, double Thickness)> plates) =>
        plates.Sum(p => p.Length * Math.Pow(p.Thickness, 3) / 3);

    /// <summary>
    /// Rolled I-section with root fillets: the open thin-walled sum plus the
    /// El Darwish–Johnston junction term and the flange-tip correction
    /// (AISC Design Guide 9, Appendix; Seaburg &amp; Carter).
    /// </summary>
    public static double ISection(double h, double b, double tw, double tf, double r)
    {
        var open = (2 * b * tf * tf * tf + (h - 2 * tf) * tw * tw * tw) / 3;

        if (r <= 0)
            return open;

        var alpha = -0.042 + 0.2204 * (tw / tf) + 0.1355 * (r / tf) - 0.0865 * (tw * r / (tf * tf)) - 0.0725 * (tw / tf) * (tw / tf);
        var d = ((tf + r) * (tf + r) + tw * (r + tw / 4)) / (2 * r + tf);
        return open + 2 * alpha * Math.Pow(d, 4) - 0.420 * Math.Pow(tf, 4);
    }
}
