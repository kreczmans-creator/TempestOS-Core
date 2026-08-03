namespace Tempest.Core.Verification;

/// <summary>The plain, JSON-serializable shape a verification is stored as — this is the <see cref="EngineeringData.IDocumentRevision.Content"/> of its own backing <see cref="EngineeringData.IEngineeringDocument"/>.</summary>
internal sealed record VerificationRecordDto(
    Guid SubjectDocumentId,
    VerificationOutcome Outcome,
    string Method,
    IReadOnlyList<VerificationCriterion> Criteria,
    IReadOnlyList<VerificationEvidenceEntry> Evidence,
    IReadOnlyList<Guid> LinkedDocumentIds,
    IReadOnlyList<Guid> LinkedCalculationRecordIds,
    IReadOnlyList<string> ReferencedMaterialIds,
    string VerifiedByPrincipalId,
    DateTimeOffset VerifiedAt);
