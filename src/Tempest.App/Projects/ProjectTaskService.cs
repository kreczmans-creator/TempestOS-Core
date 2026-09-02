using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Projects;

/// <summary>Creating and changing tasks inside a project.</summary>
public interface IProjectTaskService
{
    /// <summary>Creates a task in <paramref name="projectId"/>.</summary>
    Task<EngineeringTask> CreateAsync(
        Guid projectId,
        string identifier,
        string title,
        string? description = null,
        WorkPriority priority = WorkPriority.Normal,
        DateTimeOffset? dueDate = null,
        string? assignedToPrincipalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Assigns <paramref name="taskId"/> to <paramref name="principalId"/>, or unassigns it when <see langword="null"/>.</summary>
    Task AssignAsync(Guid taskId, string? principalId, CancellationToken cancellationToken = default);

    /// <summary>Assigns <paramref name="taskId"/> to whoever is using the application right now.</summary>
    Task AssignToCurrentPrincipalAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Moves <paramref name="taskId"/> to <paramref name="target"/>.</summary>
    Task ChangeWorkStateAsync(Guid taskId, TaskWorkState target, CancellationToken cancellationToken = default);

    /// <summary>Sets or clears <paramref name="taskId"/>'s due date.</summary>
    Task SetDueDateAsync(Guid taskId, DateTimeOffset? dueDate, CancellationToken cancellationToken = default);

    /// <summary>Sets <paramref name="taskId"/>'s priority.</summary>
    Task SetPriorityAsync(Guid taskId, WorkPriority priority, CancellationToken cancellationToken = default);

    /// <summary>Retitles and/or rewrites <paramref name="taskId"/>.</summary>
    Task EditAsync(Guid taskId, string? title = null, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Links <paramref name="taskId"/> to the Milestone or Deliverable it contributes to.</summary>
    Task ContributeToAsync(Guid taskId, Guid milestoneOrDeliverableId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The task lifecycle, as the Project Workspace performs it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No second task model.</b> Every operation here is performed on the
/// real <see cref="EngineeringTask"/> in the domain, through the same
/// <see cref="EngineeringObjectFactory{T}"/> every other Kind is created
/// by and the same mutation-then-persist path <c>RenameAsync</c> and
/// <c>MoveAsync</c> already use. That is what makes a task created here
/// survive a restart (`TD-85`/`TD-104`) without this class knowing anything
/// about persistence.
/// </para>
/// <para>
/// <b>Project membership is set by parenting, not by a field.</b> Creating
/// a task in a project moves it under that project, so
/// <see cref="ProjectMembership"/> resolves it exactly as it resolves every
/// other object. A task created here is a project task by the platform's
/// one definition of the phrase.
/// </para>
/// <para>
/// <b>Ownership comes from the principal boundary (`ADR-0116`).</b>
/// <see cref="AssignToCurrentPrincipalAsync"/> reads
/// <c>ICurrentPrincipalAccessor</c> through the domain context and assigns
/// that identity. No authentication is performed and no permission is
/// checked: assignment is a statement about who is doing a piece of work,
/// not about who is allowed to.
/// </para>
/// </remarks>
public sealed class ProjectTaskService : IProjectTaskService
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ProjectTaskService"/> class.</summary>
    public ProjectTaskService(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <inheritdoc />
    public async Task<EngineeringTask> CreateAsync(
        Guid projectId,
        string identifier,
        string title,
        string? description = null,
        WorkPriority priority = WorkPriority.Normal,
        DateTimeOffset? dueDate = null,
        string? assignedToPrincipalId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var project = await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProjectNotFoundException(projectId);

        var factory = new EngineeringObjectFactory<EngineeringTask>(
            CanonicalObjectKinds.Task,
            _context,
            (document, revision) => new EngineeringTask(
                document, revision, _context, identifier.Trim(), title.Trim(),
                EngineeringObjectMetadata.Empty, assignedToPrincipalId,
                TaskWorkState.Todo, priority, dueDate));

        var created = (EngineeringTask)await factory
            .CreateAsync(description ?? $"Task {identifier.Trim()} — {title.Trim()}.", cancellationToken)
            .ConfigureAwait(false);

        // The move is what makes it a project task, and it persists the
        // task's own state again — so a task is durable and correctly
        // owned from the moment it exists, not from the first edit.
        await created.MoveAsync(project.Id, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <inheritdoc />
    public async Task AssignAsync(Guid taskId, string? principalId, CancellationToken cancellationToken = default) =>
        await (await RequireTaskAsync(taskId, cancellationToken).ConfigureAwait(false))
            .AssignAsync(principalId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task AssignToCurrentPrincipalAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        AssignAsync(taskId, _context.ResolveCurrentPrincipalId(), cancellationToken);

    /// <inheritdoc />
    public async Task ChangeWorkStateAsync(Guid taskId, TaskWorkState target, CancellationToken cancellationToken = default) =>
        await (await RequireTaskAsync(taskId, cancellationToken).ConfigureAwait(false))
            .ChangeWorkStateAsync(target, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SetDueDateAsync(Guid taskId, DateTimeOffset? dueDate, CancellationToken cancellationToken = default) =>
        await (await RequireTaskAsync(taskId, cancellationToken).ConfigureAwait(false))
            .SetDueDateAsync(dueDate, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SetPriorityAsync(Guid taskId, WorkPriority priority, CancellationToken cancellationToken = default) =>
        await (await RequireTaskAsync(taskId, cancellationToken).ConfigureAwait(false))
            .SetPriorityAsync(priority, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task EditAsync(Guid taskId, string? title = null, string? description = null, CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(taskId, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(title))
            await task.RenameAsync(title.Trim(), cancellationToken).ConfigureAwait(false);

        // A rewritten description is a new revision, not an overwrite —
        // what a task used to say is part of its history, and the platform
        // already records that for every other object.
        if (description is not null)
            await task.ReviseAsync(description, "Task description edited.", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ContributeToAsync(Guid taskId, Guid milestoneOrDeliverableId, CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(taskId, cancellationToken).ConfigureAwait(false);

        var target = await _context.Repository.FindAsync(milestoneOrDeliverableId, cancellationToken).ConfigureAwait(false)
            ?? throw new TaskTargetNotFoundException(milestoneOrDeliverableId);

        if (target is not (IMilestone or IDeliverable))
            throw new TaskTargetNotFoundException(milestoneOrDeliverableId);

        await task.ContributeToAsync(target.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<EngineeringTask> RequireTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(taskId, cancellationToken).ConfigureAwait(false) as EngineeringTask
        ?? throw new TaskNotFoundException(taskId);
}

/// <summary>Thrown when a task operation names an object that is not a task.</summary>
public sealed class TaskNotFoundException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="TaskNotFoundException"/> class.</summary>
    public TaskNotFoundException(Guid taskId)
        : base($"No task with Id '{taskId}' exists in this session.") => TaskId = taskId;

    /// <summary>The Id that did not resolve to a task.</summary>
    public Guid TaskId { get; }
}

/// <summary>Thrown when a task is asked to contribute to something that is not a Milestone or Deliverable.</summary>
public sealed class TaskTargetNotFoundException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="TaskTargetNotFoundException"/> class.</summary>
    public TaskTargetNotFoundException(Guid targetId)
        : base($"'{targetId}' is not a Milestone or Deliverable a task can contribute to.") => TargetId = targetId;

    /// <summary>The Id that did not resolve to a valid target.</summary>
    public Guid TargetId { get; }
}
