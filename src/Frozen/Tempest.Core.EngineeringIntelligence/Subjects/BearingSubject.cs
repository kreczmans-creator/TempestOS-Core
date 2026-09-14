using Tempest.Core.Bearings;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.EngineeringIntelligence.Subjects;

/// <summary>Presents an `A4` bearing record to the rule engine.</summary>
/// <remarks>
/// <para>
/// Applicability comes from <see cref="BearingFamilyTraits"/>, exactly as
/// the other adapters take theirs from their own library's traits table.
/// </para>
/// <para>
/// <b>Speed is a list in `A4`, and deliberately so:</b> a bearing has
/// different limits under grease and under oil, and collapsing them to one
/// number would discard the lubrication condition that makes either
/// meaningful. This adapter exposes the <em>highest</em> recorded rating
/// and carries the condition it holds under in the value's own
/// <see cref="ReferenceQuantityValue.Conditions"/>, so a rule comparing
/// against it can still be read correctly. A rule that needs a particular
/// lubrication regime should read the record directly rather than through
/// this subject.
/// </para>
/// </remarks>
public sealed class BearingSubject : IAssessmentSubject
{
    private readonly IReferenceRecord<BearingDefinition> _record;

    /// <summary>Initialises a new instance of the <see cref="BearingSubject"/> class.</summary>
    /// <param name="record">The bearing record to assess, as read from the catalogue.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
    public BearingSubject(IReferenceRecord<BearingDefinition> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        _record = record;
    }

    /// <summary>The bearing record this subject presents.</summary>
    public IReferenceRecord<BearingDefinition> Record => _record;

    /// <inheritdoc />
    public string SubjectKind => AssessmentSubjectKinds.Bearing;

    /// <inheritdoc />
    public string SubjectId => _record.Id;

    /// <inheritdoc />
    public string DisplayName => _record.Definition.Identity.Designation
        ?? _record.Definition.Identity.ManufacturerPartNumber;

    /// <inheritdoc />
    public string? Family => _record.Definition.Family == BearingFamily.Unspecified
        ? null
        : _record.Definition.Family.ToString();

    /// <inheritdoc />
    public bool IsApplicabilityKnown => BearingFamilyTraits.IsApplicabilityKnown(_record.Definition.Family);

    /// <inheritdoc />
    public ReferencePin Pin => ReferencePin.For("Bearings", _record);

    /// <inheritdoc />
    ReferencePin? IAssessmentSubject.Pin => Pin;

    /// <inheritdoc />
    public SubjectQuantity GetQuantity(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var definition = _record.Definition;

        return propertyName switch
        {
            SubjectPropertyNames.BoreDiameter => Plain(definition.Geometry.Bore),
            SubjectPropertyNames.OutsideDiameter => Plain(definition.Geometry.OutsideDiameter),
            SubjectPropertyNames.Mass => Plain(definition.Mass),
            SubjectPropertyNames.MaximumSpeed => HighestSpeedRating(),
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
            SubjectPropertyNames.Designation => SubjectText.Recorded(definition.Identity.Designation),
            _ => SubjectText.NotRecorded,
        };
    }

    private SubjectQuantity HighestSpeedRating()
    {
        var ratings = _record.Definition.SpeedRatings;

        if (ratings.Count == 0)
            return SubjectQuantity.NotRecorded;

        var highest = ratings
            .OrderByDescending(rating => rating.Rating.CanonicalValue)
            .First();

        // The kind of rating (limiting, reference, and so on) and the
        // conditions the source stated both travel with the value, so a
        // rule comparing against it can still be read correctly later.
        var conditions = highest.Rating.Conditions is { } stated
            ? $"{highest.Kind} rating. {stated}"
            : $"{highest.Kind} rating; highest of {ratings.Count} recorded.";

        return SubjectQuantity.Recorded(new ReferenceQuantityValue(
            highest.Rating.Value,
            highest.Rating.Origin,
            conditions,
            highest.Rating.SourceDesignation ?? highest.ManufacturerDesignation));
    }

    private static SubjectQuantity Plain<TDimension>(Quantity<TDimension>? value)
        where TDimension : IDimension =>
        value is not { } quantity
            ? SubjectQuantity.NotRecorded
            : SubjectQuantity.Recorded(new ReferenceQuantityValue(quantity, ReferenceValueOrigin.ManufacturerCatalogue));
}
