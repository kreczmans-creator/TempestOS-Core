namespace Tempest.Core.Verification;

/// <summary>
/// An immutable record of one verification — engineering evidence
/// answering "has this engineering claim been demonstrated?", not merely
/// a pass/fail flag: every criterion checked, every piece of evidence, and
/// every linked document, calculation, or material travels with the
/// outcome.
/// </summary>
public interface IVerificationRecord
{
    /// <summary>
    /// This record's own stable identity — also the Id of the
    /// <c>EngineeringData.IEngineeringDocument</c> this record is durably
    /// stored as, usable directly with
    /// <c>EngineeringData.IEngineeringDocumentStore</c> for revision
    /// history this framework does not itself duplicate.
    /// </summary>
    Guid Id { get; }

    /// <summary>The Id of the engineering document this verification was performed against.</summary>
    Guid SubjectDocumentId { get; }

    /// <summary>Whether the engineering claim was demonstrated.</summary>
    VerificationOutcome Outcome { get; }

    /// <summary>
    /// The verification method used (e.g. "inspection", "test",
    /// "analysis", "demonstration") — deliberately an open string, not a
    /// closed vocabulary, since no real engineering standard has yet named
    /// one to validate against.
    /// </summary>
    string Method { get; }

    /// <summary>Every explicit criterion checked as part of this verification. Never <see langword="null"/>; empty if none were recorded.</summary>
    IReadOnlyList<VerificationCriterion> Criteria { get; }

    /// <summary>Every piece of supporting evidence recorded for this verification. Never <see langword="null"/>; empty if none were recorded.</summary>
    IReadOnlyList<VerificationEvidenceEntry> Evidence { get; }

    /// <summary>Every additional engineering document Id linked to this verification, beyond <see cref="SubjectDocumentId"/> itself. Never <see langword="null"/>; empty if none.</summary>
    IReadOnlyList<Guid> LinkedDocumentIds { get; }

    /// <summary>Every calculation record Id linked to this verification. Never <see langword="null"/>; empty if none.</summary>
    IReadOnlyList<Guid> LinkedCalculationRecordIds { get; }

    /// <summary>Every material Id referenced during this verification. Never <see langword="null"/>; empty if none.</summary>
    IReadOnlyList<string> ReferencedMaterialIds { get; }

    /// <summary>Who performed this verification.</summary>
    string VerifiedByPrincipalId { get; }

    /// <summary>When this verification was performed.</summary>
    DateTimeOffset VerifiedAt { get; }

    /// <summary>
    /// The underlying document's own current revision number — always
    /// <c>1</c> for a record <see cref="IVerificationService.RecordAsync"/>
    /// has just produced, since each verification creates a fresh record
    /// rather than revising an existing one.
    /// </summary>
    int RevisionNumber { get; }
}
