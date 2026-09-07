namespace Tempest.Core.Calculations;

/// <summary>
/// Enough of an executed calculation to identify it in a list without
/// opening it.
/// </summary>
/// <remarks>
/// Only the fields every <see cref="CalculationRecord{TResult}"/> carries
/// whatever its result type is. The result itself is deliberately absent:
/// reading it needs the type, and a listing of a heterogeneous set has no
/// single one. <see cref="ResultTypeName"/> is what tells a caller which
/// type to open it with.
/// </remarks>
/// <param name="Id">The record's own identity — the backing document's Id.</param>
/// <param name="CalculationId">Which calculation produced it.</param>
/// <param name="ExecutedAt">When it ran.</param>
/// <param name="ExecutedByPrincipalId">Who ran it.</param>
/// <param name="RevisionNumber">The backing document's current revision.</param>
/// <param name="ResultTypeName">The full name of the result type it was written with, where the record records one.</param>
public sealed record CalculationRecordSummary(
    Guid Id,
    string CalculationId,
    DateTimeOffset ExecutedAt,
    string ExecutedByPrincipalId,
    int RevisionNumber,
    string? ResultTypeName);
