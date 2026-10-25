using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Numerics;

namespace Tempest.Calc.Structural;

/// <summary>
/// The plate-bending kernels behind S9 (<c>struct.plate-bending</c>): Kirchhoff small-deflection theory, SI units throughout
/// (m, N, Pa). Closed forms for circular plates under uniform, central-patch and ring loads (clamped or simply supported);
/// the Navier double series for a simply supported rectangle (uniform or central patch load); a Ritz solution for a clamped
/// rectangle (approximate); natural frequencies. Moments are per unit width (N·m/m), positive when they put the bottom
/// (load-opposite) face in tension; a surface stress is 6M/t².
/// </summary>
public static class PlateBendingMath
{
    /// <summary>Flexural rigidity D = E·t³/(12(1 − ν²)).</summary>
    public static double Rigidity(double e, double t, double nu) => e * t * t * t / (12 * (1 - nu * nu));

    /// <summary>The von Mises equivalent of a plane-stress field given as moments (or stresses): √(x² − x·y + y² + 3·s²).</summary>
    public static double VonMises(double x, double y, double s = 0) => Math.Sqrt(Math.Max(0, x * x - x * y + y * y + 3 * s * s));

    // ============================================================ circular plates

    /// <summary>
    /// Axisymmetric bending of a circular plate of radius <c>a</c> under a central patch of radius <c>c</c> (pressure <c>q</c>, with
    /// <c>c = a</c> a uniform load) or a ring load at radius <c>c</c> (total force <c>F</c>), clamped or simply supported at the edge.
    /// The solution is w = A + B·r² (+ q·r⁴/64D) inside the loaded radius and A′ + B′·r² + C′·ln(r/a) + k·r²·ln(r/a) outside, with the
    /// constants from continuity of w, w′ and M_r at r = c and the edge conditions (derived; see the spec). w is positive in the load direction.
    /// </summary>
    public sealed class AxisymmetricPlate
    {
        private readonly double _a;
        private readonly double _c;
        private readonly double _nu;
        private readonly double _d;
        private readonly bool _ring;
        private readonly double _k;
        private readonly double _bOut;
        private readonly double _aOut;
        private readonly double _bIn;
        private readonly double _aIn;
        private readonly double _cOut;
        private readonly double _qq;

        private AxisymmetricPlate(double a, double c, double nu, double d, bool ring, double k, double bOut, double aOut, double bIn, double aIn, double cOut, double qq)
        {
            _a = a;
            _c = c;
            _nu = nu;
            _d = d;
            _ring = ring;
            _k = k;
            _bOut = bOut;
            _aOut = aOut;
            _bIn = bIn;
            _aIn = aIn;
            _cOut = cOut;
            _qq = qq;
        }

        public double Radius => _a;

        /// <summary>The radius of the patch or of the ring.</summary>
        public double LoadRadius => _c;

        public bool IsRing => _ring;

        /// <summary>Uniform pressure <paramref name="q"/> on the circle of radius <paramref name="c"/> (c = a: the whole plate).</summary>
        public static AxisymmetricPlate Patch(double a, double c, double nu, double d, double q, bool clamped)
        {
            var k = q * c * c / (8 * d);
            var gamma = c / a;
            var bOut = clamped ? -k / 2 * (gamma * gamma / 2 + 1) : k / (2 * (1 + nu)) * ((1 - nu) * gamma * gamma / 2 - (3 + nu));
            var aOut = -bOut * a * a;
            var bIn = bOut + k * (Math.Log(gamma) + 0.5);
            var aIn = aOut + k * c * c / 2 * Math.Log(gamma) - 5.0 / 8 * k * c * c;
            return new AxisymmetricPlate(a, c, nu, d, false, k, bOut, aOut, bIn, aIn, k * c * c / 2, q / (64 * d));
        }

        /// <summary>Total force <paramref name="force"/> spread round the circle of radius <paramref name="b"/> (b &lt; a).</summary>
        public static AxisymmetricPlate Ring(double a, double b, double nu, double d, double force, bool clamped)
        {
            var k = force / (8 * Math.PI * d);
            var beta = b / a;
            var bOut = clamped ? -k / 2 * (beta * beta + 1) : k / (2 * (1 + nu)) * (beta * beta * (1 - nu) - (3 + nu));
            var aOut = -bOut * a * a;
            var bIn = bOut + k * (1 + Math.Log(beta));
            var aIn = aOut + k * b * b * (Math.Log(beta) - 1);
            return new AxisymmetricPlate(a, b, nu, d, true, k, bOut, aOut, bIn, aIn, k * b * b, 0);
        }

        /// <summary>Deflection at radius <paramref name="r"/>.</summary>
        public double W(double r)
        {
            if (r <= _c)
                return _aIn + _bIn * r * r + _qq * r * r * r * r;

            var ln = Math.Log(r / _a);
            return _aOut + _bOut * r * r + _cOut * ln + _k * r * r * ln;
        }

        /// <summary>Radial bending moment per unit width.</summary>
        public double Mr(double r)
        {
            if (r <= _c)
                return -_d * (2 * _bIn * (1 + _nu) + _qq * r * r * (12 + 4 * _nu));

            var ln = Math.Log(r / _a);
            return -_d * (2 * _bOut * (1 + _nu) - _cOut * (1 - _nu) / (r * r) + _k * (2 * (1 + _nu) * ln + 3 + _nu));
        }

        /// <summary>Circumferential bending moment per unit width.</summary>
        public double Mt(double r)
        {
            if (r <= _c)
                return -_d * (2 * _bIn * (1 + _nu) + _qq * r * r * (4 + 12 * _nu));

            var ln = Math.Log(r / _a);
            return -_d * (2 * _bOut * (1 + _nu) + _cOut * (1 - _nu) / (r * r) + _k * (2 * (1 + _nu) * ln + 1 + 3 * _nu));
        }
    }

    /// <summary>Centre deflection of a clamped plate under a central patch of radius ratio β = c/a: q·a⁴/(64D)·(4β² − 3β⁴ + 4β⁴·ln β).</summary>
    public static double PatchCentreDeflectionClamped(double a, double c, double d, double q)
    {
        var b = c / a;
        return q * Math.Pow(a, 4) / (64 * d) * (4 * b * b - 3 * Math.Pow(b, 4) + 4 * Math.Pow(b, 4) * Math.Log(b));
    }

    /// <summary>Centre deflection of a simply supported plate under a central patch: q·a⁴/(8D)·[(3+ν)/(1+ν)·(β²/2 − β⁴/4) + β⁴/2·ln β − β⁴/8].</summary>
    public static double PatchCentreDeflectionSimple(double a, double c, double nu, double d, double q)
    {
        var b = c / a;
        var b4 = Math.Pow(b, 4);
        return q * Math.Pow(a, 4) / (8 * d) * ((3 + nu) / (1 + nu) * (b * b / 2 - b4 / 4) + b4 / 2 * Math.Log(b) - b4 / 8);
    }

    /// <summary>Centre deflection of a clamped plate under a ring load F at β = b/a: F·a²/(16πD)·(1 − β² + 2β²·ln β).</summary>
    public static double RingCentreDeflectionClamped(double a, double b, double d, double force)
    {
        var beta = b / a;
        return force * a * a / (16 * Math.PI * d) * (1 - beta * beta + 2 * beta * beta * Math.Log(beta));
    }

    /// <summary>Centre deflection of a simply supported plate under a ring load: F·a²/(16πD)·[(3+ν)/(1+ν)·(1 − β²) + 2β²·ln β].</summary>
    public static double RingCentreDeflectionSimple(double a, double b, double nu, double d, double force)
    {
        var beta = b / a;
        return force * a * a / (16 * Math.PI * d) * ((3 + nu) / (1 + nu) * (1 - beta * beta) + 2 * beta * beta * Math.Log(beta));
    }

    /// <summary>The place where a moment measure is greatest: radius and the two moments there.</summary>
    public readonly record struct MomentExtreme(double Radius, double Value, double Mr, double Mt);

    /// <summary>
    /// The greatest von Mises moment √(M_r² − M_r·M_t + M_t²) and the greatest principal moment max(|M_r|, |M_t|) over 0 ≤ r ≤ a:
    /// a grid of 2000 radii plus the load radius, the centre and the edge, then a golden-section refinement round the best point.
    /// </summary>
    public static (MomentExtreme VonMises, MomentExtreme Principal) ScanMoments(AxisymmetricPlate plate)
    {
        ArgumentNullException.ThrowIfNull(plate);
        var a = plate.Radius;
        return (Maximise(plate, a, (m, t) => VonMises(m, t)), Maximise(plate, a, (m, t) => Math.Max(Math.Abs(m), Math.Abs(t))));
    }

    private static MomentExtreme Maximise(AxisymmetricPlate plate, double a, Func<double, double, double> measure)
    {
        double F(double r) => measure(plate.Mr(r), plate.Mt(r));

        const int n = 2000;
        var best = 0.0;
        var bestR = 0.0;
        var bestI = 0;

        for (var i = 0; i <= n; i++)
        {
            var r = a * i / n;
            var v = F(r);

            if (v > best)
            {
                best = v;
                bestR = r;
                bestI = i;
            }
        }

        // The load radius is a kink (ring) or the end of the patch: test it directly.
        var rc = Math.Min(plate.LoadRadius, a);

        if (F(rc) > best)
        {
            best = F(rc);
            bestR = rc;
            bestI = -1;
        }

        if (bestI >= 0 && bestI < n)
        {
            var lo = a * Math.Max(0, bestI - 1) / n;
            var hi = a * Math.Min(n, bestI + 1) / n;
            var g = (Math.Sqrt(5) - 1) / 2;
            var x1 = hi - g * (hi - lo);
            var x2 = lo + g * (hi - lo);
            var f1 = F(x1);
            var f2 = F(x2);

            for (var it = 0; it < 60; it++)
            {
                if (f1 < f2)
                {
                    lo = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = lo + g * (hi - lo);
                    f2 = F(x2);
                }
                else
                {
                    hi = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = hi - g * (hi - lo);
                    f1 = F(x1);
                }
            }

            var rr = 0.5 * (lo + hi);

            if (F(rr) > best)
            {
                best = F(rr);
                bestR = rr;
            }
        }

        return new MomentExtreme(bestR, best, plate.Mr(bestR), plate.Mt(bestR));
    }

    /// <summary>
    /// The frequency parameter λ² of the fundamental axisymmetric mode of a circular plate, ω = (λ²/a²)·√(D/(ρ·t)): clamped from
    /// J₀(λ)·I₁(λ) + I₀(λ)·J₁(λ) = 0 (λ² = 10.2158), simply supported from J₁/J₀ + I₁/I₀ = 2λ/(1 − ν) (λ² = 4.935 at ν = 0.3).
    /// </summary>
    public static double CircularFrequencyParameter(bool clamped, double nu)
    {
        double lambda;

        if (clamped)
        {
            lambda = RootFinding.Bisect(x =>
            {
                Bessel(x, out var j0, out var j1, out var i0, out var i1);
                return j0 * i1 + i0 * j1;
            }, 2.5, 4.0);
        }
        else
        {
            lambda = RootFinding.Bisect(x =>
            {
                Bessel(x, out var j0, out var j1, out var i0, out var i1);
                return j1 * i0 + i1 * j0 - 2 * x * j0 * i0 / (1 - nu);
            }, 0.5, 2.4);
        }

        return lambda * lambda;
    }

    /// <summary>J₀, J₁, I₀, I₁ by their power series (accurate for the arguments up to 4 used here).</summary>
    public static void Bessel(double x, out double j0, out double j1, out double i0, out double i1)
    {
        var q = x * x / 4;
        double tj0 = 1, tj1 = x / 2, ti0 = 1, ti1 = x / 2;
        j0 = 1;
        j1 = x / 2;
        i0 = 1;
        i1 = x / 2;

        for (var k = 1; k < 80; k++)
        {
            tj0 *= -q / ((double)k * k);
            tj1 *= -q / ((double)k * (k + 1));
            ti0 *= q / ((double)k * k);
            ti1 *= q / ((double)k * (k + 1));
            j0 += tj0;
            j1 += tj1;
            i0 += ti0;
            i1 += ti1;

            if (Math.Abs(tj0) + Math.Abs(tj1) + Math.Abs(ti0) + Math.Abs(ti1) < 1e-18)
                break;
        }
    }

    // ============================================================ simply supported rectangle: Navier

    /// <summary>The Navier result for a simply supported rectangle: the centre values, the corner twisting moment and how the series ended.</summary>
    /// <param name="Deflection">Deflection at the centre (the maximum for a symmetric load).</param>
    /// <param name="Mx">M_x at the centre.</param>
    /// <param name="My">M_y at the centre.</param>
    /// <param name="MxyCorner">Twisting moment at a corner (magnitude).</param>
    /// <param name="TermsPerDirection">Odd terms used in each direction (m, n = 1, 3, … 2N − 1).</param>
    /// <param name="Converged">Whether the last relative change was below the tolerance.</param>
    /// <param name="LastChange">The largest relative change of the deflection, the centre von Mises moment and the corner moment over the last doubling.</param>
    public sealed record NavierResult(double Deflection, double Mx, double My, double MxyCorner, int TermsPerDirection, bool Converged, double LastChange)
    {
        public double CentreVonMises => VonMises(Mx, My);

        public double CornerVonMises => VonMises(0, 0, MxyCorner);
    }

    /// <summary>
    /// Navier double series for a simply supported rectangle a × b (x ∈ [0, a], y ∈ [0, b]) under a uniform load q, or a centred patch
    /// <paramref name="patchU"/> × <paramref name="patchV"/> of pressure q: w = Σ W_mn·sin(mπx/a)·sin(nπy/b) over odd m, n with
    /// W_mn = q_mn/(π⁴D(m²/a² + n²/b²)²). Terms are added in shells while N doubles from 8 odd terms per direction to
    /// <paramref name="maxTerms"/>, stopping when the deflection, the centre moments and the corner twisting moment each change by
    /// less than <paramref name="tolerance"/> over a doubling.
    /// </summary>
    public static NavierResult NavierSimplySupported(double a, double b, double nu, double d, double q, double? patchU, double? patchV, int maxTerms = 1024, double tolerance = 1e-6)
    {
        var nTotal = Math.Max(8, maxTerms);
        var xm = new double[nTotal];
        var yn = new double[nTotal];
        var gcx = new double[nTotal];
        var gcy = new double[nTotal];
        var gkx = new double[nTotal];
        var gky = new double[nTotal];
        var patch = patchU is not null && patchV is not null;

        for (var i = 0; i < nTotal; i++)
        {
            double m = 2 * i + 1;
            var sign = (i & 1) == 0 ? 1.0 : -1.0;
            xm[i] = m * m / (a * a);
            yn[i] = m * m / (b * b);

            if (patch)
            {
                var sx = Math.Sin(m * Math.PI * patchU!.Value / (2 * a));
                var sy = Math.Sin(m * Math.PI * patchV!.Value / (2 * b));
                gcx[i] = sx / m;
                gcy[i] = sy / m;
                gkx[i] = sign * sx;
                gky[i] = sign * sy;
            }
            else
            {
                gcx[i] = sign / m;
                gcy[i] = sign / m;
                gkx[i] = 1;
                gky[i] = 1;
            }
        }

        var stages = new List<int>();

        for (var n = 8; n < nTotal; n *= 2)
            stages.Add(n);

        stages.Add(nTotal);

        double s0 = 0, s1 = 0, s2 = 0, sk = 0;
        var done = 0;
        var prevW = double.NaN;
        var prevC = double.NaN;
        var prevK = double.NaN;
        var change = double.PositiveInfinity;
        var converged = false;
        NavierResult? last = null;

        foreach (var stage in stages)
        {
            for (var i = 0; i < stage; i++)
            {
                var xi = xm[i];
                var gi = gcx[i];
                var ki = gkx[i];

                for (var j = i < done ? done : 0; j < stage; j++)
                {
                    var den = xi + yn[j];
                    var inv = 1 / (den * den);
                    var gg = gi * gcy[j] * inv;
                    s0 += gg;
                    s1 += gg * xi;
                    s2 += gg * yn[j];
                    sk += ki * gky[j] * inv;
                }
            }

            done = stage;
            var w = 16 * q / (Math.Pow(Math.PI, 6) * d) * s0;
            var c4 = 16 * q / Math.Pow(Math.PI, 4);
            var mx = c4 * (s1 + nu * s2);
            var my = c4 * (s2 + nu * s1);
            var mxy = Math.Abs((1 - nu) * c4 * sk / (a * b));
            var vc = VonMises(mx, my);

            if (!double.IsNaN(prevW))
            {
                change = Math.Max(Rel(w, prevW), Math.Max(Rel(vc, prevC), Rel(mxy, prevK)));
                converged = change < tolerance;
            }

            prevW = w;
            prevC = vc;
            prevK = mxy;
            last = new NavierResult(w, mx, my, mxy, stage, converged, change);

            if (converged)
                break;
        }

        return last!;
    }

    private static double Rel(double now, double before)
    {
        var scale = Math.Max(Math.Abs(now), 1e-300);
        return Math.Abs(now - before) / scale;
    }

    /// <summary>Deflection and moments of the simply supported rectangle at (x, y) from the first <paramref name="terms"/> odd terms per direction (for profiles and checks).</summary>
    public static (double W, double Mx, double My, double Mxy) NavierAt(double a, double b, double nu, double d, double q, double? patchU, double? patchV, double x, double y, int terms)
    {
        var patch = patchU is not null && patchV is not null;
        double sw = 0, sx = 0, sy = 0, sxy = 0;

        for (var i = 0; i < terms; i++)
        {
            double m = 2 * i + 1;
            var sm = (i & 1) == 0 ? 1.0 : -1.0;
            var fx = Math.Sin(m * Math.PI * x / a);
            var cx = Math.Cos(m * Math.PI * x / a);
            var ax = patch ? sm * Math.Sin(m * Math.PI * patchU!.Value / (2 * a)) : 1.0;

            for (var j = 0; j < terms; j++)
            {
                double n = 2 * j + 1;
                var sn = (j & 1) == 0 ? 1.0 : -1.0;
                var fy = Math.Sin(n * Math.PI * y / b);
                var cy = Math.Cos(n * Math.PI * y / b);
                var ay = patch ? sn * Math.Sin(n * Math.PI * patchV!.Value / (2 * b)) : 1.0;
                var xm = m * m / (a * a);
                var yn = n * n / (b * b);
                var den = xm + yn;
                var wmn = ax * ay / (m * n * den * den);
                sw += wmn * fx * fy;
                sx += wmn * (xm + nu * yn) * fx * fy;
                sy += wmn * (yn + nu * xm) * fx * fy;
                sxy += wmn * m * n / (a * b) * cx * cy;
            }
        }

        var c4 = 16 * q / Math.Pow(Math.PI, 4);
        return (16 * q / (Math.Pow(Math.PI, 6) * d) * sw, c4 * sx, c4 * sy, -(1 - nu) * c4 * sxy);
    }

    // ============================================================ clamped rectangle: Ritz

    /// <summary>
    /// Ritz solution for a rectangle clamped on all four edges under a uniform load or a centred patch: w = Σ c_ij·X_i(x/a)·Y_j(y/b) with
    /// X_k(ξ) = ξ²(1 − ξ)²·P₂ₖ(2ξ − 1) (even Legendre polynomials times the clamped-clamped bubble, so w = w′ = 0 on the edges and the basis is
    /// symmetric about the centre). The coefficients minimise the total potential energy: an approximation (an upper bound on stiffness),
    /// whose deflection converges faster than its moments.
    /// </summary>
    public sealed class ClampedRitz
    {
        private const int Nodes = 56;
        private readonly double _a;
        private readonly double _b;
        private readonly double _nu;
        private readonly double _d;
        private readonly int _k;
        private readonly double[] _c;
        private readonly double[] _massB;
        private readonly double[] _chol;
        private readonly double[] _scale;

        private ClampedRitz(double a, double b, double nu, double d, int k, double[] c, double[] massB, double[] chol, double[] scale)
        {
            _a = a;
            _b = b;
            _nu = nu;
            _d = d;
            _k = k;
            _c = c;
            _massB = massB;
            _chol = chol;
            _scale = scale;
        }

        /// <summary>Terms per direction K.</summary>
        public int Terms => _k;

        /// <summary>Solves the Ritz system with <paramref name="k"/> terms per direction (1 … 24).</summary>
        public static ClampedRitz Solve(double a, double b, double nu, double d, double q, double? patchU, double? patchV, int k)
        {
            if (k < 1 || k > 24)
                throw new CalcEvaluationException("The Ritz solution takes 1 to 24 terms per direction.");

            var (xs, ws) = GaussLegendre01(Nodes);
            var amat = new double[k * k];
            var bmat = new double[k * k];
            var cmat = new double[k * k];
            var x0 = new double[k];
            var x1 = new double[k];
            var x2 = new double[k];

            for (var qn = 0; qn < Nodes; qn++)
            {
                Basis(k, xs[qn], x0, x1, x2);

                for (var i = 0; i < k; i++)
                {
                    for (var j = 0; j < k; j++)
                    {
                        amat[i * k + j] += ws[qn] * x2[i] * x2[j];
                        bmat[i * k + j] += ws[qn] * x0[i] * x0[j];
                        cmat[i * k + j] += ws[qn] * x1[i] * x1[j];
                    }
                }
            }

            var n = k * k;
            var kmat = new double[n * n];
            var a4 = Math.Pow(a, 4);
            var b4 = Math.Pow(b, 4);
            var pre = d * a * b;

            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    for (var p = 0; p < k; p++)
                    {
                        for (var r = 0; r < k; r++)
                        {
                            kmat[(i * k + j) * n + p * k + r] = pre * (amat[i * k + p] * bmat[j * k + r] / a4 + bmat[i * k + p] * amat[j * k + r] / b4 + 2 * cmat[i * k + p] * cmat[j * k + r] / (a * a * b * b));
                        }
                    }
                }
            }

            var fx = LoadIntegrals(k, xs, ws, patchU is null ? 0.0 : 0.5 - patchU.Value / (2 * a), patchU is null ? 1.0 : 0.5 + patchU.Value / (2 * a));
            var fy = LoadIntegrals(k, xs, ws, patchV is null ? 0.0 : 0.5 - patchV.Value / (2 * b), patchV is null ? 1.0 : 0.5 + patchV.Value / (2 * b));
            var f = new double[n];

            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                    f[i * k + j] = q * a * b * fx[i] * fy[j];
            }

            var scale = new double[n];

            for (var i = 0; i < n; i++)
            {
                var diag = kmat[i * n + i];

                if (!(diag > 0) || !double.IsFinite(diag))
                    throw new CalcEvaluationException("The Ritz stiffness matrix is not positive definite.");

                scale[i] = 1 / Math.Sqrt(diag);
            }

            var chol = new double[n * n];

            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j <= i; j++)
                {
                    var sum = kmat[i * n + j] * scale[i] * scale[j];

                    for (var p = 0; p < j; p++)
                        sum -= chol[i * n + p] * chol[j * n + p];

                    if (i == j)
                    {
                        if (!(sum > 0) || !double.IsFinite(sum))
                            throw new CalcEvaluationException("The Ritz stiffness matrix is not positive definite.");

                        chol[i * n + i] = Math.Sqrt(sum);
                    }
                    else
                    {
                        chol[i * n + j] = sum / chol[j * n + j];
                    }
                }
            }

            var solver = new ClampedRitz(a, b, nu, d, k, new double[n], bmat, chol, scale);
            solver.CholeskySolve(f, solver._c);
            return solver;
        }

        private void CholeskySolve(double[] rhs, double[] result)
        {
            // K̃ = S·K·S = L·Lᵀ with S the diagonal scaling: solve L·y = S·f, Lᵀ·z = y, then c = S·z.
            var n = _k * _k;
            var y = new double[n];

            for (var i = 0; i < n; i++)
            {
                var s = rhs[i] * _scale[i];

                for (var p = 0; p < i; p++)
                    s -= _chol[i * n + p] * y[p];

                y[i] = s / _chol[i * n + i];
            }

            for (var i = n - 1; i >= 0; i--)
            {
                var s = y[i];

                for (var p = i + 1; p < n; p++)
                    s -= _chol[p * n + i] * result[p];

                result[i] = s / _chol[i * n + i];
            }

            for (var i = 0; i < n; i++)
                result[i] *= _scale[i];
        }

        private static double[] LoadIntegrals(int k, double[] xs, double[] ws, double lo, double hi)
        {
            var result = new double[k];
            var x0 = new double[k];
            var x1 = new double[k];
            var x2 = new double[k];

            for (var qn = 0; qn < xs.Length; qn++)
            {
                var xi = lo + (hi - lo) * xs[qn];
                Basis(k, xi, x0, x1, x2);

                for (var i = 0; i < k; i++)
                    result[i] += ws[qn] * (hi - lo) * x0[i];
            }

            return result;
        }

        /// <summary>Deflection and moments at (x, y).</summary>
        public (double W, double Mx, double My, double Mxy) At(double x, double y)
        {
            var k = _k;
            var xa = new double[k];
            var xb = new double[k];
            var xc = new double[k];
            var ya = new double[k];
            var yb = new double[k];
            var yc = new double[k];
            Basis(k, x / _a, xa, xb, xc);
            Basis(k, y / _b, ya, yb, yc);
            double w = 0, wxx = 0, wyy = 0, wxy = 0;

            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    var cij = _c[i * k + j];
                    w += cij * xa[i] * ya[j];
                    wxx += cij * xc[i] * ya[j];
                    wyy += cij * xa[i] * yc[j];
                    wxy += cij * xb[i] * yb[j];
                }
            }

            wxx /= _a * _a;
            wyy /= _b * _b;
            wxy /= _a * _b;
            return (w, -_d * (wxx + _nu * wyy), -_d * (wyy + _nu * wxx), -_d * (1 - _nu) * wxy);
        }

        /// <summary>
        /// The greatest von Mises moment and the greatest principal moment over a quarter of the plate (0 ≤ x ≤ a/2, 0 ≤ y ≤ b/2) on a
        /// (<paramref name="grid"/> + 1)² grid including the edges and the centre, with the position of the von Mises maximum.
        /// </summary>
        public (double VonMises, double X, double Y, double Principal) Scan(int grid = 20)
        {
            var k = _k;
            var xa = new double[grid + 1][];
            var xb = new double[grid + 1][];
            var xc = new double[grid + 1][];
            var ya = new double[grid + 1][];
            var yb = new double[grid + 1][];
            var yc = new double[grid + 1][];
            var tx0 = new double[grid + 1][];
            var tx1 = new double[grid + 1][];
            var tx2 = new double[grid + 1][];

            for (var g = 0; g <= grid; g++)
            {
                var xi = 0.5 * g / grid;
                xa[g] = new double[k];
                xb[g] = new double[k];
                xc[g] = new double[k];
                ya[g] = new double[k];
                yb[g] = new double[k];
                yc[g] = new double[k];
                Basis(k, xi, xa[g], xb[g], xc[g]);
                Basis(k, xi, ya[g], yb[g], yc[g]);
                tx0[g] = new double[k];
                tx1[g] = new double[k];
                tx2[g] = new double[k];

                for (var j = 0; j < k; j++)
                {
                    for (var i = 0; i < k; i++)
                    {
                        var cij = _c[i * k + j];
                        tx0[g][j] += cij * xa[g][i];
                        tx1[g][j] += cij * xb[g][i];
                        tx2[g][j] += cij * xc[g][i];
                    }
                }
            }

            double best = 0, bestX = 0, bestY = 0, principal = 0;

            for (var gx = 0; gx <= grid; gx++)
            {
                for (var gy = 0; gy <= grid; gy++)
                {
                    double wxx = 0, wyy = 0, wxy = 0;

                    for (var j = 0; j < k; j++)
                    {
                        wxx += tx2[gx][j] * ya[gy][j];
                        wyy += tx0[gx][j] * yc[gy][j];
                        wxy += tx1[gx][j] * yb[gy][j];
                    }

                    wxx /= _a * _a;
                    wyy /= _b * _b;
                    wxy /= _a * _b;
                    var mx = -_d * (wxx + _nu * wyy);
                    var my = -_d * (wyy + _nu * wxx);
                    var mxy = -_d * (1 - _nu) * wxy;
                    var vm = VonMises(mx, my, mxy);
                    var mean = (mx + my) / 2;
                    var radius = Math.Sqrt(((mx - my) / 2) * ((mx - my) / 2) + mxy * mxy);
                    principal = Math.Max(principal, Math.Max(Math.Abs(mean + radius), Math.Abs(mean - radius)));

                    if (vm > best)
                    {
                        best = vm;
                        bestX = _a * 0.5 * gx / grid;
                        bestY = _b * 0.5 * gy / grid;
                    }
                }
            }

            return (best, bestX, bestY, principal);
        }

        /// <summary>
        /// Rayleigh–Ritz fundamental angular frequency (rad/s) with the same basis: the lowest eigenvalue of K·c = ω²·M·c by inverse iteration
        /// (M = ρt·a·b·(B ⊗ B)). <paramref name="massPerArea"/> is ρ·t in kg/m².
        /// </summary>
        public double FundamentalAngularFrequency(double massPerArea)
        {
            var k = _k;
            var n = k * k;
            var x = new double[n];
            Array.Fill(x, 1.0);
            var rhs = new double[n];
            var y = new double[n];
            var rq = 0.0;

            for (var it = 0; it < 400; it++)
            {
                ApplyMass(x, rhs, massPerArea);
                CholeskySolve(rhs, y);
                var yMx = Dot(y, rhs);
                ApplyMass(y, rhs, massPerArea);
                var yMy = Dot(y, rhs);
                var next = yMx / yMy;
                var norm = Math.Sqrt(Dot(y, y));

                for (var i = 0; i < n; i++)
                    x[i] = y[i] / norm;

                if (it > 0 && Math.Abs(next - rq) <= 1e-13 * Math.Abs(next))
                {
                    rq = next;
                    break;
                }

                rq = next;
            }

            return Math.Sqrt(Math.Max(rq, 0));
        }

        private void ApplyMass(double[] x, double[] result, double massPerArea)
        {
            var k = _k;
            var tmp = new double[k * k];

            // (B ⊗ B)·vec(X) = vec(B·X·B) with X the k × k coefficient matrix (B symmetric).
            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    double s = 0;

                    for (var p = 0; p < k; p++)
                        s += _massB[i * k + p] * x[p * k + j];

                    tmp[i * k + j] = s;
                }
            }

            for (var i = 0; i < k; i++)
            {
                for (var j = 0; j < k; j++)
                {
                    double s = 0;

                    for (var p = 0; p < k; p++)
                        s += tmp[i * k + p] * _massB[p * k + j];

                    result[i * k + j] = s * massPerArea * _a * _b;
                }
            }
        }

        private static double Dot(double[] a, double[] b)
        {
            double s = 0;

            for (var i = 0; i < a.Length; i++)
                s += a[i] * b[i];

            return s;
        }
    }

    /// <summary>
    /// Basis X_k(ξ) = g(ξ)·P₂ₖ(2ξ − 1), g = ξ²(1 − ξ)², k = 0 … K − 1, with its first and second derivatives in ξ.
    /// </summary>
    private static void Basis(int count, double xi, double[] x0, double[] x1, double[] x2)
    {
        var g = xi * xi * (1 - xi) * (1 - xi);
        var g1 = 2 * xi - 6 * xi * xi + 4 * xi * xi * xi;
        var g2 = 2 - 12 * xi + 12 * xi * xi;
        var z = 2 * xi - 1;
        var nmax = 2 * (count - 1);
        var p = new double[nmax + 2];
        var d1 = new double[nmax + 2];
        var d2 = new double[nmax + 2];
        p[0] = 1;

        if (nmax >= 1)
        {
            p[1] = z;
            d1[1] = 1;
        }

        for (var n = 1; n < nmax; n++)
        {
            p[n + 1] = ((2 * n + 1) * z * p[n] - n * p[n - 1]) / (n + 1);
            d1[n + 1] = d1[n - 1] + (2 * n + 1) * p[n];
            d2[n + 1] = d2[n - 1] + (2 * n + 1) * d1[n];
        }

        for (var k = 0; k < count; k++)
        {
            var n = 2 * k;
            var pv = p[n];
            var p1 = 2 * d1[n];
            var p2 = 4 * d2[n];
            x0[k] = g * pv;
            x1[k] = g1 * pv + g * p1;
            x2[k] = g2 * pv + 2 * g1 * p1 + g * p2;
        }
    }

    /// <summary>Gauss–Legendre nodes and weights on [0, 1].</summary>
    private static (double[] X, double[] W) GaussLegendre01(int n)
    {
        var x = new double[n];
        var w = new double[n];

        for (var i = 0; i < (n + 1) / 2; i++)
        {
            var z = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5));
            double pp = 1;

            for (var it = 0; it < 100; it++)
            {
                double p1 = 1, p2 = 0;

                for (var j = 1; j <= n; j++)
                {
                    var p3 = p2;
                    p2 = p1;
                    p1 = ((2.0 * j - 1) * z * p2 - (j - 1.0) * p3) / j;
                }

                pp = n * (z * p1 - p2) / (z * z - 1);
                var z1 = z;
                z = z1 - p1 / pp;

                if (Math.Abs(z - z1) < 1e-15)
                    break;
            }

            x[i] = 0.5 * (1 - z);
            x[n - 1 - i] = 0.5 * (1 + z);
            w[i] = 1 / ((1 - z * z) * pp * pp);
            w[n - 1 - i] = w[i];
        }

        return (x, w);
    }
}
