namespace Tempest.Core.EngineeringDomain;

public sealed class LifecycleTransitionRecord : ILifecycleTransitionRecord
{
    public LifecycleState From { get; }
    public LifecycleState To { get; }
    public string ActorPrincipalId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid? ApprovalId { get; }

    public LifecycleTransitionRecord(LifecycleState from, LifecycleState to, string actorPrincipalId, DateTimeOffset occurredAt, Guid? approvalId)
    {
        From = from;
        To = to;
        ActorPrincipalId = actorPrincipalId;
        OccurredAt = occurredAt;
        ApprovalId = approvalId;
    }
}
