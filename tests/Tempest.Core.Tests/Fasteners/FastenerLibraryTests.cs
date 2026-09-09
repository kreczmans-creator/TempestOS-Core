using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Fasteners;

// A3: the fastener-specific half of the library. The shared machinery it
// sits on (registration, revision, lifecycle, supersession, hostile data)
// is tested once, in ReferenceDataCatalogTests, and is not restated here.
public class FastenerLibraryTests
{
    private static bool HasError(IValidationResult result, string code) => result.Errors.Any(d => d.Code == code);

    private static bool HasWarning(IValidationResult result, string code) => result.Warnings.Any(d => d.Code == code);

    // ----------------------------------------------------------------
    // Model and taxonomy
    // ----------------------------------------------------------------

    [Fact]
    public void ADefinition_DefaultsEveryOptionalFieldToAbsent()
    {
        var definition = new FastenerDefinition { Family = FastenerFamily.Bolt, Designation = "FX-1" };

        Assert.Null(definition.Manufacturer);
        Assert.Null(definition.ManufacturerPartNumber);
        Assert.Null(definition.Thread);
        Assert.Null(definition.Finish);
        Assert.Null(definition.MaterialId);
        Assert.Null(definition.MaterialDesignation);
        Assert.Null(definition.EffectiveDate);
        Assert.Equal(FastenerHeadType.Unspecified, definition.HeadType);
        Assert.Equal(FastenerDriveType.Unspecified, definition.DriveType);
        Assert.False(definition.Dimensions.IsRecorded);
        Assert.False(definition.Mechanical.IsRecorded);
        Assert.Empty(definition.TorqueReferences);
        Assert.Empty(definition.Standards);
    }

    [Theory]
    [InlineData(FastenerFamily.Bolt, true, false)]
    [InlineData(FastenerFamily.Stud, true, false)]
    [InlineData(FastenerFamily.Nut, false, true)]
    [InlineData(FastenerFamily.ThreadedInsert, false, true)]
    [InlineData(FastenerFamily.Washer, false, false)]
    public void FamilyTraits_KnowWhichWayEachFamilyIsThreaded(FastenerFamily family, bool external, bool internalThread)
    {
        Assert.Equal(external, FastenerFamilyTraits.IsExternallyThreaded(family));
        Assert.Equal(internalThread, FastenerFamilyTraits.IsInternallyThreaded(family));
        Assert.Equal(external || internalThread, FastenerFamilyTraits.IsThreaded(family));
    }

    [Fact]
    public void FamilyTraits_KnowThatAStudAndASetScrewHaveNoHead()
    {
        Assert.True(FastenerFamilyTraits.HasHead(FastenerFamily.Bolt));
        Assert.False(FastenerFamilyTraits.HasHead(FastenerFamily.Stud));
        Assert.False(FastenerFamilyTraits.HasHead(FastenerFamily.SetScrew));

        // A set screw is headless but still driven — the two questions are
        // genuinely different.
        Assert.True(FastenerFamilyTraits.HasDriveFeature(FastenerFamily.SetScrew));
    }

    [Fact]
    public void FamilyTraits_RefuseToSpeakForAnUnclassifiedFastener()
    {
        Assert.False(FastenerFamilyTraits.IsApplicabilityKnown(FastenerFamily.Unspecified));
        Assert.False(FastenerFamilyTraits.IsApplicabilityKnown(FastenerFamily.Other));
        Assert.True(FastenerFamilyTraits.IsApplicabilityKnown(FastenerFamily.Washer));
    }

    [Fact]
    public void AThreadSpecification_RequiresTheSourcesOwnDesignationAndNothingElse()
    {
        Assert.Throws<ArgumentException>(() => new ThreadSpecification("  "));

        // A source that quotes only a designation has still told us
        // something exact; the numbers are never invented from it.
        var thread = new ThreadSpecification("FX10");

        Assert.Null(thread.NominalDiameter);
        Assert.Null(thread.Pitch);
        Assert.Equal(ThreadHandedness.Unspecified, thread.Handedness);
    }

    [Fact]
    public void Hardness_KeepsItsScaleAttachedAndIsNeverADimensionedQuantity()
    {
        // Vickers, Rockwell and Brinell numbers are not one quantity in
        // three units; letting the units framework convert between them
        // would produce a plausible-looking wrong answer.
        Assert.Throws<ArgumentException>(() => new FastenerHardness("  "));

        var hardness = new FastenerHardness("FX-scale", 200, 250);

        Assert.True(hardness.IsRecorded);
        Assert.False(hardness.IsInverted);
        Assert.True(new FastenerHardness("FX-scale", 250, 200).IsInverted);
    }

    [Fact]
    public void TheIdentityKey_UsesThePartNumberWhereThereIsOneAndTheDesignationOtherwise()
    {
        var generic = FastenerFixtures.HexBolt() with { Manufacturer = null };
        var branded = FastenerFixtures.HexBolt();
        var ordered = branded with { ManufacturerPartNumber = "PN-1" };

        Assert.NotEqual(generic.IdentityKey, branded.IdentityKey);
        Assert.NotEqual(branded.IdentityKey, ordered.IdentityKey);
        Assert.Equal(branded.IdentityKey, (branded with { Designation = " fx-bolt-1 " }).IdentityKey);
    }

    // ----------------------------------------------------------------
    // Catalogue
    // ----------------------------------------------------------------

    [Fact]
    public async Task AFastenerIsFindableByItsDesignationAndByItsPartNumber()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-0001", FastenerFixtures.HexBolt(), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync(
            "fst-0002",
            FastenerFixtures.HexBolt("FX-BOLT-2") with { ManufacturerPartNumber = "PN-2" },
            FastenerFixtures.SourcedProvenance());

        Assert.Equal("fst-0001", (await catalog.FindByDesignationAsync(" fx-bolt-1 ", "Fixture Fasteners"))!.Id);
        Assert.Equal("fst-0002", (await catalog.FindByPartNumberAsync("Fixture Fasteners", " pn-2 "))!.Id);
        Assert.Null(await catalog.FindByDesignationAsync("FX-BOLT-1"));
    }

    [Fact]
    public async Task RegisteringTheSameManufacturerAndPartNumberTwice_IsRefused()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var definition = FastenerFixtures.HexBolt() with { ManufacturerPartNumber = "PN-1" };
        await catalog.RegisterAsync("fst-0001", definition, FastenerFixtures.SourcedProvenance());

        var exception = await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync("fst-0002", definition with { Designation = "FX-BOLT-9" }, FastenerFixtures.SourcedProvenance()));

        Assert.Equal("fst-0001", exception.ExistingRecordId);
        Assert.Equal("Fasteners", exception.Library);
        Assert.Contains("PN-1", exception.Message);
    }

    [Fact]
    public async Task TheSameDesignationFromTwoManufacturers_AreTwoFasteners()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-0001", FastenerFixtures.HexBolt(), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync(
            "fst-0002",
            FastenerFixtures.HexBolt() with { Manufacturer = "Other Fixture Fasteners" },
            FastenerFixtures.SourcedProvenance());

        Assert.Equal(2, (await catalog.ListAsync()).Count);
    }

    [Fact]
    public async Task ReviseAsync_MovesTheIdentityIndexWithTheRecord()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-0001", FastenerFixtures.HexBolt("FX-BOLT-1"), FastenerFixtures.SourcedProvenance());

        await catalog.ReviseAsync(
            "fst-0001",
            FastenerFixtures.HexBolt("FX-BOLT-2"),
            FastenerFixtures.SourcedProvenance(),
            "Designation mis-transcribed.");

        Assert.Null(await catalog.FindByDesignationAsync("FX-BOLT-1", "Fixture Fasteners"));
        Assert.Equal("fst-0001", (await catalog.FindByDesignationAsync("FX-BOLT-2", "Fixture Fasteners"))!.Id);
    }

    // ----------------------------------------------------------------
    // Search
    // ----------------------------------------------------------------

    [Fact]
    public async Task Search_MatchesOnFamilyThreadHeadDriveAndClass()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-bolt", FastenerFixtures.HexBolt(), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync("fst-nut", FastenerFixtures.Nut(), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync("fst-washer", FastenerFixtures.Washer(), FastenerFixtures.SourcedProvenance());

        Assert.Equal(
            ["fst-bolt"],
            (await catalog.SearchAsync(new FastenerQuery { Families = [FastenerFamily.Bolt] })).Select(f => f.Id));
        Assert.Equal(
            ["fst-bolt", "fst-nut"],
            (await catalog.SearchAsync(new FastenerQuery { ThreadDesignation = " fx10 " })).Select(f => f.Id).Order());
        Assert.Equal(
            ["fst-bolt"],
            (await catalog.SearchAsync(new FastenerQuery { HeadTypes = [FastenerHeadType.Hexagon] })).Select(f => f.Id));
        Assert.Equal(
            ["fst-bolt", "fst-nut"],
            (await catalog.SearchAsync(new FastenerQuery { PropertyClass = "FX-A" })).Select(f => f.Id).Order());
    }

    [Fact]
    public async Task Search_NeverMatchesAnUnthreadedFastenerOnAThreadCriterion()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-washer", FastenerFixtures.Washer(), FastenerFixtures.SourcedProvenance());

        Assert.Empty(await catalog.SearchAsync(new FastenerQuery { ThreadSystems = [ThreadSystem.MetricCoarse] }));
        Assert.Empty(await catalog.SearchAsync(new FastenerQuery { Handedness = ThreadHandedness.RightHand }));
        Assert.Empty(await catalog.SearchAsync(new FastenerQuery { NominalDiameterMinimum = FastenerFixtures.Millimetres(0) }));
    }

    [Fact]
    public async Task Search_ComparesDimensionalBoundsInTheBaseUnitWhateverTheSourceQuoted()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-m8", FastenerFixtures.HexBolt("FX-BOLT-8", 8), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync("fst-m10", FastenerFixtures.HexBolt("FX-BOLT-10", 10), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync(
            "fst-inch",
            FastenerFixtures.HexBolt("FX-BOLT-IN") with
            {
                Thread = new ThreadSpecification(
                    "FXI",
                    ThreadSystem.UnifiedCoarse,
                    NominalDiameter: FastenerFixtures.Sourced(new Quantity<Length>(0.5, LengthUnits.Inch)),
                    Handedness: ThreadHandedness.RightHand),
            },
            FastenerFixtures.SourcedProvenance());

        var matched = await catalog.SearchAsync(new FastenerQuery
        {
            NominalDiameterMinimum = FastenerFixtures.Millimetres(9),
            NominalDiameterMaximum = FastenerFixtures.Millimetres(13),
        });

        // 0.5 in is 12.7 mm, so the inch-quoted bolt is in range and the
        // 8 mm one is not.
        Assert.Equal(["fst-inch", "fst-m10"], matched.Select(f => f.Id).Order());
    }

    [Fact]
    public async Task Search_FindsFastenersByWhetherTheyRecordAPublishedTorque()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-torque", FastenerFixtures.HexBolt(), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync(
            "fst-no-torque",
            FastenerFixtures.HexBolt("FX-BOLT-2") with { TorqueReferences = [] },
            FastenerFixtures.SourcedProvenance());

        Assert.Equal(
            ["fst-torque"],
            (await catalog.SearchAsync(new FastenerQuery { RecordsTighteningTorque = true })).Select(f => f.Id));
        Assert.Equal(
            ["fst-no-torque"],
            (await catalog.SearchAsync(new FastenerQuery { RecordsTighteningTorque = false })).Select(f => f.Id));
    }

    [Fact]
    public async Task Search_FiltersByValidationStateAndLinkedMaterial()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync(
            "fst-linked",
            FastenerFixtures.HexBolt() with { MaterialId = "mat-1" },
            FastenerFixtures.VerifiedProvenance());
        await catalog.RegisterAsync("fst-plain", FastenerFixtures.HexBolt("FX-BOLT-2"), FastenerFixtures.SourcedProvenance());
        await FastenerFixtures.ReleaseAsync(catalog, "fst-linked");

        Assert.Equal(
            ["fst-linked"],
            (await catalog.SearchAsync(new FastenerQuery { MaterialId = "mat-1" })).Select(f => f.Id));
        Assert.Equal(
            ["fst-linked"],
            (await catalog.SearchAsync(new FastenerQuery { ValidationStates = [ReferenceValidationState.Released] })).Select(f => f.Id));
    }

    // ----------------------------------------------------------------
    // Comparison
    // ----------------------------------------------------------------

    [Fact]
    public async Task Comparison_ReportsAThreadOnAWasherAsNotApplicableRatherThanAsAGap()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-bolt", FastenerFixtures.HexBolt(), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync("fst-washer", FastenerFixtures.Washer(), FastenerFixtures.SourcedProvenance());

        var comparison = FastenerComparer.Compare(await catalog.ListAsync());
        var thread = comparison.Row(FastenerComparisonProperties.ThreadDesignation)!;
        var head = comparison.Row(FastenerComparisonProperties.HeadType)!;

        Assert.Equal(ReferencePropertyAvailability.Recorded, thread.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, thread.Cells[1].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, head.Cells[1].Availability);
        Assert.False(comparison.IsSingleFamily);
    }

    [Fact]
    public async Task Comparison_DistinguishesAThreadNobodyRecordedFromOneThatCannotExist()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync(
            "fst-bolt-no-thread",
            FastenerFixtures.HexBolt() with { Thread = null },
            FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync("fst-washer", FastenerFixtures.Washer(), FastenerFixtures.SourcedProvenance());

        var cells = FastenerComparer.Compare(await catalog.ListAsync())
            .Row(FastenerComparisonProperties.ThreadDesignation)!.Cells;

        Assert.Equal(ReferencePropertyAvailability.NotRecorded, cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, cells[1].Availability);
    }

    [Fact]
    public async Task Comparison_OffersNoCanonicalValueForHardnessSoScalesCannotBeCompared()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync(
            "fst-0001",
            FastenerFixtures.HexBolt() with
            {
                Mechanical = FastenerFixtures.HexBolt().Mechanical with { Hardness = new FastenerHardness("FX-scale", 200, 250) },
            },
            FastenerFixtures.SourcedProvenance());

        var cell = FastenerComparer.Compare(await catalog.ListAsync()).Row(FastenerComparisonProperties.Hardness)!.Cells[0];

        Assert.Equal(ReferencePropertyAvailability.Recorded, cell.Availability);
        Assert.Contains("FX-scale", cell.Display);
        Assert.Null(cell.CanonicalValue);
    }

    [Fact]
    public async Task Comparison_OrdersDimensionedValuesByTheirCanonicalValue()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        await catalog.RegisterAsync("fst-a", FastenerFixtures.HexBolt("FX-BOLT-8", 8), FastenerFixtures.SourcedProvenance());
        await catalog.RegisterAsync("fst-b", FastenerFixtures.HexBolt("FX-BOLT-12", 12), FastenerFixtures.SourcedProvenance());

        var diameters = FastenerComparer.Compare(await catalog.ListAsync())
            .Row(FastenerComparisonProperties.NominalDiameter)!;

        // The comparison presents records in the order it was given them —
        // ascending record Id, from ListAsync — and never reorders by value.
        Assert.Equal([0.008, 0.012], diameters.Cells.Select(c => Math.Round(c.CanonicalValue!.Value, 6)));
    }

    // ----------------------------------------------------------------
    // Validation
    // ----------------------------------------------------------------

    [Fact]
    public async Task ACompleteFixtureFastener_PassesEveryRule()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);
        await catalog.RegisterAsync("fst-0001", FastenerFixtures.HexBolt(), FastenerFixtures.VerifiedProvenance());

        var result = await validator.ValidateAsync("fst-0001");

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task AFastenerWithNoFamily_CannotBeInterpretedAndIsAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            new FastenerDefinition { Family = FastenerFamily.Unspecified, Designation = "FX-1" },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, FastenerValidationRules.FamilyMustBeStated));
    }

    [Theory]
    [InlineData(FastenerFamily.Washer, FastenerValidationRules.ThreadNotApplicableToFamily)]
    [InlineData(FastenerFamily.RetainingRing, FastenerValidationRules.ThreadNotApplicableToFamily)]
    public async Task AThreadOnAnUnthreadedFamily_IsAnError(FastenerFamily family, string code)
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                Family = family,
                HeadType = FastenerHeadType.None,
                DriveType = FastenerDriveType.None,
                TorqueReferences = [],
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, code));
    }

    [Fact]
    public async Task AThreadedFamilyWithNoThreadRecorded_IsAGapNotAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with { Thread = null },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, FastenerValidationRules.ThreadMustBeRecordedForAThreadedFamily));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task AHeadOrDriveOnAFamilyThatHasNone_IsAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var headed = await validator.ValidateDefinitionAsync(
            FastenerFixtures.Washer() with { HeadType = FastenerHeadType.Hexagon },
            FastenerFixtures.SourcedProvenance());
        var driven = await validator.ValidateDefinitionAsync(
            FastenerFixtures.Washer() with { DriveType = FastenerDriveType.InternalHexagon },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(headed, FastenerValidationRules.HeadNotApplicableToFamily));
        Assert.True(HasError(driven, FastenerValidationRules.DriveNotApplicableToFamily));
    }

    [Fact]
    public async Task AnUnspecifiedHeadOrDrive_IsNeverTreatedAsAnInapplicableOne()
    {
        // "Not recorded" and "the family has none" are different claims,
        // and only the second can contradict the family.
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(FastenerFixtures.Washer(), FastenerFixtures.SourcedProvenance());

        Assert.False(HasError(result, FastenerValidationRules.HeadNotApplicableToFamily));
        Assert.False(HasError(result, FastenerValidationRules.DriveNotApplicableToFamily));
    }

    [Fact]
    public async Task AThreadWithAPitchNoSmallerThanItsDiameter_IsAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                Thread = new ThreadSpecification(
                    "FX10",
                    ThreadSystem.MetricCoarse,
                    NominalDiameter: FastenerFixtures.Sourced(FastenerFixtures.Millimetres(10)),
                    Pitch: FastenerFixtures.Sourced(FastenerFixtures.Millimetres(10)),
                    Handedness: ThreadHandedness.RightHand),
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, FastenerValidationRules.PitchExceedsNominalDiameter));
    }

    [Fact]
    public async Task AThreadWithNoRecordedHandedness_IsWarnedAbout()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with { Thread = new ThreadSpecification("FX10", ThreadSystem.MetricCoarse) },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, FastenerValidationRules.ThreadHandednessShouldBeRecorded));
    }

    [Fact]
    public async Task AWidthAcrossCornersNoGreaterThanTheFlats_IsGeometricallyImpossible()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                Dimensions = FastenerFixtures.HexBolt().Dimensions with
                {
                    WidthAcrossCorners = FastenerFixtures.Sourced(FastenerFixtures.Millimetres(16)),
                },
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, FastenerValidationRules.WidthAcrossCornersNotGreaterThanFlats));
    }

    [Fact]
    public async Task AWasherWithNoWall_IsAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.Washer() with
            {
                Dimensions = new FastenerDimensions(
                    InsideDiameter: FastenerFixtures.Sourced(FastenerFixtures.Millimetres(20)),
                    OutsideDiameter: FastenerFixtures.Sourced(FastenerFixtures.Millimetres(20))),
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, FastenerValidationRules.DimensionMustBePositive));
    }

    [Fact]
    public async Task AStrengthAboveTheTensileStrength_IsAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);
        var mechanical = FastenerFixtures.HexBolt().Mechanical;

        var yielding = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                Mechanical = mechanical with { YieldStrength = FastenerFixtures.Sourced(FastenerFixtures.Megapascals(900)) },
            },
            FastenerFixtures.SourcedProvenance());

        var proofing = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                Mechanical = mechanical with { ProofStrength = FastenerFixtures.Sourced(FastenerFixtures.Megapascals(900)) },
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(yielding, FastenerValidationRules.StrengthExceedsTensile));
        Assert.True(HasError(proofing, FastenerValidationRules.StrengthExceedsTensile));
    }

    [Fact]
    public async Task AProofLoadAboveTheBreakingLoad_IsAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                Mechanical = FastenerFixtures.HexBolt().Mechanical with
                {
                    ProofLoad = FastenerFixtures.Sourced(FastenerFixtures.Kilonewtons(40)),
                },
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, FastenerValidationRules.ProofLoadExceedsBreakingLoad));
    }

    [Fact]
    public async Task ATorqueRecordedWithoutItsConditions_IsWarnedAbout()
    {
        // A torque figure separated from the friction condition it was
        // published for is a number, not reference data.
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                TorqueReferences =
                [
                    new FastenerTorqueReference(FastenerFixtures.NewtonMetres(45), ReferenceValueOrigin.ManufacturerCatalogue),
                ],
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, FastenerValidationRules.TorqueReferenceStatesNoConditions));
    }

    [Fact]
    public async Task ATorqueTempestOSWorkedOutForItself_IsFlaggedAsNotReferenceData()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                TorqueReferences =
                [
                    new FastenerTorqueReference(
                        FastenerFixtures.NewtonMetres(45),
                        ReferenceValueOrigin.DerivedByTempestOS,
                        Conditions: "Assumed friction."),
                ],
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, ReferenceValidationRules.DerivedValuePresent));
    }

    [Fact]
    public async Task ATorqueOnAFamilyThatIsNotTightened_IsAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.Washer() with
            {
                TorqueReferences =
                [
                    new FastenerTorqueReference(FastenerFixtures.NewtonMetres(45), ReferenceValueOrigin.ManufacturerCatalogue, "Lubricated."),
                ],
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, FastenerValidationRules.TorqueNotApplicableToFamily));
    }

    [Fact]
    public async Task AnInvertedHardnessBand_IsAnError()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                Mechanical = FastenerFixtures.HexBolt().Mechanical with { Hardness = new FastenerHardness("FX-scale", 250, 200) },
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, FastenerValidationRules.HardnessBandInverted));
    }

    [Fact]
    public async Task APropertyClassOnAFamilyThatCarriesNone_IsWarnedAbout()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.Washer() with { Mechanical = new FastenerMechanicalProperties(PropertyClass: "FX-A") },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, FastenerValidationRules.PropertyClassNotApplicableToFamily));
    }

    [Fact]
    public async Task AMaterialNamedOnlyInText_IsWarnedAboutRatherThanAccepted()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with { MaterialDesignation = "Fixture bolt steel" },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, FastenerValidationRules.MaterialShouldBeLinked));
    }

    [Fact]
    public async Task AnOtherClassificationAnywhere_MustCarryTheSourcesOwnWording()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with { HeadType = FastenerHeadType.Other },
            FastenerFixtures.SourcedProvenance());

        var repaired = await validator.ValidateDefinitionAsync(
            FastenerFixtures.HexBolt() with
            {
                HeadType = FastenerHeadType.Other,
                SourceClassification = "The source called it a 'fixture flanged dome head'.",
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasError(result, FastenerValidationRules.OtherClassificationNeedsSourceClassification));
        Assert.False(HasError(repaired, FastenerValidationRules.OtherClassificationNeedsSourceClassification));
    }

    [Fact]
    public async Task ARecordWithNoDimensionsAndNoProperties_IsReportedAsADataGap()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        var result = await validator.ValidateDefinitionAsync(
            new FastenerDefinition
            {
                Family = FastenerFamily.Bolt,
                Designation = "FX-1",
                Thread = new ThreadSpecification("FX10", ThreadSystem.MetricCoarse, Handedness: ThreadHandedness.RightHand),
            },
            FastenerFixtures.SourcedProvenance());

        Assert.True(HasWarning(result, FastenerValidationRules.NoEngineeringDataRecorded));
    }

    // ----------------------------------------------------------------
    // Integration with the rest of Group A
    // ----------------------------------------------------------------

    [Fact]
    public async Task ALinkedMaterialIsConfirmedAgainstTheCanonicalMaterialsCatalogue()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documentStore, persistenceStore);
        var fasteners = new FastenerCatalog(documentStore, persistenceStore);
        var validator = new FastenerValidationService(fasteners, materials);

        await materials.RegisterAsync(
            "mat-bolt-steel",
            new MaterialDefinition { Name = "Fixture bolt steel", Family = MaterialFamily.Steel },
            FastenerFixtures.SourcedProvenance());

        await fasteners.RegisterAsync(
            "fst-linked",
            FastenerFixtures.HexBolt() with { MaterialId = "mat-bolt-steel" },
            FastenerFixtures.VerifiedProvenance());
        await fasteners.RegisterAsync(
            "fst-dangling",
            FastenerFixtures.HexBolt("FX-BOLT-2") with { MaterialId = "mat-missing" },
            FastenerFixtures.VerifiedProvenance());

        Assert.True((await validator.ValidateAsync("fst-linked")).IsValid);
        Assert.False(HasWarning(await validator.ValidateAsync("fst-linked"), ReferenceValidationRules.MaterialReferenceUnresolved));
        Assert.True(HasWarning(await validator.ValidateAsync("fst-dangling"), ReferenceValidationRules.MaterialReferenceUnresolved));
    }

    [Fact]
    public async Task ACitedStandardIsConfirmedAgainstTheStandardsRegister()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var standards = new Core.Standards.StandardCatalog(documentStore, persistenceStore);
        var fasteners = new FastenerCatalog(documentStore, persistenceStore);
        var validator = new FastenerValidationService(fasteners, materialCatalog: null, standards);

        await fasteners.RegisterAsync(
            "fst-0001",
            FastenerFixtures.HexBolt() with
            {
                Standards = [new StandardReference("Fixture bolt standard", StandardId: "std-missing", Body: "TFX")],
            },
            FastenerFixtures.VerifiedProvenance());

        Assert.True(HasWarning(await validator.ValidateAsync("fst-0001"), ReferenceValidationRules.StandardReferenceUnresolved));
    }

    [Fact]
    public async Task EveryFastenerRecordIsBackedByOneDocumentOfTheFastenerKind()
    {
        var catalog = FastenerFixtures.BuildCatalog(out var documentStore, out _);
        var record = await catalog.RegisterAsync("fst-0001", FastenerFixtures.HexBolt(), FastenerFixtures.SourcedProvenance());

        Assert.Equal(FastenerCatalog.FastenerDocumentKind, (await documentStore.FindAsync(record.UnderlyingDocumentId))!.Kind);
    }

    [Fact]
    public async Task AFullLifecycleFromDraftToSupersession_IsTraceableEndToEnd()
    {
        var catalog = FastenerFixtures.BuildCatalog();
        var validator = new FastenerValidationService(catalog);

        await catalog.RegisterAsync("fst-0001", FastenerFixtures.HexBolt(), FastenerFixtures.VerifiedProvenance());
        Assert.True((await validator.ValidateAsync("fst-0001")).IsValid);
        await FastenerFixtures.ReleaseAsync(catalog, "fst-0001");

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync("fst-0001", FastenerFixtures.HexBolt(), FastenerFixtures.VerifiedProvenance(), "Refused."));

        await catalog.RegisterAsync("fst-0002", FastenerFixtures.HexBolt("FX-BOLT-2"), FastenerFixtures.VerifiedProvenance());
        await catalog.SupersedeAsync("fst-0001", "fst-0002", "Superseded by fixture catalogue revision 2.");

        var superseded = await catalog.FindAsync("fst-0001");

        Assert.Equal(ReferenceValidationState.Superseded, superseded!.ValidationState);
        Assert.Equal("fst-0002", superseded.SupersededByRecordId);
        Assert.Equal(FastenerFixtures.Millimetres(50), (await catalog.GetRevisionAsync("fst-0001", 1)).Definition.Dimensions.NominalLength!.Value);
    }
}
