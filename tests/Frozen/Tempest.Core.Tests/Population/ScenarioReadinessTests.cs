using Tempest.Core.Materials;
using Tempest.Core.ReferenceData.Seeding.Datasets;

namespace Tempest.Core.Tests.Population;

// Not an end-to-end test. The next phase builds that. This is the check
// that the data it will need is present and joined up, so that the phase
// after this one discovers missing links now rather than halfway through
// writing a workflow.
public class ScenarioReadinessTests
{
    [Fact]
    public async Task TheBracketScenarioHasDataAtEveryStepOfTheChain()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        // 1. Requirement.
        var requirement = await harness.Requirements.FindByIdentifierAsync(SeedHarness.BracketRequirementIdentifier);
        Assert.NotNull(requirement);

        // 2. Candidate materials, plural — a selection with one candidate
        //    is not a selection.
        var candidates = (await harness.Materials.ListAsync())
            .Where(m => m.Definition.Properties.ContainsKey(MaterialPropertyNames.YieldStrength)
                && m.Definition.Properties.ContainsKey(MaterialPropertyNames.Density))
            .ToList();

        Assert.True(candidates.Count >= 4, $"Only {candidates.Count} materials carry both a strength and a density.");

        // And they genuinely disagree, so a trade-off exists to be made.
        var strengths = candidates
            .Select(m => m.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue)
            .ToList();

        Assert.True(strengths.Max() / strengths.Min() > 3.0,
            "The candidate strengths are too close together to force a real choice.");

        // 3. A manufacturing route, with a capability envelope.
        var milling = await harness.Processes.FindAsync(ProcessSeed.CncMilling);
        Assert.NotNull(milling);
        Assert.True(milling.Definition.Capabilities.AchievableTolerance!.IsRecorded);
        Assert.NotEmpty(milling.Definition.MaterialCompatibility);

        // 4. A calculation pack, pinned to a material revision.
        var pack = await harness.CalculationPacks.FindAsync(EngineeringAssetSeed.CalculationPackRecordId);
        Assert.NotNull(pack);
        Assert.Contains(pack.Definition.Inputs, i => i.SourcePin is not null);

        // 5. A verification artefact against the requirement.
        var verification = await harness.VerificationArtefacts.FindAsync(EngineeringAssetSeed.VerificationRecordId);
        Assert.NotNull(verification);
        Assert.Equal(requirement.Id, verification.Definition.Requirement.RequirementId);

        // 6. A design review gathering them.
        var review = await harness.DesignReviews.FindAsync(EngineeringAssetSeed.DesignReviewRecordId);
        Assert.NotNull(review);
        Assert.NotEmpty(review.Definition.Observations);

        // 7. A technical document.
        var document = await harness.TechnicalDocuments.FindAsync(EngineeringAssetSeed.TechnicalDocumentRecordId);
        Assert.NotNull(document);

        // 8. Commercial consideration: a cost and a lead time against the
        //    manufacturing route.
        Assert.NotEmpty(await harness.Costs.ListAsync());
        var leadTimes = await harness.LeadTimes.ListAsync();
        Assert.Contains(leadTimes, l => l.Definition.Applicability.ProcessRecordId == ProcessSeed.CncMilling);
    }

    [Fact]
    public async Task TheScenarioCannotYetBeRunToACompletedState_AndTheDataSaysWhy()
    {
        // The honest counterpart of the test above. Everything is present
        // and linked; nothing is finished. A later phase that mistook
        // "linked" for "ready" would build a workflow over records that
        // deliberately hold no results.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var pack = await harness.CalculationPacks.FindAsync(EngineeringAssetSeed.CalculationPackRecordId);
        Assert.Contains(pack!.Definition.Inputs, i => i.Value == "not established");
        Assert.Contains(pack.Definition.Outputs, o => o.Value == "not computed");

        var verification = await harness.VerificationArtefacts.FindAsync(EngineeringAssetSeed.VerificationRecordId);
        Assert.Null(verification!.Definition.Result);

        var review = await harness.DesignReviews.FindAsync(EngineeringAssetSeed.DesignReviewRecordId);
        Assert.Empty(review!.Definition.Participants);
        Assert.Empty(review.Definition.Decisions);

        // And the reference data underneath is all still Draft, so a
        // released design could not legitimately be built on it yet.
        var materials = await harness.Materials.ListAsync();
        Assert.All(materials, m => Assert.Equal(
            Tempest.Core.ReferenceData.ReferenceValidationState.Draft, m.ValidationState));
    }
}
