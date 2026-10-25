namespace Tempest.Calc.Mechanical;

/// <summary>
/// Extremum search over a smooth function of one angle: a dense uniform scan, then a golden-section refinement between the
/// neighbours of the best sample. Undefined points (the function returns NaN, for example an angle at which a linkage cannot be
/// assembled) are skipped, so a peak is always a value the mechanism really reaches. Used by M9 for every peak, so a peak does not
/// depend on the number of rows of the printed table.
/// </summary>
public static class MechanismPeaks
{
    private const double GoldenRatio = 0.6180339887498949;
    private const int MaxCandidates = 6;

    /// <summary>
    /// The largest (or smallest) finite value of <paramref name="f"/> on [lo, hi] and where it occurs, or <see langword="null"/>
    /// when <paramref name="f"/> is undefined everywhere on the scan.
    /// </summary>
    public static (double Value, double At)? Find(Func<double, double> f, double lo, double hi, int intervals, bool max = true)
    {
        ArgumentNullException.ThrowIfNull(f);
        intervals = Math.Max(intervals, 2);
        var xs = new double[intervals + 1];
        var ys = new double[intervals + 1];

        for (var i = 0; i <= intervals; i++)
        {
            xs[i] = lo + (hi - lo) * i / intervals;
            ys[i] = f(xs[i]);
        }

        return Refine(xs, ys, (_, x) => f(x), max);
    }

    /// <summary>
    /// The refinement half of <see cref="Find"/>, for a caller that already has the samples. <paramref name="around"/> evaluates the
    /// function near sample <c>i</c> (it lets an angle that wraps be unwrapped against that sample).
    /// </summary>
    public static (double Value, double At)? Refine(IReadOnlyList<double> xs, IReadOnlyList<double> ys, Func<int, double, double> around, bool max = true)
    {
        ArgumentNullException.ThrowIfNull(xs);
        ArgumentNullException.ThrowIfNull(ys);
        ArgumentNullException.ThrowIfNull(around);
        // Candidates: every sample that is not below (above, for a minimum) its finite neighbours. A periodic sweep has its peak
        // just before the last sample or just after the first, and the two tie, so every candidate is refined, the best few kept.
        var candidates = new List<int>();

        for (var i = 0; i < ys.Count; i++)
        {
            if (!double.IsFinite(ys[i]))
                continue;

            var leftOk = i == 0 || !double.IsFinite(ys[i - 1]) || (max ? ys[i] >= ys[i - 1] : ys[i] <= ys[i - 1]);
            var rightOk = i == ys.Count - 1 || !double.IsFinite(ys[i + 1]) || (max ? ys[i] >= ys[i + 1] : ys[i] <= ys[i + 1]);

            if (leftOk && rightOk)
                candidates.Add(i);
        }

        if (candidates.Count == 0)
            return null;

        if (candidates.Count > MaxCandidates)
        {
            var ordered = candidates.OrderBy(i => max ? -ys[i] : ys[i]).ThenBy(i => i).Take(MaxCandidates - 2).ToList();

            if (!ordered.Contains(candidates[0]))
                ordered.Add(candidates[0]);

            if (!ordered.Contains(candidates[^1]))
                ordered.Add(candidates[^1]);

            candidates = ordered;
        }

        (double Value, double At)? result = null;

        foreach (var best in candidates)
        {
            var refined = RefineAround(xs, ys, best, around, max);

            if (result is null || (max ? refined.Value > result.Value.Value : refined.Value < result.Value.Value))
                result = refined;
        }

        return result;
    }

    private static (double Value, double At) RefineAround(IReadOnlyList<double> xs, IReadOnlyList<double> ys, int best, Func<int, double, double> around, bool max)
    {
        var value = ys[best];
        var at = xs[best];
        var left = best > 0 && double.IsFinite(ys[best - 1]) ? xs[best - 1] : xs[best];
        var right = best < ys.Count - 1 && double.IsFinite(ys[best + 1]) ? xs[best + 1] : xs[best];

        if (right - left <= 0)
            return (value, at);

        double G(double x)
        {
            var y = around(best, x);
            return double.IsFinite(y) ? (max ? y : -y) : double.NegativeInfinity;
        }

        var a = left;
        var b = right;
        var c = b - GoldenRatio * (b - a);
        var d = a + GoldenRatio * (b - a);
        var fc = G(c);
        var fd = G(d);

        for (var k = 0; k < 90 && b - a > 1e-14 * Math.Max(1, Math.Abs(a) + Math.Abs(b)); k++)
        {
            if (fc > fd)
            {
                b = d;
                d = c;
                fd = fc;
                c = b - GoldenRatio * (b - a);
                fc = G(c);
            }
            else
            {
                a = c;
                c = d;
                fc = fd;
                d = a + GoldenRatio * (b - a);
                fd = G(d);
            }
        }

        var xm = 0.5 * (a + b);
        var ym = around(best, xm);

        if (double.IsFinite(ym) && (max ? ym > value : ym < value))
        {
            value = ym;
            at = xm;
        }

        return (value, at);
    }
}

/// <summary>The state of a four-bar linkage at one crank angle (SI: rad, rad/s, rad/s², metres).</summary>
public readonly record struct FourBarState(double Theta2, double Theta3, double Theta4, double Omega3, double Omega4, double Alpha3, double Alpha4, double Mu, double Residual, bool Singular);

/// <summary>
/// A planar four-bar linkage O₂–A–B–O₄: ground link d (O₂ at the origin, O₄ at (d, 0)), crank a, coupler b, rocker c. The position is
/// the closed-form solution of the vector loop a·e^{jθ₂} + b·e^{jθ₃} = d + c·e^{jθ₄} (law of cosines on the triangle O₄–A–B, which
/// is the Freudenstein result): θ₄ = φ ∓ γ with φ the direction O₄→A and γ the angle at O₄. The upper sign, φ − γ, is the open
/// circuit; φ + γ is the crossed circuit (the mirror image of B about the line O₄A). Velocity and acceleration come from the
/// differentiated loop at constant input speed.
/// </summary>
public sealed class FourBarLinkage
{
    /// <summary>The sine of the angle between coupler and rocker below which the sweep point is a toggle: the 2×2 velocity system is singular.</summary>
    public const double ToggleSine = 1e-9;

    private readonly double _d;
    private readonly double _a;
    private readonly double _b;
    private readonly double _c;
    private readonly double _sign;

    public FourBarLinkage(double ground, double crank, double coupler, double rocker, bool crossed)
    {
        _d = ground;
        _a = crank;
        _b = coupler;
        _c = rocker;
        _sign = crossed ? 1 : -1;
    }

    /// <summary>The state at crank angle <paramref name="theta2"/> turning at <paramref name="omega2"/> (constant), or <see langword="null"/> when the linkage cannot be assembled there.</summary>
    public FourBarState? At(double theta2, double omega2)
    {
        var (sin2, cos2) = Math.SinCos(theta2);
        var ax = _a * cos2;
        var ay = _a * sin2;
        var dx = ax - _d;
        var l2 = dx * dx + ay * ay;
        var l = Math.Sqrt(l2);

        if (!(l > 1e-12 * Math.Max(_d, _a)))
            return null;

        var cosGamma = (_c * _c + l2 - _b * _b) / (2 * _c * l);

        if (!(Math.Abs(cosGamma) <= 1 + 1e-12))
            return null;

        cosGamma = Math.Clamp(cosGamma, -1, 1);
        var gamma = Math.Atan2(Math.Sqrt(Math.Max(0, (1 - cosGamma) * (1 + cosGamma))), cosGamma);
        var theta4 = Math.Atan2(ay, dx) + _sign * gamma;
        var (sin4, cos4) = Math.SinCos(theta4);
        var bx = _d + _c * cos4;
        var by = _c * sin4;
        var theta3 = Math.Atan2(by - ay, bx - ax);
        var (sin3, cos3) = Math.SinCos(theta3);

        // Transmission angle between coupler and rocker, folded into 0 … 90°.
        var cosMu = Math.Clamp((_b * _b + _c * _c - l2) / (2 * _b * _c), -1, 1);
        var muInterior = Math.Acos(cosMu);
        var mu = Math.Min(muInterior, Math.PI - muInterior);

        // Loop closure: |a e2 + b e3 − c e4 − d|.
        var rx = ax + _b * cos3 - _c * cos4 - _d;
        var ry = ay + _b * sin3 - _c * sin4;
        var residual = Math.Sqrt(rx * rx + ry * ry);

        // Velocity: M [ω3, ω4]ᵀ = ω2 [a sinθ2, −a cosθ2]ᵀ, M = [[−b sinθ3, c sinθ4], [b cosθ3, −c cosθ4]], det M = b c sin(θ3 − θ4).
        var sin34 = Math.Sin(theta3 - theta4);

        if (Math.Abs(sin34) < ToggleSine)
            return new FourBarState(theta2, theta3, theta4, 0, 0, 0, 0, mu, residual, true);

        var det = _b * _c * sin34;
        var omega3 = omega2 * _a * (sin4 * cos2 - cos4 * sin2) * _c / det;
        var omega4 = omega2 * _a * _b * (sin3 * cos2 - cos3 * sin2) / det;

        // Acceleration at constant input speed: M [α3, α4]ᵀ = r.
        var r1 = _a * cos2 * omega2 * omega2 + _b * cos3 * omega3 * omega3 - _c * cos4 * omega4 * omega4;
        var r2 = _a * sin2 * omega2 * omega2 + _b * sin3 * omega3 * omega3 - _c * sin4 * omega4 * omega4;
        var alpha3 = (-_c * cos4 * r1 - _c * sin4 * r2) / det;
        var alpha4 = (-_b * sin3 * r2 - _b * cos3 * r1) / det;

        return new FourBarState(theta2, theta3, theta4, omega3, omega4, alpha3, alpha4, mu, residual, false);
    }

    /// <summary>The crank angles (radians, 0 … 2π) at which coupler and rocker are collinear (transmission angle 0 or 180°): the toggle positions that limit the rocker's stroke or the drive.</summary>
    public IReadOnlyList<double> ToggleCrankAngles()
    {
        var angles = new List<double>();

        foreach (var l in new[] { _b + _c, Math.Abs(_b - _c) })
        {
            var cos = (_d * _d + _a * _a - l * l) / (2 * _d * _a);

            if (!(Math.Abs(cos) <= 1 + 1e-12))
                continue;

            var t = Math.Acos(Math.Clamp(cos, -1, 1));
            angles.Add(t);

            if (t > 1e-12 && t < Math.PI - 1e-12)
                angles.Add(2 * Math.PI - t);
        }

        angles.Sort();
        return angles;
    }

    /// <summary>
    /// Grashof classification by the link lengths. <paramref name="fullRotation"/> is true when the input link (the crank, link 2)
    /// can turn through 360°: the criterion s + l ≤ p + q with the input link or the ground link the shortest.
    /// </summary>
    public static string Classify(double ground, double crank, double coupler, double rocker, out double sPlusL, out double pPlusQ, out bool fullRotation)
    {
        var lengths = new[] { ground, crank, coupler, rocker };
        var sorted = lengths.Order().ToArray();
        sPlusL = sorted[0] + sorted[3];
        pPlusQ = sorted[1] + sorted[2];
        var tol = 1e-9 * Math.Max(1, pPlusQ);
        var shortest = sorted[0];
        var isShort = lengths.Select(x => Math.Abs(x - shortest) <= 1e-9 * Math.Max(1, shortest)).ToArray();

        if (sPlusL > pPlusQ + tol)
        {
            fullRotation = false;
            return "Non-Grashof (triple rocker): no link turns fully, the crank sweep is limited";
        }

        if (Math.Abs(sPlusL - pPlusQ) <= tol)
        {
            fullRotation = isShort[0] || isShort[1];
            return "Grashof change-point (s + l = p + q; parallelogram, deltoid or similar): every link becomes collinear once a cycle, where the motion is not determined, so a sweep through that position is not reliable";
        }

        if (isShort[0])
        {
            fullRotation = true;
            return "Grashof double-crank (drag link), ground link shortest";
        }

        if (isShort[1])
        {
            fullRotation = true;
            return "Grashof crank-rocker, input link shortest";
        }

        fullRotation = false;
        return isShort[3]
            ? "Grashof rocker-crank, output link shortest: the input link only rocks"
            : "Grashof double-rocker, coupler shortest";
    }
}

/// <summary>The state of a slider-crank at one crank angle (SI).</summary>
public readonly record struct SliderState(double Theta, double X, double DxDtheta, double D2xDtheta2, double Theta3, double Dtheta3, double D2theta3, double Mu, double Residual);

/// <summary>
/// An offset slider-crank: crank r about the origin, connecting rod l, slider on the line y = e. x = r·cosθ + σ·√(l² − (r·sinθ − e)²),
/// σ = +1 for the open circuit (slider on the +x side of the crank pin) and −1 for the crossed one. Derivatives are with respect to
/// the crank angle; the caller multiplies by ω and ω² for time derivatives.
/// </summary>
public sealed class SliderCrankLinkage
{
    private readonly double _r;
    private readonly double _l;
    private readonly double _e;
    private readonly double _sign;

    public SliderCrankLinkage(double crank, double rod, double offset, bool crossed)
    {
        _r = crank;
        _l = rod;
        _e = offset;
        _sign = crossed ? -1 : 1;
    }

    public double Crank => _r;

    public double Rod => _l;

    public double Offset => _e;

    /// <summary>The state at crank angle θ, or <see langword="null"/> when the rod cannot reach the slide line there.</summary>
    public SliderState? At(double theta)
    {
        var (sin, cos) = Math.SinCos(theta);
        var w = _r * sin - _e;
        var q = _l * _l - w * w;

        if (!(q >= -1e-12 * _l * _l))
            return null;

        var s = _sign * Math.Sqrt(Math.Max(q, 0));

        // The rod is perpendicular to the slide line: dx/dθ is unbounded. Keep the divisor off zero.
        var guard = Math.Max(Math.Abs(s), 1e-12 * _l);
        var sg = s < 0 ? -guard : guard;
        var x = _r * cos + s;
        var dx = -_r * sin - _r * cos * w / sg;
        var d2x = -_r * cos - ((-_r * w * sin + _r * _r * cos * cos) / sg + _r * _r * w * w * cos * cos / (sg * sg * sg));
        var theta3 = Math.Atan2(-w, s);
        var dtheta3 = -_r * cos / sg;
        var d2theta3 = _r * sin / sg - _r * _r * w * cos * cos / (sg * sg * sg);
        var mu = Math.Acos(Math.Min(1, Math.Abs(w) / _l));

        // Loop closure: crank pin + rod = slider position (x, e).
        var rx = _r * cos + _l * Math.Cos(theta3) - x;
        var ry = _r * sin + _l * Math.Sin(theta3) - _e;
        return new SliderState(theta, x, dx, d2x, theta3, dtheta3, d2theta3, mu, Math.Sqrt(rx * rx + ry * ry));
    }

    /// <summary>The crank angles (radians, wrapped to 0 … 2π) of the two dead centres, outer then inner (slider velocity zero: crank and rod collinear). Inner is absent when l ≤ r + |e| fails to leave room.</summary>
    public IReadOnlyList<(string Name, double Theta)> DeadCentres()
    {
        var list = new List<(string, double)>();
        var outer = Math.Abs(_e) <= _l + _r ? Math.Asin(_e / (_l + _r)) : double.NaN;

        if (double.IsFinite(outer))
            list.Add(("outer", Wrap(_sign > 0 ? outer : Math.PI - outer)));

        if (_l > _r && Math.Abs(_e) <= _l - _r)
        {
            var inner = Math.Asin(_e / (_l - _r));
            list.Add(("inner", Wrap(_sign > 0 ? Math.PI + inner : -inner)));
        }

        return list;
    }

    /// <summary>The slide stroke between the two dead centres, √((l + r)² − e²) − √((l − r)² − e²) (the whole stroke when the crank turns fully); <see langword="null"/> when a dead centre does not exist.</summary>
    public double? Stroke() => _l > _r && Math.Abs(_e) <= _l - _r ? Math.Sqrt((_l + _r) * (_l + _r) - _e * _e) - Math.Sqrt((_l - _r) * (_l - _r) - _e * _e) : null;

    /// <summary>True when the crank can turn through 360° (l ≥ r + |e|).</summary>
    public bool FullRotation => _l >= _r + Math.Abs(_e) - 1e-12 * _l;

    private static double Wrap(double t)
    {
        var w = t % (2 * Math.PI);
        return w < 0 ? w + 2 * Math.PI : w;
    }
}
