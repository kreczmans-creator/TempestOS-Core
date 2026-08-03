namespace Tempest.Core.Requirements;

/// <summary>The concrete, immutable <see cref="IRequirementEvidence"/> snapshot returned by <see cref="IRequirementsService.GetEvidenceAsync"/>.</summary>
internal sealed class RequirementEvidence : IRequirementEvidence
{
    public Guid RequirementId { get; }
    public IReadOnlyList<Verification.IVerificationRecord> VerificationHistory { get; }
    public IReadOnlyList<EngineeringData.DocumentReference> LinkedReferences { get; }

    public RequirementEvidence(
        Guid requirementId,
        IReadOnlyList<Verification.IVerificationRecord> verificationHistory,
        IReadOnlyList<EngineeringData.DocumentReference> linkedReferences)
    {
        RequirementId = requirementId;
        VerificationHistory = verificationHistory;
        LinkedReferences = linkedReferences;
    }
}
