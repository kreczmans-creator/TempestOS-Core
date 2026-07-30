namespace Tempest.Core.Calculations;

/// <summary>
/// Thrown when <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/>
/// is given a <c>calculationId</c> that is not registered, or is
/// registered for a different <c>TInput</c>/<c>TResult</c> signature than
/// requested.
/// </summary>
public sealed class CalculationDefinitionNotFoundException : CalculationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationDefinitionNotFoundException"/> class.
    /// </summary>
    /// <param name="calculationId">The calculation identity that could not be dispatched.</param>
    public CalculationDefinitionNotFoundException(string calculationId)
        : base($"No calculation is registered with Id '{calculationId}' for the requested input/result signature.")
    {
        CalculationId = calculationId;
    }

    /// <summary>Gets the calculation identity that could not be dispatched.</summary>
    public string CalculationId { get; }
}
