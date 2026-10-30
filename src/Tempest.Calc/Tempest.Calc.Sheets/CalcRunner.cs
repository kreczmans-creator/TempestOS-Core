using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Analysis;

namespace Tempest.Calc.Sheets;

/// <summary>
/// Runs a calculator safely: validates inputs, runs, and turns every
/// engine failure into a block notice. Also the home of the generic
/// capabilities (§3.8): a Monte Carlo and a tornado sweep over any
/// calculator's scalar output.
/// </summary>
public static class CalcRunner
{
    public static CalcRunResult Run(ICalcCalculator calculator, CalcInputSet inputs, ICalcEngine? engine = null, CalcLimits? limits = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(inputs);

        // C-2: a symbol an input had before it was renamed is read as the input it has become.
        if (!CalcInputAliases.TryCanonicalise(calculator.Template, inputs, out inputs, out var aliasConflict))
            return CalcRunResult.Blocked([new CalcNotice("input-alias", CalcNoticeSeverity.Block, aliasConflict!)]);

        // ADR-0177 review: a bare number of an input or column whose default unit changed is given the unit of the version the values were recorded
        // on before anything reads it, so a calculator that reads a table cell with its own fallback unit sees the right one too; a record that
        // cannot be dated blocks with the sentence (CalcInputUnits).
        var given = inputs.Values.ToList();

        if (!CalcInputUnits.TryPin(calculator.Template, given, inputs.MadeOn, out var pinned, out var ambiguous))
            return CalcRunResult.Blocked([new CalcNotice("input-unit", CalcNoticeSeverity.Block, ambiguous!)]);

        if (!ReferenceEquals(pinned, given))
            inputs = new CalcInputSet(pinned, inputs.MadeOn);

        var notices = CalcInputValidator.Validate(calculator.Template, inputs).ToList();

        if (notices.Any(n => n.Severity == CalcNoticeSeverity.Block))
            return CalcRunResult.Blocked(notices);

        var session = (engine ?? CalcEngine.Instance).CreateSession(limits);
        var context = new CalcRunContext(session, inputs, calculator.Template, cancellationToken);

        try
        {
            calculator.Run(context);
        }
        catch (CalcException e)
        {
            context.Block("engine", CalcMessageShape.Engine(e.Message));
        }
        catch (OperationCanceledException)
        {
            context.Block("cancelled", "calculation: the calculation was cancelled before it finished.");
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or ArithmeticException or IndexOutOfRangeException or KeyNotFoundException or NullReferenceException)
        {
            // A calculator's own guard or a numeric edge the engine did not
            // see: still a block on the sheet, never a crash in the form.
            context.Block("engine", CalcMessageShape.Engine($"the calculation could not be completed: {e.Message}"));
        }

        var result = context.ToResult();
        return result with { Notices = [.. notices, .. result.Notices] };
    }

    /// <summary>A model over SI inputs for the analysis helpers: re-runs the calculator with the named numeric inputs replaced and returns one scalar output in SI.</summary>
    public static Func<IReadOnlyDictionary<string, double>, double> Model(ICalcCalculator calculator, CalcInputSet baseline, string outputSymbol, ICalcEngine? engine = null)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(baseline);

        return values =>
        {
            var inputs = baseline;

            foreach (var (symbol, si) in values)
                inputs = inputs.WithSi(symbol, si);

            var result = Run(calculator, inputs, engine);

            if (result.IsBlocked)
                throw new CalcEvaluationException(result.Blocks[0].Message);

            return result.OutputSi(outputSymbol);
        };
    }

    /// <summary>The SI values of the named numeric inputs in <paramref name="inputs"/>.</summary>
    public static IReadOnlyDictionary<string, double> NominalSi(CalcTemplate template, CalcInputSet inputs, IEnumerable<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(symbols);

        var result = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var s in symbols)
        {
            var spec = template.FindInput(s) ?? throw new CalcEvaluationException($"'{s}' is not an input of {template.Id}.");

            if (spec.Kind is not (CalcInputKind.Quantity or CalcInputKind.Number))
                throw new CalcEvaluationException($"'{s}' is not a numeric input.");

            var v = CalcInputBinder.Bind(spec, inputs.Find(s), inputs.MadeOn) as Engine.Values.ScalarValue ?? throw new CalcEvaluationException($"'{s}' is not a number.");
            result[s] = v.Si;
        }

        return result;
    }

    public static IReadOnlyList<TornadoBar> Tornado(ICalcCalculator calculator, CalcInputSet inputs, string outputSymbol, IReadOnlyList<string> inputSymbols, double relativeRange = 0.10)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var nominal = NominalSi(calculator.Template, inputs, inputSymbols);
        return Sensitivity.Tornado(Model(calculator, inputs, outputSymbol), nominal, inputSymbols, relativeRange);
    }

    /// <summary>Sample cap for a Monte Carlo that re-runs a whole calculator per sample.</summary>
    public const int MaxCalculatorSamples = 100_000;

    public static MonteCarloResult MonteCarlo(
        ICalcCalculator calculator, CalcInputSet inputs, string outputSymbol, IReadOnlyDictionary<string, Distribution> distributions,
        int samples = Engine.Analysis.MonteCarlo.DefaultSamples, ulong seed = 1, double? lowerLimit = null, double? upperLimit = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(distributions);

        // Each sample re-runs the whole calculator, so the sheet-level cap is a tenth of the engine's (R-09).
        if (samples > MaxCalculatorSamples)
            throw new CalcEvaluationException($"A calculator Monte Carlo runs at most {MaxCalculatorSamples} samples (each sample is a full run).");

        var nominal = NominalSi(calculator.Template, inputs, distributions.Keys);
        return Engine.Analysis.MonteCarlo.Run(Model(calculator, inputs, outputSymbol), nominal, distributions, samples, seed, lowerLimit, upperLimit, cancellationToken: cancellationToken);
    }
}
