using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;

namespace Tempest.Core.Tasks;

/// <summary>
/// A small, manual to-do — a title, an optional project, an optional due
/// date, and whether it is done (`WP 19.5C`, Product Owner comment item 6:
/// Home's own task tiles and task list). Follows
/// <c>Tempest.Core.Quotations.Quotation</c>'s own shape: an
/// <c>EngineeringObjectBase</c> subtype carrying its own state through
/// <c>CaptureTypeState</c>/<c>ApplyTypeState</c>/<see cref="IRehydratable{ManualTask}.Rehydrate"/>,
/// optionally parented to the project it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not <c>Tempest.Core.EngineeringDomain.EngineeringTask</c>.</b> That
/// type (Kind <c>"Task"</c>, via <c>Tempest.Workspace.CanonicalObjectKinds.Task</c>)
/// already exists — a fuller engineering task with a work state, a
/// priority and an assignee, created inside a project through
/// <c>Tempest.Workspace.Projects.ProjectTaskService</c> — but it carries
/// no discipline registration of its own (no explorer area, no ribbon
/// command, no editor declaration): it is reachable only through the
/// Project Workspace's own Tasks tab. This Work Package's own brief asks
/// for a Kind reachable through the ordinary discipline template
/// (explorer area, <c>task.create</c>/<c>task.complete</c> commands, an
/// Object Editor declaration) — a second, deliberately smaller Kind, named
/// <c>"ManualTask"</c> so it can never collide with <c>"Task"</c>.
/// </para>
/// <para>
/// <b>No status vocabulary of its own.</b> Unlike <c>Quotation</c>/
/// <c>InvoiceRequest</c>, this Kind does not hide <see cref="IHasLifecycle.Status"/>
/// — <see cref="Done"/> is its own plain field, and the generic Lifecycle
/// editor section renders the (otherwise unused) canonical status
/// harmlessly, exactly as it already does for <c>Milestone</c>/
/// <c>Deliverable</c>.
/// </para>
/// </remarks>
public sealed class ManualTask : EngineeringObjectBase, IRehydratable<ManualTask>
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> every manual task's own backing document carries (`ADR-0105`).</summary>
    public const string CanonicalKind = "ManualTask";

    private DateOnly? _dueDate;
    private bool _done;
    private DateOnly? _completedOn;

    /// <summary>Initialises a new instance of the <see cref="ManualTask"/> class.</summary>
    public ManualTask(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        DateOnly? dueDate = null, bool done = false, DateOnly? completedOn = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        _dueDate = dueDate;
        _done = done;
        _completedOn = completedOn;
    }

    /// <summary>When this task is due, or <see langword="null"/> when none is set.</summary>
    public DateOnly? DueDate => _dueDate;

    /// <summary>Whether this task is done.</summary>
    public bool Done => _done;

    /// <summary>When this task was marked done. <see langword="null"/> while <see cref="Done"/> is <see langword="false"/>.</summary>
    public DateOnly? CompletedOn => _completedOn;

    /// <summary>Marks this task done (`ITaskService.CompleteAsync`); whether that is permitted is that service's own concern, not this mutator's.</summary>
    internal Task MarkDoneAsync(DateOnly completedOn, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Done)] = true.ToString() };
                WriteJson(state, nameof(CompletedOn), completedOn);
                return state;
            },
            () => { _done = true; _completedOn = completedOn; },
            $"Marked done on {completedOn:O}.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        WriteJson(state, nameof(DueDate), _dueDate);
        state[nameof(Done)] = _done.ToString();
        WriteJson(state, nameof(CompletedOn), _completedOn);
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _dueDate = state.TypeJson<DateOnly?>(nameof(DueDate));
        _done = bool.TryParse(state.Type(nameof(Done)), out var done) && done;
        _completedOn = state.TypeJson<DateOnly?>(nameof(CompletedOn));
    }

    static ManualTask IRehydratable<ManualTask>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeJson<DateOnly?>(nameof(DueDate)),
            bool.TryParse(state.Type(nameof(Done)), out var done) && done,
            state.TypeJson<DateOnly?>(nameof(CompletedOn)));
}
