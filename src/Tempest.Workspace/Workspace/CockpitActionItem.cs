namespace Tempest.App.Workspace;

/// <summary>
/// One entry in an Engineering Cockpit action region — the "Overdue
/// Actions" list, or the per-discipline "Open Actions" triage list
/// (`WP8.0C Engineering Cockpit Specification.md` §2) — a real, overdue
/// task or action, read from the domain.
/// </summary>
/// <remarks>
/// <para>
/// This record used to describe itself as "fixed, representative
/// placeholder content, since no service tracking real open actions is
/// wired to the Workspace yet". The <b>Overdue Actions</b> half is now
/// real: <c>EngineeringTask</c> carries a due date and a work state, so
/// "overdue" is something the domain can answer rather than something a
/// card has to invent.
/// </para>
/// <para>
/// The per-discipline <b>Open Actions</b> triage entries are unchanged and
/// carry no date, which is why <see cref="DueDate"/> is optional rather
/// than required. Those entries are a different question — "what should
/// someone look at" — and are not task records.
/// </para>
/// </remarks>
/// <param name="Title">The task's own title.</param>
/// <param name="Owner">Who it is assigned to, or a plain statement that nobody is.</param>
/// <param name="DueDate">When it was due — <see langword="null"/> for an entry that is not a dated task.</param>
/// <param name="DaysOverdue">How many whole days past due it is.</param>
internal sealed record CockpitActionItem(string Title, string Owner, DateTimeOffset? DueDate = null, int DaysOverdue = 0)
{
    /// <summary>What the card shows when a task has no assignee.</summary>
    /// <remarks>
    /// Named rather than spelled inline: an unassigned overdue task is the
    /// most important row on this card, and calling it "Unassigned" is a
    /// finding, not a blank.
    /// </remarks>
    public const string NobodyAssigned = "Unassigned";
}
