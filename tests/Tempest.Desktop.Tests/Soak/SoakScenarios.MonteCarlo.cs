using System.Diagnostics;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.Tests.Soak;

internal static partial class SoakScenarios
{
    // ------------------------------------------------------------------
    // 4. Monte Carlo and sensitivity
    // ------------------------------------------------------------------

    private const string MonteCarloTemplate = "mech.material-margin";

    /// <summary>
    /// The specification's "1,000 Monte Carlo runs" is read as 1,000 runs of the Monte Carlo service, each at its default sample count
    /// (<see cref="MonteCarlo.DefaultSamples"/>), so long as that is within the service's own cap. There are two services: the engine's
    /// (<see cref="MonteCarlo.Run"/>, cap <see cref="MonteCarlo.MaxSamples"/>) and the one over a whole calculator
    /// (<see cref="CalcRunner.MonteCarlo"/>, where each sample is a full calculator run, cap <see cref="CalcRunner.MaxCalculatorSamples"/>).
    /// Both are run. Every run is checked against the distribution it samples; a run repeated with the same seed gives the same answer; a run
    /// past each cap is refused; a cancelled run stops. A tornado sweep is run the same number of times.
    /// </summary>
    public static Task MonteCarloAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        // The engine's service over a model with a known answer: y = a + 2b, a ~ Normal(10, 1), b ~ Uniform(0, 2): mean 12, sigma sqrt(1 + 4/3) = 1.5275.
        static double Model(IReadOnlyDictionary<string, double> x) => x["a"] + 2 * x["b"];
        var nominal = new Dictionary<string, double> { ["a"] = 10, ["b"] = 1 };
        var distributions = new Dictionary<string, Distribution> { ["a"] = Distribution.Normal(10, 1), ["b"] = Distribution.Uniform(0, 2) };
        var engine = Stopwatch.StartNew();
        var worstMean = 0.0;
        var worstSigma = 0.0;
        var engineSamples = 0L;

        for (var run = 0; run < scale.EngineMonteCarloRuns; run++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = MonteCarlo.Run(Model, nominal, distributions, seed: (ulong)(run + 1), lowerLimit: 6, upperLimit: 18, cancellationToken: cancellationToken);
            engineSamples += result.Samples;
            worstMean = Math.Max(worstMean, Math.Abs(result.Mean - 12));
            worstSigma = Math.Max(worstSigma, Math.Abs(result.StandardDeviation - 1.5275));
            outcome.Expect(result.Samples == MonteCarlo.DefaultSamples, () => $"Engine run {run}: {result.Samples} samples, not the default {MonteCarlo.DefaultSamples}.");
            outcome.Expect(result.Minimum <= result.Percentile05 && result.Percentile05 <= result.Median && result.Median <= result.Percentile95 && result.Percentile95 <= result.Maximum, () => $"Engine run {run}: the percentiles are out of order.");
            outcome.Expect(result.Histogram.Sum(b => b.Count) == result.Samples, () => $"Engine run {run}: the histogram does not hold every sample.");
            outcome.Expect(result.Yield is { } y && y is >= 0 and <= 1, () => $"Engine run {run}: the yield is not a fraction.");

            if (run % 100 == 0)
            {
                var again = MonteCarlo.Run(Model, nominal, distributions, seed: (ulong)(run + 1), lowerLimit: 6, upperLimit: 18, cancellationToken: cancellationToken);
                outcome.Expect(again.Mean == result.Mean && again.StandardDeviation == result.StandardDeviation && again.Histogram.Select(b => b.Count).SequenceEqual(result.Histogram.Select(b => b.Count)), () => $"Engine run {run}: the same seed gave a different answer.");
            }

            if ((run + 1) % 250 == 0)
                SoakScale.Progress($"monte carlo (engine): {run + 1} of {scale.EngineMonteCarloRuns}");
        }

        engine.Stop();
        outcome.Expect(worstMean < 0.12, () => $"Engine runs: a run's mean was {worstMean:0.000} from the true 12 (limit 0.12 at {MonteCarlo.DefaultSamples:N0} samples).");
        outcome.Expect(worstSigma < 0.12, () => $"Engine runs: a run's standard deviation was {worstSigma:0.000} from the true 1.5275.");

        // Caps: the largest run is accepted (once), one past it, zero samples and a bad bin count are refused, and a cancelled run stops.
        var capWatch = Stopwatch.StartNew();
        var atCap = MonteCarlo.Run(Model, nominal, distributions, MonteCarlo.MaxSamples, 7, cancellationToken: cancellationToken);
        capWatch.Stop();
        outcome.Expect(atCap.Samples == MonteCarlo.MaxSamples && Math.Abs(atCap.Mean - 12) < 0.02, () => $"The engine run at its cap ({MonteCarlo.MaxSamples:N0} samples) gave mean {atCap.Mean:0.0000}.");
        outcome.Expect(Record.Exception(() => MonteCarlo.Run(Model, nominal, distributions, MonteCarlo.MaxSamples + 1)) is CalcLimitException, () => "One sample past the engine's cap was not refused with a limit exception.");
        outcome.Expect(Record.Exception(() => MonteCarlo.Run(Model, nominal, distributions, 0)) is CalcLimitException, () => "Zero samples was not refused with a limit exception.");
        outcome.Expect(Record.Exception(() => MonteCarlo.Run(Model, nominal, distributions, 10, bins: MonteCarlo.MaxBins + 1)) is CalcLimitException, () => "A histogram with more bins than the cap was not refused.");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            outcome.Expect(Record.Exception(() => MonteCarlo.Run(Model, nominal, distributions, MonteCarlo.MaxSamples, cancellationToken: cancelled.Token)) is OperationCanceledException, () => "A cancelled engine run did not stop.");
        }

        // The service over a real calculator: material margin, n_y = f_y / sigma, with sigma ~ Normal(120, 10) MPa and f_y ~ Normal(300, 15) MPa.
        var entry = host.Service.Catalogue.Get(MonteCarloTemplate);
        var inputs = new CalcInputSet(SoakExamples.For(MonteCarloTemplate).Single(e => e.Name == "s355-yield-only").Inputs);
        var spec = new Dictionary<string, Distribution> { ["sigma"] = Distribution.Normal(120e6, 10e6), ["f_y"] = Distribution.Normal(300e6, 15e6) };
        var calculator = Stopwatch.StartNew();
        var calculatorSamples = 0L;
        var worstMargin = 0.0;

        for (var run = 0; run < scale.CalculatorMonteCarloRuns; run++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = CalcRunner.MonteCarlo(entry.Calculator, inputs, "n_gov", spec, scale.CalculatorMonteCarloSamples, (ulong)(run + 1), lowerLimit: 1.0, cancellationToken: cancellationToken);
            calculatorSamples += result.Samples;
            worstMargin = Math.Max(worstMargin, Math.Abs(result.Mean - 2.5));
            outcome.Expect(result.Samples == scale.CalculatorMonteCarloSamples, () => $"Calculator run {run}: {result.Samples} samples, not {scale.CalculatorMonteCarloSamples}.");
            outcome.Expect(result.Yield is >= 0.99, () => $"Calculator run {run}: the yield against a factor of 1 is {result.Yield}, not near 1.");

            if ((run + 1) % 100 == 0)
                SoakScale.Progress($"monte carlo (calculator): {run + 1} of {scale.CalculatorMonteCarloRuns}");
        }

        calculator.Stop();
        outcome.Expect(worstMargin < 0.25, () => $"Calculator runs: a run's mean factor of safety was {worstMargin:0.000} from the nominal 2.5.");

        var sameA = CalcRunner.MonteCarlo(entry.Calculator, inputs, "n_gov", spec, 500, 3, cancellationToken: cancellationToken);
        var sameB = CalcRunner.MonteCarlo(entry.Calculator, inputs, "n_gov", spec, 500, 3, cancellationToken: cancellationToken);
        outcome.Expect(sameA.Mean == sameB.Mean && sameA.StandardDeviation == sameB.StandardDeviation, () => "The same seed through a calculator gave a different answer.");

        // The calculator service's own cap: one past it refused at once; at the cap, one run in full mode.
        outcome.Expect(Record.Exception(() => CalcRunner.MonteCarlo(entry.Calculator, inputs, "n_gov", spec, CalcRunner.MaxCalculatorSamples + 1)) is CalcEvaluationException, () => "One sample past the calculator service's cap was not refused.");
        var capCalcWatch = Stopwatch.StartNew();

        if (scale.RunAtCalculatorCap)
        {
            var capped = CalcRunner.MonteCarlo(entry.Calculator, inputs, "n_gov", spec, CalcRunner.MaxCalculatorSamples, 11, cancellationToken: cancellationToken);
            outcome.Expect(capped.Samples == CalcRunner.MaxCalculatorSamples, () => "The calculator run at its cap did not take every sample.");
        }

        capCalcWatch.Stop();

        using (var stop = new CancellationTokenSource())
        {
            stop.Cancel();
            var cancelledRun = Record.Exception(() => CalcRunner.MonteCarlo(entry.Calculator, inputs, "n_gov", spec, CalcRunner.MaxCalculatorSamples, cancellationToken: stop.Token));
            outcome.Expect(cancelledRun is OperationCanceledException or CalcEvaluationException, () => $"A cancelled calculator run did not stop ({cancelledRun?.GetType().Name ?? "no exception"}).");
        }

        // Sensitivity: a tornado sweep, many times, each ordered by swing with the stress and the yield strength at the top.
        var tornado = Stopwatch.StartNew();
        var sweeps = 0;

        for (var sweep = 0; sweep < scale.TornadoSweeps; sweep++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bars = CalcRunner.Tornado(entry.Calculator, inputs, "n_gov", ["sigma", "f_y"], 0.05 + 0.05 * (sweep % 4));
            sweeps++;
            outcome.Expect(bars.Count == 2 && bars[0].Swing >= bars[1].Swing && bars[0].Swing > 0, () => $"Tornado sweep {sweep}: the bars are not ordered by a non-zero swing.");
        }

        tornado.Stop();

        outcome.Fact("Engine runs (default samples each)", scale.EngineMonteCarloRuns);
        outcome.Fact("Engine samples drawn", engineSamples);
        outcome.Fact("Engine runs, total seconds", engine.Elapsed.TotalSeconds, "0.0");
        outcome.Fact("Engine run at its 1,000,000-sample cap (ms)", capWatch.Elapsed.TotalMilliseconds, "0");
        outcome.Fact("Calculator runs", scale.CalculatorMonteCarloRuns);
        outcome.Fact("Samples per calculator run", scale.CalculatorMonteCarloSamples);
        outcome.Fact("Calculator evaluations", calculatorSamples);
        outcome.Fact("Calculator runs, total seconds", calculator.Elapsed.TotalSeconds, "0.0");
        outcome.Fact("Calculator evaluation (mean µs)", calculatorSamples == 0 ? 0 : calculator.Elapsed.TotalMilliseconds * 1000.0 / calculatorSamples, "0.0");
        outcome.Fact("Calculator run at its 100,000-sample cap (s)", scale.RunAtCalculatorCap ? capCalcWatch.Elapsed.TotalSeconds : double.NaN, "0.0");
        outcome.Fact("Tornado sweeps", sweeps);
        outcome.Fact("Tornado sweeps, total seconds", tornado.Elapsed.TotalSeconds, "0.00");
        return Task.CompletedTask;
    }

    /// <summary>The exception a delegate throws, or <see langword="null"/>.</summary>
    internal static class Record
    {
        public static Exception? Exception(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
