namespace Tempest.Calc.Mechanical;

/// <summary>What a cam segment does: the follower rises by the lift, dwells, or returns by the lift.</summary>
public enum CamSegmentKind
{
    Rise,
    Dwell,
    Return,
}

/// <summary>The motion law of a rise or a return, in the normalised angle x = θ/β.</summary>
public enum CamLaw
{
    Cycloidal,
    ModifiedTrapezoid,
    Poly345,
    Poly4567,
}

/// <summary>One cam segment: kind, law, angle β (radians) and lift h (metres; ignored for a dwell).</summary>
public readonly record struct CamSegment(CamSegmentKind Kind, CamLaw Law, double Beta, double Lift);

/// <summary>Normalised displacement f(x) and its three derivatives f′, f″, f‴ with respect to x.</summary>
public readonly record struct LawValue(double F, double F1, double F2, double F3);

/// <summary>
/// The four classical cam motion laws as closed-form functions of x = θ/β on [0, 1]; f(0) = 0, f(1) = 1, and f′, f″ vanish at both
/// ends (so a rise or return between dwells is continuous in displacement, velocity and acceleration). Peak coefficients
/// (C_v = max f′, C_a = max |f″|, C_j = max |f‴|): cycloidal 2, 2π, 4π²; modified trapezoid 2, 8π/(π + 2) ≈ 4.8881, 32π²/(π + 2) ≈ 61.43;
/// 3-4-5 polynomial 15/8, 10/√3 ≈ 5.7735, 60; 4-5-6-7 polynomial 35/16, 7.5132, 52.5.
/// </summary>
public static class CamLaws
{
    private const double Pi = Math.PI;

    // Modified trapezoid: acceleration pieces A·sin(4πx) on [0, 1/8], A on [1/8, 3/8], A·cos(4π(x − 3/8)) on [3/8, 5/8], −A on
    // [5/8, 7/8], −A·cos(4π(x − 7/8)) on [7/8, 1]; A = 8π/(π + 2) makes f(1) = 1 (and C_v = 2 exactly).
    private static readonly double Amp = 8 * Pi / (Pi + 2);
    private static readonly double K = 4 * Pi;
    private static readonly double S1 = Amp * (1.0 / (8 * K) - 1 / (K * K));
    private static readonly double V1 = Amp / K;
    private static readonly double S2 = S1 + V1 / 4 + Amp / 32;
    private static readonly double V2 = V1 + Amp / 4;
    private static readonly double S3 = S2 + V2 / 4 + 2 * Amp / (K * K);
    private static readonly double V3 = V2;
    private static readonly double S4 = S3 + V3 / 4 - Amp / 32;
    private static readonly double V4 = V3 - Amp / 4;

    public static LawValue Evaluate(CamLaw law, double x)
    {
        x = Math.Clamp(x, 0, 1);

        switch (law)
        {
            case CamLaw.Cycloidal:
            {
                var (s, c) = Math.SinCos(2 * Pi * x);
                return new LawValue(x - s / (2 * Pi), 1 - c, 2 * Pi * s, 4 * Pi * Pi * c);
            }

            case CamLaw.Poly345:
            {
                var x2 = x * x;
                var x3 = x2 * x;
                return new LawValue(
                    x3 * (10 + x * (-15 + 6 * x)),
                    x2 * (30 + x * (-60 + 30 * x)),
                    x * (60 + x * (-180 + 120 * x)),
                    60 + x * (-360 + 360 * x));
            }

            case CamLaw.Poly4567:
            {
                var x2 = x * x;
                var x3 = x2 * x;
                var x4 = x3 * x;
                return new LawValue(
                    x4 * (35 + x * (-84 + x * (70 - 20 * x))),
                    x3 * (140 + x * (-420 + x * (420 - 140 * x))),
                    x2 * (420 + x * (-1680 + x * (2100 - 840 * x))),
                    x * (840 + x * (-5040 + x * (8400 - 4200 * x))));
            }

            default:
                return ModifiedTrapezoid(x);
        }
    }

    private static LawValue ModifiedTrapezoid(double x)
    {
        if (x <= 1.0 / 8)
        {
            var (s, c) = Math.SinCos(K * x);
            return new LawValue(Amp * (x / K - s / (K * K)), Amp * (1 - c) / K, Amp * s, Amp * K * c);
        }

        if (x <= 3.0 / 8)
        {
            var u = x - 1.0 / 8;
            return new LawValue(S1 + V1 * u + Amp * u * u / 2, V1 + Amp * u, Amp, 0);
        }

        if (x <= 5.0 / 8)
        {
            var u = x - 3.0 / 8;
            var (s, c) = Math.SinCos(K * u);
            return new LawValue(S2 + V2 * u + Amp * (1 - c) / (K * K), V2 + Amp * s / K, Amp * c, -Amp * K * s);
        }

        if (x <= 7.0 / 8)
        {
            var u = x - 5.0 / 8;
            return new LawValue(S3 + V3 * u - Amp * u * u / 2, V3 - Amp * u, -Amp, 0);
        }

        {
            var u = x - 7.0 / 8;
            var (s, c) = Math.SinCos(K * u);
            return new LawValue(S4 + V4 * u + Amp * (c - 1) / (K * K), V4 - Amp * s / K, -Amp * c, Amp * K * s);
        }
    }
}

/// <summary>Displacement of the follower and its derivatives with respect to the cam angle (per radian): s (m), s′ (m/rad), s″ (m/rad²), s‴ (m/rad³).</summary>
public readonly record struct CamMotion(double S, double S1, double S2, double S3);

/// <summary>
/// The follower motion of one cam revolution as a list of segments, β summing to 360°. A rise adds the lift with its law, a return
/// subtracts it (mirrored: s = s₀ − h·f(x)), a dwell holds. The profile must close (rises equal returns) and never go below the base circle.
/// </summary>
public sealed class CamProfile
{
    private readonly CamSegment[] _segments;
    private readonly double[] _start;
    private readonly double[] _base;

    public CamProfile(IReadOnlyList<CamSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        if (segments.Count == 0)
            throw new ArgumentException("A cam needs at least one segment.", nameof(segments));

        _segments = [.. segments];
        _start = new double[_segments.Length + 1];
        _base = new double[_segments.Length + 1];

        for (var k = 0; k < _segments.Length; k++)
        {
            _start[k + 1] = _start[k] + _segments[k].Beta;
            _base[k + 1] = _base[k] + Direction(_segments[k].Kind) * _segments[k].Lift;
        }
    }

    public int Count => _segments.Length;

    public CamSegment Segment(int k) => _segments[k];

    public double Start(int k) => _start[k];

    public double End(int k) => _start[k + 1];

    /// <summary>The follower displacement at the start of segment <paramref name="k"/>.</summary>
    public double Base(int k) => _base[k];

    public double MaxDisplacement => Enumerable.Range(0, _segments.Length + 1).Max(k => _base[k]);

    /// <summary>The message when the segments are not a valid cam (sum of angles, closure, never below the base circle), else <see langword="null"/>.</summary>
    public static string? Validate(IReadOnlyList<CamSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var total = 0.0;
        var s = 0.0;
        var maxLift = 0.0;

        foreach (var seg in segments)
        {
            total += seg.Beta;
            s += Direction(seg.Kind) * seg.Lift;
            maxLift = Math.Max(maxLift, seg.Lift);

            if (s < -1e-9 * Math.Max(maxLift, 1e-6))
                return "a return takes the follower below the base circle (the returns before this point exceed the rises)";
        }

        if (Math.Abs(total - 2 * Math.PI) > 1e-9)
            return $"the segment angles add to {total * 180 / Math.PI:0.####}°, not 360°";

        if (Math.Abs(s) > 1e-9 * Math.Max(maxLift, 1e-6))
            return "the rises and returns do not cancel, so the follower does not come back to the base circle after one revolution";

        return null;
    }

    private static double Direction(CamSegmentKind kind) => kind switch
    {
        CamSegmentKind.Rise => 1,
        CamSegmentKind.Return => -1,
        _ => 0,
    };

    /// <summary>The motion inside segment <paramref name="k"/> at the normalised angle x (0 … 1); x = 1 is the left-hand limit at the segment's end.</summary>
    public CamMotion InSegment(int k, double x)
    {
        var seg = _segments[k];
        var dir = Direction(seg.Kind);

        if (dir == 0)
            return new CamMotion(_base[k], 0, 0, 0);

        var f = CamLaws.Evaluate(seg.Law, x);
        var b = seg.Beta;
        var h = dir * seg.Lift;
        return new CamMotion(_base[k] + h * f.F, h * f.F1 / b, h * f.F2 / (b * b), h * f.F3 / (b * b * b));
    }

    /// <summary>The segment index and normalised angle at cam angle θ (radians, wrapped into one revolution).</summary>
    public (int Segment, double X) Locate(double theta)
    {
        var t = theta % (2 * Math.PI);

        if (t < 0)
            t += 2 * Math.PI;

        for (var k = 0; k < _segments.Length; k++)
        {
            if (t < _start[k + 1] || k == _segments.Length - 1)
                return (k, Math.Clamp((t - _start[k]) / _segments[k].Beta, 0, 1));
        }

        return (_segments.Length - 1, 1);
    }

    public CamMotion At(double theta)
    {
        var (k, x) = Locate(theta);
        return InSegment(k, x);
    }
}

/// <summary>Everything the cam analysis reports at one cam angle (SI): motion, dynamics, geometry.</summary>
public readonly record struct CamPoint(double Theta, double S, double V, double A, double J, double PressureAngle, double Curvature, double Force, double Torque, double SPrime, double FaceDemand);

/// <summary>
/// The kinematic and dynamic analysis of a cam with a translating follower at constant cam speed ω.
/// <para>Roller follower: prime-circle radius R_p = R_b + R_r, offset e; pressure angle φ = arctan[(s′ − e)/(√(R_p² − e²) + s)] (s′ = ds/dθ),
/// with e = 0 the φ = arctan[s′/(R_p + s)] of the in-line follower. The pitch curve is P(θ) = Rot(−θ)·(e, √(R_p² − e²) + s); its signed
/// convex curvature κ gives the radius of curvature ρ = 1/κ, and the cam profile is undercut where 0 &lt; ρ ≤ R_r.</para>
/// <para>Flat-faced follower (in-line): no pressure angle; the profile radius of curvature is R_b + s + s″, which must stay positive;
/// the face must reach |s′| either side of the follower axis.</para>
/// <para>Force on the follower F = m·a + k·s + F_pre + F_ext; input torque by virtual work T = F·s′ (T·ω = F·v, constant ω).</para>
/// </summary>
public sealed class CamAnalysis
{
    private readonly CamProfile _profile;
    private readonly bool _roller;
    private readonly double _rb;
    private readonly double _rr;
    private readonly double _e;
    private readonly double _omega;
    private readonly double _mass;
    private readonly double _rate;
    private readonly double _preload;
    private readonly double _external;

    public CamAnalysis(CamProfile profile, bool roller, double baseRadius, double rollerRadius, double offset, double omega, double mass, double springRate, double preload, double external)
    {
        _profile = profile;
        _roller = roller;
        _rb = baseRadius;
        _rr = rollerRadius;
        _e = roller ? offset : 0;
        _omega = omega;
        _mass = mass;
        _rate = springRate;
        _preload = preload;
        _external = external;
    }

    public CamProfile Profile => _profile;

    /// <summary>The prime-circle radius R_p = R_b + R_r (roller) or R_b (flat-faced).</summary>
    public double PrimeRadius => _roller ? _rb + _rr : _rb;

    public CamPoint In(int segment, double x)
    {
        var m = _profile.InSegment(segment, x);
        var theta = _profile.Start(segment) + x * _profile.Segment(segment).Beta;
        return Build(theta, m);
    }

    public CamPoint At(double theta) => Build(theta, _profile.At(theta));

    private CamPoint Build(double theta, CamMotion m)
    {
        var v = _omega * m.S1;
        var a = _omega * _omega * m.S2;
        var j = _omega * _omega * _omega * m.S3;
        var force = _mass * a + _rate * m.S + _preload + _external;
        var torque = force * m.S1;
        double phi;
        double kappa;

        if (_roller)
        {
            var rp = PrimeRadius;
            var s0 = Math.Sqrt(Math.Max(rp * rp - _e * _e, 1e-300));
            phi = Math.Atan2(m.S1 - _e, s0 + m.S);
            var y = s0 + m.S;
            var (sin, cos) = Math.SinCos(theta);
            var x1 = -_e * sin + y * cos + m.S1 * sin;
            var y1 = -_e * cos - y * sin + m.S1 * cos;
            var x2 = -_e * cos + 2 * m.S1 * cos - y * sin + m.S2 * sin;
            var y2 = _e * sin - 2 * m.S1 * sin - y * cos + m.S2 * cos;
            var speed2 = x1 * x1 + y1 * y1;
            kappa = speed2 > 0 ? -(x1 * y2 - y1 * x2) / (speed2 * Math.Sqrt(speed2)) : 0;
        }
        else
        {
            phi = 0;
            var radius = _rb + m.S + m.S2;
            kappa = radius > 1e-9 ? 1 / radius : 1e9;
        }

        return new CamPoint(theta, m.S, v, a, j, phi, kappa, force, torque, m.S1, Math.Abs(m.S1));
    }
}
