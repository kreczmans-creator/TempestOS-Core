namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Where a task actually is in someone's working week.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not <see cref="LifecycleState"/>.</b> The canonical
/// lifecycle is a <em>document release</em> lifecycle: Draft → InReview →
/// Approved → Released → Superseded → Archived, with Archived and
/// Cancelled terminal and no path back from Released to Draft. That is
/// correct for a drawing and wrong for a task in two ways. A task is
/// never "in review" on its way to being done, and — decisively — a task
/// that is finished must be able to be <b>reopened</b>, which the
/// canonical table forbids by design and should keep forbidding: a
/// released drawing genuinely must not silently become a draft again.
/// </para>
/// <para>
/// So this is a family-specific state in the sense the platform already
/// defines (<see cref="IFamilySpecificState"/>), not a competing lifecycle.
/// Every value maps to a canonical equivalent through
/// <see cref="TaskWorkStates.For"/>, so anything reasoning across the whole
/// domain — evidence, reporting, the Cockpit's own open-work count — still
/// gets one answer, and the task family gets a vocabulary its users would
/// recognise.
/// </para>
/// </remarks>
public enum TaskWorkState
{
    /// <summary>Accepted, not started.</summary>
    Todo,

    /// <summary>Being worked on now.</summary>
    InProgress,

    /// <summary>Started and stopped by something outside the assignee's control.</summary>
    Blocked,

    /// <summary>Finished. Reopenable — see <see cref="TaskWorkStateTransitions"/>.</summary>
    Done,

    /// <summary>Abandoned. Distinct from <see cref="Done"/>, because "we decided not to" is not "we did it".</summary>
    Cancelled,
}

/// <summary>How urgent a piece of work is — a task, an issue, or a risk.</summary>
/// <remarks>
/// <para>
/// Four values, deliberately. A priority scale a team cannot tell apart is
/// a priority scale nobody sets, and <see cref="Normal"/> is the default so
/// that unprioritised work reads as ordinary rather than as an omission.
/// </para>
/// <para>
/// <b>One priority vocabulary for the whole platform</b>, which is why it
/// is not called <c>TaskPriority</c> any more. When the Risks, Issues and
/// Decisions surfaces needed a priority, the choice was between declaring a
/// second enum with these same four values and widening this one's name.
/// `ADR-0105` settles that: one canonical declaring class per value. A
/// second identical scale would let the two drift, and would make "High" on
/// an issue and "High" on a task different things for no reason a user
/// could see.
/// </para>
/// </remarks>
public enum WorkPriority
{
    /// <summary>Worth doing, nothing waits on it.</summary>
    Low,

    /// <summary>Ordinary work. The default.</summary>
    Normal,

    /// <summary>Wanted ahead of ordinary work.</summary>
    High,

    /// <summary>Something is stopped until this is done.</summary>
    Critical,
}

/// <summary>One task work state, and the canonical lifecycle state it corresponds to.</summary>
/// <param name="State">The task-family state.</param>
/// <param name="Name">Its display name.</param>
/// <param name="CanonicalEquivalent">The platform-wide <see cref="LifecycleState"/> it maps to.</param>
public sealed record TaskWorkStateDescriptor(TaskWorkState State, string Name, LifecycleState CanonicalEquivalent)
    : IFamilySpecificState;

/// <summary>
/// The task family's own state vocabulary, and its mapping onto the
/// canonical lifecycle — the first implementation of
/// <see cref="IFamilySpecificState"/>, which the platform declared as a
/// contract and had never used.
/// </summary>
/// <remarks>
/// The mapping is deliberately lossy in one direction and honest about it:
/// Todo, InProgress and Blocked all correspond to
/// <see cref="LifecycleState.Draft"/>, because from the canonical
/// lifecycle's point of view they are the same thing — work that has not
/// produced a released result. Nothing is lost, because
/// <see cref="TaskWorkState"/> remains the task's own real state; the
/// canonical value is what cross-domain consumers read, not what the task
/// stores.
/// </remarks>
public static class TaskWorkStates
{
    private static readonly IReadOnlyList<TaskWorkStateDescriptor> Descriptors =
    [
        new(TaskWorkState.Todo, "To do", LifecycleState.Draft),
        new(TaskWorkState.InProgress, "In progress", LifecycleState.Draft),
        new(TaskWorkState.Blocked, "Blocked", LifecycleState.Draft),
        new(TaskWorkState.Done, "Done", LifecycleState.Released),
        new(TaskWorkState.Cancelled, "Cancelled", LifecycleState.Cancelled),
    ];

    /// <summary>Every task work state, in board order — the order a task moves through.</summary>
    public static IReadOnlyList<TaskWorkStateDescriptor> All => Descriptors;

    /// <summary>The descriptor for <paramref name="state"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not a declared state.</exception>
    public static TaskWorkStateDescriptor For(TaskWorkState state) =>
        Descriptors.FirstOrDefault(d => d.State == state)
        ?? throw new ArgumentOutOfRangeException(nameof(state), state, "No descriptor is declared for this task work state.");

    /// <summary>Whether <paramref name="state"/> means the task still needs doing.</summary>
    /// <remarks>
    /// Open is the complement of finished, and <see cref="TaskWorkState.Cancelled"/>
    /// counts as finished: an abandoned task is not outstanding work, and
    /// counting it as such would make every open-task figure in the product
    /// slowly become a lie.
    /// </remarks>
    public static bool IsOpen(TaskWorkState state) =>
        state is not (TaskWorkState.Done or TaskWorkState.Cancelled);
}

/// <summary>
/// Which task work state may follow which — including the one the canonical
/// lifecycle deliberately forbids: reopening finished work.
/// </summary>
/// <remarks>
/// Mirrors <see cref="LifecycleTransitionTable"/>'s own dictionary shape and
/// its "same-to-same is never permitted" rule, so the two read alike. The
/// substantive differences are that <see cref="TaskWorkState.Done"/> and
/// <see cref="TaskWorkState.Cancelled"/> are <b>not</b> terminal — a task
/// that turned out not to be finished goes back to Todo or In progress, and
/// an abandoned one can be picked up again.
/// </remarks>
public static class TaskWorkStateTransitions
{
    private static readonly IReadOnlyDictionary<TaskWorkState, IReadOnlyList<TaskWorkState>> Permitted =
        new Dictionary<TaskWorkState, IReadOnlyList<TaskWorkState>>
        {
            [TaskWorkState.Todo] = [TaskWorkState.InProgress, TaskWorkState.Blocked, TaskWorkState.Done, TaskWorkState.Cancelled],
            [TaskWorkState.InProgress] = [TaskWorkState.Todo, TaskWorkState.Blocked, TaskWorkState.Done, TaskWorkState.Cancelled],
            [TaskWorkState.Blocked] = [TaskWorkState.Todo, TaskWorkState.InProgress, TaskWorkState.Done, TaskWorkState.Cancelled],

            // Reopening. The whole reason a task family needs its own table.
            [TaskWorkState.Done] = [TaskWorkState.Todo, TaskWorkState.InProgress],
            [TaskWorkState.Cancelled] = [TaskWorkState.Todo, TaskWorkState.InProgress],
        };

    /// <summary>Whether a task may move from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static bool IsPermitted(TaskWorkState from, TaskWorkState to) =>
        from != to && Permitted.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Every state a task in <paramref name="from"/> may move to.</summary>
    public static IReadOnlyList<TaskWorkState> GetPermittedTargets(TaskWorkState from) =>
        Permitted.TryGetValue(from, out var targets) ? targets : [];
}

/// <summary>Thrown when a task is asked to move to a work state it cannot reach from its current one.</summary>
public sealed class InvalidTaskWorkStateTransitionException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="InvalidTaskWorkStateTransitionException"/> class.</summary>
    public InvalidTaskWorkStateTransitionException(Guid taskId, TaskWorkState from, TaskWorkState to)
        : base($"Task '{taskId}' cannot move from '{from}' to '{to}'. Permitted: " +
               $"{string.Join(", ", TaskWorkStateTransitions.GetPermittedTargets(from))}.")
    {
        TaskId = taskId;
        From = from;
        To = to;
    }

    /// <summary>The task that refused the transition.</summary>
    public Guid TaskId { get; }

    /// <summary>The state it is in.</summary>
    public TaskWorkState From { get; }

    /// <summary>The state it was asked to move to.</summary>
    public TaskWorkState To { get; }
}

/// <summary>
/// The relationship kinds the task family writes (`ADR-0105` — one
/// canonical declaring class per value).
/// </summary>
/// <remarks>
/// Mirrors <see cref="Requirements.RequirementRelationshipKinds"/>, which is
/// the established precedent for a family owning its own relationship
/// vocabulary rather than spelling literals at call sites.
/// </remarks>
public static class TaskRelationshipKinds
{
    /// <summary>A task to the Milestone or Deliverable it contributes to.</summary>
    /// <remarks>
    /// One kind for both targets, deliberately. A Deliverable already knows
    /// its own <see cref="IDeliverable.MilestoneId"/>, so a task linked to a
    /// deliverable is transitively linked to that deliverable's milestone —
    /// a second relationship kind would be a second answer to a question the
    /// domain can already answer.
    /// </remarks>
    public const string ContributesTo = "contributesTo";
}
