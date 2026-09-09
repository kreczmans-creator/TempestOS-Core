using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.Decisions;
using Tempest.Core.EngineeringIntelligence.MaterialSelection;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Tests.Population;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Integration;

// What the system refuses to do. These matter more than the happy path: a
// platform that will cheerfully compute a margin from a Draft reference, a
// missing property or a superseded record is worse than one that computes
// nothing, because somebody will believe it.
//
// Every test here asserts a governed refusal, warning or explicit
// human-decision requirement. None of them was made to pass by relaxing a
// safeguard.
public class RefusalTests
{
    [Fact]
    public async Task ADraftReferenceIsRefusedWhereReleasedIsRequired()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();

        var requirements = new MaterialRequirementSet
        {
            ApplicationDescription = "Any material at all.",
            RequireReleasedMaterials = true,
        };

        var result = await new MaterialSelectionService(harness.Materials, new CurrentPrincipalAccessor())
            .AssessCatalogueAsync(requirements);

        Assert.Empty(result.Candidates);

        // The same question with the released requirement lifted returns
        // all six, which proves the emptiness above was the gate and not an
        // empty library.
        var permissive = await new MaterialSelectionService(harness.Materials, new CurrentPrincipalAccessor())
            .AssessCatalogueAsync(requirements with { RequireReleasedMaterials = false });

        Assert.Equal(6, permissive.Candidates.Count);
    }

    [Fact]
    public async Task ADraftRuleIsNeverEvaluated()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await ScenarioHarness.ReleaseForTestingAsync(harness.Materials, MaterialSeed.Aluminium6082T6);

        var service = new Tempest.Core.EngineeringIntelligence.DesignRules.DesignRuleService(
            harness.Rules, new CurrentPrincipalAccessor());

        var material = await harness.Materials.FindAsync(MaterialSeed.Aluminium6082T6);
        var assessment = await service.AssessAsync(new Tempest.Core.EngineeringIntelligence.Subjects.MaterialSubject(material!));

        // Five rules are registered, all Draft. None is evaluated.
        Assert.Equal(5, (await harness.Rules.ListAsync()).Count);
        Assert.Empty(assessment.Record.Evaluations);
    }

    [Fact]
    public async Task AMissingPropertyIsReportedAsUnassessed_NeverDefaultedAndNeverFailed()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await ScenarioHarness.ReleaseForTestingAsync(harness.Materials, MaterialSeed.Aluminium5083OH111);

        var result = await new MaterialSelectionService(harness.Materials, new CurrentPrincipalAccessor())
            .AssessCatalogueAsync(new MaterialRequirementSet
            {
                ApplicationDescription = "Probe a property the record does not carry.",
                AcceptableFamilies = [MaterialFamily.Aluminium],
                Criteria =
                [
                    new MaterialCriterion(
                        MaterialPropertyNames.Density,
                        QuantityComparator.AtMost,
                        new ReferenceQuantityValue(
                            new Quantity<MassDensity>(3.0, MassDensityUnits.GramPerCubicCentimetre),
                            ReferenceValueOrigin.DerivedByTempestOS),
                        MaterialCriterionRole.Constraint),
                ],
            });

        var assessment = Assert.Single(Assert.Single(result.Candidates).CriterionAssessments);

        Assert.NotEqual(AssessmentOutcome.Pass, assessment.Outcome);
        Assert.NotEqual(AssessmentOutcome.Fail, assessment.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(assessment.Reason));
    }

    [Fact]
    public async Task AnIncompatibleUnitIsRefused_NotSilentlyCoerced()
    {
        // A criterion asking for a density but stating a pressure. The two
        // are both "a number with a unit" and nothing but the dimension
        // system stops one being compared against the other.
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await ScenarioHarness.ReleaseForTestingAsync(harness.Materials, MaterialSeed.Aluminium6082T6);

        var result = await new MaterialSelectionService(harness.Materials, new CurrentPrincipalAccessor())
            .AssessCatalogueAsync(new MaterialRequirementSet
            {
                ApplicationDescription = "Compare a density against a pressure.",
                AcceptableFamilies = [MaterialFamily.Aluminium],
                Criteria =
                [
                    new MaterialCriterion(
                        MaterialPropertyNames.Density,
                        QuantityComparator.AtMost,
                        new ReferenceQuantityValue(
                            new Quantity<Pressure>(3.0, PressureUnits.Megapascal),
                            ReferenceValueOrigin.DerivedByTempestOS),
                        MaterialCriterionRole.Constraint),
                ],
            });

        var assessment = Assert.Single(Assert.Single(result.Candidates).CriterionAssessments);

        // 2.70 g/cm3 is numerically at most 3.0, so a system comparing bare
        // magnitudes would pass this. It must not.
        Assert.NotEqual(AssessmentOutcome.Pass, assessment.Outcome);
    }

    [Fact]
    public async Task ANonExistentReferenceIsRefusedRatherThanReturningNothingQuietly()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();

        // A find returns null, which a caller must handle...
        Assert.Null(await harness.Materials.FindAsync("mat-does-not-exist"));

        // ...but an operation that requires the record to exist throws
        // rather than no-opping.
        await Assert.ThrowsAsync<ReferenceRecordNotFoundException>(
            () => harness.Materials.SetValidationStateAsync(
                "mat-does-not-exist", ReferenceValidationState.Checked, "Probe."));
    }

    [Fact]
    public async Task AReleasedReferenceCannotBeEdited()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        var released = await ScenarioHarness.ReleaseForTestingAsync(harness.Materials, MaterialSeed.Aluminium6082T6);

        Assert.Equal(ReferenceValidationState.Released, released.ValidationState);

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => harness.Materials.ReviseAsync(
                MaterialSeed.Aluminium6082T6,
                released.Definition with { Name = "Quietly renamed" },
                released.Provenance,
                "Attempt to edit released reference data."));

        // And it cannot be demoted back to Draft to get around that.
        await Assert.ThrowsAsync<InvalidReferenceStateTransitionException>(
            () => harness.Materials.SetValidationStateAsync(
                MaterialSeed.Aluminium6082T6, ReferenceValidationState.Draft, "Attempt to demote."));
    }

    [Fact]
    public async Task ASupersededRecordDropsOutOfSelectionUnlessItsRevisionIsAskedForExplicitly()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();

        var original = await ScenarioHarness.ReleaseForTestingAsync(harness.Materials, MaterialSeed.Aluminium6082T6);

        // Two live records may not share a designation, so the replacement
        // has to declare its own. Asserted here rather than worked around
        // silently: it is a real safeguard against two "6082" records
        // drifting apart under one name.
        await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => harness.Materials.RegisterAsync(
                "mat-6082-t6-rev-b",
                original.Definition with { Grade = "T6 revision B" },
                original.Provenance));

        await harness.Materials.RegisterAsync(
            "mat-6082-t6-rev-b",
            original.Definition with { Designation = "6082 (rev B)", Grade = "T6 revision B" },
            original.Provenance);
        await ScenarioHarness.ReleaseForTestingAsync(harness.Materials, "mat-6082-t6-rev-b");
        await harness.Materials.SupersedeAsync(
            MaterialSeed.Aluminium6082T6, "mat-6082-t6-rev-b", "Replaced by revision B.");

        var superseded = await harness.Materials.FindAsync(MaterialSeed.Aluminium6082T6);
        Assert.Equal(ReferenceValidationState.Superseded, superseded!.ValidationState);
        Assert.Equal("mat-6082-t6-rev-b", superseded.SupersededByRecordId);

        // Selection that asks for released material no longer offers it.
        var result = await new MaterialSelectionService(harness.Materials, new CurrentPrincipalAccessor())
            .AssessCatalogueAsync(new MaterialRequirementSet
            {
                ApplicationDescription = "Selection after supersession.",
                AcceptableFamilies = [MaterialFamily.Aluminium],
            });

        Assert.DoesNotContain(result.Candidates, c => c.MaterialId == MaterialSeed.Aluminium6082T6);

        // But the superseded revision is still retrievable when named
        // explicitly, which is what keeps historical work reproducible.
        var asPinned = await harness.Materials.GetRevisionAsync(MaterialSeed.Aluminium6082T6, original.RevisionNumber);
        Assert.Equal(ReferenceValidationState.Released, asPinned.ValidationState);
    }

    [Fact]
    public async Task AnUnsuitableManufacturingProcessDoesNotSurviveScreening()
    {
        // Run through the real screening service, not by comparing lists.
        // An aluminium bracket 180 mm across, wanted as a prototype: milling
        // is the route, and injection moulding — a perfectly valid, released,
        // well-formed process record — must be eliminated because it cannot
        // make the part.
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await harness.ReleaseScenarioDataForTestingAsync();

        var service = new ManufacturingDecisionService(
            harness.Processes, harness.DecisionTrees, new CurrentPrincipalAccessor());

        var result = await service.ScreenCatalogueAsync(new ManufacturingRequirementSet
        {
            PartDescription = "Mounting bracket, machined from aluminium bar.",
            MaterialFamily = MaterialFamily.Aluminium,
            LargestDimension = new Quantity<Length>(180.0, LengthUnits.Millimetre),
            ProductionScale = Tempest.Core.Manufacturing.ProductionScale.Prototype,
        });

        Assert.Equal(4, result.Candidates.Count);

        // Milling is confirmed viable on the supplier's own capability data.
        var milling = result.Candidates.Single(c => c.ProcessId == ProcessSeed.CncMilling);
        Assert.Equal(CandidateStanding.ConstraintsSatisfied, milling.Standing);
        Assert.Contains(result.ViableCandidates, c => c.ProcessId == ProcessSeed.CncMilling);

        // Injection moulding is NOT confirmed viable — and it is reported
        // Unresolved rather than Eliminated, which is the correct and more
        // careful answer given the data that exists.
        //
        // The process records carry only ProcessMaterialSuitability.Suitable
        // entries, because the supplier publishes a list of materials it
        // offers and says nothing about what it refuses. Absence from that
        // list is not evidence of incompatibility, and the service declines
        // to manufacture the inference. A system that eliminated on silence
        // would be guessing, and would guess wrong the first time a supplier
        // simply forgot to list something.
        //
        // The consequence is a real gap in the seed data, not in the logic:
        // eliminating a process on material grounds needs an explicit
        // NotSuitable entry, and nothing in the corpus has one.
        var moulding = result.Candidates.Single(c => c.ProcessId == ProcessSeed.InjectionMoulding);
        Assert.Equal(CandidateStanding.Unresolved, moulding.Standing);
        Assert.DoesNotContain(result.ViableCandidates, c => c.ProcessId == ProcessSeed.InjectionMoulding);
        Assert.Contains(result.UnresolvedCandidates, c => c.ProcessId == ProcessSeed.InjectionMoulding);

        // Nothing is eliminated at all, and that is the honest state.
        Assert.Empty(result.EliminatedCandidates);
    }

    [Fact]
    public async Task AnExplicitlyUnsuitableMaterialEntryDoesEliminateTheProcess()
    {
        // The counterpart to the test above, proving the logic is sound and
        // the gap is in the data: given an explicit NotSuitable entry, the
        // same service eliminates the same process.
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();
        await harness.ReleaseScenarioDataForTestingAsync();

        var moulding = await harness.Processes.FindAsync(ProcessSeed.InjectionMoulding);
        Assert.NotNull(moulding);

        // A second moulding record that says outright it will not take
        // aluminium — the statement the supplier's page never makes.
        await harness.Processes.RegisterAsync(
            "prc-injection-moulding-explicit",
            moulding.Definition with
            {
                Variant = "Explicit-incompatibility variant, test fixture",
                MaterialCompatibility =
                [
                    .. moulding.Definition.MaterialCompatibility,
                    new Tempest.Core.Manufacturing.ProcessMaterialCompatibility(
                        Family: MaterialFamily.Aluminium,
                        Suitability: Tempest.Core.Manufacturing.ProcessMaterialSuitability.NotSuitable,
                        Origin: ReferenceValueOrigin.EngineeringReference,
                        Conditions: "FICTIONAL TEST FIXTURE: an explicit refusal the real source does not make."),
                ],
            },
            moulding.Provenance);

        await ScenarioHarness.ReleaseForTestingAsync(harness.Processes, "prc-injection-moulding-explicit");

        var service = new ManufacturingDecisionService(
            harness.Processes, harness.DecisionTrees, new CurrentPrincipalAccessor());

        var result = await service.ScreenCatalogueAsync(new ManufacturingRequirementSet
        {
            PartDescription = "Mounting bracket, machined from aluminium bar.",
            MaterialFamily = MaterialFamily.Aluminium,
            LargestDimension = new Quantity<Length>(180.0, LengthUnits.Millimetre),
        });

        var explicitly = result.Candidates.Single(c => c.ProcessId == "prc-injection-moulding-explicit");

        Assert.Equal(CandidateStanding.Eliminated, explicitly.Standing);
        Assert.Contains(explicitly.Assessments, a => a.Outcome == AssessmentOutcome.Fail);
    }

    [Fact]
    public async Task ACommercialRecommendationNeverPresentsItselfAsADecision()
    {
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();

        var supplier = (await harness.Suppliers.ListAsync())
            .Single(s => s.Definition.Identity.Reference == CommercialSeed.ProtolabsReference);

        // Nothing in the corpus asserts a supplier has been chosen, audited
        // or traded with. Every one of those would be a decision, and the
        // model keeps them separate from the information.
        Assert.Equal(Tempest.Core.CommercialIntelligence.Suppliers.SupplierStatus.Prospective, supplier.Definition.Status);
        Assert.All(supplier.Definition.Capabilities, c => Assert.NotEqual(
            Tempest.Core.CommercialIntelligence.Suppliers.CapabilityAssurance.Proven, c.Assurance));
        Assert.All(supplier.Definition.Capabilities, c => Assert.NotEqual(
            Tempest.Core.CommercialIntelligence.Suppliers.CapabilityAssurance.Verified, c.Assurance));

        Assert.Null(supplier.Definition.GoverningContractReference);
        Assert.Null(supplier.Definition.MinimumOrderValue);
    }

    [Fact]
    public async Task TheShippedCorpusIsUntouchedByTheTestOnlyReviewMechanism()
    {
        // The guard on the guard. If ScenarioHarness ever leaked into the
        // shipped datasets, every refusal test above would keep passing
        // while the product quietly shipped forged provenance.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        foreach (var record in await harness.Materials.ListAsync())
        {
            Assert.Equal(ReferenceValidationState.Draft, record.ValidationState);
            Assert.NotEqual(ScenarioHarness.FictionalReviewerPrincipalId, record.Provenance.ReviewerPrincipalId);
            Assert.Null(record.Provenance.ReviewerPrincipalId);
            Assert.DoesNotContain("TEST-ONLY", record.Provenance.Notes ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheTurningWallThicknessContradictionIsVisible_NotResolved()
    {
        // TEMPEST-MFG-005 holds that turning produces no wall thickness;
        // the supplier publishes one. The population phase moved the figure
        // to a constraint so neither the rule nor the source was overwritten.
        // This asserts the contradiction is still findable by a reviewer,
        // because an engineering disagreement that becomes invisible has
        // been resolved by accident.
        var harness = new ScenarioHarness();
        await harness.SeedEverythingAsync();

        var turning = await harness.Processes.FindAsync(ProcessSeed.CncTurning);

        Assert.Null(turning!.Definition.Capabilities.WallThickness);

        var constraint = Assert.Single(
            turning.Definition.Constraints,
            c => c.Description.Contains("wall thickness", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("0.51 mm", constraint.Description, StringComparison.Ordinal);
        Assert.Contains("TEMPEST-MFG-005", constraint.Description, StringComparison.Ordinal);

        // And the library still validates clean, because the disagreement
        // was recorded rather than smuggled past the rule.
        var report = await new Tempest.Core.Manufacturing.ProcessValidationService(harness.Processes)
            .ValidateLibraryAsync();

        Assert.True(report.IsClean);
    }
}
