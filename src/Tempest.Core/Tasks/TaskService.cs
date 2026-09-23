using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;

namespace Tempest.Core.Tasks;

/// <summary>The concrete <see cref="ITaskService"/> implementation (`WP 19.5C`).</summary>
public sealed class TaskService : ITaskService
{
    private readonly EngineeringDomainContext _context;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="TaskService"/> class.</summary>
    /// <param name="timeProvider">The clock "today" is read from. <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>; a test supplies a controllable one.</param>
    public TaskService(EngineeringDomainContext context, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<TaskActResult> CreateAsync(string title, Guid? projectId, DateOnly? dueDate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (projectId is { } id)
        {
            if (await _context.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false) is not Project project || !IsLive(project))
                return new TaskActResult(TaskRefusal.ProjectNotFound, $"No project '{id}' is registered.", null);

            if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
            {
                return new TaskActResult(
                    TaskRefusal.ProjectArchived, $"Project '{id}' is archived (closed {project.ClosedOn:O}); no new task can be added to it.", null);
            }
        }

        var trimmed = title.Trim();

        var created = await new EngineeringObjectFactory<ManualTask>(
            ManualTask.CanonicalKind,
            _context,
            (doc, rev) => new ManualTask(doc, rev, _context, identifier: null, trimmed, EngineeringObjectMetadata.Empty, dueDate))
            .CreateAsync($"Task '{trimmed}' created.", cancellationToken)
            .ConfigureAwait(false);

        if (projectId is { } parentId && created is IHasParent hasParent)
            await hasParent.MoveAsync(parentId, cancellationToken).ConfigureAwait(false);

        return new TaskActResult(TaskRefusal.None, null, (ManualTask)created);
    }

    /// <inheritdoc />
    public async Task<TaskActResult> CompleteAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (task is null)
            return NotFound(taskId);

        if (task.Done)
            return new TaskActResult(TaskRefusal.AlreadyDone, $"Task '{taskId}' is already done ({task.CompletedOn:O}).", task);

        if (await ArchivedAsync(task, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await task.MarkDoneAsync(Today(), cancellationToken).ConfigureAwait(false);

        return new TaskActResult(TaskRefusal.None, null, task);
    }

    /// <inheritdoc />
    public async Task<TaskActResult> DeleteAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (task is null)
            return NotFound(taskId);

        if (await ArchivedAsync(task, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await task.DeleteAsync(cancellationToken).ConfigureAwait(false);

        return new TaskActResult(TaskRefusal.None, null, task);
    }

    /// <summary>The archived-project guard (`WP 19.10H`, `TD-179`): every mutating command on a manual task belonging to an archived project is refused, here, before its own mutator ever runs.</summary>
    private async Task<TaskActResult?> ArchivedAsync(ManualTask task, CancellationToken cancellationToken)
    {
        if (task.ParentId is not { } projectId
            || await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
        {
            return null;
        }

        return ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? new TaskActResult(TaskRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); this task is read-only.", task)
            : null;
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    private async Task<ManualTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var candidate = await _context.Repository.FindAsync(taskId, cancellationToken).ConfigureAwait(false);
        return candidate is ManualTask { } task && IsLive(task) ? task : null;
    }

    private static TaskActResult NotFound(Guid taskId) =>
        new(TaskRefusal.TaskNotFound, $"No task '{taskId}' is registered.", null);

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
