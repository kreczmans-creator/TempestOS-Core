using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;

namespace Tempest.Workspace.Kpi;

/// <summary>
/// The five equations `ADR-0150` defines, verbatim, as pure functions over
/// plain <see cref="KpiFacts"/>-shaped data (`WP 19.1B`) — no persistence,
/// no service dependency, so every equation is unit-tested directly
/// against hand-authored fixtures with hand-computed expected values,
/// independently of how the facts were read.
/// </summary>
/// <remarks>
/// <b>Which equations are scoped to <see cref="KpiPeriod"/> and which are
/// not</b> — read directly off the `WP 19.1B` row's own wording, not
/// assumed: utilisation, margin and calc throughput are each stated "over
/// the period"/"dated in the period"/"in the period"; work in progress and
/// days sales outstanding are each stated with no period qualifier at all
/// — both are live backlog/ageing snapshots ("entries with no
/// <c>InvoicedBy</c> link", "invoices in <c>Sent</c> or later") that stay
/// true regardless of which reporting window is selected, so
/// <see cref="WorkInProgressByProject"/> and <see cref="DaysSalesOutstanding"/>
/// take an "as of" date rather than a period.
/// </remarks>
public static class KpiEquations
{
    /// <summary>
    /// Utilisation's own numerator, per principal: Σ billable hours over
    /// <paramref name="period"/> — every principal with at least one live
    /// entry in the period appears, even one whose hours are entirely
    /// non-billable (billable <c>0</c>), so an under-utilised principal is
    /// shown rather than omitted.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> BillableHoursByPrincipal(
        IReadOnlyList<TimesheetEntryFacts> entries, KpiPeriod period)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(period);

        var byPrincipal = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!period.Contains(entry.Date))
                continue;

            byPrincipal.TryAdd(entry.PrincipalIdentityId, 0m);

            if (entry.Billable)
                byPrincipal[entry.PrincipalIdentityId] += entry.Hours;
        }

        return byPrincipal;
    }

    /// <summary>
    /// Utilisation's own denominator: <paramref name="hoursPerWeek"/> (a
    /// flat weekly figure, `IWorkingPatternProvider`'s own contract) times
    /// the number of weeks <paramref name="period"/> spans — a whole week
    /// counts as one, a partial week pro-rated by its own day count, never
    /// calendar days alone (`ADR-0150`).
    /// </summary>
    public static decimal AvailableHours(decimal hoursPerWeek, KpiPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        return hoursPerWeek * period.TotalDays / 7m;
    }

    /// <summary>
    /// Margin per project: (Σ billable hours × frozen billing rate + Σ
    /// fixed-price deliverable value) − Σ all hours × frozen cost rate,
    /// over entries and completions dated in <paramref name="period"/>,
    /// grouped by project and — defensively, since two entries for the
    /// same project are expected to always share one currency (one pinned
    /// rate card) — by currency too, so a data inconsistency yields two
    /// rows rather than a thrown <see cref="CurrencyMismatchException"/>.
    /// </summary>
    public static IReadOnlyList<MarginRow> MarginByProject(
        IReadOnlyList<TimesheetEntryFacts> entries,
        IReadOnlyList<DeliverableCompletionFacts> completions,
        IReadOnlyDictionary<Guid, string> projectDisplayNames,
        KpiPeriod period)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(projectDisplayNames);
        ArgumentNullException.ThrowIfNull(period);

        var buckets = new Dictionary<(Guid ProjectId, CurrencyCode Currency), MarginAccumulator>();

        MarginAccumulator BucketFor(Guid projectId, CurrencyCode currency)
        {
            var key = (projectId, currency);
            if (!buckets.TryGetValue(key, out var accumulator))
            {
                accumulator = new MarginAccumulator(currency);
                buckets[key] = accumulator;
            }

            return accumulator;
        }

        foreach (var entry in entries)
        {
            if (!period.Contains(entry.Date))
                continue;

            var bucket = BucketFor(entry.ProjectId, entry.BillingRate.Currency);

            if (entry.Billable)
                bucket.Revenue += entry.BillingRate * entry.Hours;

            // Cost rate is expected to always share the billing rate's own
            // currency (both resolved from the same pinned rate card at
            // record time) — but this is a read model, never a place that
            // lets a data inconsistency throw and take the whole Cockpit
            // render down with it: a mismatched cost-rate currency is
            // treated exactly like a missing one, and disclosed the same way.
            if (entry.CostRate is { } costRate && costRate.Currency == bucket.Currency)
                bucket.Cost += costRate * entry.Hours;
            else
                bucket.AnyMissingCostRate = true;
        }

        foreach (var completion in completions)
        {
            if (completion.FixedPriceValue is not { } fixedPrice || !period.Contains(completion.CompletedOn))
                continue;

            BucketFor(completion.ProjectId, fixedPrice.Currency).Revenue += fixedPrice;
        }

        return
        [
            .. buckets
                .Select(kvp => new MarginRow(
                    kvp.Key.ProjectId,
                    projectDisplayNames.GetValueOrDefault(kvp.Key.ProjectId, $"Unknown project ({kvp.Key.ProjectId:N})"),
                    kvp.Value.Revenue,
                    kvp.Value.Cost,
                    kvp.Value.AnyMissingCostRate))
                .OrderBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Work in progress: Σ billable value of live entries/completions with
    /// no <c>InvoicedBy</c> link, by project, with the age of the oldest
    /// such entry as of <paramref name="asOf"/> — never scoped to a
    /// period (see this class's own remarks).
    /// </summary>
    public static IReadOnlyList<WorkInProgressRow> WorkInProgressByProject(
        IReadOnlyList<TimesheetEntryFacts> entries,
        IReadOnlyList<DeliverableCompletionFacts> completions,
        IReadOnlyDictionary<Guid, string> projectDisplayNames,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(projectDisplayNames);

        var buckets = new Dictionary<(Guid ProjectId, CurrencyCode Currency), WipAccumulator>();

        WipAccumulator BucketFor(Guid projectId, CurrencyCode currency)
        {
            var key = (projectId, currency);
            if (!buckets.TryGetValue(key, out var accumulator))
            {
                accumulator = new WipAccumulator(currency);
                buckets[key] = accumulator;
            }

            return accumulator;
        }

        foreach (var entry in entries)
        {
            if (!entry.Billable || entry.InvoicedBy is not null)
                continue;

            var bucket = BucketFor(entry.ProjectId, entry.BillingRate.Currency);
            bucket.Value += entry.BillingRate * entry.Hours;
            bucket.Consider(entry.Date);
        }

        foreach (var completion in completions)
        {
            if (completion.FixedPriceValue is not { } fixedPrice || completion.InvoicedBy is not null)
                continue;

            var bucket = BucketFor(completion.ProjectId, fixedPrice.Currency);
            bucket.Value += fixedPrice;
            bucket.Consider(completion.CompletedOn);
        }

        return
        [
            .. buckets
                .Select(kvp => new WorkInProgressRow(
                    kvp.Key.ProjectId,
                    projectDisplayNames.GetValueOrDefault(kvp.Key.ProjectId, $"Unknown project ({kvp.Key.ProjectId:N})"),
                    kvp.Value.Value,
                    Math.Max(0, asOf.DayNumber - kvp.Value.OldestDate!.Value.DayNumber)))
                .OrderBy(r => r.ProjectName, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Days sales outstanding: the mean, across every invoice request in
    /// <c>Sent</c> or later with a known issued date, of (paid date, or
    /// <paramref name="asOf"/> if unpaid) minus issued date — never scoped
    /// to a period (see this class's own remarks); "unavailable", never
    /// zero, when no such request exists.
    /// </summary>
    public static DaysSalesOutstandingResult DaysSalesOutstanding(IReadOnlyList<InvoiceRequestFacts> invoices, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        var days = new List<int>();

        foreach (var invoice in invoices)
        {
            if (invoice.IssuedDate is not { } issued)
                continue;

            if (invoice.Status is not (InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted or InvoiceRequestStatus.Voided))
                continue;

            var settled = invoice.PaidDate ?? asOf;
            days.Add(settled.DayNumber - issued.DayNumber);
        }

        return days.Count == 0
            ? new DaysSalesOutstandingResult(IsAvailable: false, AverageDays: null, InvoiceCount: 0)
            : new DaysSalesOutstandingResult(IsAvailable: true, AverageDays: (decimal)days.Sum() / days.Count, InvoiceCount: days.Count);
    }

    /// <summary>Calc throughput: Evidence records reaching Issued in <paramref name="period"/>, by their own issue date.</summary>
    public static int CalcThroughput(IReadOnlyList<EvidenceIssueFacts> issues, KpiPeriod period)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(period);

        return issues.Count(i => period.Contains(i.IssueDate));
    }

    private sealed class MarginAccumulator(CurrencyCode currency)
    {
        public CurrencyCode Currency { get; } = currency;

        public Money Revenue { get; set; } = Money.Zero(currency);

        public Money Cost { get; set; } = Money.Zero(currency);

        public bool AnyMissingCostRate { get; set; }
    }

    private sealed class WipAccumulator(CurrencyCode currency)
    {
        public Money Value { get; set; } = Money.Zero(currency);

        public DateOnly? OldestDate { get; private set; }

        public void Consider(DateOnly date)
        {
            if (OldestDate is null || date < OldestDate)
                OldestDate = date;
        }
    }
}
