namespace Tempest.Core.Bearings;

/// <summary>
/// A bearing's own material and construction information.
/// </summary>
/// <remarks>
/// Materials are referenced, never redefined. Each material field holds a
/// <c>materialId</c> registered through
/// <see cref="Materials.IMaterialCatalog"/> — the canonical TempestOS
/// materials system — so this library never becomes a second, competing
/// materials database. A reference that does not resolve is reported by
/// <see cref="IBearingValidationService"/> as a warning
/// (<see cref="BearingValidationRules.MaterialReferenceUnresolved"/>);
/// this record itself takes no dependency on the material catalogue, so a
/// bearing can be recorded before the material it names has been.
/// </remarks>
/// <param name="RingMaterialId">The <c>materialId</c> of the ring material. <see langword="null"/> if not recorded.</param>
/// <param name="RollingElementMaterialId">The <c>materialId</c> of the rolling-element material. <see langword="null"/> if not recorded, or not applicable (a plain bearing has none).</param>
/// <param name="CageMaterialId">The <c>materialId</c> of the cage material. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="SealMaterialId">The <c>materialId</c> of the seal material. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="Class">The broad construction class, where the source states one.</param>
/// <param name="CageDesignation">The cage's own designation as the source writes it (a catalogue suffix). Kept verbatim. <see langword="null"/> if none was given.</param>
/// <param name="ManufacturerDesignation">The manufacturer's own construction designation, verbatim. <see langword="null"/> if none was given.</param>
public sealed record BearingConstruction(
    string? RingMaterialId = null,
    string? RollingElementMaterialId = null,
    string? CageMaterialId = null,
    string? SealMaterialId = null,
    BearingConstructionClass Class = BearingConstructionClass.Unspecified,
    string? CageDesignation = null,
    string? ManufacturerDesignation = null)
{
    /// <summary>Every non-null material reference this record carries, in a fixed order. Never <see langword="null"/>; empty if none is recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> ReferencedMaterialIds =>
        new[] { RingMaterialId, RollingElementMaterialId, CageMaterialId, SealMaterialId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();
}
