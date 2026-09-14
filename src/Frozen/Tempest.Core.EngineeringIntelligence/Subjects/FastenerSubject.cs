using Tempest.Core.Fasteners;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.EngineeringIntelligence.Subjects;

/// <summary>Presents an `A3` fastener record to the rule engine.</summary>
/// <remarks>
/// Applicability comes from <see cref="FastenerFamilyTraits"/>: a washer
/// has no thread, a stud no head, a family that carries no property class
/// has none to record. A rule reading a thread diameter for a washer
/// therefore reports <see cref="AssessmentOutcome.NotApplicable"/> rather
/// than a data gap, and does so because `A3` says so rather than because
/// this adapter decided.
/// </remarks>
public sealed class FastenerSubject : IAssessmentSubject
{
    private readonly IReferenceRecord<FastenerDefinition> _record;

    /// <summary>Initialises a new instance of the <see cref="FastenerSubject"/> class.</summary>
    /// <param name="record">The fastener record to assess, as read from the catalogue.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
    public FastenerSubject(IReferenceRecord<FastenerDefinition> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _record = record;
    }

    /// <summary>The fastener record this subject presents.</summary>
    public IReferenceRecord<FastenerDefinition> Record => _record;

    /// <inheritdoc />
    public string SubjectKind => AssessmentSubjectKinds.Fastener;

    /// <inheritdoc />
    public string SubjectId => _record.Id;

    /// <inheritdoc />
    public string DisplayName => _record.Definition.Designation;

    /// <inheritdoc />
    public string? Family => _record.Definition.Family == FastenerFamily.Unspecified
        ? null
        : _record.Definition.Family.ToString();

    /// <inheritdoc />
    public bool IsApplicabilityKnown => FastenerFamilyTraits.IsApplicabilityKnown(_record.Definition.Family);

    /// <inheritdoc />
    public ReferencePin Pin => ReferencePin.For("Fasteners", _record);

    /// <inheritdoc />
    ReferencePin? IAssessmentSubject.Pin => Pin;

    /// <inheritdoc />
    public SubjectQuantity GetQuantity(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var definition = _record.Definition;
        var family = definition.Family;
        var known = IsApplicabilityKnown;
        var threaded = !known || FastenerFamilyTraits.IsThreaded(family);

        return propertyName switch
        {
            SubjectPropertyNames.NominalDiameter => Sourced(definition.Thread?.NominalDiameter, threaded),
            SubjectPropertyNames.ThreadPitch => Sourced(definition.Thread?.Pitch, threaded),
            SubjectPropertyNames.NominalLength => Sourced(
                definition.Dimensions.NominalLength,
                !known || FastenerFamilyTraits.HasNominalLength(family)),
            SubjectPropertyNames.WidthAcrossFlats => Sourced(definition.Dimensions.WidthAcrossFlats, applies: true),

            SubjectPropertyNames.ProofStrength => Sourced(definition.Mechanical.ProofStrength, applies: true),
            SubjectPropertyNames.FastenerTensileStrength => Sourced(definition.Mechanical.TensileStrength, applies: true),
            SubjectPropertyNames.ProofLoad => Sourced(
                definition.Mechanical.ProofLoad,
                !known || FastenerFamilyTraits.TakesProofLoad(family)),
            SubjectPropertyNames.MinimumBreakingLoad => Sourced(
                definition.Mechanical.MinimumBreakingLoad,
                !known || FastenerFamilyTraits.TakesProofLoad(family)),

            _ => SubjectQuantity.NotRecorded,
        };
    }

    /// <inheritdoc />
    public SubjectText GetText(string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        var definition = _record.Definition;
        var known = IsApplicabilityKnown;

        return attributeName switch
        {
            SubjectPropertyNames.Family => SubjectText.Recorded(Family),
            SubjectPropertyNames.Designation => SubjectText.Recorded(definition.Designation),
            SubjectPropertyNames.PropertyClass => definition.Mechanical.PropertyClass is { } propertyClass
                ? SubjectText.Recorded(propertyClass)
                : known && !FastenerFamilyTraits.TakesPropertyClass(definition.Family)
                    ? SubjectText.NotApplicable
                    : SubjectText.NotRecorded,
            SubjectPropertyNames.SurfaceTreatment => SubjectText.Recorded(definition.Finish?.Designation),
            _ => SubjectText.NotRecorded,
        };
    }

    private static SubjectQuantity Sourced<TDimension>(ReferenceValue<TDimension>? value, bool applies)
        where TDimension : IDimension
    {
        if (!applies)
            return SubjectQuantity.NotApplicable;

        return value is null
            ? SubjectQuantity.NotRecorded
            : SubjectQuantity.Recorded(new ReferenceQuantityValue(value.Value, value.Origin, value.Conditions, value.SourceDesignation));
    }
}
