using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>
/// One speed rating a source stated for a bearing, with the kind of speed
/// it is, where it came from, and the conditions it holds under.
/// </summary>
/// <remarks>
/// A bearing carries a list of these, not one number — see
/// <see cref="BearingSpeedRatingKind"/> for why.
/// </remarks>
/// <param name="Kind">Which speed this rating states.</param>
/// <param name="Rating">The speed itself, with its own origin and conditions.</param>
/// <param name="ManufacturerDesignation">The source's own label for this rating, verbatim. <see langword="null"/> if none was given.</param>
public sealed record BearingSpeedRating(
    BearingSpeedRatingKind Kind,
    BearingRatedValue<RotationalSpeed> Rating,
    string? ManufacturerDesignation = null);
