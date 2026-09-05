using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>The concrete <see cref="IBearingValidationService"/> implementation.</summary>
/// <remarks>
/// <para>
/// A read-only service over <see cref="IBearingCatalog"/>, mirroring
/// <see cref="Requirements.RequirementValidationService"/>'s own shape:
/// it stores nothing, changes nothing, and never repairs what it finds.
/// </para>
/// <para>
/// <see cref="IMaterialCatalog"/> is an <em>optional</em> collaborator.
/// With it, a bearing's own material references are confirmed to resolve
/// against the canonical Materials catalogue
/// (<see cref="BearingValidationRules.MaterialReferenceUnresolved"/>);
/// without it, that one rule is simply not evaluated. Optional rather than
/// required deliberately — a bearing record must be recordable and
/// checkable before the material it names has been registered, and A4 must
/// not make the Materials system a hard prerequisite for holding bearing
/// data at all.
/// </para>
/// </remarks>
public sealed class BearingValidationService : IBearingValidationService
{
    private const double DegreesPerRadian = 180.0 / Math.PI;

    private readonly IBearingCatalog _catalog;
    private readonly IMaterialCatalog? _materialCatalog;

    /// <summary>
    /// Initialises a new instance of the <see cref="BearingValidationService"/> class.
    /// </summary>
    /// <param name="catalog">The catalogue whose records this service validates.</param>
    /// <param name="materialCatalog">The canonical Materials catalogue, for confirming material references resolve. Optional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    public BearingValidationService(IBearingCatalog catalog, IMaterialCatalog? materialCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        _materialCatalog = materialCatalog;
    }

    /// <inheritdoc />
    public async Task<IValidationResult> ValidateAsync(string bearingId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearingId);

        var bearing = await _catalog.FindAsync(bearingId, cancellationToken).ConfigureAwait(false)
            ?? throw new BearingNotFoundException(bearingId);

        return await ValidateAsync(bearing, catalogue: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The shared body of both public validate paths.
    /// <paramref name="catalogue"/> is the already-loaded catalogue when
    /// one is available — <see cref="ValidateCatalogueAsync"/> reads the
    /// whole catalogue once and passes it down, so validating N records
    /// costs one enumeration rather than N.
    /// </summary>
    private async Task<IValidationResult> ValidateAsync(IBearing bearing, IReadOnlyList<IBearing>? catalogue, CancellationToken cancellationToken)
    {
        var errors = new List<IValidationDiagnostic>();
        var warnings = new List<IValidationDiagnostic>();

        await EvaluateDefinitionAsync(bearing.Definition, errors, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateRecordAsync(bearing, catalogue, errors, warnings, cancellationToken).ConfigureAwait(false);

        return new ValidationResult(errors, warnings);
    }

    /// <inheritdoc />
    public async Task<IValidationResult> ValidateDefinitionAsync(BearingDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<IValidationDiagnostic>();
        var warnings = new List<IValidationDiagnostic>();

        await EvaluateDefinitionAsync(definition, errors, warnings, cancellationToken).ConfigureAwait(false);

        return new ValidationResult(errors, warnings);
    }

    /// <inheritdoc />
    public async Task<BearingDataQualityReport> ValidateCatalogueAsync(CancellationToken cancellationToken = default)
    {
        var bearings = await _catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<BearingDataQualityFinding>();

        foreach (var bearing in bearings)
        {
            var result = await ValidateAsync(bearing, bearings, cancellationToken).ConfigureAwait(false);
            if (result.Errors.Count > 0 || result.Warnings.Count > 0)
                findings.Add(new BearingDataQualityFinding(bearing.BearingId, bearing.ValidationState, result));
        }

        return new BearingDataQualityReport(findings, bearings.Count);
    }

    /// <summary>Rules that read only the record's own engineering content.</summary>
    private async Task EvaluateDefinitionAsync(
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
        EvaluateProvenance(definition, errors, warnings);
        await EvaluateMaterialsAsync(definition, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Rules that need the registered record, not just its content.</summary>
    private async Task EvaluateRecordAsync(
        IBearing bearing,
        IReadOnlyList<IBearing>? catalogue,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (bearing.ValidationState == BearingValidationState.Superseded && bearing.SupersededByBearingId is null)
            warnings.Add(Diagnostic(
                BearingValidationRules.SupersededWithoutReplacement,
                $"Bearing '{bearing.BearingId}' is superseded but names no replacement."));

        // Defence in depth: BearingCatalog already prevents this at write
        // time (DuplicateBearingPartNumberException). Confirming it on read
        // catches a catalogue whose part-number index was written before
        // that guard existed, or corrupted since — the same reasoning
        // RequirementValidationService applies to duplicate identifiers.
        var key = bearing.Definition.Identity.PartNumberKey;
        var others = catalogue ?? await _catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var collisions = others
            .Where(other => !string.Equals(other.BearingId, bearing.BearingId, StringComparison.Ordinal))
            .Where(other => string.Equals(other.Definition.Identity.PartNumberKey, key, StringComparison.Ordinal))
            .Select(other => other.BearingId)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                BearingValidationRules.DuplicatePartNumber,
                $"Manufacturer '{bearing.Definition.Identity.Manufacturer}' part number "
                + $"'{bearing.Definition.Identity.ManufacturerPartNumber}' is also registered as: {string.Join(", ", collisions)}."));
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

        if (ratings is null || !AnyRatingRecorded(ratings))
        {
            if (BearingFamilyTraits.HasRollingElements(definition.Family) && BearingFamilyTraits.IsApplicabilityKnown(definition.Family))
                warnings.Add(Diagnostic(
                    BearingValidationRules.NoLoadRatingRecorded,
                    "No load rating of any kind is recorded — a data gap, not a rating of zero."));

            return;
        }

        foreach (var (label, rating) in NamedRatings(ratings))
        {
            if (rating.CanonicalValue <= 0)
                errors.Add(Diagnostic(
                    BearingValidationRules.LoadRatingMustBePositive,
                    $"Load rating '{label}' is {rating.Value}; a recorded rating must be greater than zero. Omit it instead if the source gave none."));

            if (rating.Origin == BearingValueOrigin.DerivedByTempestOS)
                warnings.Add(Diagnostic(
                    BearingValidationRules.DerivedValuePresent,
                    $"Load rating '{label}' is derived by TempestOS and must not be presented as manufacturer reference data."));
        }
    }

    private static void EvaluateSpeedRatings(BearingDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        foreach (var speed in definition.SpeedRatings)
        {
            if (speed.Rating.CanonicalValue <= 0)
                errors.Add(Diagnostic(
                    BearingValidationRules.SpeedRatingMustBePositive,
                    $"Speed rating '{speed.Kind}' is {speed.Rating.Value}; a recorded speed must be greater than zero."));

            if (speed.Rating.Origin == BearingValueOrigin.DerivedByTempestOS)
                warnings.Add(Diagnostic(
                    BearingValidationRules.DerivedValuePresent,
                    $"Speed rating '{speed.Kind}' is derived by TempestOS and must not be presented as manufacturer reference data."));
        }
    }

    private static void EvaluateMass(BearingDefinition definition, List<IValidationDiagnostic> errors)
    {
        if (definition.Mass is { } mass && mass.Value * mass.Unit.ToBaseUnitFactor < 0)
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

            var degrees = contactAngle.Value * contactAngle.Unit.ToBaseUnitFactor * DegreesPerRadian;
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

    private static void EvaluateProvenance(BearingDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var provenance = definition.Provenance;

        if (!provenance.IdentifiesASource)
            warnings.Add(Diagnostic(
                BearingValidationRules.ProvenanceMustIdentifyASource,
                "Provenance names neither a source organisation nor a source document; this record cannot leave Draft until it does."));

        if (provenance.VerificationStatus == BearingVerificationStatus.VerifiedAgainstSource && !provenance.IsVerified)
            errors.Add(Diagnostic(
                BearingValidationRules.VerificationMustBeAttributable,
                "The record is marked verified against its source but names no reviewer, no verification date, or neither."));
    }

    private async Task EvaluateMaterialsAsync(BearingDefinition definition, List<IValidationDiagnostic> warnings, CancellationToken cancellationToken)
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

        if (_materialCatalog is null)
            return;

        foreach (var materialId in construction.ReferencedMaterialIds)
        {
            var material = await _materialCatalog.FindAsync(materialId, cancellationToken).ConfigureAwait(false);
            if (material is null)
                warnings.Add(Diagnostic(
                    BearingValidationRules.MaterialReferenceUnresolved,
                    $"Material '{materialId}' is referenced but is not registered in the Materials catalogue."));
        }
    }

    private static bool AnyRatingRecorded(BearingLoadRatings ratings) => NamedRatings(ratings).Count > 0;

    private static IReadOnlyList<(string Label, BearingRatedValue<Force> Rating)> NamedRatings(BearingLoadRatings ratings)
    {
        var named = new List<(string, BearingRatedValue<Force>)>();

        if (ratings.BasicDynamicRadial is { } c) named.Add(("C", c));
        if (ratings.BasicStaticRadial is { } c0) named.Add(("C0", c0));
        if (ratings.BasicDynamicAxial is { } ca) named.Add(("Ca", ca));
        if (ratings.BasicStaticAxial is { } c0a) named.Add(("C0a", c0a));
        if (ratings.FatigueLoadLimit is { } pu) named.Add(("Pu", pu));

        foreach (var (label, rating) in ratings.ManufacturerRatings)
            named.Add((label, rating));

        return named;
    }

    private static IValidationDiagnostic Diagnostic(string code, string message) => new ValidationDiagnostic(code, message);
}
