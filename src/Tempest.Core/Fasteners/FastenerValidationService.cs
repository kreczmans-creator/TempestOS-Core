using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Fasteners;

/// <summary>The concrete <see cref="IFastenerValidationService"/> implementation.</summary>
/// <remarks>
/// Provenance, verification attributability, supersession, standard
/// resolution and material resolution are checked by
/// <see cref="ReferenceValidationService{TDefinition}"/>, shared with every
/// Group A library. Everything below is fastener engineering: the
/// applicability <see cref="FastenerFamilyTraits"/> defines, the values
/// geometry and physics forbid, and the relationships between published
/// figures that cannot both hold.
/// </remarks>
public sealed class FastenerValidationService : ReferenceValidationService<FastenerDefinition>, IFastenerValidationService
{
    /// <summary>
    /// Initialises a new instance of the <see cref="FastenerValidationService"/> class.
    /// </summary>
    /// <param name="catalog">The catalogue whose records this service validates.</param>
    /// <param name="materialCatalog">The canonical Materials catalogue, for confirming a linked material resolves. Optional.</param>
    /// <param name="standardResolver">Resolves a cited standard against the Standards Library. Optional.</param>
    public FastenerValidationService(
        IFastenerCatalog catalog,
        IMaterialCatalog? materialCatalog = null,
        IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog, standardResolver)
    {
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        FastenerDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateClassification(definition, errors, warnings);
        EvaluateThread(definition, errors, warnings);
        EvaluateDimensions(definition, errors, warnings);
        EvaluateMechanical(definition, errors, warnings);
        EvaluateTorqueReferences(definition, errors, warnings);

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);

        if (definition.MaterialId is { } materialId)
            await EvaluateMaterialReferencesAsync([materialId], warnings, cancellationToken).ConfigureAwait(false);
        else if (!string.IsNullOrWhiteSpace(definition.MaterialDesignation))
            warnings.Add(Diagnostic(
                FastenerValidationRules.MaterialShouldBeLinked,
                $"The material is recorded only as text ('{definition.MaterialDesignation}') and is not linked to a registered material record."));
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<FastenerDefinition> record,
        IReadOnlyList<IReferenceRecord<FastenerDefinition>>? library,
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
                FastenerValidationRules.DuplicateIdentity,
                $"Fastener '{record.Definition.Designation}' shares its identity key with: {string.Join(", ", collisions)}."));
    }

    private static void EvaluateClassification(FastenerDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var family = definition.Family;

        if (family == FastenerFamily.Unspecified)
            errors.Add(Diagnostic(
                FastenerValidationRules.FamilyMustBeStated,
                "The record states no fastener family, so nothing else on it can be interpreted."));

        var classifiedOther = family == FastenerFamily.Other
            || definition.HeadType == FastenerHeadType.Other
            || definition.DriveType == FastenerDriveType.Other
            || definition.Thread?.System == ThreadSystem.Other;

        if (classifiedOther && string.IsNullOrWhiteSpace(definition.SourceClassification))
            errors.Add(Diagnostic(
                FastenerValidationRules.OtherClassificationNeedsSourceClassification,
                "The record is classified 'Other' in at least one taxonomy but records none of the source's own wording in SourceClassification."));

        if (!FastenerFamilyTraits.IsApplicabilityKnown(family))
            return;

        if (definition.HeadType is not (FastenerHeadType.Unspecified or FastenerHeadType.None)
            && !FastenerFamilyTraits.HasHead(family))
            errors.Add(Diagnostic(
                FastenerValidationRules.HeadNotApplicableToFamily,
                $"A {definition.HeadType} head is recorded, but a {family} has no head."));

        if (definition.DriveType is not (FastenerDriveType.Unspecified or FastenerDriveType.None)
            && !FastenerFamilyTraits.HasDriveFeature(family))
            errors.Add(Diagnostic(
                FastenerValidationRules.DriveNotApplicableToFamily,
                $"A {definition.DriveType} drive is recorded, but a {family} has no driving feature."));

        if (definition.Mechanical.PropertyClass is not null && !FastenerFamilyTraits.TakesPropertyClass(family))
            warnings.Add(Diagnostic(
                FastenerValidationRules.PropertyClassNotApplicableToFamily,
                $"A property class is recorded, but a {family} carries none."));

        if (!definition.Dimensions.IsRecorded && !definition.Mechanical.IsRecorded)
            warnings.Add(Diagnostic(
                FastenerValidationRules.NoEngineeringDataRecorded,
                "No dimension and no mechanical property is recorded — a data gap, not a fastener with no dimensions."));
    }

    private static void EvaluateThread(FastenerDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var family = definition.Family;
        var applicabilityKnown = FastenerFamilyTraits.IsApplicabilityKnown(family);

        if (definition.Thread is null)
        {
            if (applicabilityKnown && FastenerFamilyTraits.IsThreaded(family))
                warnings.Add(Diagnostic(
                    FastenerValidationRules.ThreadMustBeRecordedForAThreadedFamily,
                    $"A {family} is threaded, but no thread specification is recorded."));

            return;
        }

        if (applicabilityKnown && !FastenerFamilyTraits.IsThreaded(family))
            errors.Add(Diagnostic(
                FastenerValidationRules.ThreadNotApplicableToFamily,
                $"A thread is recorded, but a {family} carries none."));

        var thread = definition.Thread;

        if (thread.Handedness == ThreadHandedness.Unspecified)
            warnings.Add(Diagnostic(
                FastenerValidationRules.ThreadHandednessShouldBeRecorded,
                $"Thread '{thread.Designation}' records no handedness. A left-hand thread fitted as a right-hand one fails, so this is never safe to assume."));

        EvaluatePositive(thread.NominalDiameter, "The nominal thread diameter", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(thread.Pitch, "The thread pitch", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(thread.ThreadLength, "The thread length", FastenerValidationRules.DimensionMustBePositive, errors, warnings);

        if (thread.NominalDiameter is { } diameter && thread.Pitch is { } pitch && pitch.CanonicalValue >= diameter.CanonicalValue)
            errors.Add(Diagnostic(
                FastenerValidationRules.PitchExceedsNominalDiameter,
                $"The thread pitch ({pitch.Value}) is not smaller than the nominal diameter ({diameter.Value}); no real thread is formed."));
    }

    private static void EvaluateDimensions(FastenerDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var dimensions = definition.Dimensions;

        EvaluatePositive(dimensions.NominalLength, "The nominal length", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.HeadDiameter, "The head diameter", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.HeadHeight, "The head height", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.WidthAcrossFlats, "The width across flats", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.WidthAcrossCorners, "The width across corners", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.DriveSize, "The drive size", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.Height, "The height", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.InsideDiameter, "The inside diameter", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.OutsideDiameter, "The outside diameter", FastenerValidationRules.DimensionMustBePositive, errors, warnings);
        EvaluatePositive(dimensions.ShankDiameter, "The shank diameter", FastenerValidationRules.DimensionMustBePositive, errors, warnings);

        EvaluateRange(dimensions.GripRange, "The grip range", errors, warnings);

        if (dimensions.WidthAcrossFlats is { } flats
            && dimensions.WidthAcrossCorners is { } corners
            && corners.CanonicalValue <= flats.CanonicalValue)
            errors.Add(Diagnostic(
                FastenerValidationRules.WidthAcrossCornersNotGreaterThanFlats,
                $"The width across corners ({corners.Value}) is not greater than the width across flats ({flats.Value}); no polygon has that geometry."));

        if (dimensions.InsideDiameter is { } inside
            && dimensions.OutsideDiameter is { } outside
            && outside.CanonicalValue <= inside.CanonicalValue)
            errors.Add(Diagnostic(
                FastenerValidationRules.DimensionMustBePositive,
                $"The outside diameter ({outside.Value}) is not greater than the inside diameter ({inside.Value}); the item has no wall."));
    }

    private static void EvaluateMechanical(FastenerDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var mechanical = definition.Mechanical;

        EvaluatePositive(mechanical.ProofStrength, "The proof strength", FastenerValidationRules.MechanicalValueMustBePositive, errors, warnings);
        EvaluatePositive(mechanical.TensileStrength, "The tensile strength", FastenerValidationRules.MechanicalValueMustBePositive, errors, warnings);
        EvaluatePositive(mechanical.YieldStrength, "The yield strength", FastenerValidationRules.MechanicalValueMustBePositive, errors, warnings);
        EvaluatePositive(mechanical.ProofLoad, "The proof load", FastenerValidationRules.MechanicalValueMustBePositive, errors, warnings);
        EvaluatePositive(mechanical.MinimumBreakingLoad, "The minimum breaking load", FastenerValidationRules.MechanicalValueMustBePositive, errors, warnings);
        EvaluatePositive(mechanical.StressArea, "The tensile stress area", FastenerValidationRules.MechanicalValueMustBePositive, errors, warnings);

        if (mechanical.TensileStrength is { } tensile)
        {
            if (mechanical.YieldStrength is { } yield && yield.CanonicalValue > tensile.CanonicalValue)
                errors.Add(Diagnostic(
                    FastenerValidationRules.StrengthExceedsTensile,
                    "The yield strength exceeds the tensile strength; no real fastener yields above the stress that breaks it."));

            if (mechanical.ProofStrength is { } proof && proof.CanonicalValue > tensile.CanonicalValue)
                errors.Add(Diagnostic(
                    FastenerValidationRules.StrengthExceedsTensile,
                    "The proof strength exceeds the tensile strength; no real fastener holds proof load above the stress that breaks it."));
        }

        if (mechanical.ProofLoad is { } proofLoad
            && mechanical.MinimumBreakingLoad is { } breaking
            && proofLoad.CanonicalValue > breaking.CanonicalValue)
            errors.Add(Diagnostic(
                FastenerValidationRules.ProofLoadExceedsBreakingLoad,
                "The proof load exceeds the minimum breaking load; a fastener cannot hold more than the load that breaks it."));

        if (mechanical.Hardness is { } hardness)
        {
            if (hardness.IsInverted)
                errors.Add(Diagnostic(
                    FastenerValidationRules.HardnessBandInverted,
                    $"The {hardness.Scale} hardness band has a maximum ({hardness.Maximum}) below its own minimum ({hardness.Minimum})."));

            if (hardness.IsDerived)
                warnings.Add(Diagnostic(
                    ReferenceValidationRules.DerivedValuePresent,
                    $"The {hardness.Scale} hardness was derived by TempestOS and must not be presented as source reference data."));
        }
    }

    private static void EvaluateTorqueReferences(FastenerDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.TorqueReferences.Count == 0)
            return;

        if (FastenerFamilyTraits.IsApplicabilityKnown(definition.Family) && !FastenerFamilyTraits.TakesTighteningTorque(definition.Family))
            errors.Add(Diagnostic(
                FastenerValidationRules.TorqueNotApplicableToFamily,
                $"A tightening torque is recorded, but a {definition.Family} is not tightened."));

        foreach (var torque in definition.TorqueReferences)
        {
            if (torque.CanonicalValue <= 0)
                errors.Add(Diagnostic(
                    FastenerValidationRules.MechanicalValueMustBePositive,
                    $"A tightening torque is recorded as {torque.Torque}; a published figure here must be greater than zero."));

            if (torque.IsDerived)
                warnings.Add(Diagnostic(
                    ReferenceValidationRules.DerivedValuePresent,
                    "A tightening torque is marked as derived by TempestOS. A3 records published figures only; a computed torque is calculation output and must not be presented as reference data."));

            if (!torque.StatesConditions)
                warnings.Add(Diagnostic(
                    FastenerValidationRules.TorqueReferenceStatesNoConditions,
                    $"The tightening torque {torque.Torque} records no friction or lubrication conditions. "
                    + "A torque figure separated from the conditions it was published for is a number, not reference data."));
        }
    }

    private static void EvaluatePositive<TDimension>(
        ReferenceValue<TDimension>? value,
        string label,
        string ruleCode,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
        where TDimension : IDimension =>
        EvaluatePositiveValue(value, label, ruleCode, errors, warnings);
}
