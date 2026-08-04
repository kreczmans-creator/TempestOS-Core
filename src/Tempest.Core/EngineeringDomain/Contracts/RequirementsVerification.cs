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

/// <summary>Owned by <c>Tempest.Core.Verification</c> (<c>VerificationRecord</c>); not given a competing concrete realisation here (WP 8.2C).</summary>
public interface IVerificationResult : IVerification, IHasRevisions, IHasRelationships, ITraceable
{
    Guid SubjectId { get; }
    VerificationOutcome Outcome { get; }
    string Method { get; }
}
