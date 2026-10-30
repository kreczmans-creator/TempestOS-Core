using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Engine;

/// <summary>The seam every calculator computes through (ADR-0165). One engine, many independent sessions.</summary>
public interface ICalcEngine
{
    /// <summary>The engine version recorded on every issued sheet.</summary>
    string EngineVersion { get; }

    /// <summary>Starts an empty session with the given caps (or the defaults).</summary>
    ICalcSession CreateSession(CalcLimits? limits = null, int significantFigures = 5);

    /// <summary>Parses an expression without evaluating it: a syntax check for authoring tools.</summary>
    void Validate(string expression, CalcLimits? limits = null);
}

/// <summary>
/// A scope of symbols and the ordered log of steps that produced them.
/// Deterministic: the same definitions and assignments always yield the
/// same steps. Not thread-safe; one session per calculation run.
/// </summary>
public interface ICalcSession
{
    CalcLimits Limits { get; }

    int SignificantFigures { get; }

    /// <summary>Every step so far, in order.</summary>
    IReadOnlyList<CalcStep> Steps { get; }

    /// <summary>Defines an input symbol. Recorded as an <see cref="CalcStepKind.Input"/> step when <paramref name="record"/> is true.</summary>
    CalcStep Define(string symbol, CalcValue value, string? description = null, bool record = true);

    /// <summary>Evaluates <paramref name="expression"/>, binds the result to <paramref name="symbol"/>, shown in <paramref name="unit"/> where given, and records the step.</summary>
    CalcStep Assign(string symbol, string expression, string? unit = null, string? description = null, string? clause = null);

    /// <summary>Evaluates an expression without binding or recording it.</summary>
    CalcValue Evaluate(string expression);

    /// <summary>Records a step computed natively, binding <paramref name="result"/> to <paramref name="symbol"/>.</summary>
    CalcStep Record(string symbol, string formula, string substitution, CalcValue result, string? description = null, string? clause = null);

    /// <summary>The current value of <paramref name="symbol"/>, or <see langword="null"/>.</summary>
    CalcValue? Find(string symbol);

    /// <summary>The current value of <paramref name="symbol"/>; throws when undefined.</summary>
    CalcValue Get(string symbol);

    /// <summary>The scalar value of <paramref name="symbol"/>; throws when undefined or not a scalar.</summary>
    ScalarValue GetScalar(string symbol);

    /// <summary>Every symbol currently bound.</summary>
    IReadOnlyCollection<string> Symbols { get; }
}
