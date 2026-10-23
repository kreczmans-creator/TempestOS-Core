using Tempest.Calc.Engine.Numerics;

namespace Tempest.Calc.Engine.Analysis;

/// <summary>A histogram bin: [Lower, Upper) and the count inside it.</summary>
public sealed record HistogramBin(double Lower, double Upper, int Count);

/// <summary>What a Monte Carlo run says about one output.</summary>
public sealed record MonteCarloResult(
    int Samples,
    ulong Seed,
    double Mean,
    double StandardDeviation,
    double Minimum,
    double Maximum,
    double Percentile05,
    double Median,
    double Percentile95,
    double? LowerLimit,
    double? UpperLimit,
    double? FractionBelowLower,
    double? FractionAboveUpper,
    double? Yield,
    double? DefectsPerMillion,
    double? Cpk,
    IReadOnlyList<HistogramBin> Histogram);

/// <summary>Runs any model (a function of named SI inputs to one SI output) under sampled inputs. The sample count is capped at <see cref="MaxSamples"/>.</summary>
public static class MonteCarlo
{
    public const int MaxSamples = 1_000_000;
    public const int DefaultSamples = 10_000;
    public const int MaxBins = 200;

    public static MonteCarloResult Run(
        Func<IReadOnlyDictionary<string, double>, double> model,
        IReadOnlyDictionary<string, double> nominal,
        IReadOnlyDictionary<string, Distribution> distributions,
        int samples = DefaultSamples,
        ulong seed = 1,
        double? lowerLimit = null,
        double? upperLimit = null,
        int bins = 30,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(nominal);
        ArgumentNullException.ThrowIfNull(distributions);

        if (samples < 1 || samples > MaxSamples)
            throw new CalcLimitException(nameof(MaxSamples), $"A Monte Carlo run takes between 1 and {MaxSamples:N0} samples, not {samples:N0}.");

        if (bins < 1 || bins > MaxBins)
            throw new CalcLimitException(nameof(MaxBins), $"A histogram has between 1 and {MaxBins} bins.");

        if (lowerLimit is { } ll && upperLimit is { } ul && ll > ul)
            throw new CalcEvaluationException("The lower limit is above the upper limit.");

        foreach (var (name, d) in distributions)
        {
            if (!nominal.ContainsKey(name))
                throw new CalcEvaluationException($"'{name}' has a distribution but no nominal value.");

            d.Validate(name);
        }

        var sampler = new Sampler(seed);
        var inputs = new Dictionary<string, double>(nominal, StringComparer.Ordinal);
        var outputs = new double[samples];
        var below = 0;
        var above = 0;

        for (var i = 0; i < samples; i++)
        {
            if ((i & 1023) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            foreach (var (name, d) in distributions)
                inputs[name] = sampler.Next(d);

            var y = model(inputs);

            if (!double.IsFinite(y))
                throw new CalcEvaluationException($"The model produced a non-finite output on sample {i + 1}.");

            outputs[i] = y;

            if (lowerLimit is { } lo && y < lo)
                below++;

            if (upperLimit is { } hi && y > hi)
                above++;
        }

        Array.Sort(outputs);
        var mean = Statistics.Mean(outputs);
        var sigma = Statistics.StandardDeviation(outputs);
        var histogram = BuildHistogram(outputs, bins);
        var fractionBelow = lowerLimit is null ? (double?)null : (double)below / samples;
        var fractionAbove = upperLimit is null ? (double?)null : (double)above / samples;
        var anyLimit = lowerLimit is not null || upperLimit is not null;
        var yield = anyLimit ? 1.0 - (double)(below + above) / samples : (double?)null;

        return new MonteCarloResult(
            samples, seed, mean, sigma, outputs[0], outputs[^1],
            Statistics.Quantile(outputs, 0.05), Statistics.Quantile(outputs, 0.5), Statistics.Quantile(outputs, 0.95),
            lowerLimit, upperLimit, fractionBelow, fractionAbove, yield,
            anyLimit ? 1e6 * (1.0 - yield!.Value) : null,
            Statistics.ProcessCapabilityIndex(mean, sigma, lowerLimit, upperLimit),
            histogram);
    }

    private static IReadOnlyList<HistogramBin> BuildHistogram(double[] sorted, int bins)
    {
        var min = sorted[0];
        var max = sorted[^1];

        if (max == min)
            return [new HistogramBin(min, max, sorted.Length)];

        var width = (max - min) / bins;
        var counts = new int[bins];

        foreach (var x in sorted)
        {
            var b = (int)Math.Min(bins - 1, Math.Floor((x - min) / width));
            counts[b]++;
        }

        var result = new List<HistogramBin>(bins);

        for (var i = 0; i < bins; i++)
            result.Add(new HistogramBin(min + i * width, i == bins - 1 ? max : min + (i + 1) * width, counts[i]));

        return result;
    }
}
