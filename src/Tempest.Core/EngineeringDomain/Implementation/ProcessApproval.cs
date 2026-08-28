using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>Named <c>EngineeringTask</c>, not <c>Task</c> — <see cref="System.Threading.Tasks.Task"/> is a global using in this assembly and would collide.</summary>
public class EngineeringTask : EngineeringObjectBase, ITask, IRehydratable<EngineeringTask>
{
    public string? AssignedToPrincipalId { get; }

    public EngineeringTask(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? assignedToPrincipalId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        AssignedToPrincipalId = assignedToPrincipalId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(AssignedToPrincipalId)] = AssignedToPrincipalId;

    static EngineeringTask IRehydratable<EngineeringTask>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.Type(nameof(AssignedToPrincipalId)));
}

/// <summary>Named <c>EngineeringAction</c>, not <c>Action</c> — <see cref="System.Action"/> would collide.</summary>
public sealed class EngineeringAction : EngineeringTask, IAction, IRehydratable<EngineeringAction>
{
    public Guid RaisedByObjectId { get; }

    public EngineeringAction(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid raisedByObjectId, string? assignedToPrincipalId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, assignedToPrincipalId)
    {
        RaisedByObjectId = raisedByObjectId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        base.CaptureTypeState(state);
        state[nameof(RaisedByObjectId)] = RaisedByObjectId.ToString();
    }

    static EngineeringAction IRehydratable<EngineeringAction>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeGuidOrEmpty(nameof(RaisedByObjectId)), state.Type(nameof(AssignedToPrincipalId)));
}

public sealed class Review : EngineeringObjectBase, IReview, IRehydratable<Review>
{
    public IReadOnlyList<string> ReviewerPrincipalIds { get; }

    public Review(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<string>? reviewerPrincipalIds = null)
        : base(document, currentRevision, context, identifier: null, displayName, metadata)
    {
        ReviewerPrincipalIds = reviewerPrincipalIds ?? Array.Empty<string>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        WriteList(state, nameof(ReviewerPrincipalIds), ReviewerPrincipalIds);

    static Review IRehydratable<Review>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.DisplayName, state.Metadata, state.TypeList(nameof(ReviewerPrincipalIds)));
}

public sealed class Approval : EngineeringObjectBase, IApproval, IRehydratable<Approval>
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

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(ApproverPrincipalId)] = ApproverPrincipalId;
        state[nameof(ApprovedAt)] = ApprovedAt.ToString("O");
    }

    static Approval IRehydratable<Approval>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.DisplayName, state.Metadata,
            state.Type(nameof(ApproverPrincipalId)) ?? string.Empty, state.TypeDate(nameof(ApprovedAt)) ?? document.CreatedAt);
}

public sealed class Milestone : EngineeringObjectBase, IMilestone, IRehydratable<Milestone>
{
    public DateTimeOffset TargetDate { get; }

    public Milestone(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, DateTimeOffset targetDate)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        TargetDate = targetDate;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(TargetDate)] = TargetDate.ToString("O");

    static Milestone IRehydratable<Milestone>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeDate(nameof(TargetDate)) ?? document.CreatedAt);
}

public sealed class Deliverable : EngineeringObjectBase, IDeliverable, IRehydratable<Deliverable>
{
    public Guid MilestoneId { get; }

    public Deliverable(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid milestoneId)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        MilestoneId = milestoneId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(MilestoneId)] = MilestoneId.ToString();

    static Deliverable IRehydratable<Deliverable>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidOrEmpty(nameof(MilestoneId)));
}
