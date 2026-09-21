using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace;

/// <summary>
/// The Engineering Cockpit's own health rollup and score wording
/// (`WP 8.1C`), stated once (`ADR-0151`) so the workspace-wide
/// <see cref="EngineeringCockpit.Health"/> and every per-project
/// <see cref="CockpitProjectHealth.Health"/> are the same rule over a
/// different set of discipline statuses — never two computations that
/// could drift apart.
/// </summary>
/// <remarks>
/// RAG is <see cref="EngineeringHealthStatus"/>: Blocked is red, Attention
/// is amber, Healthy is green, Unknown is grey. No second Red/Amber/Green
/// vocabulary exists anywhere in the platform — the desktop's own
/// <c>HealthColors</c> and the Dashboard Export's lower-cased words are
/// both projections of this one enum.
/// </remarks>
internal static class EngineeringHealthRollup
{
    /// <summary>
    /// Rolls <paramref name="statuses"/> up into one overall health —
    /// <see cref="EngineeringHealthStatus.Blocked"/> if any is; else
    /// <see cref="EngineeringHealthStatus.Attention"/> if any is; else
    /// <see cref="EngineeringHealthStatus.Unknown"/> if every one is;
    /// else <see cref="EngineeringHealthStatus.Healthy"/>. Identical, by
    /// construction, to the rule <see cref="EngineeringCockpit.Health"/>
    /// has applied since `WP 8.1C`.
    /// </summary>
    public static EngineeringHealthStatus RollUp(IReadOnlyList<EngineeringHealthStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        if (statuses.Any(s => s == EngineeringHealthStatus.Blocked))
            return EngineeringHealthStatus.Blocked;

        if (statuses.Any(s => s == EngineeringHealthStatus.Attention))
            return EngineeringHealthStatus.Attention;

        return statuses.All(s => s == EngineeringHealthStatus.Unknown)
            ? EngineeringHealthStatus.Unknown
            : EngineeringHealthStatus.Healthy;
    }

    /// <summary>
    /// The Engineering Health Score's own display text over
    /// <paramref name="statuses"/> — how many of the disciplines report
    /// real data at all, and how many of those are Healthy — worded
    /// exactly as <see cref="EngineeringCockpit.HealthScoreDisplay"/>
    /// always has been.
    /// </summary>
    public static string ScoreDisplay(IReadOnlyList<EngineeringHealthStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var withData = statuses.Count(s => s != EngineeringHealthStatus.Unknown);

        return withData == 0
            ? "— (no Engineering data yet)"
            : $"{statuses.Count(s => s == EngineeringHealthStatus.Healthy)}/{withData} healthy ({withData}/{statuses.Count} disciplines reporting)";
    }

    /// <summary>
    /// The "Overdue Actions" rule (`EngineeringCockpit.OverdueActions`),
    /// stated once: every <see cref="EngineeringTask"/> in
    /// <paramref name="tasks"/> that <see cref="EngineeringTask.IsOverdue"/>
    /// as of <paramref name="asOf"/>, soonest-due first, as the Cockpit's
    /// own <see cref="CockpitActionItem"/> lines.
    /// </summary>
    public static IReadOnlyList<CockpitActionItem> OverdueActions(IEnumerable<ITask> tasks, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        return
        [
            .. tasks
                .OfType<EngineeringTask>()
                .Where(t => t.IsOverdue(asOf))
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(t => new CockpitActionItem(
                    t.DisplayName,
                    t.AssignedToPrincipalId ?? CockpitActionItem.NobodyAssigned,
                    t.DueDate!.Value,
                    (int)Math.Floor((asOf - t.DueDate!.Value).TotalDays))),
        ];
    }
}
