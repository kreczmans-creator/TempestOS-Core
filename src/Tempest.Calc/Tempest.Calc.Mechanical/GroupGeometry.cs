namespace Tempest.Calc.Mechanical;

/// <summary>
/// Size, centroid and second moments of a planar group about its centroid (any consistent units). <paramref name="Ixy"/> is the
/// product moment Σ w x′y′, which is zero only for a group symmetric about a centroidal axis.
/// </summary>
public readonly record struct GroupProperties(double Size, double Xc, double Yc, double Ix, double Iy, double Ixy)
{
    /// <summary>Polar second moment about the centroid.</summary>
    public double J => Ix + Iy;

    /// <summary>Ix·Iy − Ixy²: zero when every element of the group lies on one line through the centroid.</summary>
    public double Determinant => Ix * Iy - Ixy * Ixy;

    /// <summary>Whether the group lies on one line, so it has no stiffness for bending about that line.</summary>
    public bool IsCollinear => !(J > 0) || Determinant <= 1e-9 * J * J;

    /// <summary>
    /// The out-of-plane intensity at (x′, y′) from the centroid for moments <paramref name="mx"/> (tension on +y) and <paramref name="my"/> (tension on +x):
    /// f = [(M_x I_y − M_y I_xy)·y′ + (M_y I_x − M_x I_xy)·x′] / (I_x I_y − I_xy²), the unsymmetric-bending form. For a collinear group
    /// only the moment component that bends the line itself is resisted; <see langword="null"/> when the moment has a component
    /// about the line (nothing resists it).
    /// </summary>
    public double? Bending(double mx, double my, double xp, double yp)
    {
        if (mx == 0 && my == 0)
            return 0;

        if (!IsCollinear)
            return ((mx * Iy - my * Ixy) * yp + (my * Ix - mx * Ixy) * xp) / Determinant;

        if (!(J > 0))
            return null;

        // All the inertia lies along one direction u = (ux, uy): Ix = uy²J, Iy = ux²J, Ixy = ux·uy·J.
        var ux = Math.Sqrt(Iy / J);
        var uy = (Ixy < 0 ? -1 : 1) * Math.Sqrt(Ix / J);
        var unresisted = ux * mx - uy * my;

        if (Math.Abs(unresisted) > 1e-6 * (Math.Abs(mx) + Math.Abs(my)))
            return null;

        return (uy * mx + ux * my) / J * (xp * ux + yp * uy);
    }
}

/// <summary>
/// The group-geometry kernel shared by the bolt group (M3, weighted points) and the weld group (M6, lines of unit
/// throat): centroid and second moments about it, for the elastic distribution of direct, torsional and bending effects.
/// </summary>
public static class GroupGeometry
{
    /// <summary>Points with a weight each (bolt areas): I_x = Σ w (y − y_c)², I_y = Σ w (x − x_c)², I_xy = Σ w (x − x_c)(y − y_c).</summary>
    public static GroupProperties OfPoints(IReadOnlyList<(double X, double Y, double W)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var size = points.Sum(p => p.W);
        var xc = points.Sum(p => p.W * p.X) / size;
        var yc = points.Sum(p => p.W * p.Y) / size;
        return new(size, xc, yc, points.Sum(p => p.W * (p.Y - yc) * (p.Y - yc)), points.Sum(p => p.W * (p.X - xc) * (p.X - xc)), points.Sum(p => p.W * (p.X - xc) * (p.Y - yc)));
    }

    /// <summary>Straight lines of unit width (weld throats): I_x = Σ [l (y_m − y_c)² + l Δy²/12], I_y likewise with x, I_xy = Σ [l (x_m − x_c)(y_m − y_c) + l ΔxΔy/12].</summary>
    public static GroupProperties OfLines(IReadOnlyList<(double X1, double Y1, double X2, double Y2)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var parts = lines.Select(l => (L: Math.Sqrt((l.X2 - l.X1) * (l.X2 - l.X1) + (l.Y2 - l.Y1) * (l.Y2 - l.Y1)), Xm: (l.X1 + l.X2) / 2, Ym: (l.Y1 + l.Y2) / 2, Dx: l.X2 - l.X1, Dy: l.Y2 - l.Y1)).ToList();
        var size = parts.Sum(p => p.L);
        var xc = parts.Sum(p => p.L * p.Xm) / size;
        var yc = parts.Sum(p => p.L * p.Ym) / size;
        return new(size, xc, yc,
            parts.Sum(p => p.L * (p.Ym - yc) * (p.Ym - yc) + p.L * p.Dy * p.Dy / 12),
            parts.Sum(p => p.L * (p.Xm - xc) * (p.Xm - xc) + p.L * p.Dx * p.Dx / 12),
            parts.Sum(p => p.L * (p.Xm - xc) * (p.Ym - yc) + p.L * p.Dx * p.Dy / 12));
    }
}
