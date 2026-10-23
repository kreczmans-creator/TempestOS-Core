namespace Tempest.Calc.Engine.Analysis;

/// <summary>One bar of a tornado chart: the output at the input's low and high value, and the swing.</summary>
public sealed record TornadoBar(string Input, double LowInput, double HighInput, double OutputAtLow, double OutputAtHigh)
{
    public double Swing => Math.Abs(OutputAtHigh - OutputAtLow);
}

/// <summary>Sensitivity of one output to each input, one at a time, with the others at nominal. Bars are ordered by swing, largest first.</summary>
public static class Sensitivity
{
    public const int MaxInputs = 500;

    /// <summary>
    /// Varies each named input by ±<paramref name="relativeRange"/> (or by the
    /// absolute range given per input) and records the output at both ends.
    /// </summary>
    public static IReadOnlyList<TornadoBar> Tornado(
        Func<IReadOnlyDictionary<string, double>, double> model,
        IReadOnlyDictionary<string, double> nominal,
        IReadOnlyList<string> inputs,
        double relativeRange = 0.10,
        IReadOnlyDictionary<string, (double Low, double High)>? absoluteRanges = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(nominal);
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count > MaxInputs)
            throw new CalcLimitException(nameof(MaxInputs), $"A sensitivity sweep covers at most {MaxInputs} inputs.");

        if (!(relativeRange >= 0) || !double.IsFinite(relativeRange))
            throw new CalcEvaluationException("The relative range must be a finite non-negative number.");

        var bars = new List<TornadoBar>(inputs.Count);

        foreach (var name in inputs)
        {
            if (!nominal.TryGetValue(name, out var n))
                throw new CalcEvaluationException($"'{name}' is not an input of this model.");

            double low, high;

            if (absoluteRanges is not null && absoluteRanges.TryGetValue(name, out var range))
                (low, high) = range;
            else
                (low, high) = (n - Math.Abs(n) * relativeRange, n + Math.Abs(n) * relativeRange);

            var atLow = Evaluate(model, nominal, name, low);
            var atHigh = Evaluate(model, nominal, name, high);
            bars.Add(new TornadoBar(name, low, high, atLow, atHigh));
        }

        return bars.OrderByDescending(b => b.Swing).ThenBy(b => b.Input, StringComparer.Ordinal).ToList();
    }

    private static double Evaluate(Func<IReadOnlyDictionary<string, double>, double> model, IReadOnlyDictionary<string, double> nominal, string name, double value)
    {
        var inputs = new Dictionary<string, double>(nominal, StringComparer.Ordinal) { [name] = value };
        var y = model(inputs);

        if (!double.IsFinite(y))
            throw new CalcEvaluationException($"The model produced a non-finite output with {name} = {Values.CalcValue.FormatNumber(value)}.");

        return y;
    }
}
