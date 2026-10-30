namespace Tempest.Calc.Engine.Numerics;

/// <summary>Bracketed root finding with an iteration cap. Used by calculators for implicit relations (Colebrook, wet-bulb).</summary>
public static class RootFinding
{
    /// <summary>
    /// Finds x in [lo, hi] with f(x) ≈ 0 by bisection (robust, monotone
    /// convergence), to a relative tolerance on x. f(lo) and f(hi) must
    /// differ in sign. Throws <see cref="CalcLimitException"/> if
    /// <paramref name="maxIterations"/> is exhausted first.
    /// </summary>
    public static double Bisect(Func<double, double> f, double lo, double hi, double tolerance = 1e-12, int maxIterations = 200)
    {
        ArgumentNullException.ThrowIfNull(f);

        if (!(lo < hi) || !double.IsFinite(lo) || !double.IsFinite(hi))
            throw new CalcEvaluationException("A root bracket needs finite lo < hi.");

        var flo = f(lo);
        var fhi = f(hi);

        if (flo == 0)
            return lo;

        if (fhi == 0)
            return hi;

        if (Math.Sign(flo) == Math.Sign(fhi))
            throw new CalcEvaluationException("The bracket does not contain a sign change; no root was found.");

        for (var i = 0; i < maxIterations; i++)
        {
            var mid = 0.5 * (lo + hi);
            var fm = f(mid);

            if (fm == 0 || (hi - lo) <= tolerance * Math.Max(1.0, Math.Abs(mid)))
                return mid;

            if (Math.Sign(fm) == Math.Sign(flo))
            {
                lo = mid;
                flo = fm;
            }
            else
            {
                hi = mid;
            }
        }

        throw new CalcLimitException(nameof(CalcLimits.MaxIterations), $"Root finding did not converge in {maxIterations} iterations.");
    }

    /// <summary>Fixed-point iteration x = g(x) with a cap; returns the converged value or throws.</summary>
    public static double FixedPoint(Func<double, double> g, double start, double relativeTolerance = 1e-10, int maxIterations = 200)
    {
        ArgumentNullException.ThrowIfNull(g);

        var x = start;

        for (var i = 0; i < maxIterations; i++)
        {
            var next = g(x);

            if (!double.IsFinite(next))
                throw new CalcEvaluationException("The iteration diverged to a non-finite value.");

            if (Math.Abs(next - x) <= relativeTolerance * Math.Max(1.0, Math.Abs(next)))
                return next;

            x = next;
        }

        throw new CalcLimitException(nameof(CalcLimits.MaxIterations), $"The iteration did not converge in {maxIterations} iterations.");
    }
}
