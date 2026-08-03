namespace Tempest.Core.Verification;

internal sealed class VerificationRecord : IVerificationRecord
{
    public VerificationRecord(
        Guid id,
        Guid subjectDocumentId,
        VerificationOutcome outcome,
        string method,
        IReadOnlyList<VerificationCriterion> criteria,
        IReadOnlyList<VerificationEvidenceEntry> evidence,
        IReadOnlyList<Guid> linkedDocumentIds,
        IReadOnlyList<Guid> linkedCalculationRecordIds,
        IReadOnlyList<string> referencedMaterialIds,
        string verifiedByPrincipalId,
        DateTimeOffset verifiedAt,
        int revisionNumber)
    {
        Id = id;
        SubjectDocumentId = subjectDocumentId;
        Outcome = outcome;
        Method = method;
        Criteria = criteria;
        Evidence = evidence;
        LinkedDocumentIds = linkedDocumentIds;
        LinkedCalculationRecordIds = linkedCalculationRecordIds;
        ReferencedMaterialIds = referencedMaterialIds;
        VerifiedByPrincipalId = verifiedByPrincipalId;
        VerifiedAt = verifiedAt;
        RevisionNumber = revisionNumber;
    }

    public Guid Id { get; }
    public Guid SubjectDocumentId { get; }
    public VerificationOutcome Outcome { get; }
    public string Method { get; }
    public IReadOnlyList<VerificationCriterion> Criteria { get; }
    public IReadOnlyList<VerificationEvidenceEntry> Evidence { get; }
    public IReadOnlyList<Guid> LinkedDocumentIds { get; }
    public IReadOnlyList<Guid> LinkedCalculationRecordIds { get; }
    public IReadOnlyList<string> ReferencedMaterialIds { get; }
    public string VerifiedByPrincipalId { get; }
    public DateTimeOffset VerifiedAt { get; }
    public int RevisionNumber { get; }
}
