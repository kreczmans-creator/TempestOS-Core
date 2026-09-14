using Tempest.Core.Evidence;
using Tempest.Core.Invoicing;
using Tempest.Core.Quotations;

namespace Tempest.Workspace.Tasks;

/// <summary>
/// The pure functions behind every <see cref="TaskBucket"/> rule
/// (`WP 19.5C`, Product Owner comment item 6) — no persistence dependency,
/// unit-tested against hand-authored fixtures, mirroring
/// <c>Tempest.Workspace.Kpi.KpiEquations</c>. Each rule is defined exactly
/// once, here, so a dashboard can print the same reasoning it computes
/// from.
/// </summary>
public static class TaskEquations
{
    /// <summary>A due date within this many days of "today" (inclusive) — and not overdue, and not today — is Due this week (Work Package 19.5C's own brief).</summary>
    public const int DueThisWeekWithinDays = 7;

    /// <summary>A quote Sent more than this many days ago is chased (Work Package 19.5C's own brief: "quotes ... over 7 days old to chase"). No decision was asked of, or given by, the Product Owner on this one (`WP 20.1B` brief) — it stays.</summary>
    public const int QuoteChaseAfterDays = 7;

    /// <summary>Which due-date bucket <paramref name="dueDate"/> falls into, as of <paramref name="today"/>.</summary>
    public static TaskBucket BucketForDueDate(DateOnly dueDate, DateOnly today)
    {
        if (dueDate < today)
            return TaskBucket.Overdue;

        if (dueDate == today)
            return TaskBucket.DueToday;

        return dueDate.DayNumber - today.DayNumber <= DueThisWeekWithinDays ? TaskBucket.DueThisWeek : TaskBucket.Later;
    }

    /// <summary>
    /// Whether a milestone counts as "complete" — every live deliverable
    /// under it carries a completion. A milestone with no deliverables at
    /// all is <em>not</em> complete: nothing has been delivered against it
    /// (a disclosed heuristic — <c>Milestone</c> carries no "achieved" flag
    /// of its own, mirroring <c>Tempest.Workspace.Projects.ProjectMilestoneRegister</c>'s
    /// own "date passed + linked work still open" reasoning).
    /// </summary>
    public static bool IsMilestoneComplete(Guid milestoneId, ILookup<Guid, Guid> deliverableIdsByMilestone, IReadOnlySet<Guid> completedDeliverableIds)
    {
        var underMilestone = deliverableIdsByMilestone[milestoneId].ToList();
        return underMilestone.Count > 0 && underMilestone.All(completedDeliverableIds.Contains);
    }

    /// <summary>Every live, incomplete deliverable — due-bucketed from its own parent milestone's target date (a deliverable carries no date of its own).</summary>
    public static IReadOnlyList<TaskItem> DeliverableItems(
        IReadOnlyList<DeliverableFact> deliverables, IReadOnlyDictionary<Guid, MilestoneFact> milestonesById,
        IReadOnlySet<Guid> completedDeliverableIds, DateOnly today)
    {
        var items = new List<TaskItem>();

        foreach (var deliverable in deliverables)
        {
            if (completedDeliverableIds.Contains(deliverable.DeliverableId))
                continue;

            if (!milestonesById.TryGetValue(deliverable.MilestoneId, out var milestone))
                continue;

            items.Add(new TaskItem(
                "Deliverable", deliverable.Title, milestone.ProjectId, milestone.TargetDate, deliverable.DeliverableId,
                BucketForDueDate(milestone.TargetDate, today)));
        }

        return items;
    }

    /// <summary>Every live, incomplete milestone — due-bucketed from its own target date.</summary>
    public static IReadOnlyList<TaskItem> MilestoneItems(
        IReadOnlyList<MilestoneFact> milestones, ILookup<Guid, Guid> deliverableIdsByMilestone,
        IReadOnlySet<Guid> completedDeliverableIds, DateOnly today)
    {
        var items = new List<TaskItem>();

        foreach (var milestone in milestones)
        {
            if (IsMilestoneComplete(milestone.MilestoneId, deliverableIdsByMilestone, completedDeliverableIds))
                continue;

            items.Add(new TaskItem(
                "Milestone", milestone.Title, milestone.ProjectId, milestone.TargetDate, milestone.MilestoneId,
                BucketForDueDate(milestone.TargetDate, today)));
        }

        return items;
    }

    /// <summary>Every live, open (not done) manual task — due-bucketed, or Later when it carries no due date.</summary>
    public static IReadOnlyList<TaskItem> ManualTaskItems(IReadOnlyList<ManualTaskFact> tasks, DateOnly today) =>
        tasks
            .Where(t => !t.Done)
            .Select(t => new TaskItem(
                "ManualTask", t.Title, t.ProjectId, t.DueDate, t.TaskId,
                t.DueDate is { } due ? BucketForDueDate(due, today) : TaskBucket.Later))
            .ToList();

    /// <summary>Evidence Draft, with a subject and a file — genuinely ready to check, not an empty draft (Work Package 19.5C's own brief).</summary>
    public static IReadOnlyList<TaskItem> ReviewItems(IReadOnlyList<EvidenceReviewFact> evidence) =>
        evidence
            .Where(e => e.Status == EvidenceStatus.Draft && e.HasSubjectAndFile)
            .Select(e => new TaskItem(Tempest.Core.Evidence.Evidence.CanonicalKind, e.Title, e.ProjectId, null, e.EvidenceId, TaskBucket.Reviews))
            .ToList();

    /// <summary>Evidence Checked — awaiting issue.</summary>
    public static IReadOnlyList<TaskItem> ApprovalItems(IReadOnlyList<EvidenceReviewFact> evidence) =>
        evidence
            .Where(e => e.Status == EvidenceStatus.Checked)
            .Select(e => new TaskItem(Tempest.Core.Evidence.Evidence.CanonicalKind, e.Title, e.ProjectId, null, e.EvidenceId, TaskBucket.Approvals))
            .ToList();

    /// <summary>
    /// Every live, incomplete Calculation under a project (`TD-181`,
    /// Product Owner decision 2026-09-15 §2) — ordered by due date where
    /// one is carried, with every calculation carrying none (every
    /// calculation, until a due date is ever added to the Kind) sorting
    /// last, "Later" position, mirroring <see cref="ManualTaskItems"/>'s
    /// own ordering. <paramref name="calculations"/> already excludes a
    /// calculation with no project ancestor, and one cited by issued
    /// evidence — both are <see cref="TasksReadModelService"/>'s own
    /// concern, read once off the durable state, not this pure function's.
    /// </summary>
    public static IReadOnlyList<TaskItem> CalculationItems(IReadOnlyList<CalculationChaseFact> calculations) =>
        calculations
            .Where(c => !c.Completed)
            .OrderBy(c => c.Title, StringComparer.Ordinal)
            .Select(c => new TaskItem("Calculation", c.Title, c.ProjectId, null, c.CalculationId, TaskBucket.Calculations))
            .ToList();

    /// <summary>Sent, unpaid invoice requests past their own due date (`TD-180`) — the Finance bucket lists a request only after its own <see cref="InvoiceChaseFact.DueOn"/>, replacing the thirty-day heuristic `WP 20.1B` closes out.</summary>
    public static IReadOnlyList<TaskItem> InvoiceFinanceItems(IReadOnlyList<InvoiceChaseFact> requests, DateOnly today) =>
        requests
            .Where(r => r.Status == InvoiceRequestStatus.Sent && r.PaidDate is null && r.DueOn is { } due && today > due)
            .Select(r => new TaskItem(InvoiceRequest.CanonicalKind, r.Title, r.ProjectId, r.DueOn, r.RequestId, TaskBucket.Finance))
            .ToList();

    /// <summary>Sent quotations more than <see cref="QuoteChaseAfterDays"/> days old.</summary>
    public static IReadOnlyList<TaskItem> QuotationFinanceItems(IReadOnlyList<QuotationChaseFact> quotations, DateOnly today) =>
        quotations
            .Where(q => q.Status == QuotationStatus.Sent && q.SentOn is { } sentOn && today.DayNumber - sentOn.DayNumber > QuoteChaseAfterDays)
            .Select(q => new TaskItem(Quotation.CanonicalKind, q.Title, q.ProjectId, q.SentOn, q.QuotationId, TaskBucket.Finance))
            .ToList();

    /// <summary>The next <paramref name="count"/> milestones across open projects, by date — Home's own "Upcoming milestones" panel.</summary>
    public static IReadOnlyList<UpcomingMilestone> UpcomingMilestones(IReadOnlyList<MilestoneFact> milestones, int count) =>
        milestones
            .OrderBy(m => m.TargetDate)
            .Take(count)
            .Select(m => new UpcomingMilestone(m.MilestoneId, m.Title, m.ProjectId, m.TargetDate))
            .ToList();
}
