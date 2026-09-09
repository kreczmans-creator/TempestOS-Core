using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Subjects;

/// <summary>
/// Presents an `A1` material record to the rule engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Applicability comes from `A1`, not from here.</b>
/// <see cref="MaterialFamilyTraits"/> already knows that a ceramic has no
/// yield point and a polymer no heat-treatment condition, so this adapter
/// asks it rather than re-deciding. That is what lets a rule reading
/// <c>YieldStrength</c> against a ceramic report
/// <see cref="AssessmentOutcome.NotApplicable"/> instead of a spurious
/// data gap — and it means the answer stays correct when `A1`'s traits
/// table is extended, with no change here.
/// </para>
/// <para>
/// <b>Unclassified families get no "not applicable" answer at all.</b>
/// Where <see cref="MaterialFamilyTraits.IsApplicabilityKnown"/> is false,
/// every absent property is reported as
/// <see cref="ReferencePropertyAvailability.NotRecorded"/>: "not known to
/// apply" must never be reported as "known not to apply".
/// </para>
/// </remarks>
public sealed class MaterialSubject : IAssessmentSubject
{
    private readonly IReferenceRecord<MaterialDefinition> _record;

    /// <summary>Initialises a new instance of the <see cref="MaterialSubject"/> class.</summary>
    /// <param name="record">The material record to assess, as read from the catalogue.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
    public MaterialSubject(IReferenceRecord<MaterialDefinition> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _record = record;
    }

    /// <summary>The material record this subject presents.</summary>
    public IReferenceRecord<MaterialDefinition> Record => _record;

    /// <inheritdoc />
    public string SubjectKind => AssessmentSubjectKinds.Material;

    /// <inheritdoc />
    public string SubjectId => _record.Id;

    /// <inheritdoc />
    public string DisplayName => _record.Definition.Designation is { } designation
        ? $"{_record.Definition.Name} ({designation})"
        : _record.Definition.Name;

    /// <inheritdoc />
    public string? Family => _record.Definition.Family == MaterialFamily.Unspecified
        ? null
        : _record.Definition.Family.ToString();

    /// <inheritdoc />
    public bool IsApplicabilityKnown => MaterialFamilyTraits.IsApplicabilityKnown(_record.Definition.Family);

    /// <inheritdoc />
    public ReferencePin Pin => ReferencePin.For("Materials", _record);

    /// <inheritdoc />
    ReferencePin? IAssessmentSubject.Pin => Pin;

    /// <inheritdoc />
    public SubjectQuantity GetQuantity(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (_record.Definition.Properties.TryGetValue(propertyName, out var value))
            return SubjectQuantity.Recorded(value);

        return IsNotApplicable(propertyName) ? SubjectQuantity.NotApplicable : SubjectQuantity.NotRecorded;
    }

    /// <inheritdoc />
    public SubjectText GetText(string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        var definition = _record.Definition;

        return attributeName switch
        {
            SubjectPropertyNames.Family => SubjectText.Recorded(Family),
            SubjectPropertyNames.Designation => SubjectText.Recorded(definition.Designation),
            SubjectPropertyNames.Condition => ConditionAttribute(definition),
            SubjectPropertyNames.PropertyClass => SubjectText.Recorded(definition.Grade),
            _ => SubjectText.NotRecorded,
        };
    }

    private SubjectText ConditionAttribute(MaterialDefinition definition)
    {
        if (definition.Condition is { } condition)
            return SubjectText.Recorded(condition);

        // A family with no heat-treatment condition has none to record —
        // that is a fact about the family, not a gap in the record.
        return IsApplicabilityKnown && !MaterialFamilyTraits.HasHeatTreatmentCondition(definition.Family)
            ? SubjectText.NotApplicable
            : SubjectText.NotRecorded;
    }

    private bool IsNotApplicable(string propertyName)
    {
        if (!IsApplicabilityKnown)
            return false;

        // The one property `A1`'s traits table can rule out by family. A
        // ceramic or a glass fails brittly without a yield point, so a
        // yield strength is not merely unrecorded — there is none.
        return propertyName == MaterialPropertyNames.YieldStrength
            && !MaterialFamilyTraits.HasYieldStrength(_record.Definition.Family);
    }
}
