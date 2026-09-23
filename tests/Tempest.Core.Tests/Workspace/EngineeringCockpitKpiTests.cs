using Tempest.Workspace;
using Tempest.Workspace.Kpi;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Projects;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// <see cref="EngineeringCockpit"/>'s own five `WP 19.1B` KPI card
/// properties (`ADR-0150`), the selected-period surface
/// (<see cref="EngineeringCockpit.SelectedKpiPeriod"/>/<see cref="EngineeringCockpit.SetKpiPeriodAsync"/>),
/// and its persistence — proved against a real, running
/// <see cref="WorkspaceManager"/> (the identical wiring `WorkspaceManager.StartAsync`
/// gives every production caller), never a hand-built <see cref="EngineeringCockpit"/>.
/// </summary>
public sealed class EngineeringCockpitKpiTests : IAsyncLifetime
{
    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();
        (_host, _manager) = await ProjectCommercialTestHost.StartAsync(_temp.Path);
        ProjectCommercialTestHost.SignIn(_host);
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private EngineeringCockpit Cockpit => _manager.Current!.Cockpit;

    private async Task<Guid> SetUpPricedProjectAsync()
    {
        var projectId = await ProjectCommercialTestHost.CreateProjectAsync(_host);

        var rateCards = ProjectCommercialTestHost.RateCards(_host);
        var card = Tests.BusinessGovernance.BusinessGovernanceFixtures.Card("KPI-COCKPIT-CARD") with
        {
            Entries =
            [
                new RateCardEntry(
                    "ENG-1", "Senior engineering", PricingBasis.Hourly,
                    new Money(120m, CurrencyCode.Gbp), new Money(70m, CurrencyCode.Gbp), Grade: "Senior"),
            ],
        };
        await rateCards.RegisterAsync("KPI-COCKPIT-CARD", card, Tests.BusinessGovernance.BusinessGovernanceFixtures.Verified());
        await Tests.BusinessGovernance.BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, "KPI-COCKPIT-CARD");

        var commercial = ProjectCommercialTestHost.ProjectCommercial(_host);
        await commercial.PinRateCardAsync(projectId, "KPI-COCKPIT-CARD");

        return projectId;
    }

    // ----------------------------------------------------------------
    // The selected period
    // ----------------------------------------------------------------

    [Fact]
    public void SelectedKpiPeriod_BeforePrimeAsyncEverRuns_DefaultsToThisWeek()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(KpiPeriod.ThisWeek(today), Cockpit.SelectedKpiPeriod);
    }

    [Fact]
    public async Task SetKpiPeriodAsync_TakesEffect_OnTheNextPrimeAsyncCall()
    {
        var custom = KpiPeriod.Custom(new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 7));
        await Cockpit.SetKpiPeriodAsync(custom);

        Assert.Equal(custom, Cockpit.SelectedKpiPeriod);

        await Cockpit.PrimeAsync();
        Assert.Equal(custom, Cockpit.SelectedKpiPeriod); // a load never silently reverts a caller's own choice
    }

    [Fact]
    public async Task SetKpiPeriodAsync_PersistsAcrossARestart_ASecondWorkspaceManagerOverTheSameStoreReadsItBack()
    {
        using var temp = new TempDirectory();
        var (host1, manager1) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        ProjectCommercialTestHost.SignIn(host1);

        var chosen = KpiPeriod.ThisMonth(DateOnly.FromDateTime(DateTime.UtcNow));
        await manager1.Current!.Cockpit.SetKpiPeriodAsync(chosen);

        await manager1.ShutdownAsync();
        await host1.DisposeAsync();

        var (host2, manager2) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        try
        {
            ProjectCommercialTestHost.SignIn(host2);
            var cockpit2 = manager2.Current!.Cockpit;
            await cockpit2.PrimeAsync(); // the first load reads `Cockpit.KpiPeriod` back from the durable store

            Assert.Equal(chosen, cockpit2.SelectedKpiPeriod);
        }
        finally
        {
            await manager2.ShutdownAsync();
            await host2.DisposeAsync();
        }
    }

    // ----------------------------------------------------------------
    // Honest empty state — every card, before any commercial data exists.
    // ----------------------------------------------------------------

    [Fact]
    public async Task NoCommercialDataAtAll_EveryKpiCard_ReportsItsOwnHonestEmptyState()
    {
        await Cockpit.PrimeAsync();

        var utilisation = Assert.Single(Cockpit.UtilisationKpiCards);
        Assert.False(utilisation.IsPlaceholder);
        Assert.Contains("no time recorded", utilisation.Value, StringComparison.OrdinalIgnoreCase);

        var margin = Assert.Single(Cockpit.MarginKpiCards);
        Assert.Contains("no time or deliverables", margin.Value, StringComparison.OrdinalIgnoreCase);

        var wip = Assert.Single(Cockpit.WorkInProgressKpiCards);
        Assert.Contains("no unbilled work", wip.Value, StringComparison.OrdinalIgnoreCase);

        var dso = Assert.Single(Cockpit.DaysSalesOutstandingKpiCards);
        Assert.Equal("Days sales outstanding", dso.Label);
        Assert.Contains("unavailable", dso.Value, StringComparison.OrdinalIgnoreCase);

        var throughput = Assert.Single(Cockpit.CalcThroughputKpiCards);
        Assert.Equal("Calc throughput", throughput.Label);
        Assert.StartsWith("0 ", throughput.Value, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Real data — all five cards agree on the same fixture.
    // ----------------------------------------------------------------

    [Fact]
    public async Task WithRealTimeAndADeliverable_TheFiveKpiCards_ReportRealComputedNumbers()
    {
        var projectId = await SetUpPricedProjectAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Cockpit.SetKpiPeriodAsync(KpiPeriod.Custom(today, today));

        var timesheets = ProjectCommercialTestHost.Timesheets(_host);
        var recorded = await timesheets.RecordAsync(projectId, today, 4m, billable: true, "Senior", "Cockpit KPI test work");
        Assert.True(recorded.Succeeded, recorded.Reason);

        await Cockpit.PrimeAsync();

        // Utilisation: 4h billable recorded today, the only day in this period.
        var utilisation = Assert.Single(Cockpit.UtilisationKpiCards);
        Assert.Contains("4h billable", utilisation.Value, StringComparison.Ordinal);

        // Margin: revenue = 4h × £120 = £480; cost = 4h × £70 = £280; margin = £200,
        // which is 200 / 480 × 100 = 41.666...% → 42%, rounded to the nearest point.
        var margin = Assert.Single(Cockpit.MarginKpiCards);
        Assert.Contains("200.00", margin.Value, StringComparison.Ordinal);
        Assert.Contains("42%", margin.Value, StringComparison.Ordinal);
        Assert.Equal(42, margin.PercentValue);

        // Work in progress: the same 4h × £120 = £480, unbilled, recorded today
        // (never period-scoped) — age 0 days.
        var wip = Assert.Single(Cockpit.WorkInProgressKpiCards);
        Assert.Contains("480.00", wip.Value, StringComparison.Ordinal);
        Assert.Contains("0 day(s)", wip.Value, StringComparison.Ordinal);

        // No InvoiceRequest or issued Evidence exists — these two stay honestly empty.
        Assert.Contains("unavailable", Assert.Single(Cockpit.DaysSalesOutstandingKpiCards).Value, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("0 ", Assert.Single(Cockpit.CalcThroughputKpiCards).Value, StringComparison.Ordinal);
    }
}
