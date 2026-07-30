namespace Tempest.Core.Calculations;

/// <summary>
/// A precondition or applicability limit a calculation definition
/// declares (e.g. "input length must be positive") — fixed, declared
/// once as part of a definition's own <see cref="CalculationMetadata"/>.
/// Whether a specific execution's own input actually satisfies this
/// constraint is recorded per execution as a
/// <see cref="CalculationConstraintCheck"/>, via <see cref="CalculationContext.RecordConstraintCheck"/>.
/// </summary>
/// <param name="Description">The constraint itself, in plain engineering language.</param>
public sealed record CalculationConstraint(string Description);
