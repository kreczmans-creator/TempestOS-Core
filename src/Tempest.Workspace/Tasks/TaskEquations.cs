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

    /// <summary>
    /// How many days after <see cref="InvoiceChaseFact.SentAtUtc"/> an
    /// unpaid, Sent invoice request counts as "past terms". No per-request
    /// terms field exists on <c>InvoiceRequest</c> today (confirmed by the
    /// seam map: no age/staleness helper exists) — a disclosed heuristic,
    /// not a value read from anywhere.
    /// </summary>
    public const int InvoiceTermsDays = 30;

    /// <summary>A quote Sent more than this many days ago is chased (Work Package 19.5C's own brief: "quotes ... over 7 days old to chase").</summary>
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

    /// <summary>Sent, unpaid invoice requests more than <see cref="InvoiceTermsDays"/> days old.</summary>
    public static IReadOnlyList<TaskItem> InvoiceFinanceItems(IReadOnlyList<InvoiceChaseFact> requests, DateTimeOffset asOf) =>
        requests
            .Where(r => r.Status == InvoiceRequestStatus.Sent && r.PaidDate is null
                        && r.SentAtUtc is { } sent && (asOf - sent).TotalDays > InvoiceTermsDays)
            .Select(r => new TaskItem(
                InvoiceRequest.CanonicalKind, r.Title, r.ProjectId,
                r.SentAtUtc is { } s ? DateOnly.FromDateTime(s.UtcDateTime) : null, r.RequestId, TaskBucket.Finance))
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
