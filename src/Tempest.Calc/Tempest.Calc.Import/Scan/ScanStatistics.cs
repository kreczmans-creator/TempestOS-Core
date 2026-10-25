using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Tempest.Calc.Import.Scan;

/// <summary>
/// The statistics of one feature over N parts, from first principles.
/// <list type="bullet">
/// <item><description>Mean x̄ = Σx/N and the overall sample standard deviation s = √(Σ(x − x̄)²/(N − 1)).</description></item>
/// <item><description><b>Overall σ</b> (Pp, Ppk) is s. <b>Within σ</b> (Cp, Cpk) is the pooled standard deviation of consecutive subgroups of the stated size,
/// √(Σⱼ Σ(x − x̄ⱼ)² / Σⱼ(nⱼ − 1)); with no subgrouping (size 1, which a table of one measurement per part is) there is nothing to pool, the within σ is s, and
/// <b>Cp = Pp and Cpk = Ppk</b>. A scan table does not carry the order in which parts were made, so no moving-range estimate is made.</description></item>
/// <item><description>Cp = (USL − LSL)/(6σ), Cpk = min(USL − x̄, x̄ − LSL)/(3σ) (one limit only: that side alone, and no Cp); Pp, Ppk the same with the overall σ.</description></item>
/// <item><description>Normality: the Anderson–Darling statistic A² = −N − (1/N) Σᵢ (2i − 1)[ln Φ(zᵢ) + ln(1 − Φ(z_{N+1−i}))], zᵢ = (x₍ᵢ₎ − x̄)/s, with the
/// modification A*² = A²(1 + 0.75/N + 2.25/N²) for estimated mean and variance and the p-value approximation of D'Agostino and Stephens (Goodness-of-Fit Techniques, 1986,
/// section 4.4; Anderson and Darling, 1954). Implemented from the published formulae; no table is shipped.</description></item>
/// <item><description>Outliers: Tukey's fences (Q1 − 1.5·IQR, Q3 + 1.5·IQR, quartiles by linear interpolation of the order statistics; three IQR is "far out").
/// They are reported, and left out of the statistics only on request. Nothing is ever dropped silently.</description></item>
/// </list>
/// </summary>
public static class ScanStatistics
{
    /// <summary>The fewest parts a standard deviation can be formed from.</summary>
    public const int MinPartsForSigma = 2;

    /// <summary>The fewest parts the Anderson–Darling test is run on.</summary>
    public const int MinPartsForNormality = 8;

    /// <summary>Statistics of one feature at one stage.</summary>
    public static ScanFeatureStats Compute(string feature, string stage, IReadOnlyList<ScanObservation> observations, IReadOnlyList<ScanFileInfo> files, ScanSettings settings, ImportBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(settings);

        var warnings = new List<string>();
        var total = observations.Count;
        var fileIndexes = observations.Select(o => o.File).Distinct().Order().ToList();
        var setHash = SetHash(fileIndexes, files);

        var mixed = observations.Any(o => o.DeviationOnly) && observations.Any(o => !o.DeviationOnly);
        var deviationOnly = total > 0 && observations.All(o => o.DeviationOnly);

        // Outliers are found on every value and reported; whether they stay in the statistics is the person's choice.
        var outliers = FindOutliers(observations, budget);
        var excluded = settings.ExcludeOutliers && outliers.Count > 0;
        var flagged = outliers.Select(o => (o.File, o.Line)).ToHashSet();

        // Subgroups are cut by position in the table before anything is left out, so leaving an outlier out does not move every later part into another subgroup.
        var usedIndex = new List<int>(total);

        for (var i = 0; i < total; i++)
        {
            budget?.Tick();

            if (!excluded || !flagged.Contains((observations[i].File, observations[i].Line)))
                usedIndex.Add(i);
        }

        var used = usedIndex.Select(i => observations[i]).ToList();

        if (outliers.Count > 0)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{outliers.Count} of {total} observation(s) flagged as outliers by Tukey's fences ({outliers.Count(o => o.Far)} far out); {(excluded ? "left out of the statistics at your request" : "kept in the statistics")}."));
        }

        var n = used.Count;
        var seenSerials = new HashSet<string>(StringComparer.Ordinal);
        var repeatedSerials = new HashSet<string>(StringComparer.Ordinal);

        foreach (var o in used)
        {
            if (o.Serial.Length > 0 && !seenSerials.Add(o.Serial))
                repeatedSerials.Add(o.Serial);
        }

        var duplicates = repeatedSerials.Count;
        var parts = seenSerials.Count;

        if (duplicates > 0)
            warnings.Add($"{duplicates} serial(s) appear more than once; each row is counted as a measurement, so the parts are fewer than the rows.");

        var serialSummary = SerialSummary(used, n, parts);
        var nominal = Common(used.Select(o => o.Nominal), "nominal", warnings, budget);
        var lsl = Common(used.Select(o => o.Lsl), "lower limit", warnings, budget);
        var usl = Common(used.Select(o => o.Usl), "upper limit", warnings, budget);

        if (mixed)
        {
            warnings.Add("Some rows state an actual or nominal value and others only a deviation, so they are not one population; the statistics are not formed. Give every row an actual value (or a nominal with its deviation), or separate the files.");
            return NotComputable(feature, stage, n, total, parts, nominal, lsl, usl, outliers, excluded, deviationOnly, fileIndexes, setHash, serialSummary, warnings, used.Count > 0 ? used[0].Value : 0);
        }

        if (n < MinPartsForSigma)
        {
            warnings.Add(n == 0
                ? "No usable observation; nothing can be computed."
                : $"Only {n} usable observation; a standard deviation needs at least {MinPartsForSigma} parts.");
            return NotComputable(feature, stage, n, total, parts, nominal, lsl, usl, outliers, excluded, deviationOnly, fileIndexes, setHash, serialSummary, warnings, n == 1 ? used[0].Value : 0);
        }

        var values = used.Select(o => o.Value).ToArray();
        var mean = Mean(values);
        var s = Sigma(values, mean);
        var within = s;

        if (settings.SubgroupSize >= 2)
        {
            var pooled = PooledSigma(values, usedIndex.Select(i => i / settings.SubgroupSize).ToArray(), out var dropped);

            if (pooled is { } p)
            {
                within = p;

                if (dropped > 0)
                    warnings.Add($"{dropped} observation(s) alone in their subgroup (an incomplete final subgroup, or the only one left after outliers were left out) were not used for the within-subgroup σ.");
            }
            else
            {
                warnings.Add("The subgroups give no degrees of freedom (every subgroup would hold one observation); the within-subgroup σ is taken as the overall σ.");
            }
        }

        if (!double.IsFinite(mean) || !double.IsFinite(s) || !double.IsFinite(within))
        {
            warnings.Add("The values are too large to form a standard deviation without overflow; the statistics are not formed.");
            return NotComputable(feature, stage, n, total, parts, nominal, lsl, usl, outliers, excluded, deviationOnly, fileIndexes, setHash, serialSummary, warnings, values[0]);
        }

        double? cp = null, cpk = null, pp = null, ppk = null;

        if (lsl is { } l && usl is { } u && !(u > l))
        {
            warnings.Add("The upper limit is not above the lower limit; no capability index is formed.");
        }
        else
        {
            if (s > 0)
                (pp, ppk) = Capability(mean, s, lsl, usl);
            else
                warnings.Add("The standard deviation is zero (every value is the same), so the capability indices are undefined and are not reported.");

            if (within > 0)
                (cp, cpk) = Capability(mean, within, lsl, usl);
            else if (s > 0)
                warnings.Add("The within-subgroup standard deviation is zero (the values inside every subgroup are the same), so Cp and Cpk are undefined and are not reported; Pp and Ppk use the overall standard deviation.");

            if (lsl is null && usl is null)
                warnings.Add("No tolerance is mapped, so no capability index is formed.");
            else if ((lsl is null || usl is null) && s > 0)
                warnings.Add("Only one limit is given; Cpk and Ppk are formed for that side and Cp and Pp are not defined.");
        }

        var normality = AndersonDarling(values, settings.Alpha, budget);

        if (n < settings.MinParts)
            warnings.Add($"Only {n} part(s): capability indices from fewer than {settings.MinParts} parts are uncertain, and a fitted distribution is only as good as its sample.");

        if (normality.Verdict == NormalityVerdict.Rejected)
            warnings.Add($"A normal distribution is rejected at the {settings.Alpha.ToString("0.##", CultureInfo.InvariantCulture)} level (p = {Format(normality.PValue)}); a fitted normal can misstate the tails.");
        else if (normality.Verdict == NormalityVerdict.NotAssessed)
            warnings.Add("Normality not assessed: " + normality.Note);

        return new ScanFeatureStats(
            feature, stage, n, total, parts, Computable: true, nominal, lsl, usl, mean, s, within, values.Min(), values.Max(),
            cp, cpk, pp, ppk, normality, outliers, excluded, deviationOnly, fileIndexes, setHash, serialSummary, warnings);
    }

    /// <summary>Cp and Cpk (or Pp and Ppk) for a mean, a standard deviation (positive) and limits; the indices that need a missing limit are null.</summary>
    public static (double? Cp, double? Cpk) Capability(double mean, double sigma, double? lsl, double? usl)
    {
        if (!(sigma > 0) || !double.IsFinite(sigma) || !double.IsFinite(mean))
            return (null, null);

        double? cp = null;
        double? cpk = null;

        if (lsl is { } l && usl is { } u)
        {
            cp = (u - l) / (6 * sigma);
            cpk = Math.Min(u - mean, mean - l) / (3 * sigma);
        }
        else if (usl is { } onlyU)
        {
            cpk = (onlyU - mean) / (3 * sigma);
        }
        else if (lsl is { } onlyL)
        {
            cpk = (mean - onlyL) / (3 * sigma);
        }

        return (Finite(cp), Finite(cpk));
    }

    /// <summary>The mean, summed with Neumaier's compensation.</summary>
    public static double Mean(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
            return 0;

        double sum = 0, compensation = 0;

        foreach (var v in values)
        {
            var t = sum + v;
            compensation += Math.Abs(sum) >= Math.Abs(v) ? sum - t + v : v - t + sum;
            sum = t;
        }

        return (sum + compensation) / values.Count;
    }

    /// <summary>The sample standard deviation (N − 1), by the corrected two-pass algorithm.</summary>
    public static double Sigma(IReadOnlyList<double> values, double mean)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count < 2)
            return 0;

        double sumSquares = 0, sumDeviations = 0;

        foreach (var v in values)
        {
            var d = v - mean;
            sumSquares += d * d;
            sumDeviations += d;
        }

        var variance = (sumSquares - sumDeviations * sumDeviations / values.Count) / (values.Count - 1);
        return Math.Sqrt(Math.Max(0, variance));
    }

    /// <summary>
    /// The pooled standard deviation of subgroups given by <paramref name="groupIds"/> (one id per value, non-decreasing: the subgroup each value belonged to
    /// when the table was cut, whether or not every value of it is still in use). A subgroup left with one value has no degree of freedom and is dropped.
    /// </summary>
    public static double? PooledSigma(IReadOnlyList<double> values, IReadOnlyList<int> groupIds, out int dropped)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(groupIds);
        dropped = 0;
        double sumSquares = 0;
        var dof = 0;
        var start = 0;

        while (start < values.Count)
        {
            var end = start + 1;

            while (end < values.Count && groupIds[end] == groupIds[start])
                end++;

            var count = end - start;

            if (count < 2)
            {
                dropped += count;
            }
            else
            {
                double mean = 0;

                for (var i = start; i < end; i++)
                    mean += values[i];

                mean /= count;

                for (var i = start; i < end; i++)
                {
                    var d = values[i] - mean;
                    sumSquares += d * d;
                }

                dof += count - 1;
            }

            start = end;
        }

        return dof == 0 ? null : Math.Sqrt(sumSquares / dof);
    }

    /// <summary>The pooled standard deviation of consecutive subgroups of <paramref name="size"/>; the last subgroup is used if it holds at least two. <see langword="null"/> when no subgroup has a degree of freedom.</summary>
    public static double? PooledSigma(IReadOnlyList<double> values, int size, out int dropped)
    {
        ArgumentNullException.ThrowIfNull(values);
        dropped = 0;

        if (size < 2)
            return null;

        double sumSquares = 0;
        var dof = 0;

        for (var start = 0; start < values.Count; start += size)
        {
            var count = Math.Min(size, values.Count - start);

            if (count < 2)
            {
                dropped += count;
                continue;
            }

            double mean = 0;

            for (var i = 0; i < count; i++)
                mean += values[start + i];

            mean /= count;

            for (var i = 0; i < count; i++)
            {
                var d = values[start + i] - mean;
                sumSquares += d * d;
            }

            dof += count - 1;
        }

        return dof == 0 ? null : Math.Sqrt(sumSquares / dof);
    }

    /// <summary>The Anderson–Darling normality test with estimated mean and variance.</summary>
    public static ScanNormality AndersonDarling(IReadOnlyList<double> values, double alpha = 0.05, ImportBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        var n = values.Count;

        if (n < MinPartsForNormality)
            return new ScanNormality(NormalityVerdict.NotAssessed, null, null, null, $"the test needs at least {MinPartsForNormality} parts and there are {n}.");

        var sorted = values.ToArray();
        Array.Sort(sorted);
        var mean = Mean(sorted);
        var s = Sigma(sorted, mean);

        if (!(s > 0) || !double.IsFinite(s))
            return new ScanNormality(NormalityVerdict.NotAssessed, null, null, null, "every value is the same, so there is no distribution to test.");

        var z = new double[n];

        for (var i = 0; i < n; i++)
            z[i] = (sorted[i] - mean) / s;

        double sum = 0;

        for (var i = 0; i < n; i++)
        {
            budget?.Tick();
            sum += (2 * (i + 1) - 1) * (NormalMath.LogCdf(z[i]) + NormalMath.LogCdf(-z[n - 1 - i]));
        }

        var a2 = -n - sum / n;
        var aStar = a2 * (1 + 0.75 / n + 2.25 / ((double)n * n));

        if (!double.IsFinite(a2) || !double.IsFinite(aStar))
            return new ScanNormality(NormalityVerdict.NotAssessed, null, null, null, "the statistic is not finite for these values.");

        var p = PValue(aStar);
        return new ScanNormality(p < alpha ? NormalityVerdict.Rejected : NormalityVerdict.Consistent, a2, aStar, p, string.Create(CultureInfo.InvariantCulture, $"Anderson–Darling A*² = {aStar:0.####}, p = {p:0.####}."));
    }

    /// <summary>The p-value approximation for A*² (estimated mean and variance) of D'Agostino and Stephens (1986), clipped to [0, 1].</summary>
    public static double PValue(double aStar)
    {
        double p;

        if (aStar < 0.2)
            p = 1 - Math.Exp(-13.436 + 101.14 * aStar - 223.73 * aStar * aStar);
        else if (aStar < 0.34)
            p = 1 - Math.Exp(-8.318 + 42.796 * aStar - 59.938 * aStar * aStar);
        else if (aStar < 0.6)
            p = Math.Exp(0.9177 - 4.279 * aStar - 1.38 * aStar * aStar);
        else if (aStar < 10)
            p = Math.Exp(1.2937 - 5.709 * aStar + 0.0186 * aStar * aStar);
        else
            p = 3.7e-24;

        return Math.Min(1, Math.Max(0, p));
    }

    /// <summary>Quartile by linear interpolation between order statistics (the "type 7" definition); <paramref name="sorted"/> must be sorted ascending.</summary>
    public static double Quantile(IReadOnlyList<double> sorted, double p)
    {
        ArgumentNullException.ThrowIfNull(sorted);

        if (sorted.Count == 0)
            return 0;

        var h = (sorted.Count - 1) * p;
        var lo = (int)Math.Floor(h);
        var hi = Math.Min(lo + 1, sorted.Count - 1);
        return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
    }

    // ------------------------------------------------------------------

    private static List<ScanOutlier> FindOutliers(IReadOnlyList<ScanObservation> observations, ImportBudget? budget)
    {
        var result = new List<ScanOutlier>();

        if (observations.Count < 4)
            return result;

        var sorted = observations.Select(o => o.Value).Order().ToArray();
        var q1 = Quantile(sorted, 0.25);
        var q3 = Quantile(sorted, 0.75);
        var iqr = q3 - q1;

        if (!double.IsFinite(iqr))
            return result;

        var lo = q1 - 1.5 * iqr;
        var hi = q3 + 1.5 * iqr;
        var farLo = q1 - 3 * iqr;
        var farHi = q3 + 3 * iqr;

        foreach (var o in observations)
        {
            budget?.Tick();

            if (o.Value < lo)
                result.Add(new ScanOutlier(o.File, o.Line, o.Serial, o.Value, $"below Q1 − 1.5·IQR = {Format(lo)}", o.Value < farLo));
            else if (o.Value > hi)
                result.Add(new ScanOutlier(o.File, o.Line, o.Serial, o.Value, $"above Q3 + 1.5·IQR = {Format(hi)}", o.Value > farHi));
        }

        return result;
    }

    private static ScanFeatureStats NotComputable(string feature, string stage, int n, int total, int parts, double? nominal, double? lsl, double? usl, List<ScanOutlier> outliers, bool excluded, bool deviationOnly, List<int> files, string setHash, string serialSummary, List<string> warnings, double mean) =>
        new(feature, stage, n, total, parts, Computable: false, nominal, lsl, usl, mean, 0, 0, mean, mean, null, null, null, null,
            new ScanNormality(NormalityVerdict.NotAssessed, null, null, null, "not computed."), outliers, excluded, deviationOnly, files, setHash, serialSummary, warnings);

    /// <summary>
    /// The value most rows agree on (values are told apart at nine decimals, by a dictionary, so the cost is linear in the rows); a warning when rows
    /// disagree beyond rounding. The first value to reach the highest count wins a tie.
    /// </summary>
    private static double? Common(IEnumerable<double?> values, string what, List<string> warnings, ImportBudget? budget)
    {
        var counts = new Dictionary<double, int>();
        var order = new List<double>();
        var present = 0;

        foreach (var v in values)
        {
            budget?.Tick();

            if (v is not { } x)
                continue;

            present++;
            var key = Math.Round(x, 9);

            if (counts.TryGetValue(key, out var seen))
            {
                counts[key] = seen + 1;
            }
            else
            {
                counts[key] = 1;
                order.Add(key);
            }
        }

        if (present == 0)
            return null;

        var best = order[0];

        foreach (var key in order)
        {
            if (counts[key] > counts[best])
                best = key;
        }

        if (order.Count > 1)
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"The {what} differs between rows ({order.Count} different values); the most common ({Format(best)}, in {counts[best]} of {present} rows) is used."));

        return best;
    }

    private static string SerialSummary(IReadOnlyList<ScanObservation> used, int n, int distinct)
    {
        if (distinct == 0)
            return string.Create(CultureInfo.InvariantCulture, $"n={n}");

        var first = used.First(o => o.Serial.Length > 0).Serial;
        var last = used.Last(o => o.Serial.Length > 0).Serial;
        return distinct == 1 ? first : string.Create(CultureInfo.InvariantCulture, $"{first}..{last} (n={distinct})");
    }

    private static string SetHash(IReadOnlyList<int> fileIndexes, IReadOnlyList<ScanFileInfo> files)
    {
        var hashes = fileIndexes.Where(i => i >= 0 && i < files.Count).Select(i => files[i].Sha256).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        return hashes.Count switch
        {
            0 => new string('0', 64),
            1 => hashes[0],
            _ => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", hashes)))),
        };
    }

    private static double? Finite(double? v) => v is { } x && double.IsFinite(x) ? x : null;

    private static string Format(double? v) => v is { } x ? x.ToString("0.######", CultureInfo.InvariantCulture) : "—";
}
