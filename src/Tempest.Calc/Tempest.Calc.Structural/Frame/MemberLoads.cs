namespace Tempest.Calc.Structural.Frame;

/// <summary>
/// The loads on one member in its local axes, and the Macaulay (singularity
/// function) integrals of them: the shear, moment, slope and deflection
/// contributions of the loads alone at any x along the member. Transverse
/// loads positive towards local +y; axial loads positive towards local +x.
/// </summary>
public sealed class MemberLoads
{
    private readonly List<(double A, double B, double W1, double W2)> _transverse = [];
    private readonly List<(double A, double P)> _points = [];
    private readonly List<(double A, double M)> _couples = [];
    private readonly List<(double A, double B, double P1, double P2)> _axial = [];
    private readonly List<(double A, double P)> _axialPoints = [];

    public double Length { get; }

    public MemberLoads(double length) => Length = length;

    public bool IsEmpty => _transverse.Count == 0 && _points.Count == 0 && _couples.Count == 0 && _axial.Count == 0 && _axialPoints.Count == 0;

    /// <summary>Positions where the internal-force functions kink or jump, for station placement.</summary>
    public IEnumerable<double> Breakpoints => _transverse.SelectMany(t => new[] { t.A, t.B }).Concat(_points.Select(p => p.A)).Concat(_couples.Select(c => c.A)).Concat(_axial.SelectMany(t => new[] { t.A, t.B })).Concat(_axialPoints.Select(p => p.A));

    /// <summary>Positions of the applied couples, where the moment diagram jumps.</summary>
    public IEnumerable<double> CouplePositions => _couples.Select(c => c.A);

    public void AddTransverse(double a, double b, double w1, double w2) => _transverse.Add((a, b, w1, w2));

    public void AddPoint(double a, double p) => _points.Add((a, p));

    public void AddCouple(double a, double m) => _couples.Add((a, m));

    public void AddAxial(double a, double b, double p1, double p2) => _axial.Add((a, b, p1, p2));

    public void AddAxialPoint(double a, double p) => _axialPoints.Add((a, p));

    public MemberLoads Scaled(double factor)
    {
        var m = new MemberLoads(Length);
        foreach (var t in _transverse) m._transverse.Add((t.A, t.B, t.W1 * factor, t.W2 * factor));
        foreach (var p in _points) m._points.Add((p.A, p.P * factor));
        foreach (var c in _couples) m._couples.Add((c.A, c.M * factor));
        foreach (var t in _axial) m._axial.Add((t.A, t.B, t.P1 * factor, t.P2 * factor));
        foreach (var p in _axialPoints) m._axialPoints.Add((p.A, p.P * factor));
        return m;
    }

    public void AddFrom(MemberLoads other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _transverse.AddRange(other._transverse);
        _points.AddRange(other._points);
        _couples.AddRange(other._couples);
        _axial.AddRange(other._axial);
        _axialPoints.AddRange(other._axialPoints);
    }

    private static double Mac(double x, double a, int n) => x > a ? Math.Pow(x - a, n) : 0;

    private static double Step(double x, double a) => x >= a ? 1 : 0;

    /// <summary>Σ of transverse loads to the left of x (the loads' part of the shear).</summary>
    public double Shear(double x)
    {
        var v = _points.Sum(p => p.P * Step(x, p.A));

        foreach (var (a, b, w1, w2) in _transverse)
        {
            var s = (w2 - w1) / (b - a);
            v += w1 * Mac(x, a, 1) + s * Mac(x, a, 2) / 2 - w2 * Mac(x, b, 1) - s * Mac(x, b, 2) / 2;
        }

        return v;
    }

    /// <summary>Moment of the loads to the left of x about the section (sagging positive), including applied couples.</summary>
    public double Moment(double x)
    {
        var m = _points.Sum(p => p.P * Mac(x, p.A, 1)) - _couples.Sum(c => c.M * Step(x, c.A));

        foreach (var (a, b, w1, w2) in _transverse)
        {
            var s = (w2 - w1) / (b - a);
            m += w1 * Mac(x, a, 2) / 2 + s * Mac(x, a, 3) / 6 - w2 * Mac(x, b, 2) / 2 - s * Mac(x, b, 3) / 6;
        }

        return m;
    }

    /// <summary>∫ Moment dx (EI × slope contribution of the loads).</summary>
    public double Slope(double x)
    {
        var d = _points.Sum(p => p.P * Mac(x, p.A, 2) / 2) - _couples.Sum(c => c.M * Mac(x, c.A, 1));

        foreach (var (a, b, w1, w2) in _transverse)
        {
            var s = (w2 - w1) / (b - a);
            d += w1 * Mac(x, a, 3) / 6 + s * Mac(x, a, 4) / 24 - w2 * Mac(x, b, 3) / 6 - s * Mac(x, b, 4) / 24;
        }

        return d;
    }

    /// <summary>∫∫ Moment dx² (EI × deflection contribution of the loads).</summary>
    public double Deflection(double x)
    {
        var d = _points.Sum(p => p.P * Mac(x, p.A, 3) / 6) - _couples.Sum(c => c.M * Mac(x, c.A, 2) / 2);

        foreach (var (a, b, w1, w2) in _transverse)
        {
            var s = (w2 - w1) / (b - a);
            d += w1 * Mac(x, a, 4) / 24 + s * Mac(x, a, 5) / 120 - w2 * Mac(x, b, 4) / 24 - s * Mac(x, b, 5) / 120;
        }

        return d;
    }

    /// <summary>Σ of axial loads to the left of x.</summary>
    public double Axial(double x)
    {
        var n = _axialPoints.Sum(p => p.P * Step(x, p.A));

        foreach (var (a, b, p1, p2) in _axial)
        {
            var s = (p2 - p1) / (b - a);
            n += p1 * Mac(x, a, 1) + s * Mac(x, a, 2) / 2 - p2 * Mac(x, b, 1) - s * Mac(x, b, 2) / 2;
        }

        return n;
    }

    /// <summary>∫ Axial dx (EA × axial displacement contribution of the loads).</summary>
    public double AxialDisplacement(double x)
    {
        var u = _axialPoints.Sum(p => p.P * Mac(x, p.A, 1));

        foreach (var (a, b, p1, p2) in _axial)
        {
            var s = (p2 - p1) / (b - a);
            u += p1 * Mac(x, a, 2) / 2 + s * Mac(x, a, 3) / 6 - p2 * Mac(x, b, 2) / 2 - s * Mac(x, b, 3) / 6;
        }

        return u;
    }

    /// <summary>
    /// The forces the nodes exert on a member with both ends fully fixed
    /// (local: F_xi, F_yi, M_i, F_xj, F_yj, M_j). The equivalent nodal
    /// loads for the stiffness method are the negatives.
    /// </summary>
    public double[] FixedEndForces()
    {
        var l = Length;
        var d = Deflection(l);
        var dp = Slope(l);
        var fyi = 12 * d / (l * l * l) - 6 * dp / (l * l);
        var mi = fyi * l / 2 + dp / l;
        var fyj = -(fyi + Shear(l));
        var mj = fyi * l + Moment(l) - mi;
        var fxi = -AxialDisplacement(l) / l;
        var fxj = -(fxi + Axial(l));
        return [fxi, fyi, mi, fxj, fyj, mj];
    }
}
