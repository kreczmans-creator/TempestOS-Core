namespace Tempest.Core.EngineeringDomain;

public interface ITask : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    string? AssignedToPrincipalId { get; }
}

public interface IAction : ITask
{
    Guid RaisedByObjectId { get; }
}

public interface IReview : IEngineeringObject, IHasMetadata, IHasRelationships
{
    IReadOnlyList<string> ReviewerPrincipalIds { get; }
}

public interface IApproval : IEngineeringObject, IHasMetadata, IHasRelationships
{
    string ApproverPrincipalId { get; }
    DateTimeOffset ApprovedAt { get; }
}

public interface IMilestone : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle
{
    DateTimeOffset TargetDate { get; }
}

public interface IDeliverable : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid MilestoneId { get; }
}
