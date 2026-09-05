namespace Tempest.Core.Bearings;

/// <summary>
/// How a bearing is identified commercially and by designation —
/// deliberately separate from its own TempestOS identity
/// (<see cref="IBearing.BearingId"/>).
/// </summary>
/// <remarks>
/// A manufacturer part number is not an identity. Two manufacturers use
/// the same designation for bearings that are not the same bearing, one
/// manufacturer reuses a designation across variants, and a designation
/// changes when a catalogue is revised. The TempestOS bearing Id is
/// therefore assigned by the caller and never derived from any field here;
/// <see cref="Manufacturer"/> plus <see cref="ManufacturerPartNumber"/> is
/// enforced as *unique* (<see cref="DuplicateBearingPartNumberException"/>)
/// but is never the primary key.
/// </remarks>
/// <param name="Manufacturer">The manufacturer or supplier of record. Required — a bearing record with no attributable manufacturer has no provenance worth trusting.</param>
/// <param name="ManufacturerPartNumber">The manufacturer's own ordering part number. Required.</param>
/// <param name="Designation">The bearing designation (e.g. a catalogue designation with suffixes). <see langword="null"/> if the source gives only a part number.</param>
/// <param name="Series">The bearing series the designation belongs to. <see langword="null"/> if not recorded.</param>
/// <param name="Variant">A variant or execution code distinguishing this record from a sibling sharing the same designation. <see langword="null"/> if not applicable.</param>
/// <param name="FamilyDesignation">The source's own wording for the bearing type, kept verbatim — the honest home for a family this taxonomy classifies as <see cref="BearingFamily.Other"/>. <see langword="null"/> if none was given.</param>
/// <param name="EquivalentReferences">
/// Designations other sources use for what they claim is the same
/// bearing, each with the claim's own source. Recording a claimed
/// equivalence is never asserting interchangeability — see
/// <see cref="BearingEquivalentReference"/>. Never <see langword="null"/>;
/// empty if none.
/// </param>
public sealed record BearingIdentity(
    string Manufacturer,
    string ManufacturerPartNumber,
    string? Designation = null,
    string? Series = null,
    string? Variant = null,
    string? FamilyDesignation = null,
    IReadOnlyList<BearingEquivalentReference>? EquivalentReferences = null)
{
    /// <summary>The manufacturer or supplier of record.</summary>
    public string Manufacturer { get; } = string.IsNullOrWhiteSpace(Manufacturer)
        ? throw new ArgumentException("A bearing identity must name a manufacturer.", nameof(Manufacturer))
        : Manufacturer;

    /// <summary>The manufacturer's own ordering part number.</summary>
    public string ManufacturerPartNumber { get; } = string.IsNullOrWhiteSpace(ManufacturerPartNumber)
        ? throw new ArgumentException("A bearing identity must carry a manufacturer part number.", nameof(ManufacturerPartNumber))
        : ManufacturerPartNumber;

    /// <summary>Designations other sources use for what they claim is the same bearing. Never <see langword="null"/>.</summary>
    public IReadOnlyList<BearingEquivalentReference> EquivalentReferences { get; init; } =
        EquivalentReferences ?? [];

    /// <summary>
    /// The case-insensitive key <see cref="Manufacturer"/> and
    /// <see cref="ManufacturerPartNumber"/> uniqueness is enforced on.
    /// Not an identity — see this type's own remarks.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string PartNumberKey => $"{Manufacturer.Trim()} {ManufacturerPartNumber.Trim()}".ToUpperInvariant();
}
