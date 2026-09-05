namespace Tempest.Core.Bearings;

/// <summary>
/// A standard a bearing record's own dimensional or designation
/// information is stated against.
/// </summary>
/// <remarks>
/// Recording that a source cites a standard is not the same as certifying
/// conformity. This record says only what the source itself said: nothing
/// here, and nothing in this library, asserts that a bearing complies with
/// a standard on TempestOS's own authority.
/// </remarks>
/// <param name="Designation">The standard's own designation as the source writes it (e.g. an ISO or national standard number). Required.</param>
/// <param name="Body">The organisation that publishes the standard. <see langword="null"/> if the source did not name one.</param>
/// <param name="Edition">The edition or year of the standard the source cites. <see langword="null"/> if none was given.</param>
/// <param name="Applies">What the citation covers (e.g. boundary dimensions, tolerances, designation system). Free text. <see langword="null"/> if the source did not say.</param>
public sealed record BearingStandardReference(
    string Designation,
    string? Body = null,
    string? Edition = null,
    string? Applies = null)
{
    /// <summary>The standard's own designation as the source writes it.</summary>
    public string Designation { get; } = string.IsNullOrWhiteSpace(Designation)
        ? throw new ArgumentException("A standard reference must carry a designation.", nameof(Designation))
        : Designation;
}
