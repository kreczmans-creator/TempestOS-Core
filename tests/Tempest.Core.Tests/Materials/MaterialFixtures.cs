using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Materials;

/// <summary>
/// Shared construction helpers for the Materials Library's own tests.
/// </summary>
/// <remarks>
/// <b>Every value below is fictional.</b> No material standard or supplier
/// datasheet was consulted and none is reproduced: the source organisation
/// is named "TestFixture Publications", the document is explicitly labelled
/// as a fixture, and the property values are round numbers chosen to make
/// the rules under test observable. A1's own charter forbids inventing
/// material-property values, and a fixture that looked like a real
/// datasheet extract would be exactly the fabricated engineering data that
/// rule exists to prevent.
/// </remarks>
internal static class MaterialFixtures
{
    public static MaterialCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static MaterialCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new MaterialCatalog(documentStore, persistenceStore);
    }

    public static Quantity<MassDensity> GramsPerCubicCentimetre(double value) => new(value, MassDensityUnits.GramPerCubicCentimetre);

    public static Quantity<Pressure> Megapascals(double value) => new(value, PressureUnits.Megapascal);

    public static Quantity<Pressure> Gigapascals(double value) => new(value, PressureUnits.Gigapascal);

    public static Quantity<Dimensionless> Ratio(double value) => new(value, DimensionlessUnits.One);

    public static Quantity<Temperature> DegreesCelsius(double value) => new(value, TemperatureUnits.DegreeCelsius);

    public static ReferenceQuantityValue Property(object value, ReferenceValueOrigin origin = ReferenceValueOrigin.EngineeringReference, string? conditions = null) =>
        new(value, origin, conditions);

    public static ReferenceProvenance Sourced() => new(
        SourceOrganisation: "TestFixture Publications",
        SourceDocument: "Fixture materials handbook (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 3",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    public static ReferenceProvenance Verified() => Sourced() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>A coherent fictional steel, complete enough to exercise the property rules.</summary>
    public static MaterialDefinition Steel(string designation = "FX-STEEL-1") => new()
    {
        Name = "Fixture Structural Steel",
        Family = MaterialFamily.Steel,
        Designation = designation,
        Grade = "FX-A",
        Condition = "Normalised",
        Properties = new Dictionary<string, ReferenceQuantityValue>
        {
            [MaterialPropertyNames.Density] = Property(GramsPerCubicCentimetre(7.85)),
            [MaterialPropertyNames.YoungsModulus] = Property(Gigapascals(200)),
            [MaterialPropertyNames.YieldStrength] = Property(Megapascals(300)),
            [MaterialPropertyNames.UltimateTensileStrength] = Property(Megapascals(450)),
            [MaterialPropertyNames.PoissonsRatio] = Property(Ratio(0.3)),
        },
        Standards = [new StandardReference("Fixture steel standard", Body: "TestFixture", Applies: "Mechanical properties")],
    };

    /// <summary>A fictional polymer, for cross-family comparison.</summary>
    public static MaterialDefinition Polymer(string designation = "FX-POLY-1") => new()
    {
        Name = "Fixture Engineering Thermoplastic",
        Family = MaterialFamily.Thermoplastic,
        Designation = designation,
        Properties = new Dictionary<string, ReferenceQuantityValue>
        {
            [MaterialPropertyNames.Density] = Property(GramsPerCubicCentimetre(1.14)),
            [MaterialPropertyNames.YoungsModulus] = Property(Gigapascals(3)),
            [MaterialPropertyNames.MaximumServiceTemperature] = Property(DegreesCelsius(100)),
        },
    };

    /// <summary>A fictional ceramic — a family with no yield point, so the applicability rule can be exercised.</summary>
    public static MaterialDefinition Ceramic(string designation = "FX-CER-1") => new()
    {
        Name = "Fixture Technical Ceramic",
        Family = MaterialFamily.Ceramic,
        Designation = designation,
        Properties = new Dictionary<string, ReferenceQuantityValue>
        {
            [MaterialPropertyNames.Density] = Property(GramsPerCubicCentimetre(3.9)),
            [MaterialPropertyNames.CompressiveStrength] = Property(Megapascals(2000)),
        },
    };

    public static async Task<IReferenceRecord<MaterialDefinition>> ReleaseAsync(MaterialCatalog catalog, string materialId)
    {
        await catalog.SetValidationStateAsync(materialId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(materialId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(materialId, ReferenceValidationState.Released, "Released.");
    }
}
