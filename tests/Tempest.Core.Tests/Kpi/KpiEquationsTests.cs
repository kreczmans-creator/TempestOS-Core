using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Workspace.Kpi;

namespace Tempest.Core.Tests.Kpi;

/// <summary>
/// <see cref="KpiEquations"/> (`WP 19.1B`, `ADR-0150`): each of the five
/// equations over hand-authored fixtures, with the arithmetic stated in
/// this file's own comments, plus an empty-data case per equation.
/// </summary>
public sealed class KpiEquationsTests
{
    private static readonly CurrencyCode Gbp = new("GBP");

    private static Money G(decimal amount) => new(amount, Gbp);

    // A fixed week: Monday 2026-03-09 to Sunday 2026-03-15.
    private static readonly KpiPeriod ThisWeek = KpiPeriod.Custom(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 15));

    // ----------------------------------------------------------------
    // 1. Utilisation = Σ billable hours ÷ Σ available hours, per principal.
    // ----------------------------------------------------------------

    [Fact]
    public void BillableHoursByPrincipal_SumsOnlyBillableHours_WithinThePeriod_PerPrincipal()
    {
        var entries = new[]
        {
            // alice, in period: 4h billable (Mon) + 3h billable (Wed) + 2h non-billable (Fri) = 7h billable.
            Entry("alice", new(2026, 3, 9), 4m, billable: true),
            Entry("alice", new(2026, 3, 11), 3m, billable: true),
            Entry("alice", new(2026, 3, 13), 2m, billable: false),
            // bob, in period: 5h billable (Tue) = 5h billable.
            Entry("bob", new(2026, 3, 10), 5m, billable: true),
            // carol, entirely outside the period — must not appear at all.
            Entry("carol", new(2026, 3, 1), 10m, billable: true),
        };

        var byPrincipal = KpiEquations.BillableHoursByPrincipal(entries, ThisWeek);

        Assert.Equal(2, byPrincipal.Count);
        Assert.Equal(7m, byPrincipal["alice"]); // 4 + 3 (the 2h non-billable Friday entry contributes 0)
        Assert.Equal(5m, byPrincipal["bob"]);
        Assert.False(byPrincipal.ContainsKey("carol"));
    }

    [Fact]
    public void BillableHoursByPrincipal_APrincipalWithOnlyNonBillableHoursInPeriod_StillAppears_AtZero()
    {
        var entries = new[] { Entry("dave", new(2026, 3, 9), 6m, billable: false) };

        var byPrincipal = KpiEquations.BillableHoursByPrincipal(entries, ThisWeek);

        Assert.Equal(0m, Assert.Contains("dave", byPrincipal)); // present, honestly at 0 billable hours
    }

    [Fact]
    public void BillableHoursByPrincipal_NoEntriesAtAll_IsEmpty() =>
        Assert.Empty(KpiEquations.BillableHoursByPrincipal([], ThisWeek));

    [Fact]
    public void AvailableHours_AWholeWeek_IsTheWorkingPatternsOwnWeeklyFigure()
    {
        // 37.5h/week × (7 days ÷ 7) = 37.5h exactly — a whole week is one week.
        Assert.Equal(37.5m, KpiEquations.AvailableHours(37.5m, ThisWeek));
    }

    [Fact]
    public void AvailableHours_APartialWeek_IsProRatedByDays_NeverByCalendarDaysAlone()
    {
        // Monday to Wednesday inclusive = 3 days. 37.5h/week × (3 ÷ 7) = 16.0714285714...h —
        // pro-rata by the day count, exactly `ADR-0150`'s own "never calendar days" caution
        // about the denominator, restated as a fraction of a week rather than a flat day rate.
        var threeDays = KpiPeriod.Custom(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 11));
        var expected = 37.5m * 3m / 7m;

        Assert.Equal(expected, KpiEquations.AvailableHours(37.5m, threeDays));
    }

    [Fact]
    public void UtilisationRow_Percent_IsBillableOverAvailable_RoundedToTheNearestWholePoint()
    {
        // alice: 7h billable ÷ 37.5h available × 100 = 18.666...% → rounds to 19%.
        var row = new UtilisationRow("alice", BillableHours: 7m, AvailableHours: 37.5m);
        Assert.Equal(19, row.Percent);
    }

    [Fact]
    public void UtilisationRow_Percent_ZeroAvailableHours_IsNull_NeverAFabricatedFigure()
    {
        var row = new UtilisationRow("alice", BillableHours: 5m, AvailableHours: 0m);
        Assert.Null(row.Percent);
    }

    // ----------------------------------------------------------------
    // 2. Margin per project = (Σ billable hours × billing rate + Σ fixed-price
    //    deliverable value) − Σ all hours × cost rate, over entries dated in
    //    the period.
    // ----------------------------------------------------------------

    [Fact]
    public void MarginByProject_CombinesBillableTimeFixedPriceDeliverablesAndAllHoursCost()
    {
        var project = Guid.NewGuid();
        var names = new Dictionary<Guid, string> { [project] = "Apollo" };

        var entries = new[]
        {
            // A: 4h billable @ £100/h billing, £60/h cost → revenue 400, cost 240.
            Entry(project, new(2026, 3, 9), 4m, billable: true, billingRate: 100m, costRate: 60m),
            // B: 2h NON-billable @ £60/h cost → revenue 0, cost 120 (non-billable time still costs).
            Entry(project, new(2026, 3, 10), 2m, billable: false, billingRate: 100m, costRate: 60m),
            // C: 3h billable @ £100/h billing, NO cost rate → revenue 300, cost 0, flags missing.
            Entry(project, new(2026, 3, 11), 3m, billable: true, billingRate: 100m, costRate: null),
            // D: dated outside the period — must not contribute at all.
            Entry(project, new(2026, 3, 1), 100m, billable: true, billingRate: 100m, costRate: 60m),
        };

        var completions = new[]
        {
            // A fixed-price deliverable completed in the period: revenue +500.
            Completion(project, new(2026, 3, 12), fixedPriceValue: 500m),
            // Outside the period — must not contribute.
            Completion(project, new(2026, 2, 1), fixedPriceValue: 999m),
        };

        var rows = KpiEquations.MarginByProject(entries, completions, names, ThisWeek);

        var row = Assert.Single(rows);
        Assert.Equal("Apollo", row.ProjectName);
        // Revenue = 400 (A) + 300 (C) + 500 (deliverable) = 1200.
        Assert.Equal(G(1200m), row.Revenue);
        // Cost = 240 (A) + 120 (B) + 0 (C, missing rate) = 360.
        Assert.Equal(G(360m), row.Cost);
        // Margin = 1200 - 360 = 840.
        Assert.Equal(G(840m), row.Margin);
        // MarginPercentOfRevenue = 840 / 1200 × 100 = 70%.
        Assert.Equal(70, row.MarginPercentOfRevenue);
        Assert.True(row.AnyMissingCostRate);
    }

    [Fact]
    public void MarginByProject_GroupsSeparately_PerProject_SortedByName()
    {
        var apollo = Guid.NewGuid();
        var boreas = Guid.NewGuid();
        var names = new Dictionary<Guid, string> { [apollo] = "Apollo", [boreas] = "Boreas" };

        var entries = new[]
        {
            Entry(boreas, new(2026, 3, 9), 1m, billable: true, billingRate: 50m, costRate: 20m),
            Entry(apollo, new(2026, 3, 9), 1m, billable: true, billingRate: 50m, costRate: 20m),
        };

        var rows = KpiEquations.MarginByProject(entries, [], names, ThisWeek);

        Assert.Equal(2, rows.Count);
        Assert.Equal(["Apollo", "Boreas"], rows.Select(r => r.ProjectName)); // alphabetical
    }

    [Fact]
    public void MarginByProject_NoEntriesOrCompletionsInThePeriod_IsEmpty() =>
        Assert.Empty(KpiEquations.MarginByProject([], [], new Dictionary<Guid, string>(), ThisWeek));

    // ----------------------------------------------------------------
    // 3. Work in progress = Σ billable value of entries with no InvoicedBy
    //    link, by project, with the age of the oldest such entry — never
    //    scoped to a period.
    // ----------------------------------------------------------------

    [Fact]
    public void WorkInProgressByProject_SumsUnbilledBillableValue_AndAgesFromTheOldestEntry()
    {
        var asOf = new DateOnly(2026, 2, 15);
        var project = Guid.NewGuid();
        var invoiced = Guid.NewGuid();
        var names = new Dictionary<Guid, string> { [project] = "Apollo" };

        var entries = new[]
        {
            // A: 2h billable @ £150/h, unbilled, dated 2026-01-01 (the oldest) → value 300.
            Entry(project, new(2026, 1, 1), 2m, billable: true, billingRate: 150m, costRate: null),
            // B: already invoiced — must not contribute.
            Entry(project, new(2026, 1, 5), 1m, billable: true, billingRate: 150m, costRate: null, invoicedBy: invoiced),
            // C: non-billable, unbilled — must not contribute (WIP is billable value only).
            Entry(project, new(2026, 1, 10), 5m, billable: false, billingRate: 150m, costRate: null),
            // D: 3h billable @ £150/h, unbilled, dated 2026-02-01 → value 450.
            Entry(project, new(2026, 2, 1), 3m, billable: true, billingRate: 150m, costRate: null),
        };

        var rows = KpiEquations.WorkInProgressByProject(entries, [], names, asOf);

        var row = Assert.Single(rows);
        Assert.Equal("Apollo", row.ProjectName);
        Assert.Equal(G(750m), row.Value); // 300 (A) + 450 (D)
        // Oldest unbilled entry is A, dated 2026-01-01; 2026-01-01 to 2026-02-15
        // is 31 (rest of January) + 14 (into February) = wait — January has 31
        // days, so 2026-01-01 + 31 days = 2026-02-01, + 14 more = 2026-02-15.
        Assert.Equal(45, row.OldestEntryAgeDays);
    }

    [Fact]
    public void WorkInProgressByProject_IncludesUnbilledFixedPriceDeliverables()
    {
        var asOf = new DateOnly(2026, 2, 15);
        var project = Guid.NewGuid();
        var names = new Dictionary<Guid, string> { [project] = "Boreas" };

        var completions = new[] { Completion(project, new(2026, 1, 15), fixedPriceValue: 1000m) };

        var rows = KpiEquations.WorkInProgressByProject([], completions, names, asOf);

        var row = Assert.Single(rows);
        Assert.Equal(G(1000m), row.Value);
        // 2026-01-15 to 2026-02-15 is exactly 31 days (January's own length).
        Assert.Equal(31, row.OldestEntryAgeDays);
    }

    [Fact]
    public void WorkInProgressByProject_NoUnbilledWorkAtAll_IsEmpty() =>
        Assert.Empty(KpiEquations.WorkInProgressByProject([], [], new Dictionary<Guid, string>(), new DateOnly(2026, 1, 1)));

    // ----------------------------------------------------------------
    // 4. Days sales outstanding = mean, over invoices Sent or later with a
    //    known issued date, of (paid date, or "as of" if unpaid) minus
    //    issued date — never scoped to a period.
    // ----------------------------------------------------------------

    [Fact]
    public void DaysSalesOutstanding_AveragesSettlementDays_AcrossSentAcceptedAndVoidedInvoices()
    {
        var asOf = new DateOnly(2026, 3, 1);

        var invoices = new[]
        {
            // Sent, paid: 2026-02-01 to 2026-02-11 = 10 days.
            new InvoiceRequestFacts(InvoiceRequestStatus.Sent, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 11)),
            // Accepted, unpaid: 2026-02-15 to "as of" 2026-03-01 = 14 days
            // (13 days to 2026-02-28, the last day of a non-leap February, + 1 more to 2026-03-01).
            new InvoiceRequestFacts(InvoiceRequestStatus.Accepted, new DateOnly(2026, 2, 15), null),
            // Voided, never paid: 2026-01-01 to "as of" 2026-03-01 = 59 days (31 + 28).
            new InvoiceRequestFacts(InvoiceRequestStatus.Voided, new DateOnly(2026, 1, 1), null),
            // Draft, no issued date at all — excluded.
            new InvoiceRequestFacts(InvoiceRequestStatus.Draft, null, null),
            // Rejected — never reached Sent — excluded even though it has an issued date.
            new InvoiceRequestFacts(InvoiceRequestStatus.Rejected, new DateOnly(2026, 2, 1), null),
        };

        var result = KpiEquations.DaysSalesOutstanding(invoices, asOf);

        Assert.True(result.IsAvailable);
        Assert.Equal(3, result.InvoiceCount);
        // (10 + 14 + 59) / 3 = 83 / 3 = 27.6666...
        Assert.Equal(83m / 3m, result.AverageDays);
    }

    [Fact]
    public void DaysSalesOutstanding_NoQualifyingInvoice_IsUnavailable_NeverZero()
    {
        var result = KpiEquations.DaysSalesOutstanding([], new DateOnly(2026, 1, 1));

        Assert.False(result.IsAvailable);
        Assert.Null(result.AverageDays);
        Assert.Equal(0, result.InvoiceCount);
    }

    [Fact]
    public void DaysSalesOutstanding_OnlyDraftsAndRejections_IsStillUnavailable()
    {
        var invoices = new[]
        {
            new InvoiceRequestFacts(InvoiceRequestStatus.Draft, null, null),
            new InvoiceRequestFacts(InvoiceRequestStatus.Rejected, new DateOnly(2026, 1, 1), null),
        };

        Assert.False(KpiEquations.DaysSalesOutstanding(invoices, new DateOnly(2026, 2, 1)).IsAvailable);
    }

    // ----------------------------------------------------------------
    // 5. Calc throughput = Evidence records reaching Issued in the period
    //    (their issue date).
    // ----------------------------------------------------------------

    [Fact]
    public void CalcThroughput_CountsIssuesWithinThePeriod_InclusiveAtBothEnds()
    {
        var march = KpiPeriod.Custom(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var issues = new[]
        {
            new EvidenceIssueFacts(new DateOnly(2026, 3, 5)), // inside
            new EvidenceIssueFacts(new DateOnly(2026, 3, 31)), // inside, boundary
            new EvidenceIssueFacts(new DateOnly(2026, 2, 28)), // before the period
            new EvidenceIssueFacts(new DateOnly(2026, 4, 1)), // after the period
        };

        Assert.Equal(2, KpiEquations.CalcThroughput(issues, march));
    }

    [Fact]
    public void CalcThroughput_NothingIssuedInThePeriod_IsHonestlyZero() =>
        Assert.Equal(0, KpiEquations.CalcThroughput([], ThisWeek));

    // ----------------------------------------------------------------
    // Fixtures
    // ----------------------------------------------------------------

    private static TimesheetEntryFacts Entry(
        string principalId, DateOnly date, decimal hours, bool billable,
        decimal billingRate = 100m, decimal? costRate = 50m, Guid? invoicedBy = null) =>
        new(principalId, Guid.NewGuid(), date, hours, billable, G(billingRate), costRate is { } c ? G(c) : null, invoicedBy);

    private static TimesheetEntryFacts Entry(
        Guid projectId, DateOnly date, decimal hours, bool billable,
        decimal billingRate, decimal? costRate, Guid? invoicedBy = null) =>
        new("principal-01", projectId, date, hours, billable, G(billingRate), costRate is { } c ? G(c) : null, invoicedBy);

    private static DeliverableCompletionFacts Completion(Guid projectId, DateOnly completedOn, decimal fixedPriceValue, Guid? invoicedBy = null) =>
        new(projectId, completedOn, G(fixedPriceValue), invoicedBy);
}
