namespace Tempest.Core.Bearings;

/// <summary>Whether a comparison cell holds a value, and if not, why not.</summary>
/// <remarks>
/// The distinction this whole comparison capability exists to preserve. A
/// blank cell that means "this family has no such property" and a blank
/// cell that means "nobody has recorded this yet" are entirely different
/// facts, and collapsing them would make a comparison table quietly
/// misleading.
/// </remarks>
public enum BearingPropertyAvailability
{
    /// <summary>The bearing records a value for this property.</summary>
    Recorded,

    /// <summary>The property is meaningful for this bearing's own family, but no value has been recorded. A data gap.</summary>
    NotRecorded,

    /// <summary>The property is not meaningful for this bearing's own family. Not a gap — there is nothing to record.</summary>
    NotApplicable
}

/// <summary>One bearing's own value for one comparison property.</summary>
/// <param name="Availability">Whether a value is present, and if not, why not.</param>
/// <param name="Display">The value as text, in the unit it was recorded in. <see langword="null"/> unless <paramref name="Availability"/> is <see cref="BearingPropertyAvailability.Recorded"/>.</param>
/// <param name="CanonicalValue">
/// The value in its own dimension's base unit, for ordering candidates
/// recorded in different units. <see langword="null"/> for a
/// non-numeric property, and for any cell that holds no value.
/// </param>
public sealed record BearingComparisonCell(
    BearingPropertyAvailability Availability,
    string? Display = null,
    double? CanonicalValue = null)
{
    /// <summary>A cell for a property that is meaningful for the family but has no recorded value.</summary>
    public static BearingComparisonCell NotRecorded { get; } = new(BearingPropertyAvailability.NotRecorded);

    /// <summary>A cell for a property that is not meaningful for the family.</summary>
    public static BearingComparisonCell NotApplicable { get; } = new(BearingPropertyAvailability.NotApplicable);
}

/// <summary>One property across every bearing being compared.</summary>
/// <param name="Property">The property's own stable key (see <see cref="BearingComparisonProperties"/>).</param>
/// <param name="Cells">One cell per bearing, in the same order the bearings were given. Never <see langword="null"/>.</param>
public sealed record BearingComparisonRow(string Property, IReadOnlyList<BearingComparisonCell> Cells)
{
    /// <summary>Whether at least one bearing records a value for this property.</summary>
    public bool AnyRecorded => Cells.Any(cell => cell.Availability == BearingPropertyAvailability.Recorded);
}

/// <summary>A structured, side-by-side comparison of two or more bearings.</summary>
/// <remarks>
/// <b>Structure only, never a verdict.</b> This result says what each
/// bearing records; it never says which is better, which is suitable, or
/// which should be chosen. A future selection capability consumes this;
/// A4 does not become one.
/// </remarks>
/// <param name="BearingIds">The bearings compared, in the order given. Never <see langword="null"/>.</param>
/// <param name="Rows">One row per comparison property. Never <see langword="null"/>.</param>
public sealed record BearingComparisonResult(IReadOnlyList<string> BearingIds, IReadOnlyList<BearingComparisonRow> Rows)
{
    /// <summary>Whether every bearing compared is of the same family — the context a reader needs before drawing anything from a row.</summary>
    public bool IsSingleFamily { get; init; }

    /// <summary>The rows on which at least one bearing records a value. Never <see langword="null"/>.</summary>
    public IReadOnlyList<BearingComparisonRow> PopulatedRows => Rows.Where(row => row.AnyRecorded).ToList();

    /// <summary>Returns the row for <paramref name="property"/>, or <see langword="null"/> if this comparison has none.</summary>
    public BearingComparisonRow? Row(string property) =>
        Rows.FirstOrDefault(row => string.Equals(row.Property, property, StringComparison.Ordinal));
}
