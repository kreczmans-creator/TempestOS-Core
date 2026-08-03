namespace Tempest.Core.Requirements;

/// <summary>
/// A read-side aggregation of every fact bearing on a requirement's own
/// evidentiary status. Owns no new stored data — every field is drawn
/// from an existing Engineering Core record (<c>WP7.2C Requirements
/// Platform Contracts.md</c> §7).
/// </summary>
public interface IRequirementEvidence
{
    Guid RequirementId { get; }

    /// <summary>Every verification recorded against this requirement, oldest first — from <see cref="Verification.IVerificationService.GetVerificationHistoryAsync"/> directly.</summary>
    IReadOnlyList<Verification.IVerificationRecord> VerificationHistory { get; }

    /// <summary>Every relationship recorded with this requirement as its own source — from <see cref="IRequirementsService.GetRelationshipsAsync"/> directly.</summary>
    IReadOnlyList<EngineeringData.DocumentReference> LinkedReferences { get; }
}
