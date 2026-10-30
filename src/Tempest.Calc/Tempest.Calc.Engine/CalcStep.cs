using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Engine;

/// <summary>How a step came to be: evaluated from an expression by the engine, or computed natively by a calculator and recorded with its own formula text.</summary>
public enum CalcStepKind
{
    /// <summary>An input the author defined; the formula is the symbol itself.</summary>
    Input,

    /// <summary>Evaluated by the engine from an expression.</summary>
    Evaluated,

    /// <summary>Computed in code (an iteration, a solver, a table lookup) and recorded with formula and substitution text supplied by the calculator.</summary>
    Native,
}

/// <summary>
/// One line of working: the symbol assigned, the formula as written, the
/// same formula with every symbol replaced by its value and unit, and the
/// result. This is the engine's whole transparency claim in one record.
/// </summary>
/// <param name="Ordinal">1-based position in the session.</param>
/// <param name="Symbol">The symbol assigned.</param>
/// <param name="Formula">The symbolic formula, as the author wrote it.</param>
/// <param name="Substitution">The formula with values and units substituted.</param>
/// <param name="ResultText">The result, formatted in its display unit.</param>
/// <param name="Result">The result value.</param>
/// <param name="Description">What the symbol means, for the sheet's margin.</param>
/// <param name="Clause">A standard clause reference (number and title only), for the sheet's margin.</param>
public sealed record CalcStep(
    int Ordinal,
    string Symbol,
    CalcStepKind Kind,
    string Formula,
    string Substitution,
    string ResultText,
    CalcValue Result,
    string? Description = null,
    string? Clause = null);
