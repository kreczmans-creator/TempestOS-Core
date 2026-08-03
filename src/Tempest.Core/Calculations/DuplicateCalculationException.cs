namespace Tempest.Core.Calculations;

/// <summary>
/// Thrown when <see cref="ICalculationEngine.RegisterDefinition{TInput, TResult}"/>
/// is given a <c>CalculationId</c> that is already registered.
/// </summary>
public sealed class DuplicateCalculationException : CalculationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateCalculationException"/> class.
    /// </summary>
    /// <param name="calculationId">The calculation identity that is already registered.</param>
    public DuplicateCalculationException(string calculationId)
        : base($"A calculation is already registered with Id '{calculationId}'.")
    {
        CalculationId = calculationId;
    }

    /// <summary>Gets the calculation identity that is already registered.</summary>
    public string CalculationId { get; }
}
