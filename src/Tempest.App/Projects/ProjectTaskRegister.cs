using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Projects;

/// <summary>The milestone or deliverable a task contributes to, as the register reports it.</summary>
/// <param name="ObjectId">The milestone or deliverable itself.</param>
/// <param name="Kind">Which of the two it is.</param>
/// <param name="DisplayName">Its name.</param>
/// <param name="TargetDate">The milestone's target date — a deliverable reports its own milestone's date.</param>
public sealed record ProjectTaskTarget(Guid ObjectId, string Kind, string DisplayName, DateTimeOffset? TargetDate);

/// <summary>One task belonging to a project, as the Tasks surface shows it.</summary>
/// <param name="ObjectId">The task's own identity.</param>
/// <param name="Kind">Task or Action — an Action is a task raised by something, and is listed alongside.</param>
/// <param name="Identifier">Its business identifier, where it has one.</param>
/// <param name="DisplayName">Its title.</param>
/// <param name="Description">Its current revision content — what the task actually says.</param>
/// <param name="WorkState">Where it is in someone's working week.</param>
/// <param name="Priority">How urgent it is.</param>
/// <param name="AssignedToPrincipalId">Who owns it, or <see langword="null"/> when nobody does.</param>
/// <param name="DueDate">When it is due, or <see langword="null"/>.</param>
/// <param name="IsOverdue">Whether it is past due and still open.</param>
/// <param name="ContributesTo">The milestone or deliverable it contributes to, where one is linked.</param>
public sealed record ProjectTaskEntry(
    Guid ObjectId,
    string Kind,
    string? Identifier,
    string DisplayName,
    string Description,
    TaskWorkState WorkState,
    WorkPriority Priority,
    string? AssignedToPrincipalId,
    DateTimeOffset? DueDate,
    bool IsOverdue,
    ProjectTaskTarget? ContributesTo)
{
    /// <summary>Whether this task still needs doing.</summary>
    public bool IsOpen => TaskWorkStates.IsOpen(WorkState);

    /// <summary>Whether anybody owns this task.</summary>
    /// <remarks>
    /// Unassigned is a real, common and reportable state, not a defect —
    /// so it is asked about directly rather than left for each caller to
    /// re-derive from a null check.
    /// </remarks>
    public bool IsUnassigned => AssignedToPrincipalId is null;
}

/// <summary>One column of the task board.</summary>
/// <param name="State">The work state this column holds.</param>
/// <param name="Title">Its heading.</param>
/// <param name="Entries">The tasks in it, in the register's own order.</param>
public sealed record ProjectTaskBoardColumn(TaskWorkState State, string Title, IReadOnlyList<ProjectTaskEntry> Entries);

/// <summary>The tasks belonging to a project.</summary>
public interface IProjectTaskRegister
{
    /// <summary>Every task and action in <paramref name="projectId"/>.</summary>
    Task<IReadOnlyList<ProjectTaskEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>The same tasks, grouped into board columns by work state.</summary>
    Task<IReadOnlyList<ProjectTaskBoardColumn>> ListBoardAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The project's own task register — the read model behind the Project
/// Workspace's Tasks area.
/// </summary>
/// <remarks>
/// <para>
/// <b>Membership is <see cref="ProjectMembership"/>'s answer, not a second
/// one.</b> A task belongs to a project when the durable
/// <see cref="IHasParent"/> chain from it reaches that project — so a task
/// hung on a Part inside a Sub-Assembly inside an Assembly is a project
/// task, exactly as a document in the same position is a project document.
/// No <c>ProjectId</c> field was added to the task model, for the same
/// reason none was added to requirements: it would be a competing answer to
/// a question the platform already answers.
/// </para>
/// <para>
/// <b>A read model, not a store.</b> It holds no state, caches nothing and
/// creates no persistence. Everything it reports is read from the task
/// objects themselves, which is why a task edited anywhere in the product
/// shows correctly here without this class knowing the edit happened.
/// </para>
/// <para>
/// Actions are listed alongside tasks because an <see cref="IAction"/>
/// <em>is</em> an <see cref="ITask"/> — one raised by a review or a
/// meeting. Splitting them into a separate surface would hide work from
/// the person whose job it is to see all of it.
/// </para>
/// </remarks>
public sealed class ProjectTaskRegister : IProjectTaskRegister
{
    private readonly EngineeringDomainContext _context;
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Initialises a new instance of the <see cref="ProjectTaskRegister"/> class.</summary>
    /// <param name="context">The engineering domain.</param>
    /// <param name="now">
    /// The clock "overdue" is measured against. Injectable so a test can
    /// state the date rather than depend on the day it runs — an overdue
    /// test pinned to <c>DateTimeOffset.UtcNow</c> is a test that changes
    /// its own meaning overnight.
    /// </param>
    public ProjectTaskRegister(EngineeringDomainContext context, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectTaskEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var members = await ProjectMembership
            .ListProjectMembersAsync(_context.Repository, projectId, cancellationToken)
            .ConfigureAwait(false);

        var asOf = _now();
        var entries = new List<ProjectTaskEntry>();

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member is not EngineeringTask task)
                continue;

            entries.Add(new ProjectTaskEntry(
                task.Id,
                task.Kind ?? string.Empty,
                task.Identifier,
                task.DisplayName,
                task.Content,
                task.WorkState,
                task.Priority,
                task.AssignedToPrincipalId,
                task.DueDate,
                task.IsOverdue(asOf),
                await ResolveTargetAsync(task, cancellationToken).ConfigureAwait(false)));
        }

        // A stable order the user can rely on: the work that needs
        // attention soonest first. Overdue above everything, then by due
        // date, then by priority, then by name — a task with no due date
        // sorts after every dated one rather than jumping to the top on a
        // null comparison.
        return
        [
            .. entries
                .OrderByDescending(e => e.IsOverdue)
                .ThenBy(e => e.DueDate ?? DateTimeOffset.MaxValue)
                .ThenByDescending(e => e.Priority)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.ObjectId),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectTaskBoardColumn>> ListBoardAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var entries = await ListAsync(projectId, cancellationToken).ConfigureAwait(false);

        // Every declared state gets a column, including the empty ones: a
        // board that hides "Blocked" because nothing is blocked today
        // reshapes itself under the user as work moves.
        return
        [
            .. TaskWorkStates.All.Select(descriptor => new ProjectTaskBoardColumn(
                descriptor.State,
                descriptor.Name,
                [.. entries.Where(e => e.WorkState == descriptor.State)])),
        ];
    }

    private async Task<ProjectTaskTarget?> ResolveTargetAsync(EngineeringTask task, CancellationToken cancellationToken)
    {
        var relationships = await task.GetRelationshipsAsync(cancellationToken).ConfigureAwait(false);

        var link = relationships
            .Where(r => string.Equals(r.RelationshipKind, TaskRelationshipKinds.ContributesTo, StringComparison.Ordinal))
            .Where(r => r.SourceId == task.Id)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        if (link is null)
            return null;

        var target = await _context.Repository.FindAsync(link.TargetId, cancellationToken).ConfigureAwait(false);
        if (target is null)
            return null;

        // A deliverable reports the date of the milestone it is due
        // against, because that is the date the task is actually working
        // to — a deliverable has no date of its own.
        var targetDate = target switch
        {
            IMilestone milestone => milestone.TargetDate,
            IDeliverable deliverable => (await _context.Repository.FindAsync(deliverable.MilestoneId, cancellationToken).ConfigureAwait(false)) is IMilestone owning
                ? owning.TargetDate
                : null,
            _ => (DateTimeOffset?)null,
        };

        return new ProjectTaskTarget(
            target.Id,
            target.Kind ?? string.Empty,
            (target as IHasBusinessIdentifier)?.DisplayName ?? target.Kind ?? "Target",
            targetDate);
    }
}
