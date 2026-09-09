using System.Reflection;
using System.Text.Json;
using Tempest.Core.CommercialIntelligence;
using Tempest.Core.CommercialIntelligence.Costs;
using Tempest.Core.CommercialIntelligence.Estimating;
using Tempest.Core.CommercialIntelligence.LeadTimes;
using Tempest.Core.CommercialIntelligence.Procurement;
using Tempest.Core.CommercialIntelligence.Suppliers;
using Xunit;

namespace Tempest.Core.Tests.CommercialIntelligence;

/// <summary>`D5` / `WP03.5` — comparing without deciding.</summary>
public sealed class ProcurementTests
{
    private static readonly SourcingComparisonService Service = new();

    [Fact]
    public void A_candidate_failing_a_mandatory_criterion_is_excluded_with_its_reason_attached()
    {
        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [
                CommercialFixtures.Candidate("A", "sup-1"),
                CommercialFixtures.Candidate("B", "sup-2", capability: CriterionStanding.Fails),
            ]);

        var excluded = Assert.Single(comparison.ExcludedCandidates);

        Assert.Equal("B", excluded.Code);
        Assert.Equal("CAP", excluded.Exclusion!.FailedCriterionCode);
        Assert.Contains("CAP", excluded.Exclusion.Reason);
        Assert.True(excluded.Exclusion.IsAutomatic);

        // Excluded, but still in the record. A comparison that dropped it
        // would leave nobody able to see it was ever considered.
        Assert.Equal(2, comparison.Candidates.Count);
    }

    [Fact]
    public void An_excluded_candidate_is_never_ranked()
    {
        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [
                CommercialFixtures.Candidate("A", "sup-1"),
                CommercialFixtures.Candidate("B", "sup-2", capability: CriterionStanding.Fails, cost: CriterionStanding.Exceeds),
            ]);

        Assert.DoesNotContain(comparison.Rankings, r => r.CandidateCode == "B");
    }

    [Fact]
    public void Unassessed_information_is_never_scored_as_zero()
    {
        var requirement = CommercialFixtures.Requirement();

        // Two candidates identical on everything anybody looked at. The
        // second simply has not been researched on quality.
        var researched = CommercialFixtures.Candidate("A", "sup-1", quality: CriterionStanding.Fails);
        var unresearched = CommercialFixtures.Candidate("B", "sup-2") with
        {
            Assessments =
            [
                CommercialFixtures.Assessed("CAP", CriterionStanding.Meets),
                CommercialFixtures.Assessed("COST", CriterionStanding.Meets),
                CommercialFixtures.Assessed("LEAD", CriterionStanding.Meets),
            ],
        };

        var comparison = Service.Compare("CMP-1", requirement, [researched, unresearched]);

        var b = comparison.Rankings.Single(r => r.CandidateCode == "B");

        // Scored on what is known, not punished for the gap — but the gap
        // is reported, and it caps the recommendation at Provisional.
        Assert.Equal(0.8m, b.Score);
        Assert.Contains("QUAL", b.MissingCriterionCodes);
        Assert.Equal(0.8m, b.EstablishedWeight);
        Assert.Equal(RecommendationStrength.Provisional, comparison.Strength);
        Assert.Contains(comparison.OutstandingQuestions, q => q.Contains("QUAL"));
    }

    [Fact]
    public void A_complete_comparison_with_a_clear_leader_says_so()
    {
        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [
                CommercialFixtures.Candidate("A", "sup-1", cost: CriterionStanding.Exceeds, lead: CriterionStanding.Exceeds, quality: CriterionStanding.Exceeds),
                CommercialFixtures.Candidate("B", "sup-2", cost: CriterionStanding.Marginal, lead: CriterionStanding.Marginal, quality: CriterionStanding.Marginal),
            ]);

        Assert.Equal("A", comparison.RecommendedCandidateCode);
        Assert.Equal(RecommendationStrength.Clear, comparison.Strength);
        Assert.Equal(1.0m, comparison.Rankings.Single(r => r.CandidateCode == "A").Score);
    }

    [Fact]
    public void A_narrow_lead_is_reported_as_marginal_rather_than_clear()
    {
        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [
                // Ahead only on the 0.3-weighted criterion: a six-point
                // lead, which is not a result anybody should act on
                // without looking at it.
                CommercialFixtures.Candidate("A", "sup-1", lead: CriterionStanding.Exceeds),
                CommercialFixtures.Candidate("B", "sup-2"),
            ]);

        Assert.Equal(RecommendationStrength.Marginal, comparison.Strength);
    }

    [Fact]
    public void A_comparison_that_established_nothing_recommends_nobody()
    {
        var bare = new SourcingCandidate { Code = "A", SupplierRecordId = "sup-1" };

        var comparison = Service.Compare("CMP-1", CommercialFixtures.Requirement(), [bare]);

        Assert.Null(comparison.RecommendedCandidateCode);
        Assert.Equal(RecommendationStrength.Insufficient, comparison.Strength);
        Assert.Contains("recommends nobody", comparison.RecommendationRationale);
    }

    [Fact]
    public void A_not_applicable_criterion_neither_helps_nor_harms()
    {
        var candidate = CommercialFixtures.Candidate("A", "sup-1", quality: CriterionStanding.NotApplicable);

        var ranking = Service.Compare("CMP-1", CommercialFixtures.Requirement(), [candidate])
            .Rankings.Single();

        Assert.Equal(0.8m, ranking.Score);
        Assert.DoesNotContain("QUAL", ranking.MissingCriterionCodes);
    }

    [Fact]
    public void The_comparison_is_deterministic_including_its_tie_breaking()
    {
        var requirement = CommercialFixtures.Requirement();
        var candidates = new[]
        {
            CommercialFixtures.Candidate("B", "sup-2"),
            CommercialFixtures.Candidate("A", "sup-1"),
        };

        var first = Service.Compare("CMP-1", requirement, candidates);
        var second = Service.Compare("CMP-1", requirement, candidates);

        Assert.Equal(
            first.Rankings.Select(r => r.CandidateCode),
            second.Rankings.Select(r => r.CandidateCode));

        // Tied on score, so ordinal order of the code decides — never
        // input order, which would make the result depend on how the
        // caller happened to build the list.
        Assert.Equal(["A", "B"], first.Rankings.Select(r => r.CandidateCode));
    }

    [Fact]
    public void Every_comparison_requires_a_human_decision()
    {
        var comparison = Service.Compare("CMP-1", CommercialFixtures.Requirement(), [CommercialFixtures.Candidate("A", "sup-1")]);

        Assert.True(comparison.RequiresHumanDecision);
        Assert.Equal(SourcingDecisionState.AwaitingHumanDecision, comparison.DecisionState);
        Assert.False(comparison.HasBeenDecided);
        Assert.Contains("A person must decide", comparison.RecommendationRationale);
    }

    [Fact]
    public void Choosing_against_the_recommendation_is_a_first_class_outcome()
    {
        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [
                CommercialFixtures.Candidate("A", "sup-1", cost: CriterionStanding.Exceeds),
                CommercialFixtures.Candidate("B", "sup-2"),
            ]) with
        {
            DecisionState = SourcingDecisionState.AlternativeChosen,
            ChosenCandidateCode = "B",
            DecisionRationale = "Fictional reason: existing tooling sits with B.",
            DecidedBy = CommercialFixtures.Authority(),
        };

        Assert.True(comparison.HasBeenDecided);
        Assert.True(comparison.DepartsFromRecommendation);
    }

    [Fact]
    public async Task A_decision_naming_nobody_who_took_it_is_an_error()
    {
        var catalog = CommercialFixtures.BuildComparisonCatalog();
        var service = new SourcingComparisonValidationService(catalog);

        var comparison = Service.Compare("CMP-1", CommercialFixtures.Requirement(), [CommercialFixtures.Candidate("A", "sup-1")]) with
        {
            DecisionState = SourcingDecisionState.RecommendationAccepted,
            ChosenCandidateCode = "A",
        };

        var result = await service.ValidateDefinitionAsync(comparison, CommercialFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == ProcurementValidationRules.DecisionNeedsAuthority);
    }

    [Fact]
    public async Task A_decision_under_a_named_authority_raises_no_authority_error()
    {
        var catalog = CommercialFixtures.BuildComparisonCatalog();
        var service = new SourcingComparisonValidationService(catalog);

        var comparison = Service.Compare("CMP-1", CommercialFixtures.Requirement(), [CommercialFixtures.Candidate("A", "sup-1")]) with
        {
            DecisionState = SourcingDecisionState.RecommendationAccepted,
            ChosenCandidateCode = "A",
            DecidedBy = CommercialFixtures.Authority(),
        };

        var result = await service.ValidateDefinitionAsync(comparison, CommercialFixtures.Verified());

        Assert.DoesNotContain(result.Errors, e => e.Code == ProcurementValidationRules.DecisionNeedsAuthority);
    }

    [Fact]
    public async Task Departing_from_the_recommendation_without_a_reason_is_warned_about()
    {
        var catalog = CommercialFixtures.BuildComparisonCatalog();
        var service = new SourcingComparisonValidationService(catalog);

        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [
                CommercialFixtures.Candidate("A", "sup-1", cost: CriterionStanding.Exceeds),
                CommercialFixtures.Candidate("B", "sup-2"),
            ]) with
        {
            DecisionState = SourcingDecisionState.AlternativeChosen,
            ChosenCandidateCode = "B",
            DecidedBy = CommercialFixtures.Authority(),
        };

        var result = await service.ValidateDefinitionAsync(comparison, CommercialFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == ProcurementValidationRules.DepartureNeedsRationale);
    }

    [Fact]
    public async Task A_requirement_dominated_by_one_criterion_is_warned_about()
    {
        var catalog = CommercialFixtures.BuildRequirementCatalog();
        var service = new SourcingRequirementValidationService(catalog);

        var requirement = CommercialFixtures.Requirement() with
        {
            Criteria =
            [
                new SourcingCriterion("CAP", SourcingCriterionKind.Capability, "Can do the work.", SourcingCriterionRole.Mandatory),
                new SourcingCriterion("COST", SourcingCriterionKind.Cost, "Cheapest.", SourcingCriterionRole.Weighted, 0.9m),
                new SourcingCriterion("QUAL", SourcingCriterionKind.Quality, "Approved.", SourcingCriterionRole.Weighted, 0.1m),
            ],
        };

        var result = await service.ValidateDefinitionAsync(requirement, CommercialFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == ProcurementValidationRules.SingleCriterionDominates);
    }

    [Fact]
    public async Task A_requirement_with_no_mandatory_criterion_is_warned_about()
    {
        var catalog = CommercialFixtures.BuildRequirementCatalog();
        var service = new SourcingRequirementValidationService(catalog);

        var requirement = CommercialFixtures.Requirement() with
        {
            Criteria = [new SourcingCriterion("COST", SourcingCriterionKind.Cost, "Cheapest.", SourcingCriterionRole.Weighted, 1.0m)],
        };

        var result = await service.ValidateDefinitionAsync(requirement, CommercialFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == ProcurementValidationRules.NoMandatoryCriteria);
    }

    [Fact]
    public async Task Candidates_priced_in_different_currencies_are_warned_about_rather_than_converted()
    {
        var requirements = CommercialFixtures.BuildRequirementCatalog();
        await CommercialFixtures.RegisterAsync(requirements, "req-1", CommercialFixtures.Requirement());

        var catalog = CommercialFixtures.BuildComparisonCatalog();
        var service = new SourcingComparisonValidationService(catalog, requirements);

        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [CommercialFixtures.Candidate("A", "sup-1") with { Price = new Tempest.Core.BusinessGovernance.Money(100m, CommercialFixtures.Eur) }]);

        var result = await service.ValidateDefinitionAsync(comparison, CommercialFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == ProcurementValidationRules.CandidateCurrencyMismatch);
    }

    [Fact]
    public async Task Lead_times_in_incomparable_units_are_warned_about()
    {
        var catalog = CommercialFixtures.BuildComparisonCatalog();
        var service = new SourcingComparisonValidationService(catalog);

        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [
                CommercialFixtures.Candidate("A", "sup-1") with { LeadTime = LeadTimeDuration.Weeks(3) },
                CommercialFixtures.Candidate("B", "sup-2") with { LeadTime = LeadTimeDuration.WorkingDays(15) },
            ]);

        var result = await service.ValidateDefinitionAsync(comparison, CommercialFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == ProcurementValidationRules.LeadTimesNotComparable);
    }

    [Fact]
    public async Task A_comparison_recommending_a_candidate_it_excluded_is_an_error()
    {
        var catalog = CommercialFixtures.BuildComparisonCatalog();
        var service = new SourcingComparisonValidationService(catalog);

        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [CommercialFixtures.Candidate("A", "sup-1", capability: CriterionStanding.Fails)]) with
        {
            RecommendedCandidateCode = "A",
        };

        var result = await service.ValidateDefinitionAsync(comparison, CommercialFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == ProcurementValidationRules.RecommendedCandidateIsExcluded);
    }

    [Fact]
    public void An_exclusion_must_carry_a_reason()
    {
        Assert.Throws<ArgumentException>(() => new CandidateExclusion("   "));
    }

    [Fact]
    public void A_comparison_round_trips_through_JSON_with_its_money_intact()
    {
        var comparison = Service.Compare(
            "CMP-1",
            CommercialFixtures.Requirement(),
            [CommercialFixtures.Candidate("A", "sup-1")]);

        var restored = JsonSerializer.Deserialize<SourcingComparison>(JsonSerializer.Serialize(comparison))!;

        Assert.Equal(comparison.RecommendedCandidateCode, restored.RecommendedCandidateCode);
        Assert.Equal(comparison.Strength, restored.Strength);
        Assert.Equal(
            CommercialFixtures.Gbp_(125.00m),
            restored.FindCandidate("A")!.Price!.Value);
        Assert.Equal(comparison.AllPins, restored.AllPins);
    }

    [Fact]
    public async Task A_sourcing_requirement_round_trips_through_the_catalogue()
    {
        var catalog = CommercialFixtures.BuildRequirementCatalog();

        await CommercialFixtures.RegisterAsync(catalog, "req-1", CommercialFixtures.Requirement());

        var found = await catalog.FindByReferenceAsync("REQ-1");

        Assert.NotNull(found);
        Assert.Equal(CommercialFixtures.Gbp, found.Definition.ComparisonCurrency);
        Assert.Equal(1.0m, found.Definition.TotalWeight);
        Assert.Equal(4, found.Definition.Criteria.Count);
    }

    [Fact]
    public async Task Comparisons_awaiting_a_person_are_surfaced_as_a_queue()
    {
        var catalog = CommercialFixtures.BuildComparisonCatalog();

        var awaiting = Service.Compare("CMP-1", CommercialFixtures.Requirement(), [CommercialFixtures.Candidate("A", "sup-1")])
            with { PreparedOn = CommercialFixtures.Today };

        var decided = Service.Compare("CMP-2", CommercialFixtures.Requirement(), [CommercialFixtures.Candidate("A", "sup-1")]) with
        {
            Reference = "CMP-2",
            PreparedOn = CommercialFixtures.Today.AddDays(-1),
            DecisionState = SourcingDecisionState.RecommendationAccepted,
            ChosenCandidateCode = "A",
            DecidedBy = CommercialFixtures.Authority(),
        };

        await CommercialFixtures.RegisterAsync(catalog, "cmp-1", awaiting);
        await CommercialFixtures.RegisterAsync(catalog, "cmp-2", decided);

        var queue = await catalog.FindAwaitingDecisionAsync();

        Assert.Equal("cmp-1", Assert.Single(queue).Id);
    }
}

/// <summary>
/// Structural guards over `P03`: assertions about what the code does
/// <em>not</em> contain.
/// </summary>
/// <remarks>
/// Reflection rather than review, because the constraint these tests
/// protect is the one most easily lost to a well-meaning later change. A
/// method named <c>ApproveSupplier</c> would read as an obvious
/// convenience to somebody who had not read `ADR-0135`.
/// </remarks>
public sealed class CommercialIntelligenceStructuralTests
{
    private static readonly string[] ForbiddenNameFragments =
    [
        "Award",
        "PlaceOrder",
        "RaiseOrder",
        "IssuePurchaseOrder",
        "ApproveSupplier",
        "QualifySupplier",
        "CommitExpenditure",
        "CommitSpend",
        "AcceptQuote",
        "AcceptQuotation",
        "SignContract",
        "Procure",
    ];

    public static TheoryData<Type> CommercialTypes
    {
        get
        {
            var data = new TheoryData<Type>();

            foreach (var type in typeof(SourcingComparison).Assembly
                         .GetTypes()
                         .Where(t => t.Namespace?.StartsWith("Tempest.Core.CommercialIntelligence", StringComparison.Ordinal) == true))
                data.Add(type);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CommercialTypes))]
    public void No_commercial_type_offers_an_act_of_procurement_authority(Type type)
    {
        var members = type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        foreach (var forbidden in ForbiddenNameFragments)
            Assert.DoesNotContain(members, name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_four_things_D4_keeps_apart_are_four_distinct_types_in_four_distinct_libraries()
    {
        Assert.NotEqual(typeof(CostEstimate), typeof(SupplierQuote));
        Assert.NotEqual(typeof(SupplierQuote), typeof(CustomerQuotation));
        Assert.NotEqual(typeof(CustomerQuotation), typeof(RealisedOutcome));

        var kinds = new[]
        {
            CostEstimateCatalog.EstimateDocumentKind,
            SupplierQuoteCatalog.SupplierQuoteDocumentKind,
            CustomerQuotationCatalog.QuotationDocumentKind,
        };

        Assert.Equal(kinds.Length, kinds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_P03_document_kind_and_library_name_is_unique()
    {
        string[] libraries =
        [
            SupplierCatalog.SupplierLibraryName,
            "CommercialProcessCosts",
            "CommercialLeadTimes",
            CostEstimateCatalog.EstimateLibraryName,
            SupplierQuoteCatalog.SupplierQuoteLibraryName,
            CustomerQuotationCatalog.QuotationLibraryName,
            SourcingRequirementCatalog.RequirementLibraryName,
            SourcingComparisonCatalog.ComparisonLibraryName,
        ];

        string[] kinds =
        [
            SupplierCatalog.SupplierDocumentKind,
            ProcessCostCatalog.ProcessCostDocumentKind,
            LeadTimeCatalog.LeadTimeDocumentKind,
            CostEstimateCatalog.EstimateDocumentKind,
            SupplierQuoteCatalog.SupplierQuoteDocumentKind,
            CustomerQuotationCatalog.QuotationDocumentKind,
            SourcingRequirementCatalog.RequirementDocumentKind,
            SourcingComparisonCatalog.ComparisonDocumentKind,
        ];

        Assert.Equal(libraries.Length, libraries.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(kinds.Length, kinds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Currency_is_never_represented_as_an_engineering_quantity()
    {
        // §31: money is not a physical dimension, and nothing in P03 may
        // smuggle it into UnitsAndQuantities.
        var offenders = typeof(SourcingComparison).Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith("Tempest.Core.CommercialIntelligence", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType.Namespace == "Tempest.Core.UnitsAndQuantities")
            .Where(p => p.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Price", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Money", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(offenders);
    }
}
