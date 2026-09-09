using Tempest.Core.Bearings;
using Tempest.Core.ReferenceData;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Bearings;

/// <summary>
/// Shared construction helpers for the Bearing Library's own tests.
/// </summary>
/// <remarks>
/// <b>Every value below is fictional.</b> No manufacturer catalogue was
/// consulted and none is reproduced: the manufacturer is named
/// "TestFixture Bearings", the source document is explicitly labelled as a
/// fixture, and the dimensions and ratings are round numbers chosen to
/// make the rules under test observable. This matters — A4's own charter
/// forbids inventing manufacturer specifications, and a test fixture that
/// looked like a real catalogue extract would be exactly the fabricated
/// engineering data that rule exists to prevent, whether or not anything
/// downstream ever read it.
/// </remarks>
internal static class BearingFixtures
{
    public static BearingCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static BearingCatalog BuildCatalog(out EngineeringDocumentStore documentStore) =>
        BuildCatalog(out documentStore, out _);

    public static BearingCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new BearingCatalog(documentStore, persistenceStore);
    }

    public static Quantity<Length> Millimetres(double value) => new(value, LengthUnits.Millimetre);

    public static Quantity<Force> Kilonewtons(double value) => new(value, ForceUnits.Kilonewton);

    public static Quantity<Mass> Kilograms(double value) => new(value, MassUnits.Kilogram);

    public static Quantity<RotationalSpeed> RevolutionsPerMinute(double value) => new(value, RotationalSpeedUnits.RevolutionPerMinute);

    public static Quantity<PlaneAngle> Degrees(double value) => new(value, PlaneAngleUnits.Degree);

    /// <summary>Provenance that identifies a source but has not been verified — what an honest import leaves behind.</summary>
    public static ReferenceProvenance SourcedProvenance(string document = "Fixture catalogue (not a real publication)") => new(
        SourceOrganisation: "TestFixture Bearings",
        SourceDocument: document,
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 1",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        VerificationStatus: ReferenceVerificationStatus.NotVerified,
        Notes: "Fictional fixture data.");

    /// <summary>Provenance a named reviewer has verified — the only kind that can reach Released.</summary>
    public static ReferenceProvenance VerifiedProvenance() => SourcedProvenance() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    public static BearingIdentity Identity(string partNumber = "FX-6000", string manufacturer = "TestFixture Bearings") => new(
        Manufacturer: manufacturer,
        ManufacturerPartNumber: partNumber,
        Designation: partNumber,
        Series: "FX-60");

    /// <summary>A complete, coherent deep-groove ball bearing definition, all values fictional.</summary>
    public static BearingDefinition DeepGrooveBall(
        string partNumber = "FX-6000",
        double boreMillimetres = 10.0,
        double outsideDiameterMillimetres = 26.0,
        double widthMillimetres = 8.0) => new()
        {
            Identity = Identity(partNumber),
            Family = BearingFamily.DeepGrooveBall,
            Geometry = new BearingGeometry(
                Bore: Millimetres(boreMillimetres),
                OutsideDiameter: Millimetres(outsideDiameterMillimetres),
                Width: Millimetres(widthMillimetres),
                ChamferMinimum: Millimetres(0.3)),
            LoadRatings = new BearingLoadRatings(
                BasicDynamicRadial: new ReferenceValue<Force>(Kilonewtons(4.6), ReferenceValueOrigin.ManufacturerCatalogue, SourceDesignation: "C"),
                BasicStaticRadial: new ReferenceValue<Force>(Kilonewtons(2.0), ReferenceValueOrigin.ManufacturerCatalogue, SourceDesignation: "C0")),
            SpeedRatings =
            [
                new BearingSpeedRating(
                    BearingSpeedRatingKind.ReferenceSpeed,
                    new ReferenceValue<RotationalSpeed>(RevolutionsPerMinute(32000), ReferenceValueOrigin.ManufacturerCatalogue, Conditions: "Oil lubrication")),
                new BearingSpeedRating(
                    BearingSpeedRatingKind.LimitingSpeed,
                    new ReferenceValue<RotationalSpeed>(RevolutionsPerMinute(20000), ReferenceValueOrigin.ManufacturerCatalogue)),
            ],
            Configuration = new BearingConfiguration(
                Sealing: new BearingSealingArrangement(BearingSealingType.Open, ManufacturerDesignation: null),
                InternalClearanceClass: "CN",
                Rows: BearingRowConfiguration.SingleRow),
            Mass = Kilograms(0.019),
            Standards = [new StandardReference("Fixture boundary-dimension standard", Body: "TestFixture", Applies: "Boundary dimensions")],
        };

    /// <summary>A tapered roller bearing definition — a family for which a contact angle is applicable, all values fictional.</summary>
    public static BearingDefinition TaperedRoller(string partNumber = "FX-30200") => new()
    {
        Identity = Identity(partNumber) with { Series = "FX-302" },
        Family = BearingFamily.TaperedRoller,
        Geometry = new BearingGeometry(
            Bore: Millimetres(10.0),
            OutsideDiameter: Millimetres(30.0),
            Width: Millimetres(9.0),
            OverallWidth: Millimetres(11.0)),
        LoadRatings = new BearingLoadRatings(
            BasicDynamicRadial: new ReferenceValue<Force>(Kilonewtons(15.0), ReferenceValueOrigin.ManufacturerCatalogue),
            BasicStaticRadial: new ReferenceValue<Force>(Kilonewtons(14.0), ReferenceValueOrigin.ManufacturerCatalogue)),
        Configuration = new BearingConfiguration(
            Rows: BearingRowConfiguration.SingleRow,
            ContactAngle: Degrees(14.0)),
        Mass = Kilograms(0.045),
    };

    /// <summary>A plain bush definition — a family with no rolling elements, no clearance class and no contact angle. All values fictional.</summary>
    public static BearingDefinition PlainBush(string partNumber = "FX-PB-1012") => new()
    {
        Identity = Identity(partNumber) with { Series = null },
        Family = BearingFamily.Plain,
        Geometry = new BearingGeometry(
            Bore: Millimetres(10.0),
            OutsideDiameter: Millimetres(12.0),
            Width: Millimetres(10.0)),
        Construction = new BearingConstruction(Class: BearingConstructionClass.Polymer),
        Mass = Kilograms(0.002),
    };

    /// <summary>Drives a freshly-registered bearing all the way to Released, so release-state behaviour can be tested.</summary>
    public static async Task<IReferenceRecord<BearingDefinition>> ReleaseAsync(BearingCatalog catalog, string bearingId)
    {
        await catalog.SetValidationStateAsync(bearingId, ReferenceValidationState.Checked, "Checked against fixture source.");
        await catalog.SetValidationStateAsync(bearingId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(bearingId, ReferenceValidationState.Released, "Released for engineering use.");
    }
}

/// <summary>
/// Test-only conveniences over <see cref="IBearingCatalog"/>.
/// </summary>
/// <remarks>
/// Provenance moved off <see cref="BearingDefinition"/> and onto the
/// registered record when Group A's shared layer was extracted — it is
/// catalogue governance, not engineering description. Most tests do not
/// care which provenance a fixture carries, only that it has one, so this
/// supplies the ordinary sourced-but-unverified default and leaves the
/// tests that <em>are</em> about provenance to pass their own.
/// </remarks>
internal static class BearingCatalogTestExtensions
{
    public static Task<IReferenceRecord<BearingDefinition>> RegisterAsync(
        this IBearingCatalog catalog,
        string bearingId,
        BearingDefinition definition) =>
        catalog.RegisterAsync(bearingId, definition, BearingFixtures.SourcedProvenance());

    public static Task<IReferenceRecord<BearingDefinition>> ReviseAsync(
        this IBearingCatalog catalog,
        string bearingId,
        BearingDefinition definition,
        string? changeSummary) =>
        catalog.ReviseAsync(bearingId, definition, BearingFixtures.SourcedProvenance(), changeSummary);

    public static Task<Tempest.Core.EngineeringDomain.IValidationResult> ValidateDefinitionAsync(
        this IBearingValidationService validator,
        BearingDefinition definition) =>
        validator.ValidateDefinitionAsync(definition, BearingFixtures.SourcedProvenance());
}
