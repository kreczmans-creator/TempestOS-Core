namespace Tempest.Core.ReferenceData;

/// <summary>Whether a comparison cell holds a value, and if not, why not.</summary>
/// <remarks>
/// The distinction the whole comparison capability exists to preserve. A
/// blank cell that means "this family has no such property" and a blank
/// cell that means "nobody has recorded this yet" are entirely different
/// facts, and collapsing them would make a comparison table quietly
/// misleading.
/// </remarks>
public enum ReferencePropertyAvailability
{
    /// <summary>The record holds a value for this property.</summary>
    Recorded,

    /// <summary>The property is meaningful for this record's own family, but no value has been recorded. A data gap.</summary>
    NotRecorded,

    /// <summary>The property is not meaningful for this record's own family. Not a gap — there is nothing to record.</summary>
    NotApplicable
}

/// <summary>One record's own value for one comparison property.</summary>
/// <param name="Availability">Whether a value is present, and if not, why not.</param>
/// <param name="Display">The value as text, in the unit it was recorded in. <see langword="null"/> unless <paramref name="Availability"/> is <see cref="ReferencePropertyAvailability.Recorded"/>.</param>
/// <param name="CanonicalValue">
/// The value in its own dimension's base unit, for ordering candidates
/// recorded in different units. <see langword="null"/> for a non-numeric
/// property, and for any cell that holds no value.
/// </param>
public sealed record ReferenceComparisonCell(
    ReferencePropertyAvailability Availability,
    string? Display = null,
    double? CanonicalValue = null)
{
    /// <summary>A cell for a property that is meaningful for the family but has no recorded value.</summary>
    public static ReferenceComparisonCell NotRecorded { get; } = new(ReferencePropertyAvailability.NotRecorded);

    /// <summary>A cell for a property that is not meaningful for the family.</summary>
    public static ReferenceComparisonCell NotApplicable { get; } = new(ReferencePropertyAvailability.NotApplicable);

    /// <summary>A cell holding recorded text, or <see cref="NotRecorded"/> where <paramref name="value"/> is absent or blank.</summary>
    public static ReferenceComparisonCell Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotRecorded : new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, value);

    /// <summary>
    /// A cell holding recorded text where the property applies to this
    /// family, <see cref="NotApplicable"/> where it does not, and
    /// <see cref="NotRecorded"/> where it applies but nothing was
    /// recorded. <paramref name="applicabilityKnown"/> guards the middle
    /// case: an unclassified family's own conservative "does not apply"
    /// means "not known to apply", never "known not to apply".
    /// </summary>
    public static ReferenceComparisonCell Applicable(string? value, bool applies, bool applicabilityKnown) =>
        applicabilityKnown && !applies ? NotApplicable : Text(value);
}

/// <summary>One property across every record being compared.</summary>
/// <param name="Property">The property's own stable key.</param>
/// <param name="Cells">One cell per record, in the same order the records were given. Never <see langword="null"/>.</param>
public sealed record ReferenceComparisonRow(string Property, IReadOnlyList<ReferenceComparisonCell> Cells)
{
    /// <summary>Whether at least one record holds a value for this property.</summary>
    public bool AnyRecorded => Cells.Any(cell => cell.Availability == ReferencePropertyAvailability.Recorded);
}

/// <summary>A structured, side-by-side comparison of two or more reference records.</summary>
/// <remarks>
/// <b>Structure only, never a verdict.</b> This result says what each
/// record holds; it never says which is better, which is suitable, or
/// which should be chosen. A future selection capability consumes this;
/// P01 does not become one.
/// </remarks>
/// <param name="RecordIds">The records compared, in the order given. Never <see langword="null"/>.</param>
/// <param name="Rows">One row per comparison property. Never <see langword="null"/>.</param>
public sealed record ReferenceComparisonResult(IReadOnlyList<string> RecordIds, IReadOnlyList<ReferenceComparisonRow> Rows)
{
    /// <summary>Whether every record compared is of the same family — the context a reader needs before drawing anything from a row.</summary>
    public bool IsSingleFamily { get; init; }

    /// <summary>The rows on which at least one record holds a value. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferenceComparisonRow> PopulatedRows => Rows.Where(row => row.AnyRecorded).ToList();

    /// <summary>Returns the row for <paramref name="property"/>, or <see langword="null"/> if this comparison has none.</summary>
    public ReferenceComparisonRow? Row(string property) =>
        Rows.FirstOrDefault(row => string.Equals(row.Property, property, StringComparison.Ordinal));
}
