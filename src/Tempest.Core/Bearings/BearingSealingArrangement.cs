namespace Tempest.Core.Bearings;

/// <summary>
/// A bearing's own sealing or shielding arrangement: the manufacturer's
/// own designation as written, plus this library's own common
/// classification of it where one is defensible.
/// </summary>
/// <param name="Type">
/// The common classification. <see cref="BearingSealingType.Unspecified"/>
/// where <paramref name="ManufacturerDesignation"/> could not be mapped
/// without guessing — an honest gap, never a nearest-fit.
/// </param>
/// <param name="ManufacturerDesignation">
/// The manufacturer's or source's own designation, verbatim (e.g. a
/// catalogue suffix). <see langword="null"/> if the source gave none.
/// Never normalised away: the mapping in <paramref name="Type"/> is an
/// interpretation, and the original must survive it.
/// </param>
/// <param name="SidesSealed">
/// How many sides carry the arrangement, where the source states it.
/// <see langword="null"/> if unknown.
/// </param>
public sealed record BearingSealingArrangement(
    BearingSealingType Type,
    string? ManufacturerDesignation = null,
    int? SidesSealed = null);
