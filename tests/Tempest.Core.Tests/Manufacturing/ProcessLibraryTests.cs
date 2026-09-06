using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;

namespace Tempest.Core.Tests.Manufacturing;

// A7: the manufacturing-specific half of the library. The shared machinery
// it sits on (registration, revision, lifecycle, supersession, hostile
// data) is tested once, in ReferenceDataCatalogTests, and is not restated
// here.
public class ProcessLibraryTests
{
    private static bool HasError(IValidationResult result, string code) => result.Errors.Any(d => d.Code == code);

    private static bool HasWarning(IValidationResult result, string code) => result.Warnings.Any(d => d.Code == code);

    // ----------------------------------------------------------------
    // Taxonomy and traits
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(ProcessFamily.SandCasting, ProcessGroup.Casting)]
    [InlineData(ProcessFamily.ClosedDieForging, ProcessGroup.BulkForming)]
    [InlineData(ProcessFamily.DeepDrawing, ProcessGroup.SheetForming)]
    [InlineData(ProcessFamily.WaterjetCutting, ProcessGroup.Cutting)]
    [InlineData(ProcessFamily.Milling, ProcessGroup.Machining)]
    [InlineData(ProcessFamily.PowderBedFusion, ProcessGroup.Additive)]
    [InlineData(ProcessFamily.InjectionMoulding, ProcessGroup.Moulding)]
    [InlineData(ProcessFamily.MetalInjectionMoulding, ProcessGroup.PowderProcessing)]
    [InlineData(ProcessFamily.Brazing, ProcessGroup.Joining)]
    [InlineData(ProcessFamily.QuenchAndTemper, ProcessGroup.HeatTreatment)]
    [InlineData(ProcessFamily.Anodising, ProcessGroup.SurfaceTreatment)]
    [InlineData(ProcessFamily.Honing, ProcessGroup.Finishing)]
    [InlineData(ProcessFamily.Other, ProcessGroup.Other)]
    [InlineData(ProcessFamily.Unspecified, ProcessGroup.Unspecified)]
    public void EveryFamilyBelongsToExactlyOneGroup(ProcessFamily family, ProcessGroup group) =>
        Assert.Equal(group, ProcessFamilyTraits.GroupOf(family));

    [Fact]
    public void TheGroupsPartitionTheTaxonomyWithNothingLeftOver()
    {
        var families = Enum.GetValues<ProcessFamily>();
        var grouped = Enum.GetValues<ProcessGroup>().SelectMany(ProcessFamilyTraits.FamiliesIn).ToList();

        Assert.Equal(families.Length, grouped.Count);
        Assert.Equal(families.Length, grouped.Distinct().Count());
    }

    [Fact]
    public void OnlyAProcessThatFormsAgainstAMouldOrDieHasADraftAngle()
    {
        Assert.True(ProcessFamilyTraits.UsesAMouldOrDie(ProcessFamily.SandCasting));
        Assert.True(ProcessFamilyTraits.UsesAMouldOrDie(ProcessFamily.InjectionMoulding));
        Assert.True(ProcessFamilyTraits.UsesAMouldOrDie(ProcessFamily.ClosedDieForging));
        Assert.False(ProcessFamilyTraits.UsesAMouldOrDie(ProcessFamily.Milling));
        Assert.False(ProcessFamilyTraits.UsesAMouldOrDie(ProcessFamily.OpenDieForging));
    }

    [Fact]
    public void AHeatTreatmentShapesNothingAndLeavesNoSurfaceOfItsOwn()
    {
        Assert.False(ProcessFamilyTraits.IsShaping(ProcessFamily.StressRelieving));
        Assert.False(ProcessFamilyTraits.HasSurfaceRoughnessCapability(ProcessFamily.StressRelieving));
        Assert.True(ProcessFamilyTraits.HasProcessTemperature(ProcessFamily.StressRelieving));
    }

    [Fact]
    public void FamilyTraits_RefuseToSpeakForAnUnclassifiedProcess()
    {
        Assert.False(ProcessFamilyTraits.IsApplicabilityKnown(ProcessFamily.Unspecified));
        Assert.False(ProcessFamilyTraits.IsApplicabilityKnown(ProcessFamily.Other));
        Assert.True(ProcessFamilyTraits.IsApplicabilityKnown(ProcessFamily.Milling));
    }

    [Fact]
    public void ADefinition_DefaultsEveryOptionalFieldToAbsent()
    {
        var definition = new ProcessDefinition { Family = ProcessFamily.Milling, Name = "Fixture" };

        Assert.Null(definition.Variant);
        Assert.Null(definition.Description);
        Assert.Null(definition.TypicalApplications);
        Assert.False(definition.Capabilities.IsRecorded);
        Assert.Empty(definition.MaterialCompatibility);
        Assert.Empty(definition.ProductionScales);
        Assert.Empty(definition.Constraints);
        Assert.Equal(ProcessGroup.Machining, definition.Group);
    }

    [Fact]
    public void AConstraintMustDescribeSomething() =>
        Assert.Throws<ArgumentException>(() => new ProcessConstraint("  "));

    // ----------------------------------------------------------------
    // Catalogue
    // ----------------------------------------------------------------

    [Fact]
    public async Task TwoSourcesCanBothDescribeTheSameProcess()
    {
        // Different sources publish different bands for one named process,
        // and both are real reference data; the variant keeps both.
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-a", ProcessFixtures.Casting(variant: "Handbook A"), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync("prc-b", ProcessFixtures.Casting(variant: "Handbook B"), ProcessFixtures.SourcedProvenance());

        Assert.Equal(2, (await catalog.ListAsync()).Count);
        Assert.Equal("prc-a", (await catalog.FindByNameAsync(ProcessFamily.SandCasting, " fixture sand casting ", "Handbook A"))!.Id);
    }

    [Fact]
    public async Task RegisteringTheSameFamilyNameAndVariantTwice_IsRefused()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-0001", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance());

        var exception = await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync("prc-0002", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance()));

        Assert.Equal("prc-0001", exception.ExistingRecordId);
        Assert.Equal("Manufacturing", exception.Library);
        Assert.Contains("Fixture sand casting", exception.Message);
    }

    [Fact]
    public async Task TheSameNameUnderADifferentFamily_IsADifferentProcess()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-0001", ProcessFixtures.Casting("Fixture process"), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync(
            "prc-0002",
            ProcessFixtures.Machining("Fixture process"),
            ProcessFixtures.SourcedProvenance());

        Assert.Equal(2, (await catalog.ListAsync()).Count);
    }

    [Fact]
    public async Task EveryProcessRecordIsBackedByOneDocumentOfItsOwnKind()
    {
        var catalog = ProcessFixtures.BuildCatalog(out var documentStore, out _);
        var record = await catalog.RegisterAsync("prc-0001", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance());
        var document = await documentStore.FindAsync(record.UnderlyingDocumentId);

        // Deliberately not "ManufacturingOperation" — that Kind is the
        // workspace's own operation on a real part, not a reference
        // description of a process in general.
        Assert.Equal("ManufacturingProcessReference", document!.Kind);
        Assert.Equal(ProcessCatalog.ProcessDocumentKind, document.Kind);
    }

    // ----------------------------------------------------------------
    // Search
    // ----------------------------------------------------------------

    [Fact]
    public async Task Search_FindsEveryCastingProcessWithoutEnumeratingEachOne()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-cast", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync("prc-mill", ProcessFixtures.Machining(), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync("prc-heat", ProcessFixtures.HeatTreatment(), ProcessFixtures.SourcedProvenance());

        Assert.Equal(
            ["prc-cast"],
            (await catalog.SearchAsync(new ProcessQuery { Groups = [ProcessGroup.Casting] })).Select(p => p.Id));
    }

    [Fact]
    public async Task Search_NeverReturnsAProcessASourceSaidTheMaterialIsUnsuitableFor()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-cast", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync("prc-mill", ProcessFixtures.Machining(), ProcessFixtures.SourcedProvenance());

        // The casting fixture explicitly records thermoplastic as not
        // suitable, so it must never come back as a way to process one.
        Assert.Empty(await catalog.SearchAsync(new ProcessQuery { ProcessesMaterialFamily = MaterialFamily.Thermoplastic }));
        Assert.Equal(
            ["prc-cast"],
            (await catalog.SearchAsync(new ProcessQuery { ProcessesMaterialFamily = MaterialFamily.Aluminium })).Select(p => p.Id));

        // A conditionally suitable pairing is still a pairing.
        Assert.Equal(
            ["prc-mill"],
            (await catalog.SearchAsync(new ProcessQuery { ProcessesMaterialFamily = MaterialFamily.Ceramic })).Select(p => p.Id));
    }

    [Fact]
    public async Task Search_AsksWhetherASourcesOwnBandCoversAValue()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-cast", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync("prc-mill", ProcessFixtures.Machining(), ProcessFixtures.SourcedProvenance());

        Assert.Equal(
            ["prc-mill"],
            (await catalog.SearchAsync(new ProcessQuery { ToleranceBandContains = ProcessFixtures.Millimetres(0.05) })).Select(p => p.Id));
        Assert.Equal(
            ["prc-cast"],
            (await catalog.SearchAsync(new ProcessQuery { ToleranceBandContains = ProcessFixtures.Millimetres(1.0) })).Select(p => p.Id));
    }

    [Fact]
    public async Task Search_NeverTreatsAnUnpublishedBandAsUnbounded()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-heat", ProcessFixtures.HeatTreatment(), ProcessFixtures.SourcedProvenance());

        // A heat treatment publishes no tolerance band at all; it is not a
        // process whose band contains anything.
        Assert.Empty(await catalog.SearchAsync(new ProcessQuery { ToleranceBandContains = ProcessFixtures.Millimetres(1.0) }));
    }

    [Fact]
    public async Task Search_MatchesOnProductionScaleConstraintKindAndValidationState()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-cast", ProcessFixtures.Casting(), ProcessFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("prc-heat", ProcessFixtures.HeatTreatment(), ProcessFixtures.SourcedProvenance());
        await ProcessFixtures.ReleaseAsync(catalog, "prc-cast");

        Assert.Equal(
            ["prc-heat"],
            (await catalog.SearchAsync(new ProcessQuery { ProductionScales = [ProductionScale.HighVolume] })).Select(p => p.Id));
        Assert.Equal(
            ["prc-cast"],
            (await catalog.SearchAsync(new ProcessQuery { ConstraintKinds = [ProcessConstraintKind.Geometric] })).Select(p => p.Id));
        Assert.Equal(
            ["prc-cast"],
            (await catalog.SearchAsync(new ProcessQuery { ValidationStates = [ReferenceValidationState.Released] })).Select(p => p.Id));
    }

    [Fact]
    public async Task Search_MatchesOnAnOpenEndedBand()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-mill", ProcessFixtures.Machining(), ProcessFixtures.SourcedProvenance());

        // The machining fixture's minimum feature size has no upper end, so
        // any value above the minimum is inside it.
        Assert.Single(await catalog.SearchAsync(new ProcessQuery { PartSizeBandContains = ProcessFixtures.Millimetres(100) }));
        Assert.Empty(await catalog.SearchAsync(new ProcessQuery { PartSizeBandContains = ProcessFixtures.Millimetres(5000) }));
    }

    // ----------------------------------------------------------------
    // Comparison
    // ----------------------------------------------------------------

    [Fact]
    public async Task Comparison_ReportsADraftAngleOnAMachiningProcessAsNotApplicable()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-a-cast", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync("prc-b-mill", ProcessFixtures.Machining(), ProcessFixtures.SourcedProvenance());

        var comparison = ProcessComparer.Compare(await catalog.ListAsync());
        var draft = comparison.Row(ProcessComparisonProperties.DraftAngle)!;
        var wall = comparison.Row(ProcessComparisonProperties.WallThickness)!;

        Assert.Equal(ReferencePropertyAvailability.Recorded, draft.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, draft.Cells[1].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, wall.Cells[1].Availability);
        Assert.False(comparison.IsSingleFamily);
    }

    [Fact]
    public async Task Comparison_ShowsABandAsItsOwnTwoEndsAndOrdersItByTheLower()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-a-mill", ProcessFixtures.Machining(), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync("prc-b-cast", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance());

        var tolerance = ProcessComparer.Compare(await catalog.ListAsync()).Row(ProcessComparisonProperties.AchievableTolerance)!;

        Assert.Contains(" to ", tolerance.Cells[0].Display);
        Assert.True(tolerance.Cells[0].CanonicalValue < tolerance.Cells[1].CanonicalValue);
    }

    [Fact]
    public async Task Comparison_DistinguishesABandNobodyPublishedFromOneThatCannotExist()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync("prc-a-cast", ProcessFixtures.Casting(), ProcessFixtures.SourcedProvenance());
        await catalog.RegisterAsync("prc-b-heat", ProcessFixtures.HeatTreatment(), ProcessFixtures.SourcedProvenance());

        var comparison = ProcessComparer.Compare(await catalog.ListAsync());
        var cycle = comparison.Row(ProcessComparisonProperties.CycleTime)!;
        var roughness = comparison.Row(ProcessComparisonProperties.SurfaceRoughness)!;

        Assert.Equal(ReferencePropertyAvailability.NotRecorded, cycle.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.Recorded, roughness.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, roughness.Cells[1].Availability);
    }

    [Fact]
    public async Task Comparison_ReportsAZeroCountAsRecorded()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        await catalog.RegisterAsync(
            "prc-0001",
            ProcessFixtures.HeatTreatment() with { Constraints = [] },
            ProcessFixtures.SourcedProvenance());

        var cell = ProcessComparer.Compare(await catalog.ListAsync()).Row(ProcessComparisonProperties.ConstraintCount)!.Cells[0];

        Assert.Equal(ReferencePropertyAvailability.Recorded, cell.Availability);
        Assert.Equal(0, cell.CanonicalValue);
    }

    // ----------------------------------------------------------------
    // Validation
    // ----------------------------------------------------------------

    [Fact]
    public async Task EveryCoherentFixture_PassesEveryRule()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        await catalog.RegisterAsync("prc-cast", ProcessFixtures.Casting(), ProcessFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("prc-mill", ProcessFixtures.Machining(), ProcessFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("prc-heat", ProcessFixtures.HeatTreatment(), ProcessFixtures.VerifiedProvenance());

        var report = await validator.ValidateLibraryAsync();

        Assert.Equal(3, report.RecordsExamined);
        Assert.True(
            report.Findings.Count == 0,
            string.Join("; ", report.Findings.SelectMany(f => f.Result.Errors.Concat(f.Result.Warnings)).Select(d => $"{d.Code}: {d.Message}")));
    }

    [Fact]
    public async Task ACapabilityRecordedForAProcessThatCannotHaveIt_IsAnError()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var drafted = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                Capabilities = ProcessFixtures.Machining().Capabilities with
                {
                    DraftAngle = ProcessFixtures.AngleBand(ProcessFixtures.Degrees(1), ProcessFixtures.Degrees(3)),
                },
            },
            ProcessFixtures.SourcedProvenance());

        var walled = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                Capabilities = ProcessFixtures.Machining().Capabilities with
                {
                    WallThickness = ProcessFixtures.LengthBand(ProcessFixtures.Millimetres(1), ProcessFixtures.Millimetres(10)),
                },
            },
            ProcessFixtures.SourcedProvenance());

        var roughened = await validator.ValidateDefinitionAsync(
            ProcessFixtures.HeatTreatment() with
            {
                Capabilities = ProcessFixtures.HeatTreatment().Capabilities with
                {
                    SurfaceRoughness = ProcessFixtures.LengthBand(ProcessFixtures.Micrometres(1), ProcessFixtures.Micrometres(5)),
                },
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasError(drafted, ProcessValidationRules.CapabilityNotApplicableToFamily));
        Assert.True(HasError(walled, ProcessValidationRules.CapabilityNotApplicableToFamily));
        Assert.True(HasError(roughened, ProcessValidationRules.CapabilityNotApplicableToFamily));
    }

    [Fact]
    public async Task AnInvertedCapabilityBand_IsAnError()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                Capabilities = ProcessFixtures.Machining().Capabilities with
                {
                    AchievableTolerance = ProcessFixtures.LengthBand(ProcessFixtures.Millimetres(0.2), ProcessFixtures.Millimetres(0.01)),
                },
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasError(result, ReferenceValidationRules.RangeInverted));
    }

    [Fact]
    public async Task ANonPositiveCapabilityEnd_IsAnError()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                Capabilities = ProcessFixtures.Machining().Capabilities with
                {
                    AchievableTolerance = ProcessFixtures.LengthBand(ProcessFixtures.Millimetres(0), ProcessFixtures.Millimetres(0.2)),
                },
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasError(result, ProcessValidationRules.CapabilityMustBePositive));
    }

    [Fact]
    public async Task ATemperatureBandMayLegitimatelyReachZeroOrBelow()
    {
        // The one capability whose ends are not required to be positive.
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.HeatTreatment() with
            {
                Capabilities = ProcessFixtures.HeatTreatment().Capabilities with
                {
                    ProcessTemperature = ProcessFixtures.TemperatureBand(
                        ProcessFixtures.DegreesCelsius(-40),
                        ProcessFixtures.DegreesCelsius(200)),
                },
            },
            ProcessFixtures.SourcedProvenance());

        Assert.False(HasError(result, ProcessValidationRules.CapabilityMustBePositive));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ACapabilityWithNoOriginOrNoConditions_IsWarnedAbout()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                Capabilities = ProcessFixtures.Machining().Capabilities with
                {
                    AchievableTolerance = new ReferenceRange<Core.UnitsAndQuantities.Length>(
                        ProcessFixtures.Millimetres(0.01),
                        ProcessFixtures.Millimetres(0.2),
                        ReferenceValueOrigin.Unknown),
                },
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ProcessValidationRules.CapabilityOriginShouldBeRecorded));
        Assert.True(HasWarning(result, ProcessValidationRules.CapabilityConditionsShouldBeRecorded));
    }

    [Fact]
    public async Task ContradictoryMaterialCompatibility_IsAnError()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                MaterialCompatibility =
                [
                    new ProcessMaterialCompatibility(MaterialFamily.Steel, ProcessMaterialSuitability.Suitable, Origin: ReferenceValueOrigin.EngineeringReference),
                    new ProcessMaterialCompatibility(MaterialFamily.Steel, ProcessMaterialSuitability.NotSuitable, Origin: ReferenceValueOrigin.EngineeringReference),
                ],
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasError(result, ProcessValidationRules.ContradictoryCompatibility));
    }

    [Fact]
    public async Task ARepeatedMaterialCompatibilityEntry_IsAWarning()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                MaterialCompatibility =
                [
                    new ProcessMaterialCompatibility(MaterialFamily.Steel, ProcessMaterialSuitability.Suitable, Origin: ReferenceValueOrigin.EngineeringReference),
                    new ProcessMaterialCompatibility(MaterialFamily.Steel, ProcessMaterialSuitability.Suitable, Origin: ReferenceValueOrigin.EngineeringReference),
                ],
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ProcessValidationRules.DuplicateCompatibilityEntry));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task AMaterialCompatibilityEntryThatNamesNothing_IsAnError()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with { MaterialCompatibility = [new ProcessMaterialCompatibility()] },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasError(result, ProcessValidationRules.CompatibilityMustNameAMaterial));
    }

    [Fact]
    public async Task AConditionalPairingWithNoConditions_IsWarnedAbout()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                MaterialCompatibility =
                [
                    new ProcessMaterialCompatibility(
                        MaterialFamily.Ceramic,
                        ProcessMaterialSuitability.ConditionallySuitable,
                        Origin: ReferenceValueOrigin.EngineeringReference),
                ],
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ProcessValidationRules.ConditionalCompatibilityNeedsConditions));
    }

    [Fact]
    public async Task ACompatibilityClaimTempestOSMade_IsFlaggedAsNotSourceData()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                MaterialCompatibility =
                [
                    new ProcessMaterialCompatibility(
                        MaterialFamily.Steel,
                        ProcessMaterialSuitability.Suitable,
                        Origin: ReferenceValueOrigin.DerivedByTempestOS),
                ],
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ReferenceValidationRules.DerivedValuePresent));
    }

    [Fact]
    public async Task AnUnspecifiedProductionScaleAlongsideARealOne_IsAnError()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                ProductionScales = [ProductionScale.Unspecified, ProductionScale.HighVolume],
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasError(result, ProcessValidationRules.UnspecifiedProductionScaleAlongsideAReal));
    }

    [Fact]
    public async Task AnEmptyRecord_IsReportedAsDataGapsRatherThanAsFacts()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            new ProcessDefinition { Family = ProcessFamily.Milling, Name = "Fixture bare process" },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ProcessValidationRules.NoCapabilityRecorded));
        Assert.True(HasWarning(result, ProcessValidationRules.NoMaterialCompatibilityRecorded));
        Assert.True(HasWarning(result, ProcessValidationRules.ProductionScaleShouldBeRecorded));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task AProcessWithNoFamily_CannotBeInterpretedAndIsAnError()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            new ProcessDefinition { Family = ProcessFamily.Unspecified, Name = "Fixture" },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasError(result, ProcessValidationRules.FamilyMustBeStated));
    }

    [Fact]
    public async Task AProcessClassifiedOther_MustCarryTheSourcesOwnWording()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with { Family = ProcessFamily.Other },
            ProcessFixtures.SourcedProvenance());

        var repaired = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                Family = ProcessFamily.Other,
                SourceClassification = "The source called it a 'fixture hybrid process'.",
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasError(result, ProcessValidationRules.OtherFamilyNeedsSourceClassification));
        Assert.False(HasError(repaired, ProcessValidationRules.OtherFamilyNeedsSourceClassification));
    }

    [Fact]
    public async Task AnUnclassifiedFamily_IsNeverToldItsCapabilityDoesNotApply()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Casting() with
            {
                Family = ProcessFamily.Other,
                SourceClassification = "The source called it a 'fixture hybrid process'.",
            },
            ProcessFixtures.SourcedProvenance());

        Assert.False(HasError(result, ProcessValidationRules.CapabilityNotApplicableToFamily));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task AConstraintWithNoKind_IsWarnedAbout()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            ProcessFixtures.Machining() with
            {
                Constraints = [new ProcessConstraint("A fixture constraint with no kind stated.")],
            },
            ProcessFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ProcessValidationRules.ConstraintKindShouldBeStated));
    }

    // ----------------------------------------------------------------
    // Integration with the rest of Group A
    // ----------------------------------------------------------------

    [Fact]
    public async Task ANamedMaterialIsConfirmedAgainstTheCanonicalMaterialsCatalogue()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documentStore, persistenceStore);
        var processes = new ProcessCatalog(documentStore, persistenceStore);
        var validator = new ProcessValidationService(processes, materials);

        await materials.RegisterAsync(
            "mat-fixture-alloy",
            new MaterialDefinition { Name = "Fixture casting alloy", Family = MaterialFamily.Aluminium },
            ProcessFixtures.SourcedProvenance());

        await processes.RegisterAsync(
            "prc-linked",
            ProcessFixtures.Casting() with
            {
                MaterialCompatibility =
                [
                    new ProcessMaterialCompatibility(
                        MaterialFamily.Aluminium,
                        ProcessMaterialSuitability.Suitable,
                        MaterialId: "mat-fixture-alloy",
                        Origin: ReferenceValueOrigin.EngineeringReference),
                ],
            },
            ProcessFixtures.VerifiedProvenance());

        await processes.RegisterAsync(
            "prc-dangling",
            ProcessFixtures.Casting(variant: "Handbook B") with
            {
                MaterialCompatibility =
                [
                    new ProcessMaterialCompatibility(
                        MaterialFamily.Aluminium,
                        ProcessMaterialSuitability.Suitable,
                        MaterialId: "mat-missing",
                        Origin: ReferenceValueOrigin.EngineeringReference),
                ],
            },
            ProcessFixtures.VerifiedProvenance());

        Assert.True((await validator.ValidateAsync("prc-linked")).IsValid);
        Assert.True(HasWarning(await validator.ValidateAsync("prc-dangling"), ReferenceValidationRules.MaterialReferenceUnresolved));
    }

    [Fact]
    public async Task AFullLifecycleFromDraftToSupersession_IsTraceableEndToEnd()
    {
        var catalog = ProcessFixtures.BuildCatalog();
        var validator = new ProcessValidationService(catalog);

        await catalog.RegisterAsync("prc-2018", ProcessFixtures.Casting(variant: "Handbook 2018"), ProcessFixtures.VerifiedProvenance());
        Assert.True((await validator.ValidateAsync("prc-2018")).IsValid);
        await ProcessFixtures.ReleaseAsync(catalog, "prc-2018");

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync("prc-2018", ProcessFixtures.Casting(), ProcessFixtures.VerifiedProvenance(), "Refused."));

        await catalog.RegisterAsync("prc-2026", ProcessFixtures.Casting(variant: "Handbook 2026"), ProcessFixtures.VerifiedProvenance());
        await catalog.SupersedeAsync("prc-2018", "prc-2026", "Superseded by the 2026 fixture handbook.");

        var superseded = await catalog.FindAsync("prc-2018");

        Assert.Equal(ReferenceValidationState.Superseded, superseded!.ValidationState);
        Assert.Equal("prc-2026", superseded.SupersededByRecordId);
        Assert.Equal("Handbook 2018", (await catalog.GetRevisionAsync("prc-2018", 1)).Definition.Variant);
    }
}
