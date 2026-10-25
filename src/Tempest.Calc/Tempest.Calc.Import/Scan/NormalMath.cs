namespace Tempest.Calc.Import.Scan;

/// <summary>
/// The standard normal functions the normality test needs, accurate in the far tails where the Anderson–Darling sum is
/// most sensitive. The upper tail Q(x) = 1 − Φ(x) for x below 3 is the power series of Marsaglia (2004, "Evaluating the
/// Normal Distribution", J. Stat. Software 11(5)); from 3 up it is the continued fraction for Mills' ratio evaluated in
/// the log domain, so it neither cancels nor underflows. Both are published, closed-form methods (cited, not copied).
/// </summary>
internal static class NormalMath
{
    private const double HalfLogTwoPi = 0.91893853320467274178;
    private const double InvSqrtTwoPi = 0.39894228040143267794;
    private const int FractionTerms = 400;

    /// <summary>ln Q(x) for x ≥ 0, where Q is the upper tail 1 − Φ(x).</summary>
    public static double LogUpperTail(double x)
    {
        if (double.IsNaN(x))
            return double.NaN;

        if (x < 0)
            return Log1P(-Math.Exp(LogUpperTail(-x)));

        if (x < 3.0)
        {
            var q = 0.5 - Series(x) * InvSqrtTwoPi * Math.Exp(-0.5 * x * x);
            return Math.Log(q);
        }

        // Mills' ratio: Q(x) = φ(x) / (x + 1/(x + 2/(x + 3/(x + …)))), evaluated from the tail inwards.
        var tail = x;

        for (var k = FractionTerms; k >= 1; k--)
            tail = x + k / tail;

        return -0.5 * x * x - HalfLogTwoPi - Math.Log(tail);
    }

    /// <summary>ln Φ(z).</summary>
    public static double LogCdf(double z)
    {
        if (double.IsNaN(z))
            return double.NaN;

        // Φ(z) = Q(−z); for large positive z the log of a number just below one is log1p(−Q(z)).
        return z < 0 ? LogUpperTail(-z) : Log1P(-Math.Exp(LogUpperTail(z)));
    }

    /// <summary>Φ(z).</summary>
    public static double Cdf(double z) => double.IsNaN(z) ? double.NaN : z < 0 ? Math.Exp(LogUpperTail(-z)) : 1.0 - Math.Exp(LogUpperTail(z));

    /// <summary>ln(1 + x) without losing the digits of a small x (the Kahan identity).</summary>
    internal static double Log1P(double x)
    {
        var u = 1.0 + x;
        return u == 1.0 ? x : u <= 0 ? double.NaN : Math.Log(u) * x / (u - 1.0);
    }

    private static double Series(double x)
    {
        var sum = x;
        var term = x;
        var q = x * x;

        for (var i = 3; i < 400; i += 2)
        {
            term *= q / i;
            var next = sum + term;

            if (next == sum)
                break;

            sum = next;
        }

        return sum;
    }
}
