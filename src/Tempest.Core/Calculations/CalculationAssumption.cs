namespace Tempest.Core.Calculations;

/// <summary>
/// An engineering assumption a calculation definition relies on (e.g.
/// "assumes linear elastic behaviour") — fixed, declared once as part of
/// a definition's own <see cref="CalculationMetadata"/>, never per
/// execution, so it is never possible for a calculation to be performed
/// without its own governing assumptions being explicit and traceable.
/// </summary>
/// <param name="Description">The assumption itself, in plain engineering language.</param>
/// <param name="Justification">Why this assumption is reasonable for this calculation. <see langword="null"/> if none is recorded.</param>
public sealed record CalculationAssumption(string Description, string? Justification);
