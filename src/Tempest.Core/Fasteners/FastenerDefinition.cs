using Tempest.Core.ReferenceData;

namespace Tempest.Core.Fasteners;

/// <summary>
/// The canonical engineering description of one fastener: everything a
/// source said about it, structured, dimensioned and attributable.
/// </summary>
/// <remarks>
/// <para>
/// Carries no TempestOS identity, no provenance, no validation state and
/// no revision number — those belong to the registered record
/// (<see cref="IReferenceRecord{TDefinition}"/>). Every optional field is
/// nullable and stays <see langword="null"/> where the source supplied
/// nothing; no value is ever defaulted to zero.
/// </para>
/// <para>
/// <b>What is deliberately absent.</b> No joint analysis, no preload, no
/// clamp load, no thread-engagement check, no torque TempestOS worked out,
/// and no recommendation about where a fastener should be used. A3 records
/// what fasteners exist and what their sources published about them; a
/// future calculation and selection capability consumes that.
/// </para>
/// </remarks>
public sealed record FastenerDefinition
{
    /// <summary>The fastener's own family. Required — this is what makes the rest of the record interpretable.</summary>
    public required FastenerFamily Family { get; init; }

    /// <summary>
    /// The designation the fastener is known by, as the source writes it.
    /// Required — a fastener record with no designation cannot be looked
    /// up or cited.
    /// </summary>
    public required string Designation { get; init; }

    /// <summary>The manufacturer or supplier of record, where the record describes a specific supplier's product rather than a generic item. <see langword="null"/> otherwise.</summary>
    public string? Manufacturer { get; init; }

    /// <summary>The manufacturer's own ordering part number. <see langword="null"/> if the source gives only a designation.</summary>
    public string? ManufacturerPartNumber { get; init; }

    /// <summary>The fastener's own thread. <see langword="null"/> where the family is unthreaded, or where the source did not state one.</summary>
    public ThreadSpecification? Thread { get; init; }

    /// <summary>The head form.</summary>
    public FastenerHeadType HeadType { get; init; } = FastenerHeadType.Unspecified;

    /// <summary>The driving feature.</summary>
    public FastenerDriveType DriveType { get; init; } = FastenerDriveType.Unspecified;

    /// <summary>
    /// The source's own style or form wording where this taxonomy has no
    /// field for it — a nut style, a washer form, a retaining-ring type.
    /// <see langword="null"/> if the source gave none.
    /// </summary>
    public string? StyleDesignation { get; init; }

    /// <summary>The fastener's own dimensions. Never <see langword="null"/>; an all-absent instance means nothing was recorded.</summary>
    public FastenerDimensions Dimensions { get; init; } = new();

    /// <summary>The published mechanical properties. Never <see langword="null"/>; an all-absent instance means nothing was recorded.</summary>
    public FastenerMechanicalProperties Mechanical { get; init; } = new();

    /// <summary>
    /// The <c>materialId</c> of the registered A1 material record this
    /// fastener is made from. <see langword="null"/> where the material is
    /// not registered — <see cref="MaterialDesignation"/> then still
    /// records what the source said.
    /// </summary>
    /// <remarks>
    /// A typed link, not a copy: the material's own properties belong to
    /// A1 and are never duplicated here.
    /// </remarks>
    public string? MaterialId { get; init; }

    /// <summary>The material as the source designates it, verbatim. <see langword="null"/> if the source gave none.</summary>
    public string? MaterialDesignation { get; init; }

    /// <summary>The surface finish or coating. <see langword="null"/> if none is recorded — never a claim the fastener is uncoated.</summary>
    public FastenerFinish? Finish { get; init; }

    /// <summary>
    /// Tightening torques sources published for this fastener. Never
    /// <see langword="null"/>; empty if none is recorded.
    /// </summary>
    /// <remarks>
    /// A list rather than one value, because a source legitimately
    /// publishes different figures for different friction conditions and
    /// property classes, and collapsing them to one would discard the
    /// conditions that make any of them meaningful.
    /// </remarks>
    public IReadOnlyList<FastenerTorqueReference> TorqueReferences { get; init; } = [];

    /// <summary>Every standard this record's own information is stated against. Never <see langword="null"/>; empty if none is cited.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>
    /// The source's own classification wording, verbatim — the honest home
    /// for a family, head, drive or thread system this taxonomy classifies
    /// as <c>Other</c>. <see langword="null"/> if the source gave none.
    /// </summary>
    public string? SourceClassification { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The date from which this record's own values are effective, where the source states one. <see langword="null"/> if it does not.</summary>
    public DateOnly? EffectiveDate { get; init; }

    /// <summary>The key fastener identity uniqueness is enforced on.</summary>
    /// <remarks>
    /// A manufacturer part number is not an identity — two manufacturers
    /// reuse designations for items that are not the same item, and a
    /// designation changes when a catalogue is revised — so the TempestOS
    /// record Id is always caller-assigned and never derived from here.
    /// Where a manufacturer and part number are both recorded they form
    /// the key, exactly as A4 does for bearings; otherwise the designation
    /// alone does, so a generic item registered from a standard is still
    /// held unique.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public string IdentityKey => IdentityKeyFor(Manufacturer, ManufacturerPartNumber, Designation);

    /// <summary>Builds the uniqueness key from values that are not (yet) a record — the lookup path.</summary>
    /// <exception cref="ArgumentException"><paramref name="designation"/> is null, empty, or whitespace.</exception>
    public static string IdentityKeyFor(string? manufacturer, string? partNumber, string designation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designation);

        return string.IsNullOrWhiteSpace(partNumber)
            ? $"{manufacturer?.Trim() ?? string.Empty} {designation.Trim()}".ToUpperInvariant()
            : $"{manufacturer?.Trim() ?? string.Empty} #{partNumber.Trim()}".ToUpperInvariant();
    }
}
