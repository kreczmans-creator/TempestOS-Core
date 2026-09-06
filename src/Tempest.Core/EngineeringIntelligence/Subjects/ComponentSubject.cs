using Tempest.Core.Components;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.EngineeringIntelligence.Subjects;

/// <summary>Presents an `A5` mechanical component record to the rule engine.</summary>
/// <remarks>
/// Applicability comes from <see cref="ComponentFamilyTraits"/>, which
/// already decides which of `A5`'s three typed detail records a family
/// carries. A rule reading a gear module against a spring therefore
/// reports <see cref="AssessmentOutcome.NotApplicable"/>, and a rule
/// reading a spring rate against a torsion spring reports the same —
/// because a torsion spring's rate is a torque per angle, a different
/// quantity `A5` deliberately keeps in a different field.
/// </remarks>
public sealed class ComponentSubject : IAssessmentSubject
{
    private readonly IReferenceRecord<ComponentDefinition> _record;

    /// <summary>Initialises a new instance of the <see cref="ComponentSubject"/> class.</summary>
    /// <param name="record">The component record to assess, as read from the catalogue.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
    public ComponentSubject(IReferenceRecord<ComponentDefinition> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _record = record;
    }

    /// <summary>The component record this subject presents.</summary>
    public IReferenceRecord<ComponentDefinition> Record => _record;

    /// <inheritdoc />
    public string SubjectKind => AssessmentSubjectKinds.Component;

    /// <inheritdoc />
    public string SubjectId => _record.Id;

    /// <inheritdoc />
    public string DisplayName => _record.Definition.Designation;

    /// <inheritdoc />
    public string? Family => _record.Definition.Family == ComponentFamily.Unspecified
        ? null
        : _record.Definition.Family.ToString();

    /// <inheritdoc />
    public bool IsApplicabilityKnown => ComponentFamilyTraits.IsApplicabilityKnown(_record.Definition.Family);

    /// <inheritdoc />
    public ReferencePin Pin => ReferencePin.For("Components", _record);

    /// <inheritdoc />
    ReferencePin? IAssessmentSubject.Pin => Pin;

    /// <inheritdoc />
    public SubjectQuantity GetQuantity(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var definition = _record.Definition;
        var family = definition.Family;
        var known = IsApplicabilityKnown;

        return propertyName switch
        {
            SubjectPropertyNames.BoreDiameter => Sourced(
                definition.Dimensions.BoreDiameter,
                !known || ComponentFamilyTraits.HasBore(family)),
            SubjectPropertyNames.OutsideDiameter => Sourced(definition.Dimensions.OutsideDiameter, applies: true),
            SubjectPropertyNames.Mass => Sourced(definition.Dimensions.Mass, applies: true),

            SubjectPropertyNames.MaximumSpeed => Sourced(
                definition.Ratings.MaximumSpeed,
                !known || ComponentFamilyTraits.Rotates(family)),
            SubjectPropertyNames.RatedTorque => Sourced(
                definition.Ratings.RatedTorque,
                !known || ComponentFamilyTraits.TransmitsTorque(family)),

            // A torsion spring's rate is a torque per unit angle, held in a
            // different field of a different dimension. Reporting its
            // translational rate as merely unrecorded would invite a rule
            // to conclude the spring has no rate at all.
            SubjectPropertyNames.SpringRate => Sourced(
                definition.Spring?.Rate,
                !known || (ComponentFamilyTraits.HasSpringDetail(family) && !ComponentFamilyTraits.HasTorsionalRate(family))),

            SubjectPropertyNames.Module => Sourced(
                definition.Gear?.Module,
                !known || ComponentFamilyTraits.HasGearDetail(family)),

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
            SubjectPropertyNames.Designation => SubjectText.Recorded(definition.Designation),
            SubjectPropertyNames.SurfaceTreatment => SubjectText.Recorded(definition.SurfaceTreatment),
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
