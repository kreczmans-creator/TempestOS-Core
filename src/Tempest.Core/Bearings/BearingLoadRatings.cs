using Tempest.Core.UnitsAndQuantities;

using Tempest.Core.ReferenceData;

namespace Tempest.Core.Bearings;

/// <summary>
/// The load ratings a source stated for a bearing. Every one is optional
/// and every one is nullable: a rating that was not supplied is
/// <see langword="null"/>, never zero.
/// </summary>
/// <remarks>
/// Which ratings are meaningful depends on the family — a thrust bearing
/// is rated axially, a cylindrical roller bearing radially — so nothing
/// here is required, and <see cref="BearingFamilyTraits"/> rather than a
/// default value decides what a reader should expect to find.
/// </remarks>
/// <param name="BasicDynamicRadial">Basic dynamic radial load rating, conventionally <c>C</c>.</param>
/// <param name="BasicStaticRadial">Basic static radial load rating, conventionally <c>C0</c>.</param>
/// <param name="BasicDynamicAxial">Basic dynamic axial load rating, conventionally <c>Ca</c>.</param>
/// <param name="BasicStaticAxial">Basic static axial load rating, conventionally <c>C0a</c>.</param>
/// <param name="FatigueLoadLimit">Fatigue load limit, conventionally <c>Pu</c>.</param>
/// <param name="ManufacturerRatings">
/// Further rated forces a manufacturer quotes that this shape does not
/// name, keyed by the source's own label. Kept rather than discarded —
/// normalising them away would lose information no other field carries.
/// Never <see langword="null"/>; empty if none.
/// </param>
public sealed record BearingLoadRatings(
    ReferenceValue<Force>? BasicDynamicRadial = null,
    ReferenceValue<Force>? BasicStaticRadial = null,
    ReferenceValue<Force>? BasicDynamicAxial = null,
    ReferenceValue<Force>? BasicStaticAxial = null,
    ReferenceValue<Force>? FatigueLoadLimit = null,
    IReadOnlyDictionary<string, ReferenceValue<Force>>? ManufacturerRatings = null)
{
    /// <summary>Further rated forces a manufacturer quotes that this shape does not name. Never <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, ReferenceValue<Force>> ManufacturerRatings { get; init; } =
        ManufacturerRatings ?? new Dictionary<string, ReferenceValue<Force>>();
}
