using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>The concrete <see cref="IBearingValidationService"/> implementation.</summary>
/// <remarks>
/// <para>
/// Provenance, verification attributability, supersession and reference
/// resolution are checked by
/// <see cref="ReferenceValidationService{TDefinition}"/>, shared with
/// every Group A library. Everything below is bearing engineering: the
/// dimensional relationships a bearing must satisfy, the ratings it must
/// state positively where it states them at all, and the type-aware
/// applicability <see cref="BearingFamilyTraits"/> defines.
/// </para>
/// <para>
/// <see cref="IMaterialCatalog"/> is an optional collaborator — see the
/// base class's own remarks for why.
/// </para>
/// </remarks>
public sealed class BearingValidationService : ReferenceValidationService<BearingDefinition>, IBearingValidationService
{
    private const double DegreesPerRadian = 180.0 / Math.PI;

    /// <summary>
    /// Initialises a new instance of the <see cref="BearingValidationService"/> class.
    /// </summary>
    /// <param name="catalog">The catalogue whose records this service validates.</param>
    /// <param name="materialCatalog">The canonical Materials catalogue, for confirming material references resolve. Optional.</param>
    /// <param name="standardResolver">Resolves a cited standard against the Standards Library. Optional.</param>
    public BearingValidationService(
        IBearingCatalog catalog,
        IMaterialCatalog? materialCatalog = null,
        IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog, standardResolver)
    {
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        BearingDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateClassification(definition, errors, warnings);
        EvaluateGeometry(definition, errors);
        EvaluateLoadRatings(definition, errors, warnings);
        EvaluateSpeedRatings(definition, errors, warnings);
        EvaluateMass(definition, errors);
        EvaluateConfiguration(definition, errors);
        EvaluateConstruction(definition, warnings);

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateMaterialReferencesAsync(
            definition.Construction?.ReferencedMaterialIds ?? [],
            warnings,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<BearingDefinition> record,
        IReadOnlyList<IReferenceRecord<BearingDefinition>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        // Defence in depth: the catalogue already prevents this at write
        // time (DuplicateReferenceKeyException). Confirming it on read
        // catches a catalogue whose part-number index was written before
        // that guard existed, or corrupted since — the same reasoning
        // RequirementValidationService applies to duplicate identifiers.
        var key = record.Definition.Identity.PartNumberKey;
        var others = library ?? await Catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var collisions = others
            .Where(other => !string.Equals(other.Id, record.Id, StringComparison.Ordinal))
            .Where(other => string.Equals(other.Definition.Identity.PartNumberKey, key, StringComparison.Ordinal))
            .Select(other => other.Id)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                BearingValidationRules.DuplicatePartNumber,
                $"Manufacturer '{record.Definition.Identity.Manufacturer}' part number "
                + $"'{record.Definition.Identity.ManufacturerPartNumber}' is also registered as: {string.Join(", ", collisions)}."));
    }

    private static void EvaluateClassification(BearingDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.Family == BearingFamily.Unspecified)
            errors.Add(Diagnostic(
                BearingValidationRules.FamilyMustBeStated,
                "The record states no bearing family, so nothing else on it can be interpreted."));

        if (definition.Family == BearingFamily.Other && string.IsNullOrWhiteSpace(definition.Identity.FamilyDesignation))
            errors.Add(Diagnostic(
                BearingValidationRules.OtherFamilyNeedsDesignation,
                "A bearing classified 'Other' must record the source's own wording for its type in Identity.FamilyDesignation."));

        if (string.IsNullOrWhiteSpace(definition.Identity.Designation))
            warnings.Add(Diagnostic(
                BearingValidationRules.DesignationShouldBeRecorded,
                "The record carries a part number but no bearing designation."));
    }

    private static void EvaluateGeometry(BearingDefinition definition, List<IValidationDiagnostic> errors)
    {
        var geometry = definition.Geometry;
        var bore = BearingGeometry.ToMetres(geometry.Bore);
        var outsideDiameter = BearingGeometry.ToMetres(geometry.OutsideDiameter);
        var width = BearingGeometry.ToMetres(geometry.Width);
        var overallWidth = BearingGeometry.ToMetres(geometry.OverallWidth);

        if (bore is <= 0)
            errors.Add(Diagnostic(
                BearingValidationRules.BoreMustBePositive,
                $"Bore is {geometry.Bore}; a bore must be greater than zero."));

        if (bore is not null && outsideDiameter is not null && outsideDiameter <= bore)
            errors.Add(Diagnostic(
                BearingValidationRules.OutsideDiameterMustExceedBore,
                $"Outside diameter {geometry.OutsideDiameter} is not greater than bore {geometry.Bore}."));

        if (width is <= 0)
            errors.Add(Diagnostic(
                BearingValidationRules.WidthMustBePositive,
                $"Width is {geometry.Width}; a width must be greater than zero."));

        if (overallWidth is <= 0)
            errors.Add(Diagnostic(
                BearingValidationRules.WidthMustBePositive,
                $"Overall width is {geometry.OverallWidth}; a width must be greater than zero."));

        if (width is not null && overallWidth is not null && overallWidth < width)
            errors.Add(Diagnostic(
                BearingValidationRules.OverallWidthLessThanWidth,
                $"Overall width {geometry.OverallWidth} is less than nominal width {geometry.Width}."));
    }

    private static void EvaluateLoadRatings(BearingDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var ratings = definition.LoadRatings;

        if (ratings is null || NamedRatings(ratings).Count == 0)
        {
            if (BearingFamilyTraits.HasRollingElements(definition.Family) && BearingFamilyTraits.IsApplicabilityKnown(definition.Family))
                warnings.Add(Diagnostic(
                    BearingValidationRules.NoLoadRatingRecorded,
                    "No load rating of any kind is recorded — a data gap, not a rating of zero."));

            return;
        }

        foreach (var (label, rating) in NamedRatings(ratings))
            EvaluatePositiveValue(rating, $"Load rating '{label}'", BearingValidationRules.LoadRatingMustBePositive, errors, warnings);
    }

    private static void EvaluateSpeedRatings(BearingDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        foreach (var speed in definition.SpeedRatings)
            EvaluatePositiveValue(speed.Rating, $"Speed rating '{speed.Kind}'", BearingValidationRules.SpeedRatingMustBePositive, errors, warnings);
    }

    private static void EvaluateMass(BearingDefinition definition, List<IValidationDiagnostic> errors)
    {
        if (definition.Mass is { } mass && mass.BaseValue < 0)
            errors.Add(Diagnostic(
                BearingValidationRules.MassMustNotBeNegative,
                $"Mass is {mass}; a mass cannot be negative."));
    }

    private static void EvaluateConfiguration(BearingDefinition definition, List<IValidationDiagnostic> errors)
    {
        var configuration = definition.Configuration;
        if (configuration is null)
            return;

        var family = definition.Family;
        var applicabilityKnown = BearingFamilyTraits.IsApplicabilityKnown(family);

        if (configuration.ContactAngle is { } contactAngle)
        {
            if (applicabilityKnown && !BearingFamilyTraits.HasContactAngle(family))
                errors.Add(Diagnostic(
                    BearingValidationRules.ContactAngleNotApplicableToFamily,
                    $"A contact angle is recorded, but a nominal contact angle is not a characteristic of a {family} bearing."));

            var degrees = contactAngle.BaseValue * DegreesPerRadian;
            if (degrees is <= 0 or > 90)
                errors.Add(Diagnostic(
                    BearingValidationRules.ContactAngleOutOfRange,
                    $"Contact angle {contactAngle} is {degrees:0.###} degrees; a contact angle must lie above zero and at most ninety degrees."));
        }

        var recordsClearance = configuration.InternalClearanceClass is not null
            || configuration.PreloadClass is not null
            || configuration.RadialInternalClearanceMinimum is not null
            || configuration.RadialInternalClearanceMaximum is not null;

        if (recordsClearance && applicabilityKnown && !BearingFamilyTraits.HasInternalClearance(family))
            errors.Add(Diagnostic(
                BearingValidationRules.ClearanceNotApplicableToFamily,
                $"Internal clearance or preload information is recorded, but a {family} bearing has no rolling-element internal clearance."));

        var minimum = BearingGeometry.ToMetres(configuration.RadialInternalClearanceMinimum);
        var maximum = BearingGeometry.ToMetres(configuration.RadialInternalClearanceMaximum);
        if (minimum is not null && maximum is not null && maximum < minimum)
            errors.Add(Diagnostic(
                BearingValidationRules.ClearanceRangeInverted,
                $"Maximum radial internal clearance {configuration.RadialInternalClearanceMaximum} is less than the minimum {configuration.RadialInternalClearanceMinimum}."));
    }

    private static void EvaluateConstruction(BearingDefinition definition, List<IValidationDiagnostic> warnings)
    {
        var construction = definition.Construction;
        if (construction is null)
            return;

        if (construction.RollingElementMaterialId is not null
            && BearingFamilyTraits.IsApplicabilityKnown(definition.Family)
            && !BearingFamilyTraits.HasRollingElements(definition.Family))
            warnings.Add(Diagnostic(
                BearingValidationRules.RollingElementNotApplicableToFamily,
                $"A rolling-element material is recorded, but a {definition.Family} bearing has no rolling elements."));
    }

    private static IReadOnlyList<(string Label, ReferenceValue<Force> Rating)> NamedRatings(BearingLoadRatings ratings)
    {
        var named = new List<(string, ReferenceValue<Force>)>();

        if (ratings.BasicDynamicRadial is { } c) named.Add(("C", c));
        if (ratings.BasicStaticRadial is { } c0) named.Add(("C0", c0));
        if (ratings.BasicDynamicAxial is { } ca) named.Add(("Ca", ca));
        if (ratings.BasicStaticAxial is { } c0a) named.Add(("C0a", c0a));
        if (ratings.FatigueLoadLimit is { } pu) named.Add(("Pu", pu));

        foreach (var (label, rating) in ratings.ManufacturerRatings)
            named.Add((label, rating));

        return named;
    }
}
