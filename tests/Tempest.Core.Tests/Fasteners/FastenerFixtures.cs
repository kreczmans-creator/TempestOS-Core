using Tempest.Core.EngineeringData;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Fasteners;

/// <summary>
/// Shared construction helpers for the Fastener Library's own tests.
/// </summary>
/// <remarks>
/// <b>Every value below is fictional.</b> No fastener standard, property
/// class table or manufacturer catalogue was consulted and none is
/// reproduced: the manufacturer is "Fixture Fasteners", the designations
/// are in a deliberately unusable "FX-" series, the thread designations are
/// invented, and the strengths are round numbers chosen to make the rules
/// under test observable. A3's own charter forbids inventing fastener
/// specifications, and a fixture that looked like a real property class
/// would be exactly the fabricated engineering data that rule exists to
/// prevent.
/// </remarks>
internal static class FastenerFixtures
{
    public static FastenerCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static FastenerCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new FastenerCatalog(documentStore, persistenceStore);
    }

    public static Quantity<Length> Millimetres(double value) => new(value, LengthUnits.Millimetre);

    public static Quantity<Pressure> Megapascals(double value) => new(value, PressureUnits.Megapascal);

    public static Quantity<Force> Kilonewtons(double value) => new(value, ForceUnits.Kilonewton);

    public static Quantity<Torque> NewtonMetres(double value) => new(value, TorqueUnits.NewtonMetre);

    public static ReferenceValue<TDimension> Sourced<TDimension>(Quantity<TDimension> value, string? conditions = null)
        where TDimension : IDimension =>
        new(value, ReferenceValueOrigin.ManufacturerCatalogue, conditions);

    public static ReferenceProvenance SourcedProvenance() => new(
        SourceOrganisation: "Fixture Fasteners",
        SourceDocument: "Fixture fastener catalogue (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 2",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    public static ReferenceProvenance VerifiedProvenance() => SourcedProvenance() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>A coherent fictional hexagon-head bolt, complete enough that the rules pass on it.</summary>
    public static FastenerDefinition HexBolt(string designation = "FX-BOLT-1", double nominalDiameterMillimetres = 10.0) => new()
    {
        Family = FastenerFamily.Bolt,
        Designation = designation,
        Manufacturer = "Fixture Fasteners",
        Thread = new ThreadSpecification(
            $"FX{nominalDiameterMillimetres:0}",
            ThreadSystem.MetricCoarse,
            NominalDiameter: Sourced(Millimetres(nominalDiameterMillimetres)),
            Pitch: Sourced(Millimetres(1.5)),
            Handedness: ThreadHandedness.RightHand),
        HeadType = FastenerHeadType.Hexagon,
        DriveType = FastenerDriveType.ExternalHexagon,
        Dimensions = new FastenerDimensions(
            NominalLength: Sourced(Millimetres(50)),
            WidthAcrossFlats: Sourced(Millimetres(16)),
            WidthAcrossCorners: Sourced(Millimetres(18.5)),
            HeadHeight: Sourced(Millimetres(6.4))),
        Mechanical = new FastenerMechanicalProperties(
            PropertyClass: "FX-A",
            ProofStrength: Sourced(Megapascals(500)),
            TensileStrength: Sourced(Megapascals(700)),
            YieldStrength: Sourced(Megapascals(600)),
            ProofLoad: Sourced(Kilonewtons(25)),
            MinimumBreakingLoad: Sourced(Kilonewtons(35))),
        TorqueReferences =
        [
            new FastenerTorqueReference(
                NewtonMetres(45),
                ReferenceValueOrigin.ManufacturerCatalogue,
                Conditions: "Fixture condition: lightly oiled, assumed friction coefficient 0.14.",
                PropertyClass: "FX-A"),
        ],
        Standards = [new StandardReference("Fixture bolt standard", Body: "TestFixture", Applies: "Dimensions and mechanical properties")],
    };

    /// <summary>A fictional plain washer — an unthreaded, headless family, for the applicability rules.</summary>
    public static FastenerDefinition Washer(string designation = "FX-WASH-1") => new()
    {
        Family = FastenerFamily.Washer,
        Designation = designation,
        Manufacturer = "Fixture Fasteners",
        Dimensions = new FastenerDimensions(
            InsideDiameter: Sourced(Millimetres(10.5)),
            OutsideDiameter: Sourced(Millimetres(20)),
            Height: Sourced(Millimetres(2))),
    };

    /// <summary>A fictional hexagon nut — internally threaded, headless, driven on flats.</summary>
    public static FastenerDefinition Nut(string designation = "FX-NUT-1") => new()
    {
        Family = FastenerFamily.Nut,
        Designation = designation,
        Manufacturer = "Fixture Fasteners",
        Thread = new ThreadSpecification(
            "FX10",
            ThreadSystem.MetricCoarse,
            NominalDiameter: Sourced(Millimetres(10)),
            Pitch: Sourced(Millimetres(1.5)),
            Handedness: ThreadHandedness.RightHand),
        DriveType = FastenerDriveType.ExternalHexagon,
        Dimensions = new FastenerDimensions(
            WidthAcrossFlats: Sourced(Millimetres(16)),
            WidthAcrossCorners: Sourced(Millimetres(18.5)),
            Height: Sourced(Millimetres(8))),
        Mechanical = new FastenerMechanicalProperties(PropertyClass: "FX-A", ProofLoad: Sourced(Kilonewtons(25))),
    };

    public static async Task<IReferenceRecord<FastenerDefinition>> ReleaseAsync(FastenerCatalog catalog, string fastenerId)
    {
        await catalog.SetValidationStateAsync(fastenerId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(fastenerId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(fastenerId, ReferenceValidationState.Released, "Released.");
    }
}
