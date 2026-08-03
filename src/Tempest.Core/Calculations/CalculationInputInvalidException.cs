namespace Tempest.Core.Calculations;

/// <summary>
/// Thrown by an <see cref="ICalculationDefinition{TInput, TResult}"/>'s
/// own <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/>
/// when its input fails that calculation's own validation. Propagates
/// through <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/>
/// unmodified — never swallowed, and no <see cref="CalculationRecord{TResult}"/>
/// is created for the failed execution.
/// </summary>
public sealed class CalculationInputInvalidException : CalculationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationInputInvalidException"/> class.
    /// </summary>
    /// <param name="message">A message describing why the input was rejected.</param>
    public CalculationInputInvalidException(string message)
        : base(message)
    {
    }
}
