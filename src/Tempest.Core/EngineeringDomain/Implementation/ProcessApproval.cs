using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>Named <c>EngineeringTask</c>, not <c>Task</c> — <see cref="System.Threading.Tasks.Task"/> is a global using in this assembly and would collide.</summary>
public class EngineeringTask : EngineeringObjectBase, ITask, IRehydratable<EngineeringTask>
{
    private readonly object _taskLock = new();

    private string? _assignedToPrincipalId;
    private TaskWorkState _workState;
    private WorkPriority _priority;
    private DateTimeOffset? _dueDate;

    public EngineeringTask(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? assignedToPrincipalId = null,
        TaskWorkState workState = TaskWorkState.Todo, WorkPriority priority = WorkPriority.Normal, DateTimeOffset? dueDate = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        _assignedToPrincipalId = Normalise(assignedToPrincipalId);
        _workState = workState;
        _priority = priority;
        _dueDate = dueDate;
    }

    /// <inheritdoc />
    public string? AssignedToPrincipalId
    {
        get { lock (_taskLock) { return _assignedToPrincipalId; } }
    }

    /// <summary>Where this task is in someone's working week — the task family's own state (see <see cref="TaskWorkState"/>).</summary>
    /// <remarks>
    /// Deliberately separate from <see cref="EngineeringObjectBase.Status"/>,
    /// which remains the canonical document lifecycle. Read
    /// <see cref="TaskWorkStates.For"/> to get the canonical equivalent
    /// where a cross-domain consumer needs one answer for every Kind.
    /// </remarks>
    public TaskWorkState WorkState
    {
        get { lock (_taskLock) { return _workState; } }
    }

    /// <summary>How urgent this task is.</summary>
    public WorkPriority Priority
    {
        get { lock (_taskLock) { return _priority; } }
    }

    /// <summary>When this task is due, or <see langword="null"/> when no date has been set.</summary>
    /// <remarks>
    /// Nullable on purpose. "No due date" is the honest state of most tasks
    /// most of the time, and defaulting one to the day it was created would
    /// make every overdue figure in the product meaningless.
    /// </remarks>
    public DateTimeOffset? DueDate
    {
        get { lock (_taskLock) { return _dueDate; } }
    }

    /// <summary>Whether this task is past its due date and still open.</summary>
    /// <remarks>
    /// A finished task is never overdue, however late it was — the question
    /// a user is asking is "what still needs chasing", not "what was late".
    /// </remarks>
    public bool IsOverdue(DateTimeOffset asOf)
    {
        lock (_taskLock)
            return _dueDate is { } due && due < asOf && TaskWorkStates.IsOpen(_workState);
    }

    /// <summary>Assigns this task to <paramref name="principalId"/>, or unassigns it when <see langword="null"/>.</summary>
    /// <remarks>
    /// Takes a principal id rather than resolving one itself: who is acting
    /// and who work is assigned to are different questions, and the caller
    /// gets the current principal from the boundary (`ADR-0116`) rather than
    /// from the domain. An engineering object never knows who is signed in.
    /// </remarks>
    public Task AssignAsync(string? principalId, CancellationToken cancellationToken = default)
    {
        lock (_taskLock)
            _assignedToPrincipalId = Normalise(principalId);

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Moves this task to <paramref name="target"/>.</summary>
    /// <exception cref="InvalidTaskWorkStateTransitionException">The move is not permitted from the current state.</exception>
    public Task ChangeWorkStateAsync(TaskWorkState target, CancellationToken cancellationToken = default)
    {
        lock (_taskLock)
        {
            if (!TaskWorkStateTransitions.IsPermitted(_workState, target))
                throw new InvalidTaskWorkStateTransitionException(Id, _workState, target);

            _workState = target;
        }

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Sets this task's priority.</summary>
    public Task SetPriorityAsync(WorkPriority priority, CancellationToken cancellationToken = default)
    {
        lock (_taskLock)
            _priority = priority;

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Sets or clears this task's due date.</summary>
    public Task SetDueDateAsync(DateTimeOffset? dueDate, CancellationToken cancellationToken = default)
    {
        lock (_taskLock)
            _dueDate = dueDate;

        return PersistStateAsync(cancellationToken);
    }

    /// <summary>Links this task to the Milestone or Deliverable it contributes to.</summary>
    public Task ContributeToAsync(Guid milestoneOrDeliverableId, CancellationToken cancellationToken = default) =>
        LinkAsync(milestoneOrDeliverableId, TaskRelationshipKinds.ContributesTo, cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(AssignedToPrincipalId)] = AssignedToPrincipalId;
        state[nameof(WorkState)] = WorkState.ToString();
        state[nameof(Priority)] = Priority.ToString();
        state[nameof(DueDate)] = DueDate?.ToString("O");
    }

    /// <summary>Reads a task's own persisted state back, tolerating a record written before these fields existed.</summary>
    /// <remarks>
    /// An older record has no WorkState or Priority key at all. Falling back
    /// to Todo/Normal is the honest reading of "a task that was never given
    /// one", and matches `TD-85`'s established rule that a missing field
    /// comes back visibly empty rather than failing the whole rehydration.
    /// </remarks>
    private protected static (string? Assignee, TaskWorkState WorkState, WorkPriority Priority, DateTimeOffset? DueDate) ReadTaskState(EngineeringObjectState state) =>
        (state.Type(nameof(AssignedToPrincipalId)),
         Enum.TryParse<TaskWorkState>(state.Type(nameof(WorkState)), out var workState) ? workState : TaskWorkState.Todo,
         Enum.TryParse<WorkPriority>(state.Type(nameof(Priority)), out var priority) ? priority : WorkPriority.Normal,
         state.TypeDate(nameof(DueDate)));

    private static string? Normalise(string? principalId) =>
        string.IsNullOrWhiteSpace(principalId) ? null : principalId.Trim();

    static EngineeringTask IRehydratable<EngineeringTask>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state)
    {
        var (assignee, workState, priority, dueDate) = ReadTaskState(state);
        return new EngineeringTask(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, assignee, workState, priority, dueDate);
    }
}

/// <summary>Named <c>EngineeringAction</c>, not <c>Action</c> — <see cref="System.Action"/> would collide.</summary>
public sealed class EngineeringAction : EngineeringTask, IAction, IRehydratable<EngineeringAction>
{
    public Guid RaisedByObjectId { get; }

    public EngineeringAction(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid raisedByObjectId, string? assignedToPrincipalId = null,
        TaskWorkState workState = TaskWorkState.Todo, WorkPriority priority = WorkPriority.Normal, DateTimeOffset? dueDate = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, assignedToPrincipalId, workState, priority, dueDate)
    {
        RaisedByObjectId = raisedByObjectId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        base.CaptureTypeState(state);
        state[nameof(RaisedByObjectId)] = RaisedByObjectId.ToString();
    }

    static EngineeringAction IRehydratable<EngineeringAction>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state)
    {
        var (assignee, workState, priority, dueDate) = ReadTaskState(state);
        return new EngineeringAction(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeGuidOrEmpty(nameof(RaisedByObjectId)), assignee, workState, priority, dueDate);
    }
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
