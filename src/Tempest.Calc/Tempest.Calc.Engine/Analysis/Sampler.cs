namespace Tempest.Calc.Engine.Analysis;

/// <summary>
/// A seeded, deterministic pseudo-random sampler (xoshiro256** — public
/// domain algorithm, implemented here) so a Monte Carlo result is
/// reproducible from its seed on any platform. Not for cryptography.
/// </summary>
public sealed class Sampler
{
    public const int MaxRedraws = 1_000;

    private ulong _s0, _s1, _s2, _s3;
    private double? _spareNormal;

    public Sampler(ulong seed)
    {
        Seed = seed;
        // SplitMix64 to expand the seed into four non-zero words.
        var x = seed;

        ulong Next()
        {
            x += 0x9E3779B97F4A7C15UL;
            var z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        _s0 = Next();
        _s1 = Next();
        _s2 = Next();
        _s3 = Next();
    }

    public ulong Seed { get; }

    private ulong NextRaw()
    {
        var result = Rotl(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);
        return result;
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    /// <summary>A uniform double in [0, 1).</summary>
    public double NextDouble() => (NextRaw() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>A standard normal deviate (Marsaglia polar method).</summary>
    public double NextStandardNormal()
    {
        if (_spareNormal is { } spare)
        {
            _spareNormal = null;
            return spare;
        }

        double u, v, s;

        do
        {
            u = 2.0 * NextDouble() - 1.0;
            v = 2.0 * NextDouble() - 1.0;
            s = u * u + v * v;
        }
        while (s >= 1.0 || s == 0.0);

        var m = Math.Sqrt(-2.0 * Math.Log(s) / s);
        _spareNormal = v * m;
        return u * m;
    }

    public double Next(Distribution d)
    {
        ArgumentNullException.ThrowIfNull(d);

        switch (d.Kind)
        {
            case DistributionKind.Normal:
                return d.Mean + d.Sigma * NextStandardNormal();

            case DistributionKind.Uniform:
                return d.Lower + (d.Upper - d.Lower) * NextDouble();

            case DistributionKind.Triangular:
            {
                var u = NextDouble();
                var range = d.Upper - d.Lower;

                if (range == 0)
                    return d.Lower;

                var fc = (d.Mode - d.Lower) / range;
                return u < fc
                    ? d.Lower + Math.Sqrt(u * range * (d.Mode - d.Lower))
                    : d.Upper - Math.Sqrt((1 - u) * range * (d.Upper - d.Mode));
            }

            case DistributionKind.TruncatedNormal:
            {
                if (d.Sigma == 0)
                    return Math.Clamp(d.Mean, d.Lower, d.Upper);

                for (var i = 0; i < MaxRedraws; i++)
                {
                    var x = d.Mean + d.Sigma * NextStandardNormal();

                    if (x >= d.Lower && x <= d.Upper)
                        return x;
                }

                // The bounds exclude almost the whole distribution: clamp rather than loop on.
                return Math.Clamp(d.Mean, d.Lower, d.Upper);
            }

            default:
                throw new CalcEvaluationException("Unknown distribution.");
        }
    }
}
