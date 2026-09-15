using Tempest.Core.Verification;

namespace Tempest.Core.EngineeringDomain;

/// <summary>Describes the same shape as the real, shipped <see cref="Requirements.IRequirement"/> — a deliberately loose reconciliation, not a literal match (WP8.2B Interface Catalogue.md §4). Owned by <c>Tempest.Core.Requirements</c>; not given a competing concrete realisation here (WP 8.2C).</summary>
public interface IRequirement : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, ITraceable, IValidatable
{
    string Statement { get; }
}

/// <summary>Owned by <c>Tempest.Core.Requirements</c> (<c>RequirementCollection</c>/<c>RequirementGroup</c>); not given a competing concrete realisation here (WP 8.2C).</summary>
public interface IRequirementSet : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    IReadOnlyList<Guid> MemberRequirementIds { get; }
    bool IsHierarchical { get; }
}

public interface IVerification : IEngineeringObject, IHasMetadata
{
}

public interface IVerificationActivity : IVerification, IHasLifecycle
{
    Guid SubjectId { get; }
    string Method { get; }
}

/// <summary>
/// Retired (`TD-30`, closed `WP 21.3A`): zero implementations across five
/// releases (`WP 18.0A` through `WP 21.3A`), and none is planned.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why no implementation was given.</b> `WP 8.2C` declared this
/// interface deliberately unimplemented ("not given a competing concrete
/// realisation here"), reconciling the shape a future Domain-level
/// Verification result might take against <c>Tempest.Core.Verification</c>'s
/// own real, shipped <c>VerificationRecord</c>. `WP 19.10L` made
/// <c>VerificationService.RecordAsync</c> write that record and register its
/// own <c>"verifiedBy"</c> relationship in one transaction (closing
/// `TD-32`/`TD-67`) — genuine progress, but transactional persistence of an
/// existing DTO, not giving it an <see cref="IEngineeringObject"/>'s
/// addressable identity. <c>VerificationRecord</c> matches this interface's
/// own <c>Outcome</c>/<c>Method</c> by name, yet still has no <c>Kind</c>
/// and no live <c>ReviseAsync</c>/<c>GetRevisionHistoryAsync</c>/
/// <c>LinkAsync</c>/<c>GetRelationshipsAsync</c> — the same "real Domain
/// design question, not a mechanical add" `FCR-0051` in
/// `docs/governance/Future Capability Register.md` already names for both
/// <see cref="ICalculationResult"/> and this interface together, still
/// "Identified," still unscheduled.
/// </para>
/// <para>
/// <see cref="EvidenceComposer"/> already discloses the practical
/// consequence, identically to <see cref="ICalculationResult"/>'s own
/// remarks: the pattern match never fires, so a Verification's own
/// evidence trail resolves empty (disclosed, non-blocking — `TD-30`,
/// `docs/releases/v0.19.1/Technical Debt Rationalisation — Part 2.md`).
/// The interface itself is left in place rather than deleted:
/// <see cref="IEvidence"/> already references it structurally, and
/// removing it would be a second, unrelated change this package does not
/// own.
/// </para>
/// </remarks>
public interface IVerificationResult : IVerification, IHasRevisions, IHasRelationships, ITraceable
{
    Guid SubjectId { get; }
    VerificationOutcome Outcome { get; }
    string Method { get; }
}
