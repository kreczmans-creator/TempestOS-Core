using System.Text.RegularExpressions;
using Tempest.Calc.Engine.Parsing;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Engine;

/// <inheritdoc cref="ICalcSession"/>
public sealed partial class CalcSession : ICalcSession
{
    private readonly Dictionary<string, CalcValue> _scope = new(StringComparer.Ordinal);
    private readonly List<CalcStep> _steps = [];
    private readonly Evaluator _evaluator;

    internal CalcSession(CalcLimits limits, int significantFigures)
    {
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        Limits = limits;
        SignificantFigures = Math.Clamp(significantFigures, 2, 12);
        _evaluator = new Evaluator(name => _scope.GetValueOrDefault(name), limits);
    }

    public CalcLimits Limits { get; }

    public int SignificantFigures { get; }

    public IReadOnlyList<CalcStep> Steps => _steps;

    public IReadOnlyCollection<string> Symbols => _scope.Keys;

    public CalcStep Define(string symbol, CalcValue value, string? description = null, bool record = true)
    {
        ArgumentNullException.ThrowIfNull(value);
        CheckSymbol(symbol);
        Bind(symbol, value);
        var text = value.Format(SignificantFigures);
        var step = new CalcStep(_steps.Count + 1, symbol, CalcStepKind.Input, symbol, text, text, value, description);

        if (record)
            AddStep(step);

        return step;
    }

    public CalcStep Assign(string symbol, string expression, string? unit = null, string? description = null, string? clause = null)
    {
        CheckSymbol(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        var node = Parser.Parse(expression, Limits);
        var value = _evaluator.Evaluate(node);

        if (unit is not null)
        {
            if (!UnitCatalog.TryParse(unit, out var displayUnit, out var error))
                throw new CalcUnitException(error);

            value = value.In(displayUnit);
        }

        CheckFinite(symbol, value);
        var substitution = _evaluator.Substitute(node, SignificantFigures);
        Bind(symbol, value);
        var step = new CalcStep(_steps.Count + 1, symbol, CalcStepKind.Evaluated, expression.Trim(), substitution, value.Format(SignificantFigures), value, description, clause);
        AddStep(step);
        return step;
    }

    public CalcValue Evaluate(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        return _evaluator.Evaluate(Parser.Parse(expression, Limits));
    }

    public CalcStep Record(string symbol, string formula, string substitution, CalcValue result, string? description = null, string? clause = null)
    {
        CheckSymbol(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        ArgumentNullException.ThrowIfNull(substitution);
        ArgumentNullException.ThrowIfNull(result);

        if (formula.Length > Limits.MaxExpressionLength || substitution.Length > Limits.MaxExpressionLength)
            throw new CalcLimitException(nameof(CalcLimits.MaxExpressionLength), $"A recorded formula is at most {Limits.MaxExpressionLength} characters.");

        CheckFinite(symbol, result);
        Bind(symbol, result);
        var step = new CalcStep(_steps.Count + 1, symbol, CalcStepKind.Native, formula, substitution, result.Format(SignificantFigures), result, description, clause);
        AddStep(step);
        return step;
    }

    public CalcValue? Find(string symbol) => _scope.GetValueOrDefault(symbol);

    public CalcValue Get(string symbol) => _scope.TryGetValue(symbol, out var v) ? v : throw new CalcEvaluationException($"Unknown symbol '{symbol}'.");

    public ScalarValue GetScalar(string symbol) => Get(symbol) as ScalarValue ?? throw new CalcEvaluationException($"'{symbol}' is not a single number.");

    private void AddStep(CalcStep step)
    {
        if (_steps.Count >= Limits.MaxSteps)
            throw new CalcLimitException(nameof(CalcLimits.MaxSteps), $"A session records at most {Limits.MaxSteps} steps.");

        _steps.Add(step);
    }

    private void Bind(string symbol, CalcValue value)
    {
        if (!_scope.ContainsKey(symbol) && _scope.Count >= Limits.MaxVariables)
            throw new CalcLimitException(nameof(CalcLimits.MaxVariables), $"A session holds at most {Limits.MaxVariables} symbols.");

        switch (value)
        {
            case VectorValue v when v.Length > Limits.MaxArrayElements:
            case MatrixValue m when (long)m.Rows * m.Columns > Limits.MaxArrayElements:
                throw new CalcLimitException(nameof(CalcLimits.MaxArrayElements), $"An array holds at most {Limits.MaxArrayElements} elements.");
            case TextValue t when t.Value.Length > Limits.MaxTextLength:
                throw new CalcLimitException(nameof(CalcLimits.MaxTextLength), $"A text is at most {Limits.MaxTextLength} characters.");
        }

        _scope[symbol] = value;
    }

    private void CheckSymbol(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (symbol.Length > Limits.MaxSymbolLength)
            throw new CalcLimitException(nameof(CalcLimits.MaxSymbolLength), $"A symbol is at most {Limits.MaxSymbolLength} characters.");

        if (!SymbolPattern().IsMatch(symbol))
            throw new CalcParseException($"'{symbol}' is not a valid symbol: start with a letter, then letters, digits, _ or a subscript.");

        if (symbol is "pi" or "π" or "e" or "g_n" or "true" or "false" or "and" or "or" or "not" or "in" or "if")
            throw new CalcParseException($"'{symbol}' is reserved.");
    }

    private static void CheckFinite(string symbol, CalcValue value)
    {
        var bad = value switch
        {
            ScalarValue s => !double.IsFinite(s.Si),
            VectorValue v => v.Si.Any(x => !double.IsFinite(x)),
            MatrixValue m => Enumerable.Range(0, m.Rows).Any(r => Enumerable.Range(0, m.Columns).Any(c => !double.IsFinite(m.Si(r, c)))),
            _ => false,
        };

        if (bad)
            throw new CalcEvaluationException($"'{symbol}' is not a finite number; a sheet never carries NaN or infinity.");
    }

    [GeneratedRegex(@"^\p{L}[\p{L}\p{Nd}_'₀-ₜ]*$")]
    private static partial Regex SymbolPattern();
}
