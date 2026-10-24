using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Engine.Numerics;

namespace Tempest.Calc.Mechanical.ToleranceStack;

/// <summary>How a contributor varies inside its band.</summary>
public enum ContributorDistribution
{
    /// <summary>Normal about the mid-limit; σ = half-band / k.</summary>
    Normal,

    /// <summary>Uniform across the band; σ = half-band / √3.</summary>
    Uniform,

    /// <summary>Triangular, mode at the mid-limit; σ = half-band / √6.</summary>
    Triangular,
}

/// <summary>
/// One contributor to a one-dimensional tolerance loop, in coherent SI (or
/// any one consistent length unit — the engine only adds and squares).
/// </summary>
/// <param name="Name">What the contributor is.</param>
/// <param name="Sign">+1 when it adds to the result, −1 when it subtracts.</param>
/// <param name="Nominal">The nominal size (zero-based for a position or profile zone).</param>
/// <param name="Half">The half-band: ± this about <paramref name="Mid"/>.</param>
/// <param name="Mid">The mean (mid-limit plus any stated mean shift).</param>
/// <param name="Distribution">How it varies inside the band.</param>
/// <param name="Sigma">The standard deviation of that distribution.</param>
public sealed record Contributor(string Name, double Sign, double Nominal, double Half, double Mid, ContributorDistribution Distribution, double Sigma)
{
    /// <summary>The standard deviation of a contributor with half-band <paramref name="half"/> and <paramref name="distribution"/>; <paramref name="k"/> is the number of standard deviations per half-band for a normal contributor.</summary>
    public static double SigmaFor(double half, ContributorDistribution distribution, double k) => distribution switch
    {
        ContributorDistribution.Uniform => half / Math.Sqrt(3.0),
        ContributorDistribution.Triangular => half / Math.Sqrt(6.0),
        _ => half / k,
    };

    /// <summary>
    /// A toleranced size: mid-limit = nominal + (upper + lower)/2 + shift, half-band = (upper − lower)/2. The deviations are signed
    /// (upper ≥ lower).
    /// </summary>
    public static Contributor Size(string name, double sign, double nominal, double upper, double lower, double shift = 0.0, ContributorDistribution distribution = ContributorDistribution.Normal, double k = 3.0)
    {
        Require(sign, k);

        if (!(lower <= upper))
            throw new CalcEvaluationException($"{name}: the lower deviation is above the upper deviation.");

        var half = (upper - lower) / 2.0;
        return new Contributor(name, sign, nominal, half, nominal + (upper + lower) / 2.0 + shift, distribution, SigmaFor(half, distribution, k));
    }

    /// <summary>
    /// A position or profile zone: it contributes ± half the zone plus half the stated MMC <paramref name="bonus"/> about
    /// nominal + <paramref name="shift"/>.
    /// </summary>
    public static Contributor Zone(string name, double sign, double nominal, double zone, double bonus = 0.0, double shift = 0.0, ContributorDistribution distribution = ContributorDistribution.Normal, double k = 3.0)
    {
        Require(sign, k);

        if (zone < 0)
            throw new CalcEvaluationException($"{name}: a tolerance zone cannot be negative.");

        var half = (zone + bonus) / 2.0;
        return new Contributor(name, sign, nominal, half, nominal + shift, distribution, SigmaFor(half, distribution, k));
    }

    private static void Require(double sign, double k)
    {
        if (sign != 1.0 && sign != -1.0)
            throw new CalcEvaluationException("A contributor's sign is +1 (adds) or −1 (subtracts).");

        if (!(k > 0) || !double.IsFinite(k))
            throw new CalcEvaluationException("The standard deviations per half-band must be positive.");
    }
}

/// <summary>The limits a result must stay within. Either may be absent.</summary>
/// <param name="Minimum">The lowest acceptable result.</param>
/// <param name="Maximum">The highest acceptable result.</param>
public sealed record StackRequirement(double? Minimum, double? Maximum)
{
    /// <summary>Whether any limit is stated.</summary>
    public bool IsStated => Minimum is not null || Maximum is not null;
}

/// <summary>Capability of the predicted distribution against a stated requirement.</summary>
/// <param name="Cpk">C_pk from the nearer limit.</param>
/// <param name="Cp">C_p, only when both limits are stated.</param>
/// <param name="DefectsPerMillion">Predicted parts per million outside the limits (normal approximation).</param>
/// <param name="Yield">Predicted fraction inside the limits, 0–1.</param>
public sealed record StackCapability(double Cpk, double? Cp, double DefectsPerMillion, double Yield);

/// <summary>What the worst-case, RSS and statistical analysis of a stack says (every length in the contributors' unit).</summary>
/// <param name="Contributors">The loop analysed.</param>
/// <param name="Nominal">Σ sign × nominal.</param>
/// <param name="Mean">Σ sign × mid-limit.</param>
/// <param name="WorstCase">Σ half-bands: the worst-case tolerance (±).</param>
/// <param name="WorstCaseMinimum">Mean − worst case.</param>
/// <param name="WorstCaseMaximum">Mean + worst case.</param>
/// <param name="Rss">√Σ half²: the root-sum-square tolerance (±).</param>
/// <param name="RssMinimum">Mean − RSS.</param>
/// <param name="RssMaximum">Mean + RSS.</param>
/// <param name="ModifiedRss">The modified-RSS factor × RSS (±).</param>
/// <param name="Sigma">√Σ σ_i²: the predicted standard deviation of the result.</param>
/// <param name="Requirement">The requirement the result was judged against.</param>
/// <param name="MarginToMinimum">Worst-case minimum − required minimum; <see langword="null"/> without a minimum.</param>
/// <param name="MarginToMaximum">Required maximum − worst-case maximum; <see langword="null"/> without a maximum.</param>
/// <param name="WorstCaseWithinRequirement">Whether the worst-case band sits inside the requirement (within rounding); true with no requirement.</param>
/// <param name="Capability">Capability figures; <see langword="null"/> without a requirement or with zero spread.</param>
/// <param name="WorstCaseShare">Each contributor's share of the worst-case total, per cent.</param>
/// <param name="StatisticalShare">Each contributor's share of Σ half², per cent.</param>
/// <param name="LargestStatisticalIndex">The index of the largest statistical contributor (the first on a tie).</param>
public sealed record StackResult(
    IReadOnlyList<Contributor> Contributors,
    double Nominal,
    double Mean,
    double WorstCase,
    double WorstCaseMinimum,
    double WorstCaseMaximum,
    double Rss,
    double RssMinimum,
    double RssMaximum,
    double ModifiedRss,
    double Sigma,
    StackRequirement Requirement,
    double? MarginToMinimum,
    double? MarginToMaximum,
    bool WorstCaseWithinRequirement,
    StackCapability? Capability,
    IReadOnlyList<double> WorstCaseShare,
    IReadOnlyList<double> StatisticalShare,
    int LargestStatisticalIndex);

/// <summary>
/// The one-dimensional tolerance-stack engine behind M1
/// (<c>mech.tolerance-stack</c>), extracted so other calculators (the
/// scan-to-seal chain, snap-fit undercut stacks) run the same worst-case,
/// RSS and Monte Carlo analysis without going through a worksheet. Pure
/// functions of their arguments: no session, no sheet, no clock; the Monte
/// Carlo is seeded and repeatable.
/// </summary>
public static class ToleranceStackEngine
{
    /// <summary>The most contributors one loop may carry (the M1 table cap).</summary>
    public const int MaxContributors = 200;

    /// <summary>Worst-case, RSS, modified-RSS and statistical analysis of a loop, judged against an optional requirement.</summary>
    /// <param name="contributors">The loop; at least one contributor.</param>
    /// <param name="requirement">The limits, or <see langword="null"/> for none.</param>
    /// <param name="modifiedRssFactor">The Bender-style factor applied to the RSS.</param>
    public static StackResult Analyse(IReadOnlyList<Contributor> contributors, StackRequirement? requirement = null, double modifiedRssFactor = 1.5)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        if (contributors.Count == 0)
            throw new CalcEvaluationException("A tolerance stack needs at least one contributor.");

        if (contributors.Count > MaxContributors)
            throw new CalcLimitException(nameof(MaxContributors), $"A tolerance stack carries at most {MaxContributors} contributors.");

        requirement ??= new StackRequirement(null, null);

        if (requirement.Minimum is { } lo && requirement.Maximum is { } hi && lo > hi)
            throw new CalcEvaluationException("The stated minimum is above the stated maximum.");

        var nominal = contributors.Sum(x => x.Sign * x.Nominal);
        var mean = contributors.Sum(x => x.Sign * x.Mid);
        var worstCase = contributors.Sum(x => x.Half);
        var rss = Math.Sqrt(contributors.Sum(x => x.Half * x.Half));
        var sigma = Math.Sqrt(contributors.Sum(x => x.Sigma * x.Sigma));

        double? marginMin = null;
        double? marginMax = null;
        var within = true;
        StackCapability? capability = null;

        if (requirement.IsStated)
        {
            var slack = Math.Max(Math.Abs(mean), worstCase) * 1e-9;
            within = (requirement.Minimum is null || mean - worstCase >= requirement.Minimum.Value - slack)
                && (requirement.Maximum is null || mean + worstCase <= requirement.Maximum.Value + slack);

            if (requirement.Minimum is { } min)
                marginMin = mean - worstCase - min;

            if (requirement.Maximum is { } max)
                marginMax = max - mean - worstCase;

            if (sigma > 0)
            {
                var cpk = Statistics.ProcessCapabilityIndex(mean, sigma, requirement.Minimum, requirement.Maximum)!.Value;
                var dpmo = Statistics.PartsPerMillionOutside(mean, sigma, requirement.Minimum, requirement.Maximum);
                double? cp = requirement.Minimum is { } l && requirement.Maximum is { } h ? (h - l) / (6 * sigma) : null;
                capability = new StackCapability(cpk, cp, dpmo, 1 - dpmo / 1e6);
            }
        }

        var wcTotal = worstCase > 0 ? worstCase : 1;
        var ssTotal = contributors.Sum(x => x.Half * x.Half) is var ss && ss > 0 ? ss : 1;
        var wcShare = contributors.Select(x => 100 * x.Half / wcTotal).ToArray();
        var stShare = contributors.Select(x => 100 * x.Half * x.Half / ssTotal).ToArray();
        var largest = 0;

        for (var i = 1; i < stShare.Length; i++)
        {
            if (stShare[i] > stShare[largest])
                largest = i;
        }

        return new StackResult(
            contributors, nominal, mean, worstCase, mean - worstCase, mean + worstCase, rss, mean - rss, mean + rss, modifiedRssFactor * rss, sigma,
            requirement, marginMin, marginMax, within, capability, wcShare, stShare, largest);
    }

    /// <summary>
    /// A seeded Monte Carlo of the loop: every contributor sampled from its own distribution about its mid-limit, the result the signed
    /// sum. The same contributors, sample count and seed always give the same result.
    /// </summary>
    /// <param name="contributors">The loop; at least one contributor.</param>
    /// <param name="samples">The sample count, 1 to <see cref="MonteCarlo.MaxSamples"/>.</param>
    /// <param name="seed">The random seed.</param>
    /// <param name="requirement">Limits to count yield against, or <see langword="null"/>.</param>
    /// <param name="bins">The histogram bin count.</param>
    /// <param name="cancellationToken">Cancels a long run.</param>
    public static MonteCarloResult Simulate(IReadOnlyList<Contributor> contributors, int samples, ulong seed, StackRequirement? requirement = null, int bins = 30, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        if (contributors.Count == 0)
            throw new CalcEvaluationException("A tolerance stack needs at least one contributor.");

        var nominalMap = new Dictionary<string, double>(StringComparer.Ordinal);
        var dists = new Dictionary<string, Distribution>(StringComparer.Ordinal);
        var keys = new string[contributors.Count]; // built once: a string per sample per contributor dominated the run at the caps (R-21)

        for (var i = 0; i < contributors.Count; i++)
        {
            var x = contributors[i];
            var key = $"c{i + 1}";
            keys[i] = key;
            nominalMap[key] = x.Mid;
            dists[key] = x.Distribution switch
            {
                ContributorDistribution.Uniform => Distribution.Uniform(x.Mid - x.Half, x.Mid + x.Half),
                ContributorDistribution.Triangular => Distribution.Triangular(x.Mid - x.Half, x.Mid, x.Mid + x.Half),
                _ => Distribution.Normal(x.Mid, x.Sigma),
            };
        }

        var signs = contributors.Select(x => x.Sign).ToArray();

        return MonteCarlo.Run(v =>
        {
            var s = 0.0;

            for (var i = 0; i < signs.Length; i++)
                s += signs[i] * v[keys[i]];

            return s;
        }, nominalMap, dists, samples, seed, requirement?.Minimum, requirement?.Maximum, bins, cancellationToken);
    }
}
