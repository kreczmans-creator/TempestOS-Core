using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding.Datasets;

namespace Tempest.Core.Tests.Population;

// A populated corpus is only worth having if its references resolve. These
// tests walk each link the seed corpus claims and check that the thing on
// the other end exists, is the thing that was meant, and is still at the
// revision that was pinned.
public class CrossDomainReferenceTests
{
    [Fact]
    public async Task EveryStandardCitation_ResolvesToARegisteredStandard()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var registered = (await harness.Standards.ListAsync()).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var dangling = new List<string>();

        foreach (var record in await harness.Materials.ListAsync())
            Collect(dangling, $"Material/{record.Id}", record.Definition.Standards, registered);
        foreach (var record in await harness.Fasteners.ListAsync())
            Collect(dangling, $"Fastener/{record.Id}", record.Definition.Standards, registered);
        foreach (var record in await harness.Bearings.ListAsync())
            Collect(dangling, $"Bearing/{record.Id}", record.Definition.Standards, registered);
        foreach (var record in await harness.Processes.ListAsync())
            Collect(dangling, $"Process/{record.Id}", record.Definition.Standards, registered);
        foreach (var record in await harness.Rules.ListAsync())
            Collect(dangling, $"Rule/{record.Id}", record.Definition.Standards, registered);

        Assert.True(dangling.Count == 0, string.Join(Environment.NewLine, dangling));

        // And the links are not vacuously absent: the corpus really does cite.
        var citationCount = (await harness.Materials.ListAsync()).Sum(r => r.Definition.Standards.Count);
        Assert.True(citationCount >= 8, $"Only {citationCount} material standard citations were found.");
    }

    [Fact]
    public async Task ACitationCanCarryADifferentEditionFromTheIndexedStandard()
    {
        // Two Aalco datasheets cite different editions of EN 573-3, and a
        // bearing page cites ISO 15:2011 where the index holds 2017. That
        // is a real disagreement between sources, and the model keeps it
        // rather than flattening it: the citation carries the edition the
        // citing document stated.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var aluminium6082 = await harness.Materials.FindAsync(MaterialSeed.Aluminium6082T6);
        var aluminium5083 = await harness.Materials.FindAsync(MaterialSeed.Aluminium5083OH111);

        var edition6082 = aluminium6082!.Definition.Standards.Single(s => s.Designation == "EN 573-3").Edition;
        var edition5083 = aluminium5083!.Definition.Standards.Single(s => s.Designation == "EN 573-3").Edition;

        Assert.Equal("2009", edition6082);
        Assert.Equal("2019", edition5083);

        // Both resolve to the same indexed standard, which deliberately
        // records no edition of its own precisely because its citers
        // disagree.
        var indexed = await harness.Standards.FindAsync(StandardSeed.En573Part3);
        Assert.NotNull(indexed);
        Assert.Null(indexed.Definition.Edition);
    }

    [Fact]
    public async Task TheCalculationPackPinsTheMaterialRevisionItActuallyRead()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var pack = await harness.CalculationPacks.FindAsync(EngineeringAssetSeed.CalculationPackRecordId);
        Assert.NotNull(pack);

        var materialInput = pack.Definition.Inputs.Single(i => i.SourcePin is not null);
        var pin = materialInput.SourcePin!;

        Assert.Equal(harness.Materials.LibraryName, pin.Library);
        Assert.Equal(MaterialSeed.Aluminium6082T6, pin.RecordId);

        var material = await harness.Materials.FindAsync(pin.RecordId);
        Assert.NotNull(material);
        Assert.Equal(material.RevisionNumber, pin.RevisionNumber);

        // And the pinned revision really does hold the value the pack quotes.
        var yield = material.Definition.Properties[MaterialPropertyNames.YieldStrength];
        Assert.Equal(260e6, yield.CanonicalValue, 0);
        Assert.Contains("20 mm to 150 mm", yield.Conditions);
    }

    [Fact]
    public async Task RevisingAMaterialLeavesThePinPointingAtWhatTheCalculationActuallyUsed()
    {
        // The whole point of pinning. A calculation performed last year
        // must still be reconstructible after the material record is
        // corrected, or the reproducibility claim is decoration.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var pack = await harness.CalculationPacks.FindAsync(EngineeringAssetSeed.CalculationPackRecordId);
        var pin = pack!.Definition.Inputs.Single(i => i.SourcePin is not null).SourcePin!;

        var before = await harness.Materials.FindAsync(pin.RecordId);
        var pinnedYield = before!.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue;
        var pinnedNotes = before.Definition.Notes;

        await harness.Materials.ReviseAsync(
            pin.RecordId,
            before.Definition with { Notes = "Revised after the calculation was performed." },
            before.Provenance,
            "Later correction, after the calculation pack pinned revision "
            + pin.RevisionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");

        var after = await harness.Materials.FindAsync(pin.RecordId);
        Assert.True(after!.RevisionNumber > pin.RevisionNumber);

        // The pin still names the older revision, and that revision is
        // still retrievable with the values the calculation relied on.
        var asPinned = await harness.Materials.GetRevisionAsync(pin.RecordId, pin.RevisionNumber);
        Assert.Equal(pin.RevisionNumber, asPinned.RevisionNumber);
        Assert.Equal(pinnedYield, asPinned.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue, 0);
        Assert.Equal(pinnedNotes, asPinned.Definition.Notes);
        Assert.DoesNotContain("Revised after the calculation", asPinned.Definition.Notes);

        // Whereas the current record does carry the correction, so the two
        // really are different and the pin is doing work.
        Assert.Contains("Revised after the calculation", after.Definition.Notes);
    }

    [Fact]
    public async Task TheVerificationArtefactNamesARequirementThatReallyExists()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var artefact = await harness.VerificationArtefacts.FindAsync(EngineeringAssetSeed.VerificationRecordId);
        Assert.NotNull(artefact);

        var requirement = await harness.Requirements.FindAsync(artefact.Definition.Requirement.RequirementId);
        Assert.NotNull(requirement);
        Assert.Equal(SeedHarness.BracketRequirementIdentifier, requirement.Identifier);

        // Nothing has been verified, and the artefact says so rather than
        // reporting a standing it has not earned.
        Assert.Equal(VerificationStanding.NotPerformed, artefact.Definition.Standing);
        Assert.Null(artefact.Definition.Result);
    }

    [Fact]
    public async Task TheDesignReviewGathersTheCalculationVerificationAndRequirementTogether()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var review = await harness.DesignReviews.FindAsync(EngineeringAssetSeed.DesignReviewRecordId);
        Assert.NotNull(review);

        var pack = await harness.CalculationPacks.FindAsync(EngineeringAssetSeed.CalculationPackRecordId);
        var artefact = await harness.VerificationArtefacts.FindAsync(EngineeringAssetSeed.VerificationRecordId);

        Assert.Contains(pack!.Definition.Reference, review.Definition.CalculationPackReferences);
        Assert.Contains(artefact!.Definition.Reference, review.Definition.VerificationArtefactReferences);
        Assert.Contains(artefact.Definition.Requirement.RequirementId, review.Definition.RequirementIds);
    }

    [Fact]
    public async Task TheWorkedExampleReadsTheSameMaterialRecordItsLessonTeachesFrom()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var example = await harness.WorkedExamples.FindAsync(KnowledgeSeed.WorkedExampleRecordId);
        Assert.NotNull(example);

        var pinnedSteps = example.Definition.Steps.Where(s => s.SourcePin is not null).ToList();
        Assert.Equal(2, pinnedSteps.Count);

        foreach (var step in pinnedSteps)
        {
            var material = await harness.Materials.FindAsync(step.SourcePin!.RecordId);
            Assert.NotNull(material);
            Assert.Equal(MaterialSeed.Aluminium6082T6, material.Id);
            Assert.Equal(material.RevisionNumber, step.SourcePin.RevisionNumber);
        }

        // The example is reachable from the lesson that sets it, so the
        // Academy path is a real path rather than two records that happen
        // to be about the same topic.
        var lesson = await harness.AcademyNodes.FindAsync(KnowledgeSeed.LessonNodeRecordId);
        Assert.NotNull(lesson);
        Assert.Contains(
            lesson.Definition.Activities,
            a => a.WorkedExampleReference == example.Definition.Reference);
    }

    [Fact]
    public async Task TheCommercialRecordsPointAtRealProcessesAndMaterials()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var cost = await harness.Costs.FindAsync(CommercialSeed.MouldToolingCost);
        Assert.NotNull(cost);
        Assert.NotNull(await harness.Processes.FindAsync(cost.Definition.Applicability.ProcessRecordId!));

        foreach (var lead in await harness.LeadTimes.ListAsync())
            Assert.NotNull(await harness.Processes.FindAsync(lead.Definition.Applicability.ProcessRecordId!));

        var aalco = (await harness.Suppliers.ListAsync())
            .Single(s => s.Definition.Identity.Reference == CommercialSeed.AalcoReference);

        var citedMaterials = aalco.Definition.Capabilities.SelectMany(c => c.MaterialRecordIds).ToList();
        Assert.NotEmpty(citedMaterials);

        foreach (var materialId in citedMaterials)
            Assert.NotNull(await harness.Materials.FindAsync(materialId));

        var protolabs = (await harness.Suppliers.ListAsync())
            .Single(s => s.Definition.Identity.Reference == CommercialSeed.ProtolabsReference);

        foreach (var capability in protolabs.Definition.Capabilities.Where(c => c.ProcessRecordId is not null))
            Assert.NotNull(await harness.Processes.FindAsync(capability.ProcessRecordId!));
    }

    [Fact]
    public async Task AProcessNamesMaterialFamiliesThatTheMaterialLibraryActuallyHolds()
    {
        // Compatibility is recorded at family level because that is what
        // the supplier states. This checks the family link is still a real
        // link — that "Aluminium" resolves to registered aluminium records
        // rather than naming a family nothing is in.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var milling = await harness.Processes.FindAsync(ProcessSeed.CncMilling);
        Assert.NotNull(milling);

        var materials = await harness.Materials.ListAsync();
        var familiesHeld = materials.Select(m => m.Definition.Family).ToHashSet();

        var resolvable = milling.Definition.MaterialCompatibility
            .Where(c => familiesHeld.Contains(c.Family))
            .ToList();

        Assert.NotEmpty(resolvable);

        var aluminium = materials.Where(m => m.Definition.Family == MaterialFamily.Aluminium).ToList();
        Assert.Equal(2, aluminium.Count);
        Assert.Contains(milling.Definition.MaterialCompatibility, c => c.Family == MaterialFamily.Aluminium);

        // The families the supplier lists but this library holds nothing
        // for are a real and expected gap, not a broken reference.
        var unresolvable = milling.Definition.MaterialCompatibility
            .Where(c => !familiesHeld.Contains(c.Family))
            .Select(c => c.Family)
            .ToList();

        Assert.Contains(MaterialFamily.Titanium, unresolvable);
    }

    private static void Collect(
        List<string> dangling,
        string owner,
        IReadOnlyList<StandardReference> citations,
        IReadOnlySet<string> registered)
    {
        foreach (var citation in citations.Where(c => c.IsResolved))
        {
            if (!registered.Contains(citation.StandardId!))
                dangling.Add($"{owner} cites '{citation.Designation}' as '{citation.StandardId}', which is not registered.");
        }
    }
}
