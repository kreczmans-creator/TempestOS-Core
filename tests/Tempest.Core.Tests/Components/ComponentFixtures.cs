using Tempest.Core.Components;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Components;

/// <summary>
/// Shared construction helpers for the Mechanical Components Library's own
/// tests.
/// </summary>
/// <remarks>
/// <b>Every value below is fictional.</b> No spring, gear or drive
/// catalogue was consulted and none is reproduced: the manufacturer is
/// "Fixture Components", the designations are in a deliberately unusable
/// "FX-" series, and the geometry is round numbers chosen to make the
/// rules under test observable — internally consistent so that a coherent
/// fixture passes, and deliberately breakable one field at a time so a
/// rule can be seen to fire. A5's own charter forbids inventing component
/// specifications.
/// </remarks>
internal static class ComponentFixtures
{
    public static ComponentCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static ComponentCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new ComponentCatalog(documentStore, persistenceStore);
    }

    public static Quantity<Length> Millimetres(double value) => new(value, LengthUnits.Millimetre);

    public static Quantity<Stiffness> NewtonsPerMillimetre(double value) => new(value, StiffnessUnits.NewtonPerMillimetre);

    public static Quantity<TorsionalStiffness> NewtonMetresPerDegree(double value) => new(value, TorsionalStiffnessUnits.NewtonMetrePerDegree);

    public static Quantity<PlaneAngle> Degrees(double value) => new(value, PlaneAngleUnits.Degree);

    public static Quantity<Torque> NewtonMetres(double value) => new(value, TorqueUnits.NewtonMetre);

    public static Quantity<RotationalSpeed> RevolutionsPerMinute(double value) => new(value, RotationalSpeedUnits.RevolutionPerMinute);

    public static Quantity<Force> Newtons(double value) => new(value, ForceUnits.Newton);

    public static Quantity<Dimensionless> Count(double value) => new(value, DimensionlessUnits.One);

    public static ReferenceValue<TDimension> Sourced<TDimension>(Quantity<TDimension> value, string? conditions = null)
        where TDimension : IDimension =>
        new(value, ReferenceValueOrigin.ManufacturerCatalogue, conditions);

    public static ReferenceProvenance SourcedProvenance() => new(
        SourceOrganisation: "Fixture Components",
        SourceDocument: "Fixture component catalogue (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 5",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    public static ReferenceProvenance VerifiedProvenance() => SourcedProvenance() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>A coherent fictional compression spring: outside minus inside is exactly two wire diameters.</summary>
    public static ComponentDefinition CompressionSpring(string designation = "FX-CSPR-1") => new()
    {
        Family = ComponentFamily.CompressionSpring,
        Designation = designation,
        Manufacturer = "Fixture Components",
        Spring = new SpringDetail(
            Rate: Sourced(NewtonsPerMillimetre(5)),
            FreeLength: Sourced(Millimetres(50)),
            SolidLength: Sourced(Millimetres(20)),
            OutsideDiameter: Sourced(Millimetres(20)),
            InsideDiameter: Sourced(Millimetres(16)),
            WireDiameter: Sourced(Millimetres(2)),
            TotalCoils: Sourced(Count(10)),
            ActiveCoils: Sourced(Count(8)),
            MaximumDeflection: Sourced(Millimetres(30)),
            MaximumLoad: Sourced(Newtons(150)),
            EndType: SpringEndType.ClosedGround,
            WindingDirection: SpringWindingDirection.RightHand),
    };

    /// <summary>A coherent fictional torsion spring — the one family whose rate is a torque per unit angle.</summary>
    public static ComponentDefinition TorsionSpring(string designation = "FX-TSPR-1") => new()
    {
        Family = ComponentFamily.TorsionSpring,
        Designation = designation,
        Manufacturer = "Fixture Components",
        Spring = new SpringDetail(
            TorsionalRate: Sourced(NewtonMetresPerDegree(0.05)),
            OutsideDiameter: Sourced(Millimetres(20)),
            InsideDiameter: Sourced(Millimetres(16)),
            WireDiameter: Sourced(Millimetres(2)),
            TotalCoils: Sourced(Count(6)),
            MaximumTorque: Sourced(NewtonMetres(4)),
            EndType: SpringEndType.Leg,
            WindingDirection: SpringWindingDirection.RightHand),
    };

    /// <summary>A coherent fictional spur gear.</summary>
    public static ComponentDefinition SpurGear(string designation = "FX-GEAR-1", int teeth = 40) => new()
    {
        Family = ComponentFamily.SpurGear,
        Designation = designation,
        Manufacturer = "Fixture Components",
        Gear = new GearDetail(
            NumberOfTeeth: teeth,
            Module: Sourced(Millimetres(2)),
            PressureAngle: Sourced(Degrees(20)),
            HelixHand: GearHelixHand.None,
            FaceWidth: Sourced(Millimetres(20)),
            PitchDiameter: Sourced(Millimetres(teeth * 2)),
            OutsideDiameter: Sourced(Millimetres((teeth * 2) + 4)),
            QualityGrade: "FX-Q7"),
        Dimensions = new ComponentDimensions(
            BoreDiameter: Sourced(Millimetres(15)),
            OutsideDiameter: Sourced(Millimetres((teeth * 2) + 4))),
        Ratings = new ComponentRatings(
            MaximumSpeed: Sourced(RevolutionsPerMinute(3000)),
            RatedTorque: Sourced(NewtonMetres(20)),
            MaximumTorque: Sourced(NewtonMetres(30))),
    };

    /// <summary>A coherent fictional timing pulley.</summary>
    public static ComponentDefinition TimingPulley(string designation = "FX-PUL-1") => new()
    {
        Family = ComponentFamily.TimingPulley,
        Designation = designation,
        Manufacturer = "Fixture Components",
        DriveElement = new DriveElementDetail(
            ProfileDesignation: "FX5M",
            Pitch: Sourced(Millimetres(5)),
            Width: Sourced(Millimetres(15)),
            NumberOfTeeth: 24,
            PitchDiameter: Sourced(Millimetres(38.2)),
            OutsideDiameter: Sourced(Millimetres(37))),
        Dimensions = new ComponentDimensions(
            BoreDiameter: Sourced(Millimetres(12)),
            OutsideDiameter: Sourced(Millimetres(37))),
        Ratings = new ComponentRatings(MaximumSpeed: Sourced(RevolutionsPerMinute(6000))),
    };

    /// <summary>A fictional shaft coupling — a family with no typed detail of its own, which is a fact rather than a gap.</summary>
    public static ComponentDefinition ShaftCoupling(string designation = "FX-CPL-1") => new()
    {
        Family = ComponentFamily.ShaftCoupling,
        Designation = designation,
        Manufacturer = "Fixture Components",
        Dimensions = new ComponentDimensions(
            BoreDiameter: Sourced(Millimetres(20)),
            OutsideDiameter: Sourced(Millimetres(55)),
            OverallLength: Sourced(Millimetres(66))),
        Ratings = new ComponentRatings(
            RatedTorque: Sourced(NewtonMetres(60)),
            MaximumSpeed: Sourced(RevolutionsPerMinute(10000))),
    };

    public static async Task<IReferenceRecord<ComponentDefinition>> ReleaseAsync(ComponentCatalog catalog, string componentId)
    {
        await catalog.SetValidationStateAsync(componentId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(componentId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(componentId, ReferenceValidationState.Released, "Released.");
    }
}
