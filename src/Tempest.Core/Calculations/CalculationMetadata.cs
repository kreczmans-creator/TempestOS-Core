namespace Tempest.Core.Calculations;

/// <summary>
/// Fixed, declarative information about an
/// <see cref="ICalculationDefinition{TInput, TResult}"/> — every
/// execution of that definition carries the same metadata, copied
/// directly into its own <see cref="CalculationRecord{TResult}"/> so the
/// record remains self-contained evidence, never requiring a live lookup
/// of the original definition (which may not even still be registered)
/// to know what assumptions or constraints governed it.
/// </summary>
/// <param name="Name">A short, human-readable name.</param>
/// <param name="Description">A longer description of what this calculation computes. <see langword="null"/> if none is recorded.</param>
/// <param name="Category">An open, caller-assigned classification (e.g. "Structural"). <see langword="null"/> if uncategorised.</param>
/// <param name="Assumptions">Every engineering assumption this calculation relies on. Never <see langword="null"/>; empty if none.</param>
/// <param name="Constraints">Every precondition or applicability limit this calculation declares. Never <see langword="null"/>; empty if none.</param>
public sealed record CalculationMetadata(
    string Name,
    string? Description,
    string? Category,
    IReadOnlyList<CalculationAssumption> Assumptions,
    IReadOnlyList<CalculationConstraint> Constraints);
