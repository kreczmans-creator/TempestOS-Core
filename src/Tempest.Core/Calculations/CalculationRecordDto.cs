namespace Tempest.Core.Calculations;

/// <summary>The plain, JSON-serializable shape a calculation execution is stored as — this is the <see cref="EngineeringData.IDocumentRevision.Content"/> of its own backing <see cref="EngineeringData.IEngineeringDocument"/>.</summary>
internal sealed record CalculationRecordDto<TResult>(
    string CalculationId,
    TResult Result,
    IReadOnlyList<CalculationAssumption> Assumptions,
    IReadOnlyList<CalculationIntermediateResult> IntermediateResults,
    CalculationValidationResult Validation,
    IReadOnlyList<string> ReferencedMaterialIds,
    DateTimeOffset ExecutedAt,
    string ExecutedByPrincipalId);
