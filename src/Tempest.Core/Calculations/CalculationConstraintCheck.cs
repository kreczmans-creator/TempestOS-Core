namespace Tempest.Core.Calculations;

/// <summary>
/// The outcome of checking one <see cref="CalculationConstraint"/> against
/// a specific execution's own actual input — recorded via
/// <see cref="CalculationContext.RecordConstraintCheck"/>.
/// </summary>
/// <param name="Description">The constraint that was checked, in plain engineering language — typically matching a <see cref="CalculationConstraint.Description"/> declared in the same definition's own <see cref="CalculationMetadata"/>.</param>
/// <param name="IsSatisfied">Whether the constraint held for this execution's own actual input.</param>
/// <param name="Detail">Further detail about the check (e.g. the actual value checked). <see langword="null"/> if none.</param>
public sealed record CalculationConstraintCheck(string Description, bool IsSatisfied, string? Detail);
