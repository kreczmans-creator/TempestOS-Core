using System.Text.Json;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.CommercialIntelligence;
using Tempest.Core.CommercialIntelligence.Costs;
using Tempest.Core.CommercialIntelligence.Estimating;
using Tempest.Core.CommercialIntelligence.LeadTimes;
using Tempest.Core.ReferenceData;
using Xunit;

namespace Tempest.Core.Tests.CommercialIntelligence;

/// <summary>`D4` / `WP03.4` — the four things that must not be conflated.</summary>
public sealed class EstimatingTests
{
    [Fact]
    public void An_estimate_with_an_unpriced_line_has_no_total_rather_than_a_low_one()
    {
        var estimate = CommercialFixtures.Estimate() with
        {
            Lines =
            [
                new EstimateLine("L1", EstimateLineKind.Process, "Turning.", 10m, CostFigure.Quoted(CommercialFixtures.Gbp_(12.50m))),
                new EstimateLine("L2", EstimateLineKind.Tooling, "Fixture nobody has priced.", 1m, CostFigure.Unknown),
            ],
        };

        Assert.True(estimate.Total.IsUnknown);
        Assert.Single(estimate.UnpricedLines);
        Assert.False(estimate.IsPriced);
    }

    [Fact]
    public void An_estimate_totals_exactly_in_decimal()
    {
        var estimate = CommercialFixtures.Estimate() with
        {
            Quantity = 3,
            Lines = [new EstimateLine("L1", EstimateLineKind.Material, "Bar.", 3m, CostFigure.Quoted(CommercialFixtures.Gbp_(0.10m)))],
        };

        Assert.Equal(0.30m, estimate.Total.Lowest!.Value.Amount);
        Assert.Equal(0.10m, estimate.PerUnit.Lowest!.Value.Amount);
    }

    [Fact]
    public void A_line_pins_the_revision_it_was_derived_from()
    {
        var estimate = CommercialFixtures.Estimate();

        Assert.True(estimate.IsFullyTraceable);
        Assert.Contains(estimate.AllPins, p => p is { Library: "CommercialProcessCosts", RecordId: "cost-1", RevisionNumber: 1 });
    }

    [Fact]
    public void An_estimate_built_only_from_judgement_reports_every_line_untraceable()
    {
        var estimate = CommercialFixtures.Estimate() with
        {
            Lines = [new EstimateLine("L1", EstimateLineKind.Labour, "A day of somebody's time.", 1m, CostFigure.Estimated(CommercialFixtures.Gbp_(500m)))],
        };

        Assert.False(estimate.IsFullyTraceable);
        Assert.Single(estimate.UntraceableLines);
    }

    [Fact]
    public void The_longest_lead_time_is_unknown_where_the_units_cannot_be_compared()
    {
        var estimate = CommercialFixtures.Estimate() with
        {
            Lines =
            [
                new EstimateLine("L1", EstimateLineKind.Process, "Turning.", 1m, CostFigure.Quoted(CommercialFixtures.Gbp_(10m)), LeadTime: LeadTimeDuration.Weeks(3)),
                new EstimateLine("L2", EstimateLineKind.Inspection, "Inspection.", 1m, CostFigure.Quoted(CommercialFixtures.Gbp_(10m)), LeadTime: LeadTimeDuration.WorkingDays(5)),
            ],
        };

        // Five working days is not five sevenths of a week, and TempestOS
        // will not assume a shift pattern to make the two comparable.
        Assert.Null(estimate.LongestLeadTime);
    }

    [Fact]
    public void An_estimate_round_trips_through_JSON_with_its_money_intact()
    {
        // The P07 defect, guarded at the D4 boundary: money that
        // round-tripped to "0.00 (unspecified)" would silently turn every
        // historical estimate into a free one.
        var estimate = CommercialFixtures.Estimate();

        var restored = JsonSerializer.Deserialize<CostEstimate>(JsonSerializer.Serialize(estimate))!;

        Assert.Equal(estimate.Currency, restored.Currency);
        Assert.Equal(estimate.Total.Lowest!.Value.Amount, restored.Total.Lowest!.Value.Amount);
        Assert.Equal(estimate.Total.Lowest!.Value.Currency, restored.Total.Lowest!.Value.Currency);
        Assert.Equal(estimate.AllPins, restored.AllPins);
    }

    [Fact]
    public void A_supplier_quote_round_trips_through_JSON_with_its_money_intact()
    {
        var quote = CommercialFixtures.Quote();

        var restored = JsonSerializer.Deserialize<SupplierQuote>(JsonSerializer.Serialize(quote))!;

        Assert.Equal(quote.Total, restored.Total);
        Assert.Equal(CommercialFixtures.Gbp_(125.00m), restored.Total);
    }

    [Fact]
    public void A_customer_quotation_round_trips_through_JSON_with_its_money_intact()
    {
        var quotation = CommercialFixtures.Quotation();

        var restored = JsonSerializer.Deserialize<CustomerQuotation>(JsonSerializer.Serialize(quotation))!;

        Assert.Equal(quotation.Total, restored.Total);
        Assert.Equal(quotation.EstimatePin, restored.EstimatePin);
    }

    [Fact]
    public void A_quote_binds_the_supplier_only_while_it_is_firm_and_current()
    {
        var quote = CommercialFixtures.Quote(validTo: CommercialFixtures.Today.AddDays(10));

        Assert.True(quote.IsBindingAt(CommercialFixtures.Today));
        Assert.False(quote.IsBindingAt(CommercialFixtures.Today.AddDays(11)));
        Assert.False((quote with { Firmness = QuoteFirmness.Indicative }).IsBindingAt(CommercialFixtures.Today));
        Assert.False((quote with { Validity = null }).IsBindingAt(CommercialFixtures.Today));
    }

    [Fact]
    public void A_quote_in_a_second_currency_cannot_be_added_to_the_first()
    {
        var quote = CommercialFixtures.Quote() with
        {
            Lines = [new SupplierQuoteLine("1", "Turning.", 1m, new Money(10m, CommercialFixtures.Eur))],
        };

        Assert.Throws<CurrencyMismatchException>(() => quote.Total);
    }

    [Fact]
    public async Task An_issued_quotation_naming_nobody_who_issued_it_is_an_error()
    {
        var catalog = CommercialFixtures.BuildQuotationCatalog();
        var service = new CustomerQuotationValidationService(catalog, timeProvider: CommercialFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            CommercialFixtures.Quotation(status: QuotationStatus.Issued) with { IssuedOn = CommercialFixtures.Today },
            CommercialFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == EstimatingValidationRules.IssuedQuotationNeedsAuthority);
    }

    [Fact]
    public async Task A_quotation_issued_under_a_named_authority_raises_no_authority_error()
    {
        var catalog = CommercialFixtures.BuildQuotationCatalog();
        var service = new CustomerQuotationValidationService(catalog, timeProvider: CommercialFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            CommercialFixtures.Quotation(status: QuotationStatus.Issued) with
            {
                IssuedOn = CommercialFixtures.Today,
                IssuedUnderAuthority = CommercialFixtures.Authority(),
            },
            CommercialFixtures.Verified());

        Assert.DoesNotContain(result.Errors, e => e.Code == EstimatingValidationRules.IssuedQuotationNeedsAuthority);
    }

    [Fact]
    public void A_quotation_at_or_below_the_estimated_cost_yields_no_positive_margin()
    {
        var quotation = CommercialFixtures.Quotation(unitPrice: 10.00m);
        var cost = CostFigure.Quoted(CommercialFixtures.Gbp_(125.00m));

        Assert.True(quotation.MarginOver(cost) < 0m);
    }

    [Fact]
    public void A_margin_against_an_unpriced_estimate_is_not_a_number()
    {
        Assert.Null(CommercialFixtures.Quotation().MarginOver(CostFigure.Unknown));
    }

    [Fact]
    public void A_realised_outcome_measures_the_estimate_it_was_recorded_against()
    {
        var outcome = new RealisedOutcome(CommercialFixtures.Gbp_(150m), CommercialFixtures.Today);

        Assert.Equal(0.20m, outcome.VarianceFrom(CostFigure.Quoted(CommercialFixtures.Gbp_(125m))));
        Assert.Null(outcome.VarianceFrom(CostFigure.Quoted(new Money(125m, CommercialFixtures.Eur))));
        Assert.False(outcome.IsEvidenced);
    }

    [Fact]
    public async Task An_estimate_pinned_to_a_superseded_cost_is_warned_about_but_never_altered()
    {
        var costs = CommercialFixtures.BuildCostCatalog();

        var registered = await CommercialFixtures.RegisterReleasedAsync(costs, "cost-1", CommercialFixtures.Cost());

        var replacement = await CommercialFixtures.RegisterAsync(costs, "cost-2", CommercialFixtures.Cost("COST-2", 14.00m));
        await costs.SupersedeAsync(registered.Id, replacement.Id, "Price moved.");

        var estimates = CommercialFixtures.BuildEstimateCatalog();
        var estimate = CommercialFixtures.Estimate() with
        {
            Lines =
            [
                new EstimateLine(
                    "L1",
                    EstimateLineKind.Process,
                    "Turning.",
                    10m,
                    CostFigure.Quoted(CommercialFixtures.Gbp_(12.50m)),
                    SourcePins: [new ReferencePin(costs.LibraryName, registered.Id, 1)]),
            ],
        };

        var service = new CostEstimateValidationService(
            estimates,
            [new CatalogPinResolver<ProcessCostRecord>(costs)],
            CommercialFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(estimate, CommercialFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == EstimatingValidationRules.PinnedSourceSuperseded);

        // The estimate itself is untouched: superseding a source is news
        // about the library, not a correction to history.
        Assert.Equal(CommercialFixtures.Gbp_(125.00m), estimate.Total.Lowest!.Value);
    }

    [Fact]
    public async Task The_estimating_service_prices_from_released_records_and_pins_them()
    {
        var costs = CommercialFixtures.BuildCostCatalog();
        var registered = await CommercialFixtures.RegisterReleasedAsync(costs, "cost-1", CommercialFixtures.Cost());

        var leadTimes = CommercialFixtures.BuildLeadTimeCatalog();
        var lead = await CommercialFixtures.RegisterReleasedAsync(leadTimes, "lead-1", CommercialFixtures.LeadTime());

        var service = new EstimatingService(costs, leadTimes, CommercialFixtures.Clock());

        var result = await service.BuildAsync(new EstimateRequest(
            "EST-9",
            "Fictional turned parts.",
            CommercialFixtures.Gbp,
            [new EstimateRequestItem("L1", "Turning.", EstimateLineKind.Process, CommercialFixtures.Enquiry(), 10m)]));

        Assert.True(result.IsComplete);
        Assert.Equal(CommercialFixtures.Gbp_(125.00m), result.Estimate.Total.Lowest!.Value);
        // The pin carries the revision the record actually stood at when
        // it was read, which the walk to Released has already advanced.
        Assert.Contains(result.Estimate.AllPins, p => p.RecordId == registered.Id && p.RevisionNumber == registered.RevisionNumber);
        Assert.Contains(result.Estimate.AllPins, p => p.RecordId == lead.Id && p.RevisionNumber == lead.RevisionNumber);
    }

    [Fact]
    public async Task A_draft_cost_record_is_never_reached_for_a_price()
    {
        var costs = CommercialFixtures.BuildCostCatalog();
        await CommercialFixtures.RegisterAsync(costs, "cost-1", CommercialFixtures.Cost());

        var service = new EstimatingService(costs, timeProvider: CommercialFixtures.Clock());

        var result = await service.BuildAsync(new EstimateRequest(
            "EST-9",
            "Fictional turned parts.",
            CommercialFixtures.Gbp,
            [new EstimateRequestItem("L1", "Turning.", EstimateLineKind.Process, CommercialFixtures.Enquiry(), 10m)]));

        Assert.False(result.IsComplete);
        Assert.True(result.Estimate.Total.IsUnknown);
        Assert.Contains(result.Gaps, g => g.ItemReference == "L1");
    }

    [Fact]
    public async Task A_cost_in_another_currency_produces_a_gap_rather_than_a_conversion()
    {
        var costs = CommercialFixtures.BuildCostCatalog();
        await CommercialFixtures.RegisterReleasedAsync(costs, "cost-1", CommercialFixtures.Cost(currency: CommercialFixtures.Eur));

        var service = new EstimatingService(costs, timeProvider: CommercialFixtures.Clock());

        var result = await service.BuildAsync(new EstimateRequest(
            "EST-9",
            "Fictional turned parts.",
            CommercialFixtures.Gbp,
            [new EstimateRequestItem("L1", "Turning.", EstimateLineKind.Process, CommercialFixtures.Enquiry(), 10m)]));

        Assert.Contains(result.Gaps, g => g.Reason.Contains("does not convert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reproducing_an_estimate_reports_the_library_moving_and_leaves_the_estimate_alone()
    {
        var costs = CommercialFixtures.BuildCostCatalog();
        var registered = await CommercialFixtures.RegisterReleasedAsync(costs, "cost-1", CommercialFixtures.Cost());

        var service = new EstimatingService(costs, timeProvider: CommercialFixtures.Clock());

        var built = await service.BuildAsync(new EstimateRequest(
            "EST-9",
            "Fictional turned parts.",
            CommercialFixtures.Gbp,
            [new EstimateRequestItem("L1", "Turning.", EstimateLineKind.Process, CommercialFixtures.Enquiry(), 10m)]));

        Assert.True((await service.ReproduceAsync(built.Estimate)).Reproduces);

        var replacement = await CommercialFixtures.RegisterAsync(costs, "cost-2", CommercialFixtures.Cost("COST-2", 99.00m));
        await costs.SupersedeAsync(registered.Id, replacement.Id, "Price moved.");

        var reproduction = await service.ReproduceAsync(built.Estimate);

        Assert.False(reproduction.Reproduces);
        Assert.NotEmpty(reproduction.Divergences);

        // The historical estimate still says what it said.
        Assert.Equal(CommercialFixtures.Gbp_(125.00m), built.Estimate.Total.Lowest!.Value);
    }

    [Fact]
    public async Task The_estimating_service_is_deterministic()
    {
        var costs = CommercialFixtures.BuildCostCatalog();
        var registered = await CommercialFixtures.RegisterReleasedAsync(costs, "cost-1", CommercialFixtures.Cost());

        var service = new EstimatingService(costs, timeProvider: CommercialFixtures.Clock());
        var request = new EstimateRequest(
            "EST-9",
            "Fictional turned parts.",
            CommercialFixtures.Gbp,
            [new EstimateRequestItem("L1", "Turning.", EstimateLineKind.Process, CommercialFixtures.Enquiry(), 10m)]);

        var first = (await service.BuildAsync(request)).Estimate;
        var second = (await service.BuildAsync(request)).Estimate;

        // Compared by content rather than by record equality: the
        // generated lists are distinct instances, which is not a
        // difference anybody reading the estimate would see.
        Assert.Equal(first.Total, second.Total);
        Assert.Equal(first.AllPins, second.AllPins);
        Assert.Equal(
            first.Lines.Select(l => (l.Reference, l.UnitCost, l.Quantity)),
            second.Lines.Select(l => (l.Reference, l.UnitCost, l.Quantity)));
    }

    [Fact]
    public async Task An_estimate_and_a_supplier_quote_are_held_in_separate_libraries()
    {
        var estimates = CommercialFixtures.BuildEstimateCatalog();
        var quotes = CommercialFixtures.BuildQuoteCatalog();

        Assert.NotEqual(estimates.LibraryName, quotes.LibraryName);
        Assert.NotEqual(estimates.DocumentKind, quotes.DocumentKind);
        Assert.NotEqual(CustomerQuotationCatalog.QuotationDocumentKind, quotes.DocumentKind);
    }

    [Fact]
    public async Task A_line_citing_an_assumption_the_estimate_does_not_state_is_an_error()
    {
        var estimates = CommercialFixtures.BuildEstimateCatalog();
        var service = new CostEstimateValidationService(estimates, timeProvider: CommercialFixtures.Clock());

        var estimate = CommercialFixtures.Estimate() with
        {
            Lines =
            [
                new EstimateLine(
                    "L1",
                    EstimateLineKind.Process,
                    "Turning.",
                    1m,
                    CostFigure.Quoted(CommercialFixtures.Gbp_(10m)),
                    AssumptionReferences: ["A-NOT-STATED"]),
            ],
        };

        var result = await service.ValidateDefinitionAsync(estimate, CommercialFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == EstimatingValidationRules.AssumptionReferenceUnresolved);
    }

    [Fact]
    public void An_estimate_line_refuses_a_negative_quantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EstimateLine("L1", EstimateLineKind.Material, "Bar.", -1m, CostFigure.Unknown));
    }

    [Fact]
    public void An_assumption_must_say_what_is_being_assumed()
    {
        Assert.Throws<ArgumentException>(() => new EstimateAssumption("A1", "   "));
    }
}
