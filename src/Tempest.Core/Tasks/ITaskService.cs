namespace Tempest.Core.Tasks;

/// <summary>
/// The acts a <see cref="ManualTask"/> supports: create, complete, delete
/// (`WP 19.5C`, Product Owner comment item 6). Every act is one
/// transaction with an audit row; whether an act is <em>permitted</em> is
/// decided here, before <see cref="ManualTask"/>'s own mutator ever runs,
/// and reported back as a refusal result rather than an exception,
/// mirroring <c>Tempest.Core.Quotations.IQuotationService</c>.
/// </summary>
public interface ITaskService
{
    /// <summary>Creates a new, open <see cref="ManualTask"/>.</summary>
    /// <param name="title">The task's own title.</param>
    /// <param name="projectId">The project this task belongs to, or <see langword="null"/> for a task with no project.</param>
    /// <param name="dueDate">When this task is due, or <see langword="null"/> for none.</param>
    /// <remarks>Refused, as a result, when <paramref name="projectId"/> is given but does not identify a live project.</remarks>
    Task<TaskActResult> CreateAsync(string title, Guid? projectId, DateOnly? dueDate, CancellationToken cancellationToken = default);

    /// <summary>Marks <paramref name="taskId"/>'s own task done, as of today.</summary>
    /// <remarks>Refused, as a result, when the task is already done.</remarks>
    Task<TaskActResult> CompleteAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Deletes <paramref name="taskId"/>'s own task.</summary>
    Task<TaskActResult> DeleteAsync(Guid taskId, CancellationToken cancellationToken = default);
}
