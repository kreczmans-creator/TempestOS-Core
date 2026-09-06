namespace Tempest.Core.Fasteners;

/// <summary>
/// A hardness figure or band as a source published it, together with the
/// scale it was measured on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not a <c>Quantity</c>.</b> A hardness number is a
/// scale-specific ordinal reading produced by one test method — a Vickers
/// number, a Rockwell C number and a Brinell number are not the same
/// quantity expressed in different units, and no exact conversion exists
/// between them. Recording one as a dimensioned quantity would let the
/// Units &amp; Quantities framework convert between scales that cannot be
/// converted, which is precisely the kind of plausible-looking wrong
/// answer P01 exists to prevent. The scale therefore travels with the
/// number, as text, and the two are never separated.
/// </para>
/// <para>
/// This is a deliberate, disclosed exception to Group A recording
/// engineering values as dimensioned quantities — made because hardness is
/// genuinely not a dimensioned quantity, not because it was inconvenient
/// to model as one.
/// </para>
/// </remarks>
/// <param name="Scale">The hardness scale and test method, as the source designates it. Required.</param>
/// <param name="Minimum">The lower end of the published band. <see langword="null"/> if the source stated none.</param>
/// <param name="Maximum">The upper end of the published band. <see langword="null"/> if the source stated none.</param>
/// <param name="Origin">Where the figure came from.</param>
/// <param name="Conditions">The conditions the figure holds under, as the source states them. <see langword="null"/> if none was given.</param>
public sealed record FastenerHardness(
    string Scale,
    double? Minimum = null,
    double? Maximum = null,
    ReferenceData.ReferenceValueOrigin Origin = ReferenceData.ReferenceValueOrigin.Unknown,
    string? Conditions = null)
{
    /// <summary>The hardness scale and test method.</summary>
    public string Scale { get; } = string.IsNullOrWhiteSpace(Scale)
        ? throw new ArgumentException("A hardness figure must name the scale it was measured on.", nameof(Scale))
        : Scale.Trim();

    /// <summary>Whether either end of the band is recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecorded => Minimum is not null || Maximum is not null;

    /// <summary>Whether the band is inverted — a maximum below its own minimum, which describes no real band.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsInverted => Minimum is { } min && Maximum is { } max && max < min;

    /// <summary>Whether TempestOS computed the figure rather than taking it from a source.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDerived => Origin == ReferenceData.ReferenceValueOrigin.DerivedByTempestOS;
}
