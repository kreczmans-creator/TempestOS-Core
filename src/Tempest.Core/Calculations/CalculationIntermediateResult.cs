namespace Tempest.Core.Calculations;

/// <summary>
/// A named intermediate value a calculation definition recorded while
/// computing its own final result — makes an evidentiary record
/// inspectable step-by-step, not only as a final answer.
/// </summary>
/// <param name="Name">A short, human-readable name for this intermediate value.</param>
/// <param name="Value">The value itself — typically a boxed <c>Quantity&lt;TDimension&gt;</c> where dimensioned, but not constrained to one.</param>
public sealed record CalculationIntermediateResult(string Name, object Value);
