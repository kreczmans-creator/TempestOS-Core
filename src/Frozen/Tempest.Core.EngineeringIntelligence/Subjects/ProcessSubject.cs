using Tempest.Core.Manufacturing;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.EngineeringIntelligence.Subjects;

/// <summary>
/// Presents an `A7` manufacturing process record to the rule engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>A capability is a band; a rule compares a value.</b> `A7` records
/// what a process can achieve as a
/// <see cref="ReferenceRange{TDimension}"/> with two ends, either of which
/// may be open. A rule needs a single number to compare against, so this
/// adapter exposes the <em>ends</em> as separate named properties —
/// <see cref="SubjectPropertyNames.FinestAchievableTolerance"/> and its
/// siblings — rather than inventing a midpoint nobody published.
/// </para>
/// <para>
/// An open end is reported as
/// <see cref="ReferencePropertyAvailability.NotRecorded"/>, never as
/// infinity or zero: a source that stated a lower limit and no upper one
/// stated no upper one, and a rule comparing against a number the source
/// did not publish would be reasoning from a value TempestOS made up.
/// </para>
/// <para>
/// <b>Applicability comes from `A7`.</b>
/// <see cref="ProcessFamilyTraits"/> knows that a heat treatment leaves no
/// surface of its own and that a machining process forms nothing against a
/// mould, so a rule reading a surface roughness or a draft angle for one
/// gets <see cref="AssessmentOutcome.NotApplicable"/> rather than a
/// spurious gap.
/// </para>
/// </remarks>
public sealed class ProcessSubject : IAssessmentSubject
{
    private readonly IReferenceRecord<ProcessDefinition> _record;

    /// <summary>Initialises a new instance of the <see cref="ProcessSubject"/> class.</summary>
    /// <param name="record">The process record to assess, as read from the catalogue.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
    public ProcessSubject(IReferenceRecord<ProcessDefinition> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _record = record;
    }

    /// <summary>The process record this subject presents.</summary>
    public IReferenceRecord<ProcessDefinition> Record => _record;

    /// <inheritdoc />
    public string SubjectKind => AssessmentSubjectKinds.Process;

    /// <inheritdoc />
    public string SubjectId => _record.Id;

    /// <inheritdoc />
    public string DisplayName => _record.Definition.Variant is { } variant
        ? $"{_record.Definition.Name} ({variant})"
        : _record.Definition.Name;

    /// <inheritdoc />
    public string? Family => _record.Definition.Family == ProcessFamily.Unspecified
        ? null
        : _record.Definition.Family.ToString();

    /// <inheritdoc />
    public bool IsApplicabilityKnown => ProcessFamilyTraits.IsApplicabilityKnown(_record.Definition.Family);

    /// <inheritdoc />
    public ReferencePin Pin => ReferencePin.For("Manufacturing", _record);

    /// <inheritdoc />
    ReferencePin? IAssessmentSubject.Pin => Pin;

    /// <inheritdoc />
    public SubjectQuantity GetQuantity(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var capabilities = _record.Definition.Capabilities;
        var family = _record.Definition.Family;
        var known = IsApplicabilityKnown;

        return propertyName switch
        {
            SubjectPropertyNames.FinestAchievableTolerance => Lower(capabilities.AchievableTolerance, applies: true),
            SubjectPropertyNames.CoarsestAchievableTolerance => Upper(capabilities.AchievableTolerance, applies: true),

            SubjectPropertyNames.FinestSurfaceRoughness => Lower(
                capabilities.SurfaceRoughness,
                applies: !known || ProcessFamilyTraits.HasSurfaceRoughnessCapability(family)),

            SubjectPropertyNames.MinimumWallThickness => Lower(
                capabilities.WallThickness,
                applies: !known || ProcessFamilyTraits.HasWallThicknessCapability(family)),

            SubjectPropertyNames.MinimumPartSize => Lower(capabilities.PartSize, applies: !known || !ProcessFamilyTraits.IsJoining(family)),
            SubjectPropertyNames.MaximumPartSize => Upper(capabilities.PartSize, applies: !known || !ProcessFamilyTraits.IsJoining(family)),
            SubjectPropertyNames.MaximumPartMass => Upper(capabilities.PartMass, applies: !known || !ProcessFamilyTraits.IsJoining(family)),
            SubjectPropertyNames.MinimumFeatureSize => Lower(capabilities.MinimumFeatureSize, applies: true),

            _ => SubjectQuantity.NotRecorded,
        };
    }

    /// <inheritdoc />
    public SubjectText GetText(string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        var definition = _record.Definition;

        return attributeName switch
        {
            SubjectPropertyNames.Family => SubjectText.Recorded(Family),
            SubjectPropertyNames.Designation => SubjectText.Recorded(definition.Name),
            SubjectPropertyNames.ProductionScale => definition.ProductionScales.Count == 0
                ? SubjectText.NotRecorded
                : SubjectText.Recorded(string.Join(", ", definition.ProductionScales)),
            _ => SubjectText.NotRecorded,
        };
    }

    /// <summary>Whether the process is recorded as suitable, or conditionally suitable, for a material family.</summary>
    /// <remarks>
    /// Exposed as a typed question rather than through
    /// <see cref="GetText"/>, because "which families does this process
    /// work on" is a set membership rather than an attribute value, and
    /// forcing it through a text match would lose the distinction between
    /// a family a source ruled out and a family it never mentioned.
    /// </remarks>
    /// <returns>
    /// <see cref="AssessmentOutcome.Pass"/> where a source recorded the
    /// pairing as suitable or conditionally suitable,
    /// <see cref="AssessmentOutcome.Fail"/> where a source explicitly ruled
    /// it out, and <see cref="AssessmentOutcome.NotRecorded"/> where no
    /// source mentioned it — which is not the same as ruling it out.
    /// </returns>
    public AssessmentOutcome AssessMaterialFamily(Materials.MaterialFamily family)
    {
        var entries = _record.Definition.MaterialCompatibility
            .Where(entry => entry.Family == family)
            .ToList();

        if (entries.Count == 0)
            return AssessmentOutcome.NotRecorded;

        if (entries.Any(entry => entry.Suitability == ProcessMaterialSuitability.NotSuitable))
            return AssessmentOutcome.Fail;

        if (entries.Any(entry => entry.Suitability == ProcessMaterialSuitability.Suitable))
            return AssessmentOutcome.Pass;

        if (entries.Any(entry => entry.Suitability == ProcessMaterialSuitability.ConditionallySuitable))
            return AssessmentOutcome.Concern;

        // Recorded, but the source never said whether the pairing works.
        return AssessmentOutcome.Indeterminate;
    }

    private static SubjectQuantity Lower<TDimension>(ReferenceRange<TDimension>? band, bool applies)
        where TDimension : IDimension =>
        End(band?.Minimum, band, applies);

    private static SubjectQuantity Upper<TDimension>(ReferenceRange<TDimension>? band, bool applies)
        where TDimension : IDimension =>
        End(band?.Maximum, band, applies);

    private static SubjectQuantity End<TDimension>(Quantity<TDimension>? end, ReferenceRange<TDimension>? band, bool applies)
        where TDimension : IDimension
    {
        if (!applies)
            return SubjectQuantity.NotApplicable;

        // An open end is genuinely open. Reporting it as a number would be
        // TempestOS publishing a limit the source did not.
        if (end is not { } value)
            return SubjectQuantity.NotRecorded;

        return SubjectQuantity.Recorded(new ReferenceQuantityValue(
            value,
            band!.Origin,
            band.Conditions,
            band.SourceDesignation));
    }
}
