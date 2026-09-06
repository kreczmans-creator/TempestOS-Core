using System.Text.Json;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.CommercialIntelligence;
using Tempest.Core.CommercialIntelligence.Costs;
using Tempest.Core.CommercialIntelligence.LeadTimes;
using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.ReferenceData;
using Xunit;

namespace Tempest.Core.Tests.CommercialIntelligence;

/// <summary>`D1` / `WP03.1` — knowing who a supplier is.</summary>
public sealed class SupplierTests
{
    [Fact]
    public async Task A_supplier_record_round_trips_through_the_catalogue()
    {
        var catalog = CommercialFixtures.BuildSupplierCatalog();

        await CommercialFixtures.RegisterAsync(catalog, "sup-1", CommercialFixtures.Supplier());

        var found = await catalog.FindByReferenceAsync("SUP-1");

        Assert.NotNull(found);
        Assert.Equal("Notional Machining Ltd", found.Definition.Identity.LegalName);
        Assert.Equal(CommercialFixtures.Gbp, found.Definition.TradingCurrency);
    }

    [Fact]
    public async Task A_released_supplier_record_cannot_be_revised_in_place()
    {
        var catalog = CommercialFixtures.BuildSupplierCatalog();

        await CommercialFixtures.RegisterReleasedAsync(catalog, "sup-1", CommercialFixtures.Supplier());

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(() =>
            catalog.ReviseAsync("sup-1", CommercialFixtures.Supplier(name: "Renamed Ltd"), CommercialFixtures.Verified(), "Rename."));
    }

    [Fact]
    public void An_identity_answers_to_its_aliases_but_not_to_a_stranger()
    {
        var identity = CommercialFixtures.Supplier().Identity with
        {
            Aliases = [new SupplierAlias("Notional Machining")],
        };

        Assert.True(identity.AnswersTo("Notional Machining Ltd"));
        Assert.True(identity.AnswersTo("notional machining"));
        Assert.False(identity.AnswersTo("Fictional Castings Ltd"));
    }

    [Fact]
    public void An_identity_with_no_registration_number_carries_no_hard_identifier()
    {
        var identity = CommercialFixtures.Supplier().Identity with { RegistrationNumber = null };

        Assert.False(identity.HasHardIdentifier);
    }

    [Fact]
    public void A_supplier_record_describes_and_never_qualifies()
    {
        var record = CommercialFixtures.Supplier();

        // Capability assurance is a record of what somebody established,
        // not a status TempestOS confers.
        Assert.Empty(record.Capabilities);
        Assert.Equal(SupplierStatus.Active, record.Status);
    }
}

/// <summary>`D2` / `WP03.2` and `D3` / `WP03.3` — what things cost and how long they take.</summary>
public sealed class CostAndLeadTimeTests
{
    [Fact]
    public async Task A_cost_record_round_trips_through_the_catalogue_with_its_money_intact()
    {
        var catalog = CommercialFixtures.BuildCostCatalog();

        await CommercialFixtures.RegisterAsync(catalog, "cost-1", CommercialFixtures.Cost());

        var found = await catalog.FindByReferenceAsync("COST-1");

        Assert.NotNull(found);
        Assert.Equal(CommercialFixtures.Gbp_(12.50m), found.Definition.Cost.Lowest!.Value);
        Assert.Equal(CostCertainty.Quoted, found.Definition.Cost.Certainty);
    }

    [Fact]
    public void A_cost_figure_round_trips_through_JSON()
    {
        // The defect this test exists for: CostFigure's constructor is
        // private, so without an explicit annotation the serialiser has
        // nothing to call and every persisted cost fails to load.
        var figure = CostFigure.Range(CommercialFixtures.Gbp_(10m), CommercialFixtures.Gbp_(15m));

        var restored = JsonSerializer.Deserialize<CostFigure>(JsonSerializer.Serialize(figure))!;

        Assert.Equal(figure, restored);
        Assert.Equal(CostCertainty.Ranged, restored.Certainty);
        Assert.Equal(CommercialFixtures.Gbp_(15m), restored.Highest!.Value);
    }

    [Fact]
    public void An_unknown_cost_swallows_a_known_one_rather_than_understating_the_total()
    {
        var sum = CostFigure.Quoted(CommercialFixtures.Gbp_(100m)) + CostFigure.Unknown;

        Assert.True(sum.IsUnknown);
    }

    [Fact]
    public void Summing_no_figures_gives_zero_rather_than_unknown()
    {
        var sum = CostFigure.Sum([], CommercialFixtures.Gbp);

        Assert.False(sum.IsUnknown);
        Assert.Equal(CommercialFixtures.Gbp_(0m), sum.Lowest!.Value);
    }

    [Fact]
    public void Adding_two_currencies_throws_rather_than_converting()
    {
        Assert.Throws<CurrencyMismatchException>(() =>
            CostFigure.Quoted(CommercialFixtures.Gbp_(10m)) + CostFigure.Quoted(new Money(10m, CommercialFixtures.Eur)));
    }

    [Fact]
    public async Task A_cost_record_applies_only_where_its_applicability_says_it_does()
    {
        var catalog = CommercialFixtures.BuildCostCatalog();
        await CommercialFixtures.RegisterReleasedAsync(catalog, "cost-1", CommercialFixtures.Cost());

        Assert.Single(await catalog.FindApplicableAsync(CommercialFixtures.Enquiry()));
        Assert.Empty(await catalog.FindApplicableAsync(CommercialFixtures.Enquiry(processRecordId: "proc-milling")));
        Assert.Empty(await catalog.FindApplicableAsync(CommercialFixtures.Enquiry() with { AsAt = CommercialFixtures.Today.AddYears(5) }));
    }

    [Fact]
    public void A_working_day_is_never_converted_into_elapsed_time()
    {
        Assert.Null(LeadTimeDuration.WorkingDays(5).ToElapsed());
        Assert.NotNull(LeadTimeDuration.Weeks(1).ToElapsed());

        Assert.False(LeadTimeDuration.WorkingDays(5).IsComparableWith(LeadTimeDuration.Weeks(1)));
        Assert.Throws<ArgumentException>(() =>
            LeadTimeDuration.WorkingDays(5).CompareTo(LeadTimeDuration.Weeks(1)));
    }

    [Fact]
    public void Calendar_units_compare_freely_with_each_other()
    {
        Assert.True(LeadTimeDuration.Weeks(1).IsComparableWith(LeadTimeDuration.CalendarDays(7)));
        Assert.Equal(0, LeadTimeDuration.Weeks(1).CompareTo(LeadTimeDuration.CalendarDays(7)));
    }

    [Fact]
    public async Task Applicable_lead_times_come_back_strongest_claim_first()
    {
        var catalog = CommercialFixtures.BuildLeadTimeCatalog();

        await CommercialFixtures.RegisterReleasedAsync(catalog, "lt-est", CommercialFixtures.LeadTime("LT-EST", kind: LeadTimeKind.Estimated));
        await CommercialFixtures.RegisterReleasedAsync(catalog, "lt-com", CommercialFixtures.LeadTime("LT-COM", kind: LeadTimeKind.Committed));
        await CommercialFixtures.RegisterReleasedAsync(catalog, "lt-typ", CommercialFixtures.LeadTime("LT-TYP", kind: LeadTimeKind.Typical));

        var applicable = await catalog.FindApplicableAsync(CommercialFixtures.Enquiry());

        Assert.Equal(LeadTimeKind.Committed, applicable[0].Definition.Kind);
        Assert.Equal(LeadTimeKind.Estimated, applicable[^1].Definition.Kind);
    }

    [Fact]
    public void Lead_time_performance_is_only_measurable_where_the_units_agree()
    {
        var comparable = new LeadTimePerformance("SUP-1", LeadTimeDuration.Weeks(3), LeadTimeDuration.Weeks(4), LeadTimeKind.Committed);
        var incomparable = new LeadTimePerformance("SUP-1", LeadTimeDuration.Weeks(3), LeadTimeDuration.WorkingDays(20), LeadTimeKind.Committed);

        Assert.True(comparable.WasLate);
        Assert.True(comparable.WasBinding);
        Assert.Null(incomparable.Overrun);
        Assert.Null(incomparable.WasLate);
    }

    [Fact]
    public void Commercial_quality_is_only_decision_grade_when_verified()
    {
        Assert.True(CommercialQualities.IsDecisionGrade(CommercialQuality.Verified));
        Assert.False(CommercialQualities.IsDecisionGrade(CommercialQuality.Unverified));
        Assert.False(CommercialQualities.IsDecisionGrade(CommercialQuality.Stale));
    }

    [Fact]
    public void The_weakest_quality_in_a_set_governs_and_an_empty_set_is_incomplete()
    {
        Assert.Equal(
            CommercialQuality.Stale,
            CommercialQualities.Weakest([CommercialQuality.Verified, CommercialQuality.Stale]));

        Assert.Equal(CommercialQuality.Incomplete, CommercialQualities.Weakest([]));
    }

    [Fact]
    public void An_unstated_geographic_scope_covers_nothing_rather_than_everything()
    {
        Assert.False(GeographicScope.Unstated.Covers("GB"));
        Assert.True(new GeographicScope("GB").Covers("gb"));
    }

    [Fact]
    public void An_undated_source_reads_as_older_than_any_threshold()
    {
        Assert.True(CommercialSource.Unrecorded.IsOlderThan(CommercialFixtures.Today, 30));
        Assert.False(CommercialFixtures.Source(CommercialFixtures.Today).IsOlderThan(CommercialFixtures.Today, 30));
    }
}
