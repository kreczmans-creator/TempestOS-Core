using Tempest.Core.Bearings;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Bearings;

// Data-quality rule tests: dimensional impossibility, non-positive
// ratings, type-aware applicability, provenance completeness, duplicate
// detection, material-reference resolution, and the catalogue-wide report.
public class BearingValidationServiceTests
{
    private static (BearingCatalog Catalog, BearingValidationService Validator) Build(IMaterialCatalog? materials = null)
    {
        var catalog = BearingFixtures.BuildCatalog();
        return (catalog, new BearingValidationService(catalog, materials));
    }

    private static bool HasError(IValidationResult result, string code) =>
        result.Errors.Any(diagnostic => diagnostic.Code == code);

    private static bool HasWarning(IValidationResult result, string code) =>
        result.Warnings.Any(diagnostic => diagnostic.Code == code);

    [Fact]
    public void Constructor_NullCatalog_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BearingValidationService(null!));
    }

    [Fact]
    public async Task ValidateAsync_UnknownBearing_Throws()
    {
        var (_, validator) = Build();

        await Assert.ThrowsAsync<BearingNotFoundException>(() => validator.ValidateAsync("brg-missing"));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_NullDefinition_Throws()
    {
        var (_, validator) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => validator.ValidateDefinitionAsync(null!));
    }

    [Fact]
    public async Task ValidateAsync_ACoherentRecord_HasNoErrors()
    {
        var (catalog, validator) = Build();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        var result = await validator.ValidateAsync("brg-0001");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ----------------------------------------------------------------
    // Dimensional rules
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValidateDefinitionAsync_ZeroBore_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Geometry = new BearingGeometry(Bore: BearingFixtures.Millimetres(0), OutsideDiameter: BearingFixtures.Millimetres(26)),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.BoreMustBePositive));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_NegativeBore_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Geometry = new BearingGeometry(Bore: BearingFixtures.Millimetres(-10)),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.BoreMustBePositive));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_OutsideDiameterInsideTheBore_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall(boreMillimetres: 26, outsideDiameterMillimetres: 10);

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.OutsideDiameterMustExceedBore));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_OutsideDiameterEqualToTheBore_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall(boreMillimetres: 26, outsideDiameterMillimetres: 26);

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.OutsideDiameterMustExceedBore));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_DimensionsInDifferentUnits_AreComparedCorrectly()
    {
        // A one-inch bore in a 26 mm outside diameter is impossible, and
        // stays impossible across units.
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Geometry = new BearingGeometry(
                Bore: new Quantity<Length>(1.0, LengthUnits.Inch),
                OutsideDiameter: BearingFixtures.Millimetres(26)),
        };

        Assert.False(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.OutsideDiameterMustExceedBore));

        var impossible = definition with
        {
            Geometry = new BearingGeometry(
                Bore: new Quantity<Length>(2.0, LengthUnits.Inch),
                OutsideDiameter: BearingFixtures.Millimetres(26)),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(impossible), BearingValidationRules.OutsideDiameterMustExceedBore));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_AnUnrecordedDimension_IsNotTreatedAsZero()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with { Geometry = new BearingGeometry() };

        var result = await validator.ValidateDefinitionAsync(definition);

        Assert.False(HasError(result, BearingValidationRules.BoreMustBePositive));
        Assert.False(HasError(result, BearingValidationRules.WidthMustBePositive));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ZeroWidth_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall(widthMillimetres: 0);

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.WidthMustBePositive));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_OverallWidthLessThanNominalWidth_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.TaperedRoller() with
        {
            Geometry = new BearingGeometry(
                Bore: BearingFixtures.Millimetres(10),
                OutsideDiameter: BearingFixtures.Millimetres(30),
                Width: BearingFixtures.Millimetres(11),
                OverallWidth: BearingFixtures.Millimetres(9)),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.OverallWidthLessThanWidth));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_NegativeMass_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with { Mass = BearingFixtures.Kilograms(-0.019) };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.MassMustNotBeNegative));
    }

    // ----------------------------------------------------------------
    // Rating rules
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValidateDefinitionAsync_ZeroLoadRating_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            LoadRatings = new BearingLoadRatings(
                BasicDynamicRadial: new BearingRatedValue<Force>(BearingFixtures.Kilonewtons(0), BearingValueOrigin.ManufacturerCatalogue)),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.LoadRatingMustBePositive));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ManufacturerSpecificRating_IsCheckedToo()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            LoadRatings = new BearingLoadRatings(
                ManufacturerRatings: new Dictionary<string, BearingRatedValue<Force>>
                {
                    ["Fixture rating"] = new(BearingFixtures.Kilonewtons(-1), BearingValueOrigin.ManufacturerCatalogue),
                }),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.LoadRatingMustBePositive));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_NoLoadRatingOnARollingElementBearing_IsAWarningNotAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with { LoadRatings = null };

        var result = await validator.ValidateDefinitionAsync(definition);

        Assert.True(HasWarning(result, BearingValidationRules.NoLoadRatingRecorded));
        Assert.False(HasError(result, BearingValidationRules.NoLoadRatingRecorded));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_NoLoadRatingOnAPlainBearing_IsNotEvenAWarning()
    {
        var (_, validator) = Build();

        Assert.False(HasWarning(await validator.ValidateDefinitionAsync(BearingFixtures.PlainBush()), BearingValidationRules.NoLoadRatingRecorded));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ZeroSpeedRating_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            SpeedRatings =
            [
                new BearingSpeedRating(
                    BearingSpeedRatingKind.LimitingSpeed,
                    new BearingRatedValue<RotationalSpeed>(BearingFixtures.RevolutionsPerMinute(0), BearingValueOrigin.ManufacturerCatalogue)),
            ],
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.SpeedRatingMustBePositive));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ADerivedValue_IsFlaggedSoItIsNeverReadAsManufacturerData()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            LoadRatings = new BearingLoadRatings(
                BasicDynamicRadial: new BearingRatedValue<Force>(BearingFixtures.Kilonewtons(4.6), BearingValueOrigin.DerivedByTempestOS)),
        };

        Assert.True(HasWarning(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.DerivedValuePresent));
    }

    // ----------------------------------------------------------------
    // Classification and type-aware applicability
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValidateDefinitionAsync_NoFamily_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with { Family = BearingFamily.Unspecified };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.FamilyMustBeStated));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_FamilyOtherWithoutTheSourcesOwnWording_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with { Family = BearingFamily.Other };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.OtherFamilyNeedsDesignation));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_FamilyOtherWithTheSourcesOwnWording_IsAccepted()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Family = BearingFamily.Other,
            Identity = BearingFixtures.Identity() with { FamilyDesignation = "Fixture combined axial-radial unit" },
        };

        Assert.False(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.OtherFamilyNeedsDesignation));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_NoDesignation_IsAWarning()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Identity = new BearingIdentity("TestFixture Bearings", "FX-6000"),
        };

        Assert.True(HasWarning(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.DesignationShouldBeRecorded));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ContactAngleOnADeepGrooveBallBearing_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Configuration = new BearingConfiguration(ContactAngle: BearingFixtures.Degrees(15)),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.ContactAngleNotApplicableToFamily));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ContactAngleOnATaperedRollerBearing_IsAccepted()
    {
        var (_, validator) = Build();

        Assert.False(HasError(await validator.ValidateDefinitionAsync(BearingFixtures.TaperedRoller()), BearingValidationRules.ContactAngleNotApplicableToFamily));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(120.0)]
    public async Task ValidateDefinitionAsync_ImpossibleContactAngle_IsAnError(double degrees)
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.TaperedRoller() with
        {
            Configuration = new BearingConfiguration(ContactAngle: BearingFixtures.Degrees(degrees)),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.ContactAngleOutOfRange));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ContactAngleInRadians_IsCheckedInTheSameRange()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.TaperedRoller() with
        {
            Configuration = new BearingConfiguration(ContactAngle: new Quantity<PlaneAngle>(0.25, PlaneAngleUnits.Radian)),
        };

        Assert.False(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.ContactAngleOutOfRange));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ClearanceClassOnAPlainBearing_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.PlainBush() with
        {
            Configuration = new BearingConfiguration(InternalClearanceClass: "C3"),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.ClearanceNotApplicableToFamily));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_InvertedClearanceRange_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Configuration = new BearingConfiguration(
                RadialInternalClearanceMinimum: BearingFixtures.Millimetres(0.02),
                RadialInternalClearanceMaximum: BearingFixtures.Millimetres(0.005)),
        };

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.ClearanceRangeInverted));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_RollingElementMaterialOnAPlainBearing_IsAWarning()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.PlainBush() with
        {
            Construction = new BearingConstruction(RollingElementMaterialId: "steel-100cr6"),
        };

        Assert.True(HasWarning(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.RollingElementNotApplicableToFamily));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_AnUnclassifiedFamily_DoesNotTriggerApplicabilityErrors()
    {
        // Applicability is unknown for Other, so a contact angle on it is
        // not evidence of a defect.
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Family = BearingFamily.Other,
            Identity = BearingFixtures.Identity() with { FamilyDesignation = "Fixture special" },
            Configuration = new BearingConfiguration(ContactAngle: BearingFixtures.Degrees(15), InternalClearanceClass: "C3"),
        };

        var result = await validator.ValidateDefinitionAsync(definition);

        Assert.False(HasError(result, BearingValidationRules.ContactAngleNotApplicableToFamily));
        Assert.False(HasError(result, BearingValidationRules.ClearanceNotApplicableToFamily));
    }

    // ----------------------------------------------------------------
    // Provenance rules
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValidateDefinitionAsync_NoSourceIdentified_IsAWarning()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall(provenance: BearingProvenance.Unknown);

        Assert.True(HasWarning(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.ProvenanceMustIdentifyASource));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_VerifiedWithoutAReviewer_IsAnError()
    {
        var (_, validator) = Build();
        var definition = BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.SourcedProvenance() with
        {
            VerificationStatus = BearingVerificationStatus.VerifiedAgainstSource,
        });

        Assert.True(HasError(await validator.ValidateDefinitionAsync(definition), BearingValidationRules.VerificationMustBeAttributable));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ProperlyVerified_IsAccepted()
    {
        var (_, validator) = Build();

        var result = await validator.ValidateDefinitionAsync(BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.VerifiedProvenance()));

        Assert.False(HasError(result, BearingValidationRules.VerificationMustBeAttributable));
        Assert.False(HasWarning(result, BearingValidationRules.ProvenanceMustIdentifyASource));
    }

    // ----------------------------------------------------------------
    // Record-level rules
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValidateAsync_ASupersededRecordNamingNoReplacement_IsAWarning()
    {
        // Reached by writing a superseded state directly through the
        // document store — the catalogue's own SupersedeAsync always
        // records a replacement, so this guard only fires on data that
        // predates it or was written by something else.
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var catalog = new BearingCatalog(documentStore, persistenceStore);
        var validator = new BearingValidationService(catalog);

        var bearing = await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        var content = (await documentStore.GetRevisionHistoryAsync(bearing.UnderlyingDocumentId))[^1].Content
            .Replace("\"ValidationState\":\"Draft\"", "\"ValidationState\":\"Superseded\"", StringComparison.Ordinal);
        await documentStore.ReviseAsync(bearing.UnderlyingDocumentId, content, "Hand-written state.");

        Assert.True(HasWarning(await validator.ValidateAsync("brg-0001"), BearingValidationRules.SupersededWithoutReplacement));
    }

    [Fact]
    public async Task ValidateAsync_DuplicatePartNumbers_AreDetectedAsAnError()
    {
        // Defence in depth: the catalogue prevents this at write time, so
        // the duplicate has to be created behind its back to test the
        // read-time confirmation.
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var catalog = new BearingCatalog(documentStore, persistenceStore);
        var validator = new BearingValidationService(catalog);

        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));
        var second = await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6001"));

        var content = (await documentStore.GetRevisionHistoryAsync(second.UnderlyingDocumentId))[^1].Content
            .Replace("FX-6001", "FX-6000", StringComparison.Ordinal);
        await documentStore.ReviseAsync(second.UnderlyingDocumentId, content, "Hand-written collision.");

        var result = await validator.ValidateAsync("brg-0002");

        Assert.True(HasError(result, BearingValidationRules.DuplicatePartNumber));
    }

    // ----------------------------------------------------------------
    // Materials integration
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValidateAsync_WithNoMaterialCatalogue_DoesNotReportMaterialReferences()
    {
        var (catalog, validator) = Build();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall() with
        {
            Construction = new BearingConstruction(RingMaterialId: "never-registered"),
        });

        Assert.False(HasWarning(await validator.ValidateAsync("brg-0001"), BearingValidationRules.MaterialReferenceUnresolved));
    }

    [Fact]
    public async Task ValidateAsync_AnUnresolvedMaterialReference_IsAWarning()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documentStore, persistenceStore);
        var catalog = new BearingCatalog(documentStore, persistenceStore);
        var validator = new BearingValidationService(catalog, materials);

        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall() with
        {
            Construction = new BearingConstruction(RingMaterialId: "never-registered"),
        });

        Assert.True(HasWarning(await validator.ValidateAsync("brg-0001"), BearingValidationRules.MaterialReferenceUnresolved));
    }

    [Fact]
    public async Task ValidateAsync_AResolvedMaterialReference_IsAccepted()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documentStore, persistenceStore);
        var catalog = new BearingCatalog(documentStore, persistenceStore);
        var validator = new BearingValidationService(catalog, materials);

        await materials.RegisterAsync(
            "fixture-ring-steel",
            "Fixture ring steel",
            new Dictionary<string, MaterialProperty>(),
            category: "TestFixture");

        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall() with
        {
            Construction = new BearingConstruction(RingMaterialId: "fixture-ring-steel"),
        });

        Assert.False(HasWarning(await validator.ValidateAsync("brg-0001"), BearingValidationRules.MaterialReferenceUnresolved));
    }

    // ----------------------------------------------------------------
    // Catalogue-wide report
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValidateCatalogueAsync_AnEmptyCatalogue_IsClean()
    {
        var (_, validator) = Build();

        var report = await validator.ValidateCatalogueAsync();

        Assert.Equal(0, report.BearingsExamined);
        Assert.Empty(report.Findings);
        Assert.True(report.IsClean);
    }

    [Fact]
    public async Task ValidateCatalogueAsync_ReportsOnlyRecordsWithSomethingToSay()
    {
        var (catalog, validator) = Build();
        await catalog.RegisterAsync("brg-good", BearingFixtures.DeepGrooveBall("FX-6000"));
        await catalog.RegisterAsync("brg-bad", BearingFixtures.DeepGrooveBall("FX-6001", boreMillimetres: 30, outsideDiameterMillimetres: 20));

        var report = await validator.ValidateCatalogueAsync();

        Assert.Equal(2, report.BearingsExamined);
        Assert.Equal(["brg-bad"], report.Findings.Select(f => f.BearingId));
        Assert.Equal(1, report.BearingsWithErrors);
        Assert.False(report.IsClean);
    }

    [Fact]
    public async Task ValidateCatalogueAsync_CarriesEachRecordsValidationStateAsContext()
    {
        var (catalog, validator) = Build();
        await catalog.RegisterAsync("brg-bad", BearingFixtures.DeepGrooveBall(boreMillimetres: 30, outsideDiameterMillimetres: 20));

        var report = await validator.ValidateCatalogueAsync();

        Assert.Equal(BearingValidationState.Draft, report.Findings[0].ValidationState);
    }

    [Fact]
    public async Task ValidateCatalogueAsync_CountsWarningsSeparatelyFromErrors()
    {
        var (catalog, validator) = Build();
        await catalog.RegisterAsync("brg-warn", BearingFixtures.DeepGrooveBall() with { LoadRatings = null });

        var report = await validator.ValidateCatalogueAsync();

        Assert.Equal(0, report.BearingsWithErrors);
        Assert.Equal(1, report.BearingsWithWarnings);
        Assert.True(report.IsClean);
    }
}
