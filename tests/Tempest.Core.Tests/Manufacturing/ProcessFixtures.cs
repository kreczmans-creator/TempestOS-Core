using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Manufacturing;

/// <summary>
/// Shared construction helpers for the Manufacturing Process Library's own
/// tests.
/// </summary>
/// <remarks>
/// <b>Every capability band below is fictional.</b> No process handbook,
/// supplier catalogue or capability study was consulted and none is
/// reproduced: the source is "TestFixture Manufacturing", the processes
/// are named with a fixture prefix, and the bands are round numbers chosen
/// to make the rules under test observable. A7's own charter forbids
/// inventing process capability, and a fixture that looked like a real
/// capability table would be exactly the fabricated engineering data that
/// rule exists to prevent — the more so because a capability band read as
/// real would steer a manufacturing decision.
/// </remarks>
internal static class ProcessFixtures
{
    public static ProcessCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static ProcessCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new ProcessCatalog(documentStore, persistenceStore);
    }

    public static Quantity<Length> Millimetres(double value) => new(value, LengthUnits.Millimetre);

    public static Quantity<Length> Micrometres(double value) => new(value, LengthUnits.Micrometre);

    public static Quantity<Mass> Kilograms(double value) => new(value, MassUnits.Kilogram);

    public static Quantity<PlaneAngle> Degrees(double value) => new(value, PlaneAngleUnits.Degree);

    public static Quantity<Temperature> DegreesCelsius(double value) => new(value, TemperatureUnits.DegreeCelsius);

    // Quantity<T> is a struct, so a nullable band end is Nullable<Quantity<T>>
    // and type inference cannot reach TDimension from a bare argument.
    // Named per dimension rather than made generic-with-explicit-arguments,
    // which reads better at every call site in this file.
    public static ReferenceRange<Length> LengthBand(Quantity<Length>? minimum, Quantity<Length>? maximum, string? conditions = "Fixture conditions.") =>
        new(minimum, maximum, ReferenceValueOrigin.EngineeringReference, conditions);

    public static ReferenceRange<Mass> MassBand(Quantity<Mass>? minimum, Quantity<Mass>? maximum, string? conditions = "Fixture conditions.") =>
        new(minimum, maximum, ReferenceValueOrigin.EngineeringReference, conditions);

    public static ReferenceRange<PlaneAngle> AngleBand(Quantity<PlaneAngle>? minimum, Quantity<PlaneAngle>? maximum, string? conditions = "Fixture conditions.") =>
        new(minimum, maximum, ReferenceValueOrigin.EngineeringReference, conditions);

    public static ReferenceRange<Temperature> TemperatureBand(Quantity<Temperature>? minimum, Quantity<Temperature>? maximum, string? conditions = "Fixture conditions.") =>
        new(minimum, maximum, ReferenceValueOrigin.EngineeringReference, conditions);

    public static ReferenceProvenance SourcedProvenance() => new(
        SourceOrganisation: "TestFixture Manufacturing",
        SourceDocument: "Fixture process handbook (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 8",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    public static ReferenceProvenance VerifiedProvenance() => SourcedProvenance() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>A coherent fictional casting process — a family with a mould, a wall thickness and a process temperature.</summary>
    public static ProcessDefinition Casting(string name = "Fixture sand casting", string? variant = null) => new()
    {
        Family = ProcessFamily.SandCasting,
        Name = name,
        Variant = variant,
        Description = "A fixture casting process invented for tests; it describes nothing real.",
        Capabilities = new ProcessCapabilities(
            AchievableTolerance: LengthBand(Millimetres(0.5), Millimetres(2.0)),
            SurfaceRoughness: LengthBand(Micrometres(6.0), Micrometres(25.0)),
            WallThickness: LengthBand(Millimetres(3.0), Millimetres(50.0)),
            PartSize: LengthBand(Millimetres(50), Millimetres(2000)),
            PartMass: MassBand(Kilograms(0.5), Kilograms(500)),
            DraftAngle: AngleBand(Degrees(1), Degrees(3)),
            ProcessTemperature: TemperatureBand(DegreesCelsius(700), DegreesCelsius(1500))),
        MaterialCompatibility =
        [
            new ProcessMaterialCompatibility(MaterialFamily.CastIron, ProcessMaterialSuitability.Suitable, Origin: ReferenceValueOrigin.EngineeringReference),
            new ProcessMaterialCompatibility(MaterialFamily.Aluminium, ProcessMaterialSuitability.Suitable, Origin: ReferenceValueOrigin.EngineeringReference),
            new ProcessMaterialCompatibility(
                MaterialFamily.Thermoplastic,
                ProcessMaterialSuitability.NotSuitable,
                Origin: ReferenceValueOrigin.EngineeringReference,
                Notes: "The fixture source states this pairing does not apply."),
        ],
        ProductionScales = [ProductionScale.Prototype, ProductionScale.LowVolume, ProductionScale.MediumVolume],
        Constraints =
        [
            new ProcessConstraint(
                "Fixture constraint: the source states a minimum section below which the fixture process does not fill.",
                ProcessConstraintKind.Geometric,
                ReferenceValueOrigin.EngineeringReference),
        ],
        TypicalApplications = "What the fixture source says it is used for; not a TempestOS recommendation.",
        Standards = [new StandardReference("Fixture casting standard", Body: "TestFixture", Applies: "Tolerances")],
    };

    /// <summary>A coherent fictional machining process — no mould, no wall thickness of its own.</summary>
    public static ProcessDefinition Machining(string name = "Fixture milling", string? variant = null) => new()
    {
        Family = ProcessFamily.Milling,
        Name = name,
        Variant = variant,
        Capabilities = new ProcessCapabilities(
            AchievableTolerance: LengthBand(Millimetres(0.01), Millimetres(0.2)),
            SurfaceRoughness: LengthBand(Micrometres(0.8), Micrometres(6.3)),
            PartSize: LengthBand(Millimetres(5), Millimetres(1000)),
            MinimumFeatureSize: LengthBand(Millimetres(0.5), null),
            CornerRadius: LengthBand(Millimetres(0.5), Millimetres(25))),
        MaterialCompatibility =
        [
            new ProcessMaterialCompatibility(MaterialFamily.Steel, ProcessMaterialSuitability.Suitable, Origin: ReferenceValueOrigin.EngineeringReference),
            new ProcessMaterialCompatibility(
                MaterialFamily.Ceramic,
                ProcessMaterialSuitability.ConditionallySuitable,
                Origin: ReferenceValueOrigin.EngineeringReference,
                Conditions: "The fixture source states this holds only under stated fixture conditions."),
        ],
        ProductionScales = [ProductionScale.Prototype, ProductionScale.LowVolume],
        Constraints =
        [
            new ProcessConstraint(
                "Fixture constraint: the source states the tool must reach the feature.",
                ProcessConstraintKind.Geometric,
                ReferenceValueOrigin.EngineeringReference),
        ],
    };

    /// <summary>A coherent fictional heat treatment — no shape capability of its own at all.</summary>
    public static ProcessDefinition HeatTreatment(string name = "Fixture stress relieving") => new()
    {
        Family = ProcessFamily.StressRelieving,
        Name = name,
        Capabilities = new ProcessCapabilities(
            ProcessTemperature: TemperatureBand(DegreesCelsius(500), DegreesCelsius(650)),
            PartMass: MassBand(Kilograms(0.1), Kilograms(2000))),
        MaterialCompatibility =
        [
            new ProcessMaterialCompatibility(MaterialFamily.Steel, ProcessMaterialSuitability.Suitable, Origin: ReferenceValueOrigin.EngineeringReference),
        ],
        ProductionScales = [ProductionScale.LowVolume, ProductionScale.MediumVolume, ProductionScale.HighVolume],
    };

    public static async Task<IReferenceRecord<ProcessDefinition>> ReleaseAsync(ProcessCatalog catalog, string processId)
    {
        await catalog.SetValidationStateAsync(processId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(processId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(processId, ReferenceValidationState.Released, "Released.");
    }
}
