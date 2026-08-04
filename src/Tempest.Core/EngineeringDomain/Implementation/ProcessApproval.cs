using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>Named <c>EngineeringTask</c>, not <c>Task</c> — <see cref="System.Threading.Tasks.Task"/> is a global using in this assembly and would collide.</summary>
public class EngineeringTask : EngineeringObjectBase, ITask
{
    public string? AssignedToPrincipalId { get; }

    public EngineeringTask(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? assignedToPrincipalId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        AssignedToPrincipalId = assignedToPrincipalId;
    }
}

/// <summary>Named <c>EngineeringAction</c>, not <c>Action</c> — <see cref="System.Action"/> would collide.</summary>
public sealed class EngineeringAction : EngineeringTask, IAction
{
    public Guid RaisedByObjectId { get; }

    public EngineeringAction(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid raisedByObjectId, string? assignedToPrincipalId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, assignedToPrincipalId)
    {
        RaisedByObjectId = raisedByObjectId;
    }
}

public sealed class Review : EngineeringObjectBase, IReview
{
    public IReadOnlyList<string> ReviewerPrincipalIds { get; }

    public Review(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<string>? reviewerPrincipalIds = null)
        : base(document, currentRevision, context, identifier: null, displayName, metadata)
    {
        ReviewerPrincipalIds = reviewerPrincipalIds ?? Array.Empty<string>();
    }
}

public sealed class Approval : EngineeringObjectBase, IApproval
{
    public string ApproverPrincipalId { get; }
    public DateTimeOffset ApprovedAt { get; }

    public Approval(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, string approverPrincipalId, DateTimeOffset approvedAt)
        : base(document, currentRevision, context, identifier: null, displayName, metadata)
    {
        ApproverPrincipalId = approverPrincipalId;
        ApprovedAt = approvedAt;
    }
}

public sealed class Milestone : EngineeringObjectBase, IMilestone
{
    public DateTimeOffset TargetDate { get; }

    public Milestone(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, DateTimeOffset targetDate)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        TargetDate = targetDate;
    }
}

public sealed class Deliverable : EngineeringObjectBase, IDeliverable
{
    public Guid MilestoneId { get; }

    public Deliverable(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid milestoneId)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        MilestoneId = milestoneId;
    }
}
