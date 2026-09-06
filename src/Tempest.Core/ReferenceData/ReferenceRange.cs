using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// A sourced engineering range — a minimum, a maximum, or both.
/// </summary>
/// <remarks>
/// <para>
/// A great deal of reference data is a range rather than a point: an
/// internal clearance band, a process's own achievable tolerance, a
/// material property quoted between limits. Recording one as two unrelated
/// values loses the fact that they are the ends of one thing, and
/// recording only a midpoint invents a value nobody published.
/// </para>
/// <para>
/// An open end is genuinely open: a range with only a maximum means the
/// source stated an upper limit and nothing else, never that the minimum
/// is zero.
/// </para>
/// </remarks>
/// <typeparam name="TDimension">The physical dimension of both ends.</typeparam>
/// <param name="Minimum">The lower end. <see langword="null"/> if the source stated none.</param>
/// <param name="Maximum">The upper end. <see langword="null"/> if the source stated none.</param>
/// <param name="Origin">Where the range came from.</param>
/// <param name="Conditions">The conditions the range holds under, as the source states them. <see langword="null"/> if none was given.</param>
/// <param name="SourceDesignation">The source's own label for this range. <see langword="null"/> if none was given.</param>
public sealed record ReferenceRange<TDimension>(
    Quantity<TDimension>? Minimum,
    Quantity<TDimension>? Maximum,
    ReferenceValueOrigin Origin,
    string? Conditions = null,
    string? SourceDesignation = null)
    where TDimension : IDimension
{
    /// <summary>Whether either end is recorded at all.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecorded => Minimum is not null || Maximum is not null;

    /// <summary>Whether the range is inverted — a maximum below its own minimum, which describes no real range.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsInverted =>
        Minimum is { } min && Maximum is { } max && max.BaseValue < min.BaseValue;

    /// <summary>
    /// Whether <paramref name="candidate"/> falls within this range.
    /// An open end never excludes; a range recording neither end includes
    /// everything, because it constrains nothing.
    /// </summary>
    public bool Contains(Quantity<TDimension> candidate)
    {
        var value = candidate.BaseValue;

        if (Minimum is { } min && value < min.BaseValue)
            return false;

        if (Maximum is { } max && value > max.BaseValue)
            return false;

        return true;
    }
}
