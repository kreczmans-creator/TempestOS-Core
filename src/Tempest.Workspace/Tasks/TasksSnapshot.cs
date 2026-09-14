namespace Tempest.Workspace.Tasks;

/// <summary>
/// Which bucket a <see cref="TaskItem"/> belongs to (`WP 19.5C`, Product
/// Owner comment item 6) — the Home dashboard's own tiles (Overdue, Due
/// today, Due this week, Approvals, Finance) plus <see cref="Reviews"/>,
/// the Engineering dashboard's own second tile.
/// </summary>
public enum TaskBucket
{
    /// <summary>Past its own due date and not complete.</summary>
    Overdue,

    /// <summary>Due today and not complete.</summary>
    DueToday,

    /// <summary>Due within the next seven days and not complete.</summary>
    DueThisWeek,

    /// <summary>Due further out, or with no due date at all, and not complete.</summary>
    Later,

    /// <summary>Evidence Draft, with a subject and a file — genuinely ready to check.</summary>
    Reviews,

    /// <summary>Evidence Checked — awaiting issue.</summary>
    Approvals,

    /// <summary>An unpaid invoice request past terms, or a quote sent more than seven days ago — to chase.</summary>
    Finance,
}

/// <summary>
/// One row the Tasks read model shows — a deliverable, a milestone, a
/// piece of Evidence awaiting check or issue, an invoice request or quote
/// to chase, or a manual task (`WP 19.5C`).
/// </summary>
/// <param name="Kind">The source object's own canonical Kind — <c>"Deliverable"</c>, <c>"Milestone"</c>, <see cref="Tempest.Core.Evidence.Evidence.CanonicalKind"/>, <see cref="Tempest.Core.Invoicing.InvoiceRequest.CanonicalKind"/>, <see cref="Tempest.Core.Quotations.Quotation.CanonicalKind"/>, or <see cref="Tempest.Core.Tasks.ManualTask.CanonicalKind"/>.</param>
/// <param name="Title">The source object's own display name.</param>
/// <param name="ProjectId">The project this item belongs to, when one could be resolved. <see langword="null"/> for a manual task with no project.</param>
/// <param name="DueDate">This item's own due date, when it has one.</param>
/// <param name="ObjectId">The source object's own id — with <see cref="Kind"/>, what opens it right up.</param>
/// <param name="Bucket">Which bucket this item belongs to.</param>
public sealed record TaskItem(string Kind, string Title, Guid? ProjectId, DateOnly? DueDate, Guid ObjectId, TaskBucket Bucket);

/// <summary>One milestone on the "next ten, across open projects, by date" panel (`WP 19.5C`, Product Owner comment item 6 — Home's own "Upcoming milestones").</summary>
public sealed record UpcomingMilestone(Guid MilestoneId, string Title, Guid ProjectId, DateOnly TargetDate);

/// <summary>
/// The complete Tasks read model (`WP 19.5C`): the buckets and counts the
/// Home and Engineering dashboard tiles show, the open-tasks list, the
/// Reviews and Approvals lists the Engineering dashboard needs by name,
/// and the next ten upcoming milestones.
/// </summary>
/// <param name="Counts">How many items are in each <see cref="TaskBucket"/>.</param>
/// <param name="OpenTasks">Every due-bucketed item (Overdue/Due today/Due this week/Later) — deliverables, milestones and manual tasks — the Home task list and the Engineering dashboard's "Open tasks" panel.</param>
/// <param name="Reviews">Evidence Draft, with a subject and a file — genuinely ready to check.</param>
/// <param name="Approvals">Evidence Checked — awaiting issue.</param>
/// <param name="Finance">Unpaid invoice requests past terms, and quotes sent more than seven days ago.</param>
/// <param name="UpcomingMilestones">The next ten milestones across open projects, by date.</param>
public sealed record TasksSnapshot(
    IReadOnlyDictionary<TaskBucket, int> Counts,
    IReadOnlyList<TaskItem> OpenTasks,
    IReadOnlyList<TaskItem> Reviews,
    IReadOnlyList<TaskItem> Approvals,
    IReadOnlyList<TaskItem> Finance,
    IReadOnlyList<UpcomingMilestone> UpcomingMilestones);
