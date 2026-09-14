using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.DesignRules;
using Tempest.Core.EngineeringIntelligence.MaterialSelection;
using Tempest.Core.EngineeringIntelligence.Subjects;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Integration;

// The bracket scenario, run through the real services against the real
// seeded records. The chain under test is:
//
//   Requirement -> reference data -> material selection -> manufacturing
//   -> calculation -> verification -> review -> documentation -> commercial
//
// Nothing here orchestrates that chain. Each step is exercised against the
// service that owns it, and what is being proved is that the steps refer to
// one another correctly — not that some new engine drives them.
public class BracketScenarioTests
{
    private static MaterialRequirementSet BracketRequirements(bool requireReleased = true) => new()
    {
        ApplicationDescription =
            "Mounting bracket, machined from bar, carrying a static axial load. Mass matters because the "
            + "bracket is carried; corrosion resistance does not, because the installation is indoors and dry.",
        AcceptableFamilies = [MaterialFamily.Aluminium, MaterialFamily.Steel, MaterialFamily.StainlessSteel],
        RequireReleasedMaterials = requireReleased,
        Criteria =
        [
            new MaterialCriterion(
                MaterialPropertyNames.YieldStrength,
                QuantityComparator.AtLeast,
                new ReferenceQuantityValue(
                    new Quantity<Pressure>(200.0, PressureUnits.Megapascal),
                    ReferenceValueOrigin.DerivedByTempestOS,
                    "ASSUMPTION: a 200 MPa floor stands in for the real design stress, which no requirement "
                    + "states. Marked derived, not sourced, because Tempest chose it."),
                MaterialCriterionRole.Constraint,
                "Minimum specified proof stress"),
            new MaterialCriterion(
                MaterialPropertyNames.Density,
                QuantityComparator.AtMost,
                new ReferenceQuantityValue(
                    new Quantity<MassDensity>(3.0, MassDensityUnits.GramPerCubicCentimetre),
                    ReferenceValueOrigin.DerivedByTempestOS,
                    "ASSUMPTION: a 3.0 g/cm3 ceiling expresses 'this is carried by hand'. Tempest's choice, "
                    + "not a requirement."),
                MaterialCriterionRole.Constraint,
                "Maximum density"),
        ],
        Notes = "Both criteria are explicitly labelled assumptions. REQ-BRACKET-001 states that a positive "
            + "margin is required and sets no load, so no criterion here can be derived from it.",
    };

    private static MaterialSelectionService SelectionService(ScenarioHarness harness) =>
        new(harness.Materials, new CurrentPrincipalAccessor(), harness.Rules);

    // ---- Step 1-3: requirement, reference data, material selection ----

    [Fact]
    public async Task SelectionOverTheRealCorpus_RefusesToActWhileEveryMaterialIsDraft()
    {
        // The corpus exactly as it ships. This is the expected architecture,
        // not a defect: nothing has been verified, so nothing is released,
        // so selection has no candidates to weigh.
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();

        var result = await SelectionService(harness).AssessCatalogueAsync(BracketRequirements());

        Assert.Empty(result.Candidates);

        // And the reason is visible rather than looking like an empty
        // library: the records are there, they are simply not usable.
        // (Not an exact count - the corpus grows; what matters here is
        // that it is non-empty despite offering zero usable candidates.)
        Assert.NotEmpty(await harness.Materials.ListAsync());
    }

    [Fact]
    public async Task SelectionOverReleasedData_WeighsEveryCandidateAndEliminatesOnRealNumbers()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await harness.ReleaseScenarioDataForTestingAsync();

        var result = await SelectionService(harness).AssessCatalogueAsync(BracketRequirements());

        // Three families were acceptable, so copper is not offered at all -
        // checked against its own known, stable identity rather than an
        // exact candidate count that would break the moment the corpus
        // gains another acceptable-family material.
        Assert.NotEmpty(result.Candidates);
        Assert.DoesNotContain(result.Candidates, c => c.MaterialId == MaterialSeed.CopperCw004A);

        // 6082-T6 is the only grade that clears both a 200 MPa floor and a
        // 3.0 g/cm3 ceiling, and it does so on its real published numbers:
        // 260 MPa and 2.70 g/cm3.
        var surviving = result.Candidates.Where(c => c.Standing == CandidateStanding.ConstraintsSatisfied).ToList();
        Assert.Single(surviving);
        Assert.Equal(MaterialSeed.Aluminium6082T6, surviving[0].MaterialId);

        // S355J2 is strong enough and far too heavy; 5083 is light enough
        // and too weak. Both are eliminated for the right reason, which is
        // what makes this a selection rather than a filter that happened to
        // leave one thing.
        var steel = result.Candidates.Single(c => c.MaterialId == MaterialSeed.S355J2);
        Assert.Equal(CandidateStanding.Eliminated, steel.Standing);
        Assert.Contains(steel.CriterionAssessments, a =>
            a.Criterion.Contains("Density", StringComparison.OrdinalIgnoreCase)
            && a.Outcome == AssessmentOutcome.Fail);

        var marine = result.Candidates.Single(c => c.MaterialId == MaterialSeed.Aluminium5083OH111);
        Assert.Equal(CandidateStanding.Eliminated, marine.Standing);
    }

    [Fact]
    public async Task TheSurvivingCandidateCarriesThePinThatMakesTheChoiceReproducible()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await harness.ReleaseScenarioDataForTestingAsync();

        var result = await SelectionService(harness).AssessCatalogueAsync(BracketRequirements());
        var chosen = result.Candidates.Single(c => c.Standing == CandidateStanding.ConstraintsSatisfied);

        Assert.Equal(harness.Materials.LibraryName, chosen.Pin.Library);
        Assert.Equal(MaterialSeed.Aluminium6082T6, chosen.Pin.RecordId);

        var record = await harness.Materials.FindAsync(chosen.Pin.RecordId);
        Assert.Equal(record!.RevisionNumber, chosen.Pin.RevisionNumber);
        Assert.Equal(ReferenceValidationState.Released, chosen.ValidationState);
    }

    [Fact]
    public async Task AMaterialMissingThePropertyACriterionAsksFor_IsUnresolvedNotEliminated()
    {
        // The 5083 record has no density, because its source published an
        // impossible one and the population phase refused to invent a
        // replacement. A criterion on density must therefore report that it
        // could not be assessed — never that the material failed, and never
        // that it passed on a default.
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await ScenarioHarness.ReleaseForTestingAsync(harness.Materials, MaterialSeed.Aluminium5083OH111);

        var densityOnly = new MaterialRequirementSet
        {
            ApplicationDescription = "Density-only probe against a record whose source density is unusable.",
            AcceptableFamilies = [MaterialFamily.Aluminium],
            Criteria =
            [
                new MaterialCriterion(
                    MaterialPropertyNames.Density,
                    QuantityComparator.AtMost,
                    new ReferenceQuantityValue(
                        new Quantity<MassDensity>(3.0, MassDensityUnits.GramPerCubicCentimetre),
                        ReferenceValueOrigin.DerivedByTempestOS),
                    MaterialCriterionRole.Constraint,
                    "Maximum density"),
            ],
        };

        var result = await SelectionService(harness).AssessCatalogueAsync(densityOnly);
        var candidate = Assert.Single(result.Candidates);

        Assert.Equal(MaterialSeed.Aluminium5083OH111, candidate.MaterialId);
        Assert.Equal(CandidateStanding.Unresolved, candidate.Standing);

        var assessment = Assert.Single(candidate.CriterionAssessments);
        Assert.NotEqual(AssessmentOutcome.Pass, assessment.Outcome);
        Assert.NotEqual(AssessmentOutcome.Fail, assessment.Outcome);
    }

    // ---- Step 4: manufacturing ----

    [Fact]
    public async Task DesignRulesAssessTheChosenMaterialAndReportAgainstRealRecords()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await harness.ReleaseScenarioDataForTestingAsync();

        var service = new DesignRuleService(harness.Rules, new CurrentPrincipalAccessor());
        var material = await harness.Materials.FindAsync(MaterialSeed.Aluminium6082T6);

        var assessment = await service.AssessAsync(new MaterialSubject(material!));

        // The two authored material rules apply to a material subject and
        // are the ones that come back; the three supplier capability rules
        // are scoped to processes and correctly do not.
        Assert.NotEmpty(assessment.Record.Evaluations);
        Assert.All(assessment.Record.Evaluations, e =>
            Assert.StartsWith("TDE-DES-", e.RuleCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DesignRulesAgainstAProcessSubject_ReturnTheSupplierCapabilityRules()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await harness.ReleaseScenarioDataForTestingAsync();

        var service = new DesignRuleService(harness.Rules, new CurrentPrincipalAccessor());
        var milling = await harness.Processes.FindAsync(ProcessSeed.CncMilling);

        var assessment = await service.AssessAsync(new ProcessSubject(milling!));

        Assert.Contains(assessment.Record.Evaluations, e => e.RuleCode == "MFG-CAP-001");
        Assert.Contains(assessment.Record.Evaluations, e => e.RuleCode == "MFG-CAP-002");
    }

    // ---- Steps 5-8: the assets that already reference the reference data ----

    [Fact]
    public async Task TheAssetChainReferencesTheSameRecordsSelectionChose()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await harness.ReleaseScenarioDataForTestingAsync();

        var result = await SelectionService(harness).AssessCatalogueAsync(BracketRequirements());
        var chosen = result.Candidates.Single(c => c.Standing == CandidateStanding.ConstraintsSatisfied);

        var pack = await harness.CalculationPacks.FindAsync(EngineeringAssetSeed.CalculationPackRecordId);
        var packPin = pack!.Definition.Inputs.Single(i => i.SourcePin is not null).SourcePin!;

        // The calculation stands on the same record the selection chose.
        // Not the same revision — the calculation pinned r1 and the
        // test-only review has moved the record on — and that difference is
        // exactly what a reviewer needs to see.
        Assert.Equal(chosen.Pin.RecordId, packPin.RecordId);
        Assert.True(chosen.Pin.RevisionNumber > packPin.RevisionNumber);

        var verification = await harness.VerificationArtefacts.FindAsync(EngineeringAssetSeed.VerificationRecordId);
        var requirement = await harness.Requirements.FindByIdentifierAsync(ScenarioRecords.RequirementIdentifier);
        Assert.Equal(requirement!.Id, verification!.Definition.Requirement.RequirementId);

        var review = await harness.DesignReviews.FindAsync(EngineeringAssetSeed.DesignReviewRecordId);
        Assert.Contains(pack.Definition.Reference, review!.Definition.CalculationPackReferences);
        Assert.Contains(requirement.Id, review.Definition.RequirementIds);

        var document = await harness.TechnicalDocuments.FindAsync(EngineeringAssetSeed.TechnicalDocumentRecordId);
        Assert.NotNull(document);
    }

    // ---- Step 9: commercial ----

    [Fact]
    public async Task TheCommercialLayerAnswersForTheChosenRouteWithoutBecomingADecision()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();

        var leadTimes = await harness.LeadTimes.ListAsync();
        var machining = leadTimes.Single(l => l.Definition.Applicability.ProcessRecordId == ProcessSeed.CncMilling);

        // Information, and it says which kind it is. Estimated, not
        // Historical: Tempest has placed no orders and observed no delivery,
        // and the record refuses to imply otherwise.
        Assert.Equal(Tempest.Core.CommercialIntelligence.LeadTimeKind.Estimated, machining.Definition.Kind);
        Assert.NotNull(machining.Definition.Source.ObservedOn);

        var supplier = (await harness.Suppliers.ListAsync())
            .Single(s => s.Definition.Identity.Reference == CommercialSeed.ProtolabsReference);

        // A capability the supplier claims, not one anybody has proven, and
        // the supplier is Prospective rather than Active. Nothing in this
        // chain has been allowed to promote information into a decision.
        Assert.Equal(Tempest.Core.CommercialIntelligence.Suppliers.SupplierStatus.Prospective, supplier.Definition.Status);
        Assert.All(supplier.Definition.Capabilities, c => Assert.Equal(
            Tempest.Core.CommercialIntelligence.Suppliers.CapabilityAssurance.Offered, c.Assurance));

        var cost = await harness.Costs.FindAsync(CommercialSeed.MouldToolingCost);
        Assert.Equal(Tempest.Core.CommercialIntelligence.CostCertainty.Quoted, cost!.Definition.Cost.Certainty);
        Assert.Equal(ScenarioHarness.FictionalReviewDate, cost.Definition.Source.ObservedOn);
    }

    // ---- Step 10: knowledge ----

    [Fact]
    public async Task TheWorkedExampleTeachesFromTheSameRecordTheScenarioSelected()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();

        var example = await harness.WorkedExamples.FindAsync(KnowledgeSeed.WorkedExampleRecordId);
        var pinned = example!.Definition.Steps.Where(s => s.SourcePin is not null).ToList();

        Assert.NotEmpty(pinned);
        Assert.All(pinned, s => Assert.Equal(MaterialSeed.Aluminium6082T6, s.SourcePin!.RecordId));

        var challenge = await harness.Challenges.FindAsync(KnowledgeSeed.ChallengeRecordId);
        Assert.Equal("CHL-MAT-001", challenge!.Definition.Reference);

        // The challenge is fictional as a scenario and says so, while the
        // numbers it turns on are the record's real published minima.
        Assert.Contains("fictional", challenge.Definition.Notes, StringComparison.OrdinalIgnoreCase);
    }
}
