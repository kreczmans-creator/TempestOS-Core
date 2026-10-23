namespace Tempest.Calc.Structural.Geometry;

/// <summary>A point in the section plane (SI metres).</summary>
public readonly record struct Point2(double X, double Y);

/// <summary>One closed loop of a section outline: solid (+1) or hole (−1). Vertices are stored counter-clockwise for a solid and clockwise for a hole, so the signed Green's-theorem integrals carry the sign.</summary>
public sealed class SectionLoop
{
    public const int MaxVertices = 20_000;

    public SectionLoop(IReadOnlyList<Point2> vertices, bool isHole)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Count < 3)
            throw new ArgumentException("A loop needs at least three vertices.", nameof(vertices));

        if (vertices.Count > MaxVertices)
            throw new ArgumentException($"A loop may have at most {MaxVertices} vertices.", nameof(vertices));

        var list = vertices.ToList();

        if (list.Count > 1 && list[0] == list[^1])
            list.RemoveAt(list.Count - 1);

        var signed = SignedArea(list);

        if (signed == 0)
            throw new ArgumentException("A loop encloses no area.", nameof(vertices));

        if ((signed > 0) == isHole)
            list.Reverse();

        Vertices = list;
        IsHole = isHole;
    }

    public IReadOnlyList<Point2> Vertices { get; }

    public bool IsHole { get; }

    public static double SignedArea(IReadOnlyList<Point2> p)
    {
        double a = 0;

        for (var i = 0; i < p.Count; i++)
        {
            var q = p[(i + 1) % p.Count];
            a += p[i].X * q.Y - q.X * p[i].Y;
        }

        return a / 2;
    }

    /// <summary>The same loop moved and rotated (radians, counter-clockwise).</summary>
    public SectionLoop Transform(double dx, double dy, double angle)
    {
        var (s, c) = Math.SinCos(angle);
        return new SectionLoop(Vertices.Select(p => new Point2(c * p.X - s * p.Y + dx, s * p.X + c * p.Y + dy)).ToList(), IsHole);
    }

    /// <summary>The loop with x and y swapped (used to compute y-axis quantities with x-axis code). Orientation flips, so the sign is restored by the constructor.</summary>
    public SectionLoop Swapped() => new(Vertices.Select(p => new Point2(p.Y, p.X)).ToList(), IsHole);

    /// <summary>The part of the loop on the side y ≥ y0 (Sutherland–Hodgman against one half-plane). Empty when nothing is left.</summary>
    public IReadOnlyList<Point2> ClipAbove(double y0)
    {
        var output = new List<Point2>();

        for (var i = 0; i < Vertices.Count; i++)
        {
            var cur = Vertices[i];
            var prev = Vertices[(i + Vertices.Count - 1) % Vertices.Count];
            var curIn = cur.Y >= y0;
            var prevIn = prev.Y >= y0;

            if (curIn)
            {
                if (!prevIn)
                    output.Add(Intersect(prev, cur, y0));

                output.Add(cur);
            }
            else if (prevIn)
            {
                output.Add(Intersect(prev, cur, y0));
            }
        }

        return output;
    }

    private static Point2 Intersect(Point2 a, Point2 b, double y0)
    {
        var t = (y0 - a.Y) / (b.Y - a.Y);
        return new Point2(a.X + t * (b.X - a.X), y0);
    }
}

/// <summary>Green's-theorem integrals of one polygon about the origin: signed area, first moments and second moments.</summary>
public readonly record struct PolygonIntegrals(double A, double Qx, double Qy, double Ixx, double Iyy, double Ixy)
{
    public static PolygonIntegrals Of(IReadOnlyList<Point2> p)
    {
        double a = 0, qx = 0, qy = 0, ixx = 0, iyy = 0, ixy = 0;

        for (var i = 0; i < p.Count; i++)
        {
            var (x0, y0) = p[i];
            var (x1, y1) = p[(i + 1) % p.Count];
            var cross = x0 * y1 - x1 * y0;
            a += cross;
            qy += (x0 + x1) * cross;
            qx += (y0 + y1) * cross;
            iyy += (x0 * x0 + x0 * x1 + x1 * x1) * cross;
            ixx += (y0 * y0 + y0 * y1 + y1 * y1) * cross;
            ixy += (x0 * y1 + 2 * x0 * y0 + 2 * x1 * y1 + x1 * y0) * cross;
        }

        return new PolygonIntegrals(a / 2, qx / 6, qy / 6, ixx / 12, iyy / 12, ixy / 24);
    }

    public static PolygonIntegrals operator +(PolygonIntegrals l, PolygonIntegrals r) => new(l.A + r.A, l.Qx + r.Qx, l.Qy + r.Qy, l.Ixx + r.Ixx, l.Iyy + r.Iyy, l.Ixy + r.Ixy);
}

/// <summary>The geometric properties of a section made of loops (SI). I values are about the centroidal axes; Ixx = ∫y² dA, Iyy = ∫x² dA, Ixy = ∫xy dA.</summary>
public sealed record SectionProperties(
    double A, double Xc, double Yc,
    double Ixx, double Iyy, double Ixy,
    double I1, double I2, double Theta,
    double YTop, double YBottom, double XRight, double XLeft,
    double WplX, double WplY, double YPna, double XPna,
    int VertexCount)
{
    public double WelXTop => Ixx / YTop;

    public double WelXBottom => Ixx / YBottom;

    public double WelX => Math.Min(WelXTop, WelXBottom);

    public double WelY => Math.Min(Iyy / XRight, Iyy / XLeft);

    public double Ix => Math.Sqrt(Ixx / A);

    public double Iy => Math.Sqrt(Iyy / A);

    public double I1Radius => Math.Sqrt(I1 / A);

    public double I2Radius => Math.Sqrt(I2 / A);
}

/// <summary>Computes <see cref="SectionProperties"/> from loops by Green's theorem; the plastic moduli by bisection on the equal-area axis.</summary>
public static class PolygonSection
{
    public const int MaxLoops = 200;
    private const int BisectionSteps = 80;

    public static SectionProperties Compute(IReadOnlyList<SectionLoop> loops)
    {
        ArgumentNullException.ThrowIfNull(loops);

        if (loops.Count == 0 || loops.Count > MaxLoops)
            throw new ArgumentException($"A section needs 1 … {MaxLoops} loops.", nameof(loops));

        var total = loops.Aggregate(default(PolygonIntegrals), (acc, l) => acc + PolygonIntegrals.Of(l.Vertices));

        if (total.A <= 0)
            throw new ArgumentException("The section has no net area: the holes remove everything.", nameof(loops));

        var xc = total.Qy / total.A;
        var yc = total.Qx / total.A;
        var ixx = total.Ixx - total.A * yc * yc;
        var iyy = total.Iyy - total.A * xc * xc;
        var ixy = total.Ixy - total.A * xc * yc;
        var avg = (ixx + iyy) / 2;
        var radius = Math.Sqrt(((ixx - iyy) / 2) * ((ixx - iyy) / 2) + ixy * ixy);
        var theta = Math.Abs(ixy) < 1e-12 * Math.Max(ixx, iyy) && Math.Abs(ixx - iyy) < 1e-12 * Math.Max(ixx, iyy) ? 0 : 0.5 * Math.Atan2(-2 * ixy, ixx - iyy);

        var all = loops.SelectMany(l => l.Vertices).ToList();
        var yTop = all.Max(p => p.Y) - yc;
        var yBottom = yc - all.Min(p => p.Y);
        var xRight = all.Max(p => p.X) - xc;
        var xLeft = xc - all.Min(p => p.X);

        var (wplX, yPna) = PlasticModulus(loops, total.A, yc - yBottom, yc + yTop);
        var swapped = loops.Select(l => l.Swapped()).ToList();
        var (wplY, xPna) = PlasticModulus(swapped, total.A, xc - xLeft, xc + xRight);

        return new SectionProperties(total.A, xc, yc, ixx, iyy, ixy, avg + radius, avg - radius, theta,
            yTop, yBottom, xRight, xLeft, wplX, wplY, yPna, xPna, all.Count);
    }

    /// <summary>The plastic modulus about a horizontal axis: the axis that halves the area, then the first moments of the two halves about it.</summary>
    private static (double Wpl, double Axis) PlasticModulus(IReadOnlyList<SectionLoop> loops, double area, double yMin, double yMax)
    {
        double lo = yMin, hi = yMax;

        for (var i = 0; i < BisectionSteps; i++)
        {
            var mid = (lo + hi) / 2;
            var above = Above(loops, mid).A;

            if (above > area / 2)
                lo = mid;
            else
                hi = mid;
        }

        var axis = (lo + hi) / 2;
        var top = Above(loops, axis);
        var qTop = top.Qx - axis * top.A;
        var qAll = loops.Aggregate(default(PolygonIntegrals), (acc, l) => acc + PolygonIntegrals.Of(l.Vertices));
        var qBottom = (qAll.Qx - axis * qAll.A) - qTop;
        return (Math.Abs(qTop) + Math.Abs(qBottom), axis);
    }

    private static PolygonIntegrals Above(IReadOnlyList<SectionLoop> loops, double y0)
    {
        var sum = default(PolygonIntegrals);

        foreach (var loop in loops)
        {
            var clipped = loop.ClipAbove(y0);

            if (clipped.Count >= 3)
                sum += PolygonIntegrals.Of(clipped);
        }

        return sum;
    }
}
