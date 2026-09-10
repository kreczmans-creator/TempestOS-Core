using Tempest.Core.BusinessGovernance;

namespace Tempest.Workspace.Kpi;

/// <summary>
/// One principal's own utilisation for a <see cref="KpiPeriod"/>
/// (`ADR-0150`): billable hours over available hours, the latter from
/// <c>IWorkingPatternProvider</c>, never calendar days.
/// </summary>
public sealed record UtilisationRow(string PrincipalIdentityId, decimal BillableHours, decimal AvailableHours)
{
    /// <summary>Billable ÷ available as a whole percentage, rounded to the nearest whole point — <see langword="null"/> when <see cref="AvailableHours"/> is zero (an honest "cannot compute," never a fabricated 0%).</summary>
    public int? Percent => AvailableHours > 0m
        ? (int)Math.Round(BillableHours / AvailableHours * 100m, 0, MidpointRounding.AwayFromZero)
        : null;
}

/// <summary>
/// One project's own margin for a <see cref="KpiPeriod"/> (`ADR-0150`):
/// billable value (time billed plus fixed-price deliverables) less cost
/// (every hour, billable or not, at its own frozen cost rate).
/// </summary>
/// <param name="ProjectId">The project this margin is computed for.</param>
/// <param name="ProjectName">The project's own display name, read at the same sequence.</param>
/// <param name="Revenue">Σ billable hours × frozen billing rate, plus Σ fixed-price deliverable value completed in the period.</param>
/// <param name="Cost">Σ all hours (billable or not) × frozen cost rate — an entry with no cost rate contributes zero, see <see cref="AnyMissingCostRate"/>.</param>
/// <param name="AnyMissingCostRate">Whether at least one contributing entry carried no frozen cost rate — its hours still count in <see cref="Cost"/>'s denominator of hours worked, at zero cost, so the card can say so rather than silently understating cost.</param>
public sealed record MarginRow(Guid ProjectId, string ProjectName, Money Revenue, Money Cost, bool AnyMissingCostRate)
{
    /// <summary>Revenue less cost.</summary>
    public Money Margin => Revenue - Cost;

    /// <summary>Margin as a whole percentage of <see cref="Revenue"/>, rounded to the nearest whole point — <see langword="null"/> when <see cref="Revenue"/> is zero.</summary>
    public int? MarginPercentOfRevenue => Revenue.Amount != 0m
        ? (int)Math.Round(Margin.Amount / Revenue.Amount * 100m, 0, MidpointRounding.AwayFromZero)
        : null;
}

/// <summary>
/// One project's own work in progress (`ADR-0150`): billable value not yet
/// linked to an invoice request, with the age of the oldest such entry —
/// a live backlog snapshot, never scoped to the selected
/// <see cref="KpiPeriod"/> (unbilled work from any date is still owed).
/// </summary>
public sealed record WorkInProgressRow(Guid ProjectId, string ProjectName, Money Value, int OldestEntryAgeDays);

/// <summary>
/// Days sales outstanding across every <c>InvoiceRequest</c> that has ever
/// reached <c>Sent</c> or later — a live aggregate, never scoped to the
/// selected <see cref="KpiPeriod"/>, and never a fabricated zero when the
/// figure cannot honestly be computed (`ADR-0150`).
/// </summary>
/// <param name="IsAvailable">Whether at least one invoice request carries an issued date read from a connector — <see langword="false"/> when no connector has ever been authorised, or nothing has been sent yet.</param>
/// <param name="AverageDays">The mean of (paid date, or "as of" if unpaid) minus issued date, in days — <see langword="null"/> when <see cref="IsAvailable"/> is <see langword="false"/>.</param>
/// <param name="InvoiceCount">How many invoice requests contributed to <see cref="AverageDays"/>.</param>
public sealed record DaysSalesOutstandingResult(bool IsAvailable, decimal? AverageDays, int InvoiceCount);

/// <summary>
/// Every coherent-read financial ingredient <see cref="WorkspaceSnapshotReader"/>
/// computes directly from its one scan of the object-state collection
/// (`WP 19.1B`) — everything except <see cref="UtilisationRow.AvailableHours"/>,
/// which needs a separate, non-transactional call to
/// <c>IWorkingPatternProvider</c> per principal (see
/// <see cref="KpiSnapshotService"/>'s own remarks on why that call cannot
/// be made from inside the same read transaction).
/// </summary>
public sealed record KpiFinancials(
    IReadOnlyDictionary<string, decimal> BillableHoursByPrincipal,
    IReadOnlyList<MarginRow> MarginByProject,
    IReadOnlyList<WorkInProgressRow> WorkInProgress,
    DaysSalesOutstandingResult DaysSalesOutstanding,
    int CalcThroughput);

/// <summary>
/// The Home cockpit's own complete, five-equation KPI read, for one
/// <see cref="Period"/> as of one <see cref="AsOf"/> date (`WP 19.1B`,
/// `ADR-0150`). Built by <see cref="KpiSnapshotService.GetSnapshotAsync"/>.
/// </summary>
public sealed record KpiSnapshot(
    KpiPeriod Period,
    DateOnly AsOf,
    IReadOnlyList<UtilisationRow> Utilisation,
    IReadOnlyList<MarginRow> MarginByProject,
    IReadOnlyList<WorkInProgressRow> WorkInProgress,
    DaysSalesOutstandingResult DaysSalesOutstanding,
    int CalcThroughput);
