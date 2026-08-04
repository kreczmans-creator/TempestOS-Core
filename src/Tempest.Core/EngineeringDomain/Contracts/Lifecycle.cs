namespace Tempest.Core.EngineeringDomain;

public enum LifecycleState
{
    Draft,
    InReview,
    Approved,
    Released,
    Superseded,
    Obsolete,
    Archived,
    Cancelled,
}

public interface IFamilySpecificState
{
    string Name { get; }
    LifecycleState CanonicalEquivalent { get; }
}

public interface ILifecycleTransitionRecord
{
    LifecycleState From { get; }
    LifecycleState To { get; }
    string ActorPrincipalId { get; }
    DateTimeOffset OccurredAt { get; }
    Guid? ApprovalId { get; }
}

public interface ILifecycleTransitionTable
{
    bool IsPermitted(LifecycleState from, LifecycleState to);
    IReadOnlyList<LifecycleState> GetPermittedTargets(LifecycleState from);
}

public interface IApprovalGate
{
    Task<bool> IsSatisfiedAsync(Guid objectId, CancellationToken cancellationToken = default);
    Task<IApproval?> GetSatisfyingApprovalAsync(Guid objectId, CancellationToken cancellationToken = default);
}

public interface IReviewGate
{
    Task<IReview> RequestReviewAsync(Guid objectId, IReadOnlyList<string> reviewerPrincipalIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IReview>> GetReviewsAsync(Guid objectId, CancellationToken cancellationToken = default);
}

public interface IReleaseGate
{
    Task<bool> IsSatisfiedAsync(Guid baselineId, CancellationToken cancellationToken = default);
}

public interface ILifecycleValidationRule
{
    Task<IValidationResult> ValidateTransitionAsync(Guid objectId, LifecycleState from, LifecycleState to, CancellationToken cancellationToken = default);
}
