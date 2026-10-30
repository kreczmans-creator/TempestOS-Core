using System.Text.Json;

namespace Tempest.Core.Calculations;

/// <summary>
/// One stored calculation record read without its result type: what a
/// migration or an archive viewer needs when the definition that wrote it
/// may no longer be registered (ADR-0167). <see cref="Result"/> and
/// <see cref="Input"/> are the stored JSON as written; nothing is coerced.
/// </summary>
public sealed record CalculationRecordDocument(
    Guid Id,
    string CalculationId,
    JsonElement Result,
    string? ResultTypeName,
    IReadOnlyList<CalculationAssumption> Assumptions,
    IReadOnlyList<CalculationIntermediateResult> IntermediateResults,
    CalculationValidationResult Validation,
    IReadOnlyList<string> ReferencedMaterialIds,
    DateTimeOffset ExecutedAt,
    string ExecutedByPrincipalId,
    int RevisionNumber,
    JsonElement? Input,
    string? InputTypeName,
    Guid? PredecessorRecordId);
