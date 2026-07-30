namespace Tempest.Core.Calculations;

/// <summary>
/// The overall validation outcome of one calculation execution, derived
/// automatically from every <see cref="CalculationConstraintCheck"/> the
/// definition recorded via its own <see cref="CalculationContext"/>.
/// </summary>
/// <remarks>
/// A constraint violation severe enough to make the result itself
/// meaningless is expected to be reported by throwing
/// <see cref="CalculationInputInvalidException"/> from
/// <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/> directly
/// — no <see cref="CalculationRecord{TResult}"/> is ever created for that
/// case. <see cref="Conditional"/> exists for the softer case: a
/// definition that computes and returns a real result while still
/// recording that one or more advisory constraints were not met.
/// </remarks>
public enum CalculationValidationOutcome
{
    /// <summary>Every recorded constraint check was satisfied.</summary>
    Valid,

    /// <summary>At least one recorded constraint check was not satisfied, but the definition still returned a result rather than throwing.</summary>
    Conditional
}
