using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.MaterialSelection;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;

namespace Tempest.Core.Tests.EngineeringIntelligence;

// Selection logic is where "the system chose a material" would be easiest
// to imply and most wrong. These tests hold the line: every candidate is
// reported, a gap is never a pass, and nothing is ranked.
public class MaterialSelectionServiceTests
{
    private static MaterialSelectionService Service(MaterialCatalog materials) =>
        new(materials,
            new CurrentPrincipalAccessor(),
            timeProvider: EngineeringIntelligenceFixtures.Clock());

    private static MaterialRequirementSet Requirements(params MaterialCriterion[] criteria) => new()
    {
        ApplicationDescription = "A fixture bracket. Not a real application.",
        Criteria = criteria,
    };

    private static MaterialCriterion AtLeastYield(double megapascals, MaterialCriterionRole role = MaterialCriterionRole.Constraint) =>
        new(MaterialPropertyNames.YieldStrength,
            QuantityComparator.AtLeast,
            MaterialFixtures.Property(MaterialFixtures.Megapascals(megapascals)),
            role);

    private static async Task<IReferenceRecord<MaterialDefinition>> ReleasedSteelAsync(MaterialCatalog catalog, string id = "mat-1")
    {
        await catalog.RegisterAsync(id, MaterialFixtures.Steel($"FX-STEEL-{id}"), MaterialFixtures.Verified());
        return await MaterialFixtures.ReleaseAsync(catalog, id);
    }

    [Fact]
    public async Task ACandidateMeetingEveryConstraint_HasItsConstraintsSatisfied()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var steel = await ReleasedSteelAsync(catalog);

        var result = await Service(catalog).AssessAsync(Requirements(AtLeastYield(250)), [steel]);

        var candidate = Assert.Single(result.SatisfyingCandidates);
        Assert.Equal(CandidateStanding.ConstraintsSatisfied, candidate.Standing);
    }

    [Fact]
    public async Task EvenASingleSatisfyingCandidate_StillNeedsAnEngineersDecision()
    {
        // "Satisfies the stated constraints" is a claim about the criteria
        // that were checked, and silent about cost, availability and
        // whether the criteria were the right ones. A result reporting "no
        // decision needed" would be claiming to have selected the material.
        var catalog = MaterialFixtures.BuildCatalog();
        var steel = await ReleasedSteelAsync(catalog);

        var result = await Service(catalog).AssessAsync(Requirements(AtLeastYield(250)), [steel]);

        Assert.Single(result.SatisfyingCandidates);
        Assert.True(result.RequiresHumanDecision);
        Assert.False(result.HasOutstandingQuestions);
    }

    [Fact]
    public async Task ACandidateFailingAConstraint_IsEliminated_ButStillReported()
    {
        // Quietly dropping it would leave an engineer unable to tell "not
        // considered" from "considered and ruled out for this reason".
        var catalog = MaterialFixtures.BuildCatalog();
        var steel = await ReleasedSteelAsync(catalog);

        var result = await Service(catalog).AssessAsync(Requirements(AtLeastYield(400)), [steel]);

        var eliminated = Assert.Single(result.EliminatedCandidates);
        Assert.Equal(CandidateStanding.Eliminated, eliminated.Standing);
        Assert.Empty(result.SatisfyingCandidates);
    }

    [Fact]
    public async Task ACandidateWithNoRecordedValueForAConstraint_IsUnresolved_NotSatisfying()
    {
        // The fixture polymer records no yield strength. It has not passed
        // a yield-strength constraint, and it has not failed one either.
        var catalog = MaterialFixtures.BuildCatalog();

        await catalog.RegisterAsync("mat-poly", MaterialFixtures.Polymer(), MaterialFixtures.Verified());
        var polymer = await MaterialFixtures.ReleaseAsync(catalog, "mat-poly");

        var result = await Service(catalog).AssessAsync(Requirements(AtLeastYield(250)), [polymer]);

        var candidate = Assert.Single(result.UnresolvedCandidates);
        Assert.Equal(CandidateStanding.Unresolved, candidate.Standing);
        Assert.NotEmpty(candidate.OpenGaps);
        Assert.Empty(result.SatisfyingCandidates);
        Assert.True(result.HasOutstandingQuestions);
    }

    [Fact]
    public async Task ACriterionThatDoesNotApplyToTheFamily_IsNotTreatedAsAMissingValue()
    {
        // A ceramic has no yield point, and `A1`'s own traits table says
        // so. "There is none" is a fact about the family, not a gap in the
        // record — and the criterion still does not report a pass.
        var catalog = MaterialFixtures.BuildCatalog();

        await catalog.RegisterAsync("mat-cer", MaterialFixtures.Ceramic(), MaterialFixtures.Verified());
        var ceramic = await MaterialFixtures.ReleaseAsync(catalog, "mat-cer");

        var result = await Service(catalog).AssessAsync(Requirements(AtLeastYield(250)), [ceramic]);

        var assessment = Assert.Single(result.Candidates).AllCriterionAssessments.Single();

        Assert.Equal(AssessmentOutcome.NotApplicable, assessment.Outcome);
        Assert.False(AssessmentOutcomes.IsAffirmative(assessment.Outcome));
    }

    [Fact]
    public async Task APreferenceAMaterialMisses_DoesNotEliminateIt()
    {
        // A constraint and a preference are not the same criterion with a
        // different weight. Missing a preference leaves a material in the
        // running, with the shortfall reported rather than scored away.
        var catalog = MaterialFixtures.BuildCatalog();
        var steel = await ReleasedSteelAsync(catalog);

        var result = await Service(catalog).AssessAsync(
            Requirements(AtLeastYield(250), AtLeastYield(400, MaterialCriterionRole.Preference)),
            [steel]);

        var candidate = Assert.Single(result.SatisfyingCandidates);
        Assert.Equal(CandidateStanding.ConstraintsSatisfied, candidate.Standing);
        Assert.NotEmpty(candidate.UnmetPreferences);
    }

    [Fact]
    public async Task AnEvidenceCriterion_LeavesEveryCandidateAwaitingEvidence()
    {
        // Corrosion resistance in a particular service fluid is a real
        // selection criterion that no recorded property answers. It must
        // not silently pass.
        var catalog = MaterialFixtures.BuildCatalog();
        var steel = await ReleasedSteelAsync(catalog);

        var requirements = Requirements(AtLeastYield(250)) with
        {
            EvidenceCriteria = [new MaterialEvidenceCriterion("Compatible with the service fluid.")],
        };

        var result = await Service(catalog).AssessAsync(requirements, [steel]);

        var candidate = Assert.Single(result.UnresolvedCandidates);
        Assert.Contains(
            candidate.AllCriterionAssessments,
            assessment => assessment.Outcome == AssessmentOutcome.EvidenceRequired);
    }

    [Fact]
    public async Task EveryCandidateAssessment_PinsTheMaterialRevisionItRead()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var steel = await ReleasedSteelAsync(catalog);

        var result = await Service(catalog).AssessAsync(Requirements(AtLeastYield(250)), [steel]);

        var candidate = Assert.Single(result.SatisfyingCandidates);

        Assert.Contains(
            candidate.AllPins,
            pin => pin.RecordId == "mat-1" && pin.RevisionNumber == steel.RevisionNumber);
    }

    [Fact]
    public async Task AnUnreleasedMaterial_IsNotAssessedFromTheCatalogueByDefault()
    {
        // Draft reference data must not reach a selection result without
        // somebody asking for it.
        var catalog = MaterialFixtures.BuildCatalog();

        await catalog.RegisterAsync("mat-draft", MaterialFixtures.Steel(), MaterialFixtures.Verified());

        var result = await Service(catalog).AssessCatalogueAsync(Requirements(AtLeastYield(250)));

        Assert.Empty(result.SatisfyingCandidates);
        Assert.Empty(result.UnresolvedCandidates);
        Assert.Empty(result.EliminatedCandidates);
    }

    [Fact]
    public async Task AnAssessmentIsReproducibleFromItsOwnPins()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var steel = await ReleasedSteelAsync(catalog);
        var service = Service(catalog);
        var requirements = Requirements(AtLeastYield(250));

        var original = await service.AssessAsync(requirements, [steel]);
        var pins = original.SatisfyingCandidates
            .SelectMany(c => c.AllPins)
            .Where(pin => pin.Library == MaterialSelectionService.MaterialLibrary)
            .ToList();

        var reproduced = await service.ReproduceAsync(requirements, pins);

        Assert.Equal(
            original.SatisfyingCandidates.Single().Standing,
            reproduced.SatisfyingCandidates.Single().Standing);
    }

    [Fact]
    public async Task TheAssessmentRecordsWhenItWasRunAndByWhom()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var steel = await ReleasedSteelAsync(catalog);

        var result = await Service(catalog).AssessAsync(Requirements(AtLeastYield(250)), [steel]);

        Assert.Equal(EngineeringIntelligenceFixtures.FixedNow, result.AssessedAt);
        Assert.False(string.IsNullOrWhiteSpace(result.AssessedByPrincipalId));
    }
}
