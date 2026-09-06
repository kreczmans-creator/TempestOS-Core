using Tempest.Core.ReferenceData;

namespace Tempest.Core.Materials;

/// <summary>
/// The canonical engineering description of one material: everything a
/// source said about it, structured, dimensioned and attributable.
/// </summary>
/// <remarks>
/// Carries no TempestOS identity, no provenance, no validation state and
/// no revision number — those belong to the registered record
/// (<see cref="IReferenceRecord{TDefinition}"/>), because they are
/// catalogue governance rather than engineering description. Every
/// optional field is nullable and stays <see langword="null"/> where the
/// source supplied nothing; no property is ever defaulted to zero.
/// </remarks>
public sealed record MaterialDefinition
{
    /// <summary>The material's own display name. Required.</summary>
    public required string Name { get; init; }

    /// <summary>The material's own family. Required — this is what makes the rest of the record interpretable.</summary>
    public required MaterialFamily Family { get; init; }

    /// <summary>
    /// The designation the material is known by (a standard grade
    /// designation, a trade name). <see langword="null"/> where the source
    /// gives only a name.
    /// </summary>
    public string? Designation { get; init; }

    /// <summary>The grade within the designation, where the source distinguishes them. <see langword="null"/> if not recorded.</summary>
    public string? Grade { get; init; }

    /// <summary>
    /// The delivery or heat-treatment condition the properties are stated
    /// for (a temper, a quench-and-temper condition, an annealed state).
    /// <see langword="null"/> if not recorded, or not applicable to the
    /// family (see <see cref="MaterialFamilyTraits.HasHeatTreatmentCondition"/>).
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// The source's own classification wording, verbatim — the honest home
    /// for a family this taxonomy classifies as
    /// <see cref="MaterialFamily.Other"/>, and the continuation of this
    /// library's own original open <c>Category</c>. <see langword="null"/>
    /// if the source gave none.
    /// </summary>
    public string? SourceClassification { get; init; }

    /// <summary>The supplier or producer of record, where the material is a specific supplier's product rather than a generic grade. <see langword="null"/> otherwise.</summary>
    public string? Supplier { get; init; }

    /// <summary>The supplier's own designation for the material, verbatim. <see langword="null"/> if none was given.</summary>
    public string? SupplierDesignation { get; init; }

    /// <summary>
    /// Every registered engineering property, keyed by name. Well-known
    /// names are listed in <see cref="MaterialPropertyNames"/> and are
    /// dimension-checked; any other name is legitimate and stored as
    /// given. Never <see langword="null"/>; empty if none is recorded.
    /// </summary>
    public IReadOnlyDictionary<string, ReferenceQuantityValue> Properties { get; init; } = new Dictionary<string, ReferenceQuantityValue>();

    /// <summary>Every standard this record's own information is stated against. Never <see langword="null"/>; empty if none is cited.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>
    /// Processing, forming or manufacturing notes the source supplied,
    /// verbatim. Recording what a source says about how a material is
    /// worked is data; deciding which process suits a job is not, and
    /// belongs to A7 and to a future selection capability.
    /// <see langword="null"/> if the source gave none.
    /// </summary>
    public string? ProcessingNotes { get; init; }

    /// <summary>
    /// Corrosion and environmental information the source supplied,
    /// verbatim. <see langword="null"/> if the source gave none.
    /// </summary>
    public string? EnvironmentalNotes { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The date from which this record's own values are effective, where the source states one. <see langword="null"/> if it does not.</summary>
    public DateOnly? EffectiveDate { get; init; }

    /// <summary>The key material designation uniqueness is enforced on, or <see langword="null"/> where the record states no designation.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DesignationKey => Designation is null ? null : DesignationKeyFor(Supplier, Designation);

    /// <summary>
    /// Builds the uniqueness key from a supplier and designation that are
    /// not (yet) a record — the lookup path. A generic grade and a
    /// supplier's own product legitimately share a designation, so the
    /// supplier is part of the key.
    /// </summary>
    public static string DesignationKeyFor(string? supplier, string designation) =>
        $"{supplier?.Trim() ?? string.Empty} {designation.Trim()}".ToUpperInvariant();
}
