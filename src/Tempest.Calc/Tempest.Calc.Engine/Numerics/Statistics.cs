namespace Tempest.Calc.Engine.Numerics;

/// <summary>The normal-distribution functions the tolerancing and Monte Carlo work needs.</summary>
public static class Statistics
{
    /// <summary>Standard normal cumulative distribution Φ(z), by the complementary error function.</summary>
    public static double NormalCdf(double z) => 0.5 * Erfc(-z / Math.Sqrt(2.0));

    /// <summary>The fraction of a normal population outside [lower, upper], in parts per million.</summary>
    public static double PartsPerMillionOutside(double mean, double sigma, double? lower, double? upper)
    {
        if (!(sigma > 0))
            return 0;

        var below = lower is { } l ? NormalCdf((l - mean) / sigma) : 0.0;
        // The upper tail as Φ(−z), not 1 − Φ(z): the subtraction cancels to a few digits deep in the tail (Green G-07; the
        // legacy calc.tolerance-stack took the tail directly and was right to 5 s.f. where this was not).
        var above = upper is { } u ? NormalCdf(-(u - mean) / sigma) : 0.0;
        return 1e6 * (below + above);
    }

    /// <summary>C_pk: the nearer limit's distance from the mean over three standard deviations; <see langword="null"/> without a limit or with zero spread.</summary>
    public static double? ProcessCapabilityIndex(double mean, double sigma, double? lower, double? upper)
    {
        if (!(sigma > 0) || (lower is null && upper is null))
            return null;

        var candidates = new List<double>(2);

        if (lower is { } l)
            candidates.Add((mean - l) / (3 * sigma));

        if (upper is { } u)
            candidates.Add((u - mean) / (3 * sigma));

        return candidates.Min();
    }

    /// <summary>Complementary error function, Numerical Recipes' Chebyshev fit (fractional error below 1.2e-7).</summary>
    public static double Erfc(double x)
    {
        var z = Math.Abs(x);
        var t = 1.0 / (1.0 + 0.5 * z);
        var ans = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 +
            t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
        return x >= 0 ? ans : 2.0 - ans;
    }

    /// <summary>Inverse of the standard normal CDF (Acklam's algorithm, relative error about 1e-9), for quantile reporting.</summary>
    public static double NormalQuantile(double p)
    {
        if (!(p > 0 && p < 1))
            throw new CalcEvaluationException("A quantile needs a probability strictly between 0 and 1.");

        double[] a = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
        double[] d = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00];
        const double plow = 0.02425;
        const double phigh = 1 - plow;

        if (p < plow)
        {
            var q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        if (p <= phigh)
        {
            var q = p - 0.5;
            var r = q * q;
            return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q / (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }

        var q2 = Math.Sqrt(-2 * Math.Log(1 - p));
        return -(((((c[0] * q2 + c[1]) * q2 + c[2]) * q2 + c[3]) * q2 + c[4]) * q2 + c[5]) / ((((d[0] * q2 + d[1]) * q2 + d[2]) * q2 + d[3]) * q2 + 1);
    }

    public static double Mean(IReadOnlyList<double> xs)
    {
        ArgumentNullException.ThrowIfNull(xs);

        if (xs.Count == 0)
            throw new CalcEvaluationException("The mean of an empty set is undefined.");

        var s = 0.0;

        foreach (var x in xs)
            s += x;

        return s / xs.Count;
    }

    /// <summary>Sample standard deviation (n − 1).</summary>
    public static double StandardDeviation(IReadOnlyList<double> xs)
    {
        ArgumentNullException.ThrowIfNull(xs);

        if (xs.Count < 2)
            return 0;

        var m = Mean(xs);
        var s = 0.0;

        foreach (var x in xs)
            s += (x - m) * (x - m);

        return Math.Sqrt(s / (xs.Count - 1));
    }

    /// <summary>The p-quantile of a sample by linear interpolation between order statistics.</summary>
    public static double Quantile(IReadOnlyList<double> sorted, double p)
    {
        ArgumentNullException.ThrowIfNull(sorted);

        if (sorted.Count == 0)
            throw new CalcEvaluationException("A quantile of an empty sample is undefined.");

        if (!(p >= 0 && p <= 1))
            throw new CalcEvaluationException("A quantile probability lies between 0 and 1.");

        var pos = p * (sorted.Count - 1);
        var lo = (int)Math.Floor(pos);
        var hi = Math.Min(lo + 1, sorted.Count - 1);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}
