namespace Tempest.Core.Calculations;

/// <summary>Which bound <see cref="CalculationBoundExceededException"/> reports was exceeded.</summary>
public enum CalculationBoundKind
{
    /// <summary>The number of intermediate results recorded for one execution.</summary>
    IntermediateResultCount,

    /// <summary>The total estimated size, in bytes, of every intermediate result recorded for one execution.</summary>
    IntermediateResultTotalSize,
}

/// <summary>
/// Thrown when a single calculation execution's own recorded intermediate
/// results exceed a configured bound (`TD-22`) — refuses the record rather
/// than let a runaway or misbehaving <see cref="ICalculationDefinition{TInput, TResult}"/>
/// record without limit. No <see cref="CalculationRecord{TResult}"/> is
/// created for the refused execution, mirroring <see cref="CalculationInputInvalidException"/>'s
/// own "nothing is left behind by a failed call" behaviour.
/// </summary>
public sealed class CalculationBoundExceededException : CalculationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationBoundExceededException"/> class.
    /// </summary>
    /// <param name="calculationId">The definition whose own execution exceeded the bound.</param>
    /// <param name="kind">Which bound was exceeded.</param>
    /// <param name="limit">The configured limit that was exceeded.</param>
    public CalculationBoundExceededException(string calculationId, CalculationBoundKind kind, long limit)
        : base(BuildMessage(calculationId, kind, limit))
    {
        CalculationId = calculationId;
        Kind = kind;
        Limit = limit;
    }

    /// <summary>Gets the definition whose own execution exceeded the bound.</summary>
    public string CalculationId { get; }

    /// <summary>Gets which bound was exceeded.</summary>
    public CalculationBoundKind Kind { get; }

    /// <summary>Gets the configured limit that was exceeded.</summary>
    public long Limit { get; }

    private static string BuildMessage(string calculationId, CalculationBoundKind kind, long limit) => kind switch
    {
        CalculationBoundKind.IntermediateResultCount =>
            $"Calculation '{calculationId}' recorded more than {limit} intermediate result(s) — refused rather than recorded without limit.",
        CalculationBoundKind.IntermediateResultTotalSize =>
            $"Calculation '{calculationId}' recorded intermediate results totalling more than {limit} byte(s) — refused rather than recorded without limit.",
        _ => $"Calculation '{calculationId}' exceeded a recording bound ({kind}, limit {limit}).",
    };
}
