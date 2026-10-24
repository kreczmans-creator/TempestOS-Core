namespace Tempest.Calc.Mechanical;

/// <summary>Size, centroid and second moments of a planar group about its centroid (any consistent units).</summary>
public readonly record struct GroupProperties(double Size, double Xc, double Yc, double Ix, double Iy)
{
    /// <summary>Polar second moment about the centroid.</summary>
    public double J => Ix + Iy;
}

/// <summary>
/// The group-geometry kernel shared by the bolt group (M3, weighted points) and the weld group (M6, lines of unit
/// throat): centroid and second moments about it, for the elastic distribution of direct, torsional and bending effects.
/// </summary>
public static class GroupGeometry
{
    /// <summary>Points with a weight each (bolt areas): I_x = Σ w (y − y_c)², I_y = Σ w (x − x_c)².</summary>
    public static GroupProperties OfPoints(IReadOnlyList<(double X, double Y, double W)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var size = points.Sum(p => p.W);
        var xc = points.Sum(p => p.W * p.X) / size;
        var yc = points.Sum(p => p.W * p.Y) / size;
        return new(size, xc, yc, points.Sum(p => p.W * (p.Y - yc) * (p.Y - yc)), points.Sum(p => p.W * (p.X - xc) * (p.X - xc)));
    }

    /// <summary>Straight lines of unit width (weld throats): I_x = Σ [l (y_m − y_c)² + l Δy²/12], I_y likewise with x.</summary>
    public static GroupProperties OfLines(IReadOnlyList<(double X1, double Y1, double X2, double Y2)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var parts = lines.Select(l => (L: Math.Sqrt((l.X2 - l.X1) * (l.X2 - l.X1) + (l.Y2 - l.Y1) * (l.Y2 - l.Y1)), Xm: (l.X1 + l.X2) / 2, Ym: (l.Y1 + l.Y2) / 2, Dx: l.X2 - l.X1, Dy: l.Y2 - l.Y1)).ToList();
        var size = parts.Sum(p => p.L);
        var xc = parts.Sum(p => p.L * p.Xm) / size;
        var yc = parts.Sum(p => p.L * p.Ym) / size;
        return new(size, xc, yc,
            parts.Sum(p => p.L * (p.Ym - yc) * (p.Ym - yc) + p.L * p.Dy * p.Dy / 12),
            parts.Sum(p => p.L * (p.Xm - xc) * (p.Xm - xc) + p.L * p.Dx * p.Dx / 12));
    }
}
