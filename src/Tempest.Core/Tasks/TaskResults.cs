namespace Tempest.Core.Tasks;

/// <summary>Why an <see cref="ITaskService"/> act was refused, or <see cref="None"/> if it was not.</summary>
public enum TaskRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No project is registered under the requested id.</summary>
    ProjectNotFound,

    /// <summary>No manual task is registered under the requested id.</summary>
    TaskNotFound,

    /// <summary>The task is already done.</summary>
    AlreadyDone,

    /// <summary>The task's own project is Archive — closed 90 days or more ago — and read-only (`WP 19.10H`, `TD-179`).</summary>
    ProjectArchived,
}

/// <summary>The outcome of an <see cref="ITaskService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="TaskRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Task">The task acted on, when it could be resolved.</param>
public sealed record TaskActResult(TaskRefusal Refusal, string? Reason, ManualTask? Task)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == TaskRefusal.None;
}
