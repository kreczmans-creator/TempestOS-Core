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

/// <summary>
/// Retired (`TD-30`, closed `WP 21.3A`): zero implementations across five
/// releases (`WP 18.0A` through `WP 21.3A`), and none is planned.
/// </summary>
/// <remarks>
/// <b>Why no implementation was given.</b> This is a workflow-gate concept
/// — a decision point a caller consults before treating an object as
/// approved — and the platform deliberately has none: "no workflow engine"
/// is a standing product guard (`docs/releases/v1.0.0/WorkPackages.md`;
/// `docs/releases/v1.0.0/D-028 Evidence is the product, calculation is
/// where the engineer does it.md`). Every discipline instead reads approval
/// straight off the flat <see cref="LifecycleState"/> an object's own
/// <c>IHasLifecycle.Status</c> already carries (see
/// <c>Tempest.Workspace.Calculations.CalculationsPropertyFacetProvider</c>'s
/// own "Approval State" facet), a decision multiple ADRs ratify explicitly
/// against this exact gap (`ADR-0087`, `ADR-0090`) — implementing
/// <see cref="IApprovalGate"/> now would not close a gap the platform
/// disclosed; it would build the workflow engine the product guard refuses.
/// Its own <see cref="GetSatisfyingApprovalAsync"/> also depends on
/// <see cref="IApproval"/>, itself unimplemented anywhere, so even a
/// partial implementation would need to invent that shape first — a second,
/// larger Domain-design question, not a mechanical add.
/// </remarks>
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
