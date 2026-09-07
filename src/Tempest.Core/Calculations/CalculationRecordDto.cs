namespace Tempest.Core.Calculations;

// ResultTypeName records which type Result was serialised from.
//
// Without it, reading a record back as the wrong result type silently
// succeeds: System.Text.Json ignores members it does not recognise and
// leaves the rest at their defaults, so a bracket check read as a bolt
// shear result returns a well-formed object full of zeroes. An engineer
// would have no way to tell that from a real answer.
//
// Nullable, because records written before this field existed do not carry
// it. Those can still be read; they simply cannot be checked.

/// <summary>The plain, JSON-serializable shape a calculation execution is stored as — this is the <see cref="EngineeringData.IDocumentRevision.Content"/> of its own backing <see cref="EngineeringData.IEngineeringDocument"/>.</summary>
internal sealed record CalculationRecordDto<TResult>(
    string CalculationId,
    TResult Result,
    IReadOnlyList<CalculationAssumption> Assumptions,
    IReadOnlyList<CalculationIntermediateResult> IntermediateResults,
    CalculationValidationResult Validation,
    IReadOnlyList<string> ReferencedMaterialIds,
    DateTimeOffset ExecutedAt,
    string ExecutedByPrincipalId,
    string? ResultTypeName = null);
