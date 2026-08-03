namespace Tempest.Core.Calculations;

/// <summary>The complete validation outcome of one calculation execution.</summary>
/// <param name="Outcome">The overall outcome, derived automatically from <paramref name="ConstraintChecks"/>.</param>
/// <param name="ConstraintChecks">Every constraint check the definition recorded for this execution. Never <see langword="null"/>; empty if the definition recorded none.</param>
public sealed record CalculationValidationResult(
    CalculationValidationOutcome Outcome,
    IReadOnlyList<CalculationConstraintCheck> ConstraintChecks);
