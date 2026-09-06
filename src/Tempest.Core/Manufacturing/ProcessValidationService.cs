using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Manufacturing;

/// <summary>The concrete <see cref="IProcessValidationService"/> implementation.</summary>
/// <remarks>
/// Provenance, verification attributability, supersession, standard
/// resolution and material resolution are checked by
/// <see cref="ReferenceValidationService{TDefinition}"/>, shared with every
/// Group A library. Everything below is manufacturing reference-keeping:
/// which capabilities describe which processes, the bands that cannot
/// hold, and the material claims that contradict one another.
/// </remarks>
public sealed class ProcessValidationService : ReferenceValidationService<ProcessDefinition>, IProcessValidationService
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ProcessValidationService"/> class.
    /// </summary>
    /// <param name="catalog">The library whose records this service validates.</param>
    /// <param name="materialCatalog">The canonical Materials catalogue, for confirming a named material resolves. Optional.</param>
    /// <param name="standardResolver">Resolves a cited standard against the Standards Library. Optional.</param>
    public ProcessValidationService(
        IProcessCatalog catalog,
        IMaterialCatalog? materialCatalog = null,
        IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog, standardResolver)
    {
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        ProcessDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateClassification(definition, errors, warnings);
        EvaluateCapabilities(definition, errors, warnings);
        EvaluateMaterialCompatibility(definition, errors, warnings);
        EvaluateProductionScales(definition, errors, warnings);
        EvaluateConstraints(definition, warnings);

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateMaterialReferencesAsync(
            definition.MaterialCompatibility.Select(entry => entry.MaterialId).OfType<string>(),
            warnings,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<ProcessDefinition> record,
        IReadOnlyList<IReferenceRecord<ProcessDefinition>>? library,
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
                ProcessValidationRules.DuplicateProcessIdentity,
                $"Process '{record.Definition.Name}' shares its identity key with: {string.Join(", ", collisions)}."));
    }

    private static void EvaluateClassification(ProcessDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.Family == ProcessFamily.Unspecified)
            errors.Add(Diagnostic(
                ProcessValidationRules.FamilyMustBeStated,
                "The record states no process family, so nothing else on it can be interpreted."));

        if (definition.Family == ProcessFamily.Other && string.IsNullOrWhiteSpace(definition.SourceClassification))
            errors.Add(Diagnostic(
                ProcessValidationRules.OtherFamilyNeedsSourceClassification,
                "A process classified 'Other' must record the source's own classification wording in SourceClassification."));

        if (!definition.Capabilities.IsRecorded)
            warnings.Add(Diagnostic(
                ProcessValidationRules.NoCapabilityRecorded,
                "No capability of any kind is recorded — a data gap, not a process with no capabilities."));

        if (definition.MaterialCompatibility.Count == 0)
            warnings.Add(Diagnostic(
                ProcessValidationRules.NoMaterialCompatibilityRecorded,
                "The record associates the process with no material — a data gap, not a process that works on nothing."));
    }

    private static void EvaluateCapabilities(ProcessDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var capabilities = definition.Capabilities;
        var family = definition.Family;
        var known = ProcessFamilyTraits.IsApplicabilityKnown(family);

        Band(capabilities.AchievableTolerance, "The achievable tolerance", positiveOnly: true, errors, warnings);
        Band(capabilities.SurfaceRoughness, "The surface roughness", positiveOnly: true, errors, warnings);
        Band(capabilities.WallThickness, "The wall thickness", positiveOnly: true, errors, warnings);
        Band(capabilities.PartSize, "The part size", positiveOnly: true, errors, warnings);
        Band(capabilities.PartMass, "The part mass", positiveOnly: true, errors, warnings);
        Band(capabilities.MinimumFeatureSize, "The minimum feature size", positiveOnly: true, errors, warnings);
        Band(capabilities.CornerRadius, "The corner radius", positiveOnly: false, errors, warnings);
        Band(capabilities.HoleDiameter, "The hole diameter", positiveOnly: true, errors, warnings);
        Band(capabilities.AspectRatio, "The aspect ratio", positiveOnly: true, errors, warnings);
        Band(capabilities.CycleTime, "The cycle time", positiveOnly: true, errors, warnings);
        Band(capabilities.DraftAngle, "The draft angle", positiveOnly: false, errors, warnings);

        // A temperature band is the one capability whose ends may
        // legitimately be negative or zero, so it is range-checked only.
        EvaluateRange(capabilities.ProcessTemperature, "The process temperature", errors, warnings);
        Origin(capabilities.ProcessTemperature, "The process temperature", warnings);

        if (!known)
            return;

        if (capabilities.DraftAngle is not null && !ProcessFamilyTraits.UsesAMouldOrDie(family))
            errors.Add(Diagnostic(
                ProcessValidationRules.CapabilityNotApplicableToFamily,
                $"A draft angle is recorded, but {family} forms nothing against a mould or die."));

        if (capabilities.WallThickness is not null && !ProcessFamilyTraits.HasWallThicknessCapability(family))
            errors.Add(Diagnostic(
                ProcessValidationRules.CapabilityNotApplicableToFamily,
                $"A wall thickness capability is recorded, but {family} does not produce a wall thickness."));

        if (capabilities.SurfaceRoughness is not null && !ProcessFamilyTraits.HasSurfaceRoughnessCapability(family))
            errors.Add(Diagnostic(
                ProcessValidationRules.CapabilityNotApplicableToFamily,
                $"A surface roughness capability is recorded, but {family} leaves no surface of its own."));

        if (capabilities.ProcessTemperature is not null && !ProcessFamilyTraits.HasProcessTemperature(family))
            warnings.Add(Diagnostic(
                ProcessValidationRules.CapabilityNotApplicableToFamily,
                $"A process temperature is recorded, but {family} has no controlled process temperature."));
    }

    private static void EvaluateMaterialCompatibility(ProcessDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var seen = new Dictionary<string, ProcessMaterialSuitability>(StringComparer.Ordinal);

        foreach (var entry in definition.MaterialCompatibility)
        {
            if (!entry.NamesAMaterial)
            {
                errors.Add(Diagnostic(
                    ProcessValidationRules.CompatibilityMustNameAMaterial,
                    "A material compatibility entry names no family, no registered material and no designation, so it says nothing."));
                continue;
            }

            var subject = entry.SubjectKey;

            if (seen.TryGetValue(subject, out var existing))
            {
                if (existing == entry.Suitability)
                    warnings.Add(Diagnostic(
                        ProcessValidationRules.DuplicateCompatibilityEntry,
                        $"The record states the same thing about {Describe(entry)} twice."));
                else
                    errors.Add(Diagnostic(
                        ProcessValidationRules.ContradictoryCompatibility,
                        $"The record says {Describe(entry)} is both {existing} and {entry.Suitability}."));
            }
            else
            {
                seen[subject] = entry.Suitability;
            }

            if (entry.Suitability == ProcessMaterialSuitability.Unspecified)
                warnings.Add(Diagnostic(
                    ProcessValidationRules.CompatibilitySuitabilityShouldBeStated,
                    $"The record associates {Describe(entry)} with the process but does not say whether the pairing works."));

            if (entry.Suitability == ProcessMaterialSuitability.ConditionallySuitable && string.IsNullOrWhiteSpace(entry.Conditions))
                warnings.Add(Diagnostic(
                    ProcessValidationRules.ConditionalCompatibilityNeedsConditions,
                    $"{Describe(entry)} is recorded as conditionally suitable, but the conditions that make it so are not stated."));

            if (entry.IsDerived)
                warnings.Add(Diagnostic(
                    ReferenceValidationRules.DerivedValuePresent,
                    $"The compatibility claim about {Describe(entry)} was derived by TempestOS. "
                    + "Whether a material can be processed a given way is a manufacturing judgement and must not be presented as source reference data."));
        }
    }

    private static void EvaluateProductionScales(ProcessDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.ProductionScales.Count == 0)
        {
            warnings.Add(Diagnostic(
                ProcessValidationRules.ProductionScaleShouldBeRecorded,
                "No production scale is recorded — a data gap, not a process that suits no scale."));
            return;
        }

        if (definition.ProductionScales.Contains(ProductionScale.Unspecified) && definition.ProductionScales.Count > 1)
            errors.Add(Diagnostic(
                ProcessValidationRules.UnspecifiedProductionScaleAlongsideAReal,
                "The record lists an unspecified production scale alongside real ones, which says both that a scale is recorded and that none is."));
    }

    private static void EvaluateConstraints(ProcessDefinition definition, List<IValidationDiagnostic> warnings)
    {
        foreach (var constraint in definition.Constraints)
        {
            if (constraint.Kind == ProcessConstraintKind.Unspecified)
                warnings.Add(Diagnostic(
                    ProcessValidationRules.ConstraintKindShouldBeStated,
                    $"The constraint \"{Truncate(constraint.Description)}\" does not say what kind of limitation it describes, so it cannot be filtered on."));

            if (constraint.IsDerived)
                warnings.Add(Diagnostic(
                    ReferenceValidationRules.DerivedValuePresent,
                    $"The constraint \"{Truncate(constraint.Description)}\" was derived by TempestOS and must not be presented as a source's own statement."));
        }
    }

    /// <summary>
    /// Checks one capability band: not inverted, not negative where the
    /// quantity forbids it, attributed, and stating the conditions a
    /// capability figure is meaningless without.
    /// </summary>
    private static void Band<TDimension>(
        ReferenceRange<TDimension>? band,
        string label,
        bool positiveOnly,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
        where TDimension : IDimension
    {
        if (band is null)
            return;

        EvaluateRange(band, label, errors, warnings);
        Origin(band, label, warnings);

        if (!positiveOnly)
            return;

        foreach (var (end, name) in new[] { (band.Minimum, "minimum"), (band.Maximum, "maximum") })
        {
            if (end is { } value && value.BaseValue <= 0)
                errors.Add(Diagnostic(
                    ProcessValidationRules.CapabilityMustBePositive,
                    $"{label} has a {name} of {value}; a recorded value here must be greater than zero. Omit it instead if the source gave none."));
        }
    }

    private static void Origin<TDimension>(ReferenceRange<TDimension>? band, string label, List<IValidationDiagnostic> warnings)
        where TDimension : IDimension
    {
        if (band is null || !band.IsRecorded)
            return;

        if (band.Origin == ReferenceValueOrigin.Unknown)
            warnings.Add(Diagnostic(
                ProcessValidationRules.CapabilityOriginShouldBeRecorded,
                $"{label} records no origin, so who published it is unknown."));

        if (string.IsNullOrWhiteSpace(band.Conditions))
            warnings.Add(Diagnostic(
                ProcessValidationRules.CapabilityConditionsShouldBeRecorded,
                $"{label} records no conditions. A capability band depends on feature, material and equipment, and one stated without them is a number rather than reference data."));
    }

    private static string Describe(ProcessMaterialCompatibility entry) =>
        entry.MaterialId is { } id
            ? $"material '{id}'"
            : entry.MaterialDesignation is { } designation
                ? $"material '{designation}'"
                : $"the {entry.Family} family";

    private static string Truncate(string text) => text.Length <= 60 ? text : text[..57] + "...";
}
