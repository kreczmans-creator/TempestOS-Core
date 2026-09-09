using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Components;

/// <summary>The concrete <see cref="IComponentValidationService"/> implementation.</summary>
/// <remarks>
/// Provenance, verification attributability, supersession, standard
/// resolution and material resolution are checked by
/// <see cref="ReferenceValidationService{TDefinition}"/>, shared with every
/// Group A library. Everything below is component engineering: which typed
/// detail a family may carry, the geometry that cannot hold, and the
/// published figures that contradict each other.
/// </remarks>
public sealed class ComponentValidationService : ReferenceValidationService<ComponentDefinition>, IComponentValidationService
{
    /// <summary>
    /// The widest pressure angle a real involute gear is cut at, in
    /// radians. Used only to catch a transcription error — a value outside
    /// this cannot be a pressure angle at all — never to judge whether a
    /// gear is well proportioned.
    /// </summary>
    private const double MaximumCrediblePressureAngleRadians = 45.0 * Math.PI / 180.0;

    /// <summary>
    /// Initialises a new instance of the <see cref="ComponentValidationService"/> class.
    /// </summary>
    /// <param name="catalog">The catalogue whose records this service validates.</param>
    /// <param name="materialCatalog">The canonical Materials catalogue, for confirming a linked material resolves. Optional.</param>
    /// <param name="standardResolver">Resolves a cited standard against the Standards Library. Optional.</param>
    public ComponentValidationService(
        IComponentCatalog catalog,
        IMaterialCatalog? materialCatalog = null,
        IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog, standardResolver)
    {
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        ComponentDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateClassification(definition, errors, warnings);
        EvaluateDetailApplicability(definition, errors, warnings);
        EvaluateSpring(definition, errors, warnings);
        EvaluateGear(definition, errors, warnings);
        EvaluateDriveElement(definition, errors, warnings);
        EvaluateDimensions(definition, errors, warnings);
        EvaluateRatings(definition, errors, warnings);

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);

        if (definition.MaterialId is { } materialId)
            await EvaluateMaterialReferencesAsync([materialId], warnings, cancellationToken).ConfigureAwait(false);
        else if (!string.IsNullOrWhiteSpace(definition.MaterialDesignation))
            warnings.Add(Diagnostic(
                ComponentValidationRules.MaterialShouldBeLinked,
                $"The material is recorded only as text ('{definition.MaterialDesignation}') and is not linked to a registered material record."));
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<ComponentDefinition> record,
        IReadOnlyList<IReferenceRecord<ComponentDefinition>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var key = record.Definition.IdentityKey;

        // Defence in depth: the catalogue already prevents this at write
        // time. Confirming it on read catches an index written before that
        // guard existed, or corrupted since.
        var others = library ?? await Catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var collisions = others
            .Where(other => !string.Equals(other.Id, record.Id, StringComparison.Ordinal))
            .Where(other => string.Equals(other.Definition.IdentityKey, key, StringComparison.Ordinal))
            .Select(other => other.Id)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                ComponentValidationRules.DuplicateIdentity,
                $"Component '{record.Definition.Designation}' shares its identity key with: {string.Join(", ", collisions)}."));
    }

    private static void EvaluateClassification(ComponentDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.Family == ComponentFamily.Unspecified)
            errors.Add(Diagnostic(
                ComponentValidationRules.FamilyMustBeStated,
                "The record states no component family, so nothing else on it can be interpreted."));

        if (definition.Family == ComponentFamily.Other && string.IsNullOrWhiteSpace(definition.SourceClassification))
            errors.Add(Diagnostic(
                ComponentValidationRules.OtherFamilyNeedsSourceClassification,
                "A component classified 'Other' must record the source's own classification wording in SourceClassification."));

        if (definition.Spring is null && definition.Gear is null && definition.DriveElement is null
            && !definition.Dimensions.IsRecorded && !definition.Ratings.IsRecorded)
            warnings.Add(Diagnostic(
                ComponentValidationRules.NoEngineeringDataRecorded,
                "No dimension, rating or detail of any kind is recorded — a data gap, not a component with no properties."));
    }

    /// <summary>
    /// The rule that makes one taxonomy over three kinds of component safe:
    /// a typed detail belongs to exactly the families it describes.
    /// </summary>
    private static void EvaluateDetailApplicability(ComponentDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var family = definition.Family;
        var present = new List<string>();

        if (definition.Spring is not null)
            present.Add(nameof(ComponentDefinition.Spring));
        if (definition.Gear is not null)
            present.Add(nameof(ComponentDefinition.Gear));
        if (definition.DriveElement is not null)
            present.Add(nameof(ComponentDefinition.DriveElement));

        if (present.Count > 1)
            errors.Add(Diagnostic(
                ComponentValidationRules.MultipleDetailsRecorded,
                $"The record carries {string.Join(" and ", present)} detail at once; a component is one kind of thing."));

        if (!ComponentFamilyTraits.IsApplicabilityKnown(family))
            return;

        if (definition.Spring is not null && !ComponentFamilyTraits.HasSpringDetail(family))
            errors.Add(Diagnostic(
                ComponentValidationRules.DetailNotApplicableToFamily,
                $"A spring detail is recorded, but a {family} is not a spring."));

        if (definition.Gear is not null && !ComponentFamilyTraits.HasGearDetail(family))
            errors.Add(Diagnostic(
                ComponentValidationRules.DetailNotApplicableToFamily,
                $"A gear detail is recorded, but a {family} is not a gear."));

        if (definition.DriveElement is not null && !ComponentFamilyTraits.HasDriveElementDetail(family))
            errors.Add(Diagnostic(
                ComponentValidationRules.DetailNotApplicableToFamily,
                $"A drive-element detail is recorded, but a {family} is not a belt, chain, pulley or sprocket."));

        if (definition.Spring is null && ComponentFamilyTraits.HasSpringDetail(family))
            warnings.Add(MissingDetail(family, "spring"));

        if (definition.Gear is null && ComponentFamilyTraits.HasGearDetail(family))
            warnings.Add(MissingDetail(family, "gear"));

        if (definition.DriveElement is null && ComponentFamilyTraits.HasDriveElementDetail(family))
            warnings.Add(MissingDetail(family, "drive-element"));
    }

    private static IValidationDiagnostic MissingDetail(ComponentFamily family, string detail) => Diagnostic(
        ComponentValidationRules.DetailShouldBeRecordedForFamily,
        $"A {family} is described by a {detail} detail, but none is recorded.");

    private static void EvaluateSpring(ComponentDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.Spring is not { } spring)
            return;

        var family = definition.Family;
        var known = ComponentFamilyTraits.IsApplicabilityKnown(family);

        // A torsion spring's rate is a torque per unit angle and nothing
        // else's is. Recording one as the other is a category error the
        // units alone cannot catch, which is why TorsionalStiffness is a
        // dimension of its own.
        if (known && ComponentFamilyTraits.HasSpringDetail(family))
        {
            var torsional = ComponentFamilyTraits.HasTorsionalRate(family);

            if (torsional && spring.Rate is not null)
                errors.Add(Diagnostic(
                    ComponentValidationRules.SpringRateFormDoesNotMatchFamily,
                    $"A {family}'s rate is a torque per unit angle, but a force-per-deflection rate is recorded."));

            if (!torsional && spring.TorsionalRate is not null)
                errors.Add(Diagnostic(
                    ComponentValidationRules.SpringRateFormDoesNotMatchFamily,
                    $"A {family}'s rate is a force per unit deflection, but a torque-per-angle rate is recorded."));
        }

        EvaluatePositive(spring.Rate, "The spring rate", errors, warnings);
        EvaluatePositive(spring.TorsionalRate, "The torsional spring rate", errors, warnings);
        EvaluatePositive(spring.FreeLength, "The free length", errors, warnings);
        EvaluatePositive(spring.SolidLength, "The solid length", errors, warnings);
        EvaluatePositive(spring.OutsideDiameter, "The coil outside diameter", errors, warnings);
        EvaluatePositive(spring.InsideDiameter, "The coil inside diameter", errors, warnings);
        EvaluatePositive(spring.WireDiameter, "The wire diameter", errors, warnings);
        EvaluatePositive(spring.TotalCoils, "The total coil count", errors, warnings);
        EvaluatePositive(spring.ActiveCoils, "The active coil count", errors, warnings);
        EvaluatePositive(spring.MaximumDeflection, "The maximum deflection", errors, warnings);
        EvaluatePositive(spring.MaximumLoad, "The maximum load", errors, warnings);
        EvaluatePositive(spring.MaximumTorque, "The maximum torque", errors, warnings);

        if (spring.FreeLength is { } free && spring.SolidLength is { } solid && solid.CanonicalValue >= free.CanonicalValue)
            errors.Add(Diagnostic(
                ComponentValidationRules.SolidLengthNotShorterThanFreeLength,
                $"The solid length ({solid.Value}) is not shorter than the free length ({free.Value}); the spring has no travel."));

        if (spring.TotalCoils is { } total && spring.ActiveCoils is { } active && active.CanonicalValue > total.CanonicalValue)
            errors.Add(Diagnostic(
                ComponentValidationRules.ActiveCoilsExceedTotalCoils,
                $"The active coil count ({active.Value}) exceeds the total coil count ({total.Value})."));

        if (spring.OutsideDiameter is { } outside && spring.InsideDiameter is { } inside)
        {
            if (inside.CanonicalValue >= outside.CanonicalValue)
                errors.Add(Diagnostic(
                    ComponentValidationRules.InsideDiameterNotSmallerThanOutside,
                    $"The coil inside diameter ({inside.Value}) is not smaller than the outside diameter ({outside.Value})."));
            else if (spring.WireDiameter is { } wire)
            {
                // Outside minus inside is two wire diameters, exactly. A
                // mismatch means one of the three was mis-transcribed.
                var implied = (outside.CanonicalValue - inside.CanonicalValue) / 2.0;
                var tolerance = Math.Max(implied, wire.CanonicalValue) * 0.02;

                if (Math.Abs(implied - wire.CanonicalValue) > tolerance)
                    errors.Add(Diagnostic(
                        ComponentValidationRules.WireDiameterInconsistentWithCoilDiameters,
                        $"The wire diameter ({wire.Value}) does not agree with the recorded coil diameters, which imply {implied:0.#####} m."));
            }
        }

        if (known && ComponentFamilyTraits.IsHelicalSpring(family) && spring.WindingDirection == SpringWindingDirection.Unspecified)
            warnings.Add(Diagnostic(
                ComponentValidationRules.HandednessShouldBeRecorded,
                "The spring records no winding direction. A spring wound the wrong way for its application unwinds under load, so this is never safe to assume."));
    }

    private static void EvaluateGear(ComponentDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.Gear is not { } gear)
            return;

        var family = definition.Family;
        var known = ComponentFamilyTraits.IsApplicabilityKnown(family);

        if (gear.NumberOfTeeth is { } teeth && teeth <= 0)
            errors.Add(Diagnostic(
                ComponentValidationRules.ToothCountMustBePositive,
                $"The tooth count is recorded as {teeth}; a gear has at least one tooth. Omit it instead if the source gave none."));

        if (gear.NumberOfStarts is { } starts && starts <= 0)
            errors.Add(Diagnostic(
                ComponentValidationRules.ToothCountMustBePositive,
                $"The worm start count is recorded as {starts}; a worm has at least one start."));

        EvaluatePositive(gear.Module, "The module", errors, warnings);
        EvaluatePositive(gear.FaceWidth, "The face width", errors, warnings);
        EvaluatePositive(gear.PitchDiameter, "The pitch diameter", errors, warnings);
        EvaluatePositive(gear.OutsideDiameter, "The tip diameter", errors, warnings);
        EvaluatePositive(gear.Lead, "The lead", errors, warnings);

        if (gear.PressureAngle is { } pressure
            && (pressure.CanonicalValue <= 0 || pressure.CanonicalValue >= MaximumCrediblePressureAngleRadians))
            errors.Add(Diagnostic(
                ComponentValidationRules.PressureAngleOutOfRange,
                $"The pressure angle is {pressure.Value}; no real involute gear is cut above 45 degrees or at or below zero."));

        if (known && gear.HelixAngle is not null && family is ComponentFamily.SpurGear)
            errors.Add(Diagnostic(
                ComponentValidationRules.HelixAngleDoesNotMatchFamily,
                "A helix angle is recorded, but a spur gear's teeth are parallel to its own axis."));

        if (known && family is ComponentFamily.HelicalGear && gear.HelixHand == GearHelixHand.Unspecified)
            warnings.Add(Diagnostic(
                ComponentValidationRules.HandednessShouldBeRecorded,
                "The gear records no helix hand. A meshing external pair needs opposite hands, so this is never safe to assume."));

        if (known && family is ComponentFamily.SpurGear && gear.HelixHand is not (GearHelixHand.Unspecified or GearHelixHand.None))
            errors.Add(Diagnostic(
                ComponentValidationRules.HelixAngleDoesNotMatchFamily,
                $"A {gear.HelixHand} helix hand is recorded, but a spur gear has no helix."));

        // An external gear's tips stand outside its reference cylinder; an
        // internal gear's stand inside it, which is why the rule is
        // restricted rather than applied to every gear.
        var external = family is ComponentFamily.SpurGear or ComponentFamily.HelicalGear or ComponentFamily.BevelGear;

        if (external
            && gear.PitchDiameter is { } pitch
            && gear.OutsideDiameter is { } tip
            && tip.CanonicalValue <= pitch.CanonicalValue)
            errors.Add(Diagnostic(
                ComponentValidationRules.OutsideDiameterNotGreaterThanPitchDiameter,
                $"The tip diameter ({tip.Value}) is not greater than the pitch diameter ({pitch.Value}); an external gear's teeth stand outside its reference cylinder."));
    }

    private static void EvaluateDriveElement(ComponentDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.DriveElement is not { } drive)
            return;

        EvaluatePositive(drive.Pitch, "The pitch", errors, warnings);
        EvaluatePositive(drive.Width, "The width", errors, warnings);
        EvaluatePositive(drive.PitchLength, "The pitch length", errors, warnings);
        EvaluatePositive(drive.PitchDiameter, "The pitch diameter", errors, warnings);
        EvaluatePositive(drive.OutsideDiameter, "The outside diameter", errors, warnings);
        EvaluatePositive(drive.MinimumPulleyDiameter, "The minimum pulley diameter", errors, warnings);

        foreach (var (count, label) in new[]
                 {
                     (drive.NumberOfTeeth, "tooth"), (drive.NumberOfLinks, "link"), (drive.NumberOfGrooves, "groove"),
                 })
        {
            if (count is { } value && value <= 0)
                errors.Add(Diagnostic(
                    ComponentValidationRules.ToothCountMustBePositive,
                    $"The {label} count is recorded as {value}; omit it instead if the source gave none."));
        }
    }

    private static void EvaluateDimensions(ComponentDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var dimensions = definition.Dimensions;

        EvaluatePositive(dimensions.BoreDiameter, "The bore diameter", errors, warnings);
        EvaluatePositive(dimensions.OutsideDiameter, "The outside diameter", errors, warnings);
        EvaluatePositive(dimensions.OverallLength, "The overall length", errors, warnings);
        EvaluatePositive(dimensions.OverallWidth, "The overall width", errors, warnings);
        EvaluatePositive(dimensions.OverallHeight, "The overall height", errors, warnings);
        EvaluatePositive(dimensions.Mass, "The mass", errors, warnings);

        if (dimensions.BoreDiameter is { } bore)
        {
            if (dimensions.OutsideDiameter is { } outside && bore.CanonicalValue >= outside.CanonicalValue)
                errors.Add(Diagnostic(
                    ComponentValidationRules.BoreNotSmallerThanOutsideDiameter,
                    $"The bore ({bore.Value}) is not smaller than the outside diameter ({outside.Value}); the component has no wall."));

            if (ComponentFamilyTraits.IsApplicabilityKnown(definition.Family) && !ComponentFamilyTraits.HasBore(definition.Family))
                warnings.Add(Diagnostic(
                    ComponentValidationRules.BoreNotApplicableToFamily,
                    $"A bore is recorded, but a {definition.Family} has none."));
        }
    }

    private static void EvaluateRatings(ComponentDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var ratings = definition.Ratings;
        var family = definition.Family;
        var known = ComponentFamilyTraits.IsApplicabilityKnown(family);

        EvaluatePositive(ratings.MaximumSpeed, "The maximum speed", errors, warnings);
        EvaluatePositive(ratings.RatedTorque, "The rated torque", errors, warnings);
        EvaluatePositive(ratings.MaximumTorque, "The maximum torque", errors, warnings);
        EvaluatePositive(ratings.RatedPower, "The rated power", errors, warnings);
        EvaluatePositive(ratings.MaximumAxialLoad, "The maximum axial load", errors, warnings);
        EvaluatePositive(ratings.MaximumRadialLoad, "The maximum radial load", errors, warnings);

        EvaluateRange(ratings.OperatingTemperatureRange, "The operating temperature range", errors, warnings);

        if (known && ratings.MaximumSpeed is not null && !ComponentFamilyTraits.Rotates(family))
            warnings.Add(Diagnostic(
                ComponentValidationRules.SpeedRatingNotApplicableToFamily,
                $"A speed rating is recorded, but a {family} does not rotate in service."));

        if (known && (ratings.RatedTorque is not null || ratings.MaximumTorque is not null) && !ComponentFamilyTraits.TransmitsTorque(family))
            warnings.Add(Diagnostic(
                ComponentValidationRules.TorqueRatingNotApplicableToFamily,
                $"A torque rating is recorded, but a {family} transmits no torque."));

        if (ratings.RatedTorque is { } rated && ratings.MaximumTorque is { } maximum && rated.CanonicalValue > maximum.CanonicalValue)
            errors.Add(Diagnostic(
                ComponentValidationRules.RatedTorqueExceedsMaximumTorque,
                $"The rated torque ({rated.Value}) exceeds the maximum torque ({maximum.Value})."));
    }

    private static void EvaluatePositive<TDimension>(
        ReferenceValue<TDimension>? value,
        string label,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
        where TDimension : IDimension =>
        EvaluatePositiveValue(value, label, ComponentValidationRules.ValueMustBePositive, errors, warnings);
}
