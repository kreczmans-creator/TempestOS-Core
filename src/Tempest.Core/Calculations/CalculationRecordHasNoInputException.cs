namespace Tempest.Core.Calculations;

/// <summary>
/// Thrown by <see cref="ICalculationEngine.ReRunAsync{TInput, TResult}(Guid, CancellationToken)"/>
/// when the record it was asked to re-run with identical input was
/// executed before input retention (`TD-29`) and carries none. A re-run
/// with a supplied, changed input (<see cref="ICalculationEngine.ReRunAsync{TInput, TResult}(Guid, TInput, CancellationToken)"/>)
/// needs no retained input and is unaffected.
/// </summary>
public sealed class CalculationRecordHasNoInputException : CalculationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationRecordHasNoInputException"/> class.
    /// </summary>
    /// <param name="recordId">The record that carries no retained input.</param>
    public CalculationRecordHasNoInputException(Guid recordId)
        : base($"Calculation record '{recordId}' was executed before input retention (TD-29) and carries no input — it cannot be re-run with \"identical input\". Supply a changed input instead.")
    {
        RecordId = recordId;
    }

    /// <summary>Gets the record that carries no retained input.</summary>
    public Guid RecordId { get; }
}
