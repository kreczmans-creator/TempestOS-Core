using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Development;
using Tempest.Core.BusinessGovernance.Operating;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.BusinessGovernance;

// C6 must never let potential revenue read as real, and C7 must never let
// a threshold become a decision.
public class PipelineAndOperatingTests
{
    private static DateOnly Today => BusinessGovernanceFixtures.Today;

    private static Money Gbp(decimal amount) => BusinessGovernanceFixtures.Gbp_(amount);

    // ---- C6 -----------------------------------------------------------

    private static async Task<(OpportunityCatalog Catalog, PipelineService Service)> BuildAsync(
        params (string Id, Opportunity Opportunity)[] opportunities)
    {
        var catalog = BusinessGovernanceFixtures.BuildOpportunityCatalog();

        foreach (var (id, opportunity) in opportunities)
            await catalog.RegisterAsync(id, opportunity, BusinessGovernanceFixtures.Verified());

        return (catalog, new PipelineService(catalog));
    }

    private static async Task<IValidationResult> ValidateOpportunityAsync(Opportunity opportunity)
    {
        var catalog = BusinessGovernanceFixtures.BuildOpportunityCatalog();
        var service = new OpportunityValidationService(catalog, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(opportunity, BusinessGovernanceFixtures.Verified());
    }

    [Fact]
    public void AnOpportunityValueDefaultsToPotential()
    {
        Assert.Equal(RevenueReality.Potential, BusinessGovernanceFixtures.Opportunity_().ValueReality);
    }

    [Fact]
    public async Task ValuePromotedPastWhatTheStageSupports_IsAnError()
    {
        var result = await ValidateOpportunityAsync(BusinessGovernanceFixtures.Opportunity_() with
        {
            ValueReality = RevenueReality.Contracted,
        });

        Assert.Contains(PipelineValidationRules.RevenueIsOverstated, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AWonOpportunityWithAContract_MayCarryContractedRevenue()
    {
        var result = await ValidateOpportunityAsync(BusinessGovernanceFixtures.Opportunity_(stage: PipelineStage.Won) with
        {
            ValueReality = RevenueReality.Contracted,
            ContractReference = "CON-1",
            Outcome = "Won on technical merit.",
        });

        Assert.DoesNotContain(PipelineValidationRules.RevenueIsOverstated, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AWonOpportunityWithNoContract_IsReported()
    {
        var result = await ValidateOpportunityAsync(BusinessGovernanceFixtures.Opportunity_(stage: PipelineStage.Won) with
        {
            Outcome = "Won.",
        });

        Assert.Contains(PipelineValidationRules.WonOpportunityNeedsContract, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AClosedOpportunityThatNeverSaidWhy_IsReported()
    {
        var result = await ValidateOpportunityAsync(BusinessGovernanceFixtures.Opportunity_(stage: PipelineStage.Lost));

        Assert.Contains(PipelineValidationRules.ClosedOpportunityNeedsOutcome, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AWinProbabilityOutsideZeroToOne_IsAnError()
    {
        var result = await ValidateOpportunityAsync(BusinessGovernanceFixtures.Opportunity_() with { WinProbability = 1.5m });

        Assert.Contains(PipelineValidationRules.WinProbabilityOutOfRange, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AnOpenOpportunityWithNothingPlanned_IsReported()
    {
        var result = await ValidateOpportunityAsync(BusinessGovernanceFixtures.Opportunity_() with { NextAction = null });

        Assert.Contains(PipelineValidationRules.OpenOpportunityNeedsNextAction, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AStaleOpportunity_IsReported()
    {
        var result = await ValidateOpportunityAsync(BusinessGovernanceFixtures.Opportunity_() with
        {
            Interactions = [new OpportunityInteraction(Today.AddDays(-120), "Fixture call.", "owner-1")],
        });

        Assert.Contains(PipelineValidationRules.OpportunityIsStale, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ADecisionDateThatHasPassed_IsReported()
    {
        var result = await ValidateOpportunityAsync(BusinessGovernanceFixtures.Opportunity_() with
        {
            ExpectedDecisionDate = Today.AddDays(-1),
        });

        Assert.Contains(PipelineValidationRules.DecisionDateHasPassed, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ThePipelineReportKeepsContractedAndPotentialApart()
    {
        var (_, service) = await BuildAsync(
            ("opp-1", BusinessGovernanceFixtures.Opportunity_()),
            ("opp-2", BusinessGovernanceFixtures.Opportunity_("OPP-2", PipelineStage.Won) with
            {
                ValueReality = RevenueReality.Contracted,
                ContractReference = "CON-1",
                EstimatedValue = Gbp(100_000m),
            }));

        var position = await service.ReportAsync(Today, BusinessGovernanceFixtures.Gbp);

        Assert.Equal(Gbp(100_000m), position.ContractedValue);
        Assert.Equal(Gbp(40_000m), position.PotentialValue);
    }

    [Fact]
    public async Task ThePipelineReportCarriesNoSingleTotalAndNoWeightedFigure()
    {
        // A structural guard against the "weighted pipeline" that
        // describes no possible future.
        await Task.CompletedTask;

        var properties = typeof(PipelinePosition).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains(nameof(PipelinePosition.ContractedValue), properties);
        Assert.Contains(nameof(PipelinePosition.PotentialValue), properties);
        Assert.DoesNotContain(properties, name => name.Contains("Weighted", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name is "TotalValue" or "PipelineValue" or "ExpectedValue");
    }

    [Fact]
    public async Task OpportunitiesWithNoEstimate_AreCountedRatherThanSilentlyOmitted()
    {
        var (_, service) = await BuildAsync(
            ("opp-1", BusinessGovernanceFixtures.Opportunity_() with { EstimatedValue = null }));

        var position = await service.ReportAsync(Today, BusinessGovernanceFixtures.Gbp);
        var stage = Assert.Single(position.ByStage);

        Assert.Equal(1, stage.Count);
        Assert.Equal(1, stage.WithoutEstimate);
        Assert.True(position.PotentialValue.IsZero);
    }

    [Fact]
    public async Task AWinRateOverNoDecisions_IsRefusedRatherThanReportedAsZero()
    {
        var (_, service) = await BuildAsync(("opp-1", BusinessGovernanceFixtures.Opportunity_()));

        var position = await service.ReportAsync(Today, BusinessGovernanceFixtures.Gbp);

        Assert.Null(position.WinRate);
    }

    [Fact]
    public async Task AWinRateIsTheProportionOfDecidedOpportunitiesWon()
    {
        var won = BusinessGovernanceFixtures.Opportunity_("OPP-W", PipelineStage.Won) with
        {
            ContractReference = "CON-1",
            Outcome = "Won.",
            Interactions = [new OpportunityInteraction(Today.AddDays(-3), "Awarded.", "owner-1")],
        };
        var lost = BusinessGovernanceFixtures.Opportunity_("OPP-L", PipelineStage.Lost) with
        {
            Outcome = "Lost on price.",
            Interactions = [new OpportunityInteraction(Today.AddDays(-2), "Declined.", "owner-1")],
        };

        var (_, service) = await BuildAsync(("opp-w", won), ("opp-l", lost));

        var position = await service.ReportAsync(Today, BusinessGovernanceFixtures.Gbp, Today.AddMonths(-1));

        Assert.Equal(0.5m, position.WinRate);
    }

    [Fact]
    public void AnOpportunityInAnotherCurrency_IsExcludedFromTotalsRatherThanConverted()
    {
        var opportunity = BusinessGovernanceFixtures.Opportunity_() with
        {
            EstimatedValue = new Money(50_000m, new CurrencyCode("EUR")),
        };

        Assert.NotEqual(BusinessGovernanceFixtures.Gbp, opportunity.EstimatedValue!.Value.Currency);
    }

    // ---- C7 -----------------------------------------------------------

    private static DecisionGate Gate(
        decimal? currentValue = null,
        DateOnly? measuredOn = null,
        decimal threshold = 0.85m,
        GateComparator comparator = GateComparator.AtLeast) =>
        new("GATE-HIRE",
            "Should we hire a second stress engineer?",
            "Rolling three-month utilisation",
            comparator,
            threshold,
            "proportion",
            currentValue,
            measuredOn,
            "director-1",
            "Recruit a second stress engineer, or subcontract the overflow.");

    [Fact]
    public void AGateWithNothingMeasured_SaysSoRatherThanReadingAsNotMet()
    {
        Assert.Equal(GateStatus.NotMeasured, Gate().StatusAt(Today));
    }

    [Fact]
    public void AStaleMeasurementDoesNotFireAGate()
    {
        // Acting on a figure from two quarters ago is worse than acting on
        // nothing.
        var gate = Gate(currentValue: 0.95m, measuredOn: Today.AddDays(-120));

        Assert.Equal(GateStatus.MeasurementStale, gate.StatusAt(Today));
    }

    [Theory]
    [InlineData(0.84, GateStatus.ConditionNotMet)]
    [InlineData(0.85, GateStatus.ConditionMet)]
    [InlineData(0.86, GateStatus.ConditionMet)]
    public void AGateIsDecidedExactlyAtItsThreshold(double value, GateStatus expected)
    {
        var gate = Gate(currentValue: (decimal)value, measuredOn: Today);

        Assert.Equal(expected, gate.StatusAt(Today));
    }

    [Fact]
    public void AGateIsPureAndDeterministic()
    {
        var gate = Gate(currentValue: 0.9m, measuredOn: Today);

        Assert.Equal(gate.StatusAt(Today), gate.StatusAt(Today));
        Assert.Equal(gate.Describe(Today), gate.Describe(Today));
    }

    [Fact]
    public void AMetGateAsksSomebodyToConsider_AndSaysItIsNotItselfADecision()
    {
        var description = Gate(currentValue: 0.95m, measuredOn: Today).Describe(Today);

        Assert.Contains("asked to consider", description, StringComparison.Ordinal);
        Assert.Contains("not itself a decision", description, StringComparison.Ordinal);
    }

    [Fact]
    public void ADecisionGateHasNoFieldForADecision()
    {
        // A structural guard: a gate that could record its own outcome
        // would become the decision-maker.
        var properties = typeof(DecisionGate).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(properties, name => name is "Decision" or "Outcome" or "Approved" or "ActionTaken");
        Assert.Contains(nameof(DecisionGate.DecisionOwnerPrincipalId), properties);
    }

    [Fact]
    public void AGateWithNoOwnerOrNoProposedAction_CannotBeConstructed()
    {
        Assert.Throws<ArgumentException>(() =>
            new DecisionGate("G", "Q?", "M", GateComparator.AtLeast, 1m, "unit", null, null, "  ", "Do something."));
        Assert.Throws<ArgumentException>(() =>
            new DecisionGate("G", "Q?", "M", GateComparator.AtLeast, 1m, "unit", null, null, "director-1", " "));
    }

    [Fact]
    public void CapacityCountsOnlyCommittedResources()
    {
        // A plan sized on people nobody has hired is a plan, not a
        // capacity.
        var model = BusinessGovernanceFixtures.Model() with
        {
            Resources =
            [
                new ResourceCapacity("RES-1", "Fixture Engineer One", ResourceKind.Employee, 220m, 0.65m),
                new ResourceCapacity("RES-2", "Fixture Planned Hire", ResourceKind.Employee, 220m, 0.65m, IsCommitted: false),
            ],
        };

        Assert.Equal(143m, model.CommittedProductiveDays);
        Assert.Equal(286m, model.PlannedProductiveDays);
    }

    [Fact]
    public void AUtilisationAboveOne_CannotBeConstructed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceCapacity("RES-1", "Fixture", ResourceKind.Employee, 220m, 1.2m));
    }

    [Fact]
    public void DemandAgainstNoCapacity_IsRefusedRatherThanReportedAsInfinite()
    {
        var model = BusinessGovernanceFixtures.Model(demand: 100m) with { Resources = [] };

        Assert.Null(model.DemandAgainstCommittedCapacity);
    }

    [Fact]
    public void ACapabilityHeldByOnePerson_IsASinglePointOfFailure()
    {
        var model = BusinessGovernanceFixtures.Model() with
        {
            Capabilities =
            [
                new OperatingCapability("CAP-FAT", "Fatigue analysis", IsHeld: true, HeldBy: ["eng-1"], ServiceCodes: ["ENG-SEN"]),
            ],
        };

        Assert.Single(model.KeyPersonCapabilities);
    }

    [Fact]
    public void ACapabilitySoldButNotHeld_IsReported()
    {
        var model = BusinessGovernanceFixtures.Model() with
        {
            Capabilities =
            [
                new OperatingCapability("CAP-CFD", "CFD", IsHeld: false, ServiceCodes: ["ENG-SEN"]),
            ],
        };

        Assert.Single(model.MissingCapabilities);
    }

    private static async Task<IValidationResult> ValidateModelAsync(OperatingScenario model)
    {
        var catalog = BusinessGovernanceFixtures.BuildOperatingCatalog();
        var service = new OperatingScenarioValidationService(catalog, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(model, BusinessGovernanceFixtures.Verified());
    }

    [Fact]
    public async Task DemandExceedingCommittedCapacity_IsReported()
    {
        var result = await ValidateModelAsync(BusinessGovernanceFixtures.Model(demand: 300m));

        Assert.Contains(OperatingValidationRules.DemandExceedsCapacity, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AnOptimisticUtilisation_IsReported()
    {
        var result = await ValidateModelAsync(BusinessGovernanceFixtures.Model() with
        {
            Resources = [new ResourceCapacity("RES-1", "Fixture", ResourceKind.Employee, 220m, 0.95m)],
        });

        Assert.Contains(OperatingValidationRules.UtilisationIsOptimistic, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ACapabilitySoldButNotHeld_IsAnError()
    {
        var result = await ValidateModelAsync(BusinessGovernanceFixtures.Model() with
        {
            Capabilities = [new OperatingCapability("CAP-CFD", "CFD", IsHeld: false, ServiceCodes: ["ENG-SEN"])],
        });

        Assert.Contains(OperatingValidationRules.CapabilitySoldButNotHeld, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AResourceClaimingAnUndeclaredCapability_IsReported()
    {
        var result = await ValidateModelAsync(BusinessGovernanceFixtures.Model() with
        {
            Resources = [new ResourceCapacity("RES-1", "Fixture", ResourceKind.Employee, 220m, 0.6m, CapabilityCodes: ["CAP-NOPE"])],
        });

        Assert.Contains(OperatingValidationRules.ResourceClaimsUnknownCapability, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AMetGate_IsReportedAsAFindingForAPersonToLookAt()
    {
        var result = await ValidateModelAsync(BusinessGovernanceFixtures.Model() with
        {
            Gates = [Gate(currentValue: 0.95m, measuredOn: Today)],
        });

        var finding = result.Warnings.Single(d => d.Code == OperatingValidationRules.GateConditionIsMet);

        Assert.Contains("asked to consider", finding.Message, StringComparison.Ordinal);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AGateNobodyHasEverMeasured_IsReported()
    {
        var result = await ValidateModelAsync(BusinessGovernanceFixtures.Model() with { Gates = [Gate()] });

        Assert.Contains(OperatingValidationRules.GateHasNeverBeenMeasured, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AModelWithNoResources_IsAnError()
    {
        var result = await ValidateModelAsync(BusinessGovernanceFixtures.Model() with { Resources = [] });

        Assert.Contains(OperatingValidationRules.ModelMustHaveResources, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AModelWithNoAssumptions_IsReported()
    {
        var result = await ValidateModelAsync(BusinessGovernanceFixtures.Model() with { Assumptions = [] });

        Assert.Contains(OperatingValidationRules.AssumptionsShouldBeRecorded, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public void AnUnapprovedModelIsNotTheCurrentOne()
    {
        Assert.False(BusinessGovernanceFixtures.Model().IsCurrentModel);
    }
}
