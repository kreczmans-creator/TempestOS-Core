using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>
/// The canonical engineering description of one bearing: everything a
/// source said about it, structured, dimensioned and attributable.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this type is not.</b> It carries no TempestOS identity, no
/// provenance, no validation state and no revision number — those belong
/// to the registered record (<see cref="IReferenceRecord{TDefinition}"/>),
/// because they are catalogue governance rather than engineering
/// description. It carries no
/// supplier, price, lead time or stock information — those belong to a
/// future commercial capability (P03). It carries no statement of
/// suitability for any application — that is engineering judgement and
/// belongs to a future selection capability (P02). This type is
/// deliberately only the data.
/// </para>
/// <para>
/// <b>Missing is not zero.</b> Every optional field is nullable, and a
/// field the source did not supply stays <see langword="null"/>. Nothing
/// in this library ever substitutes a default for an unknown engineering
/// value.
/// </para>
/// <para>
/// <b>Type-aware, not flat.</b> A property that is not meaningful for a
/// family is simply not set — see <see cref="BearingFamilyTraits"/>, which
/// is what lets a reader tell "not applicable to this family" apart from
/// "applicable but not recorded".
/// </para>
/// </remarks>
public sealed record BearingDefinition
{
    /// <summary>How the bearing is identified commercially and by designation. Required.</summary>
    public required BearingIdentity Identity { get; init; }

    /// <summary>The bearing's own family. Required — this is what makes the rest of the record interpretable.</summary>
    public required BearingFamily Family { get; init; }

    /// <summary>The bearing's own dimensional data. Required as a record (individual dimensions within it are each optional).</summary>
    public required BearingGeometry Geometry { get; init; }

    /// <summary>The load ratings the source stated. <see langword="null"/> if the source gave none.</summary>
    public BearingLoadRatings? LoadRatings { get; init; }

    /// <summary>Every speed rating the source stated, each with its own kind, origin and conditions. Never <see langword="null"/>; empty if none.</summary>
    public IReadOnlyList<BearingSpeedRating> SpeedRatings { get; init; } = [];

    /// <summary>Sealing, clearance, precision, rows and contact angle. <see langword="null"/> if the source gave none of them.</summary>
    public BearingConfiguration? Configuration { get; init; }

    /// <summary>Material and construction information, referencing the canonical Materials catalogue. <see langword="null"/> if the source gave none.</summary>
    public BearingConstruction? Construction { get; init; }

    /// <summary>Lubrication information the source supplied. <see langword="null"/> if it supplied none.</summary>
    public BearingLubrication? Lubrication { get; init; }

    /// <summary>The bearing's own mass. <see langword="null"/> if not recorded.</summary>
    public Quantity<Mass>? Mass { get; init; }

    /// <summary>Every standard this record's own information is stated against. Never <see langword="null"/>; empty if none is cited.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>
    /// The source's own application classification for the bearing (e.g. a
    /// catalogue's own product-family grouping), verbatim. Recording how a
    /// manufacturer categorises its own product is data; deciding whether
    /// a bearing suits an application is not, and this library does not do
    /// it. <see langword="null"/> if the source gave none.
    /// </summary>
    public string? ApplicationClassification { get; init; }

    /// <summary>
    /// Manufacturer-specific data that cannot be normalised into any field
    /// above without losing what it means, kept verbatim and keyed by the
    /// source's own label. Never <see langword="null"/>; empty if none.
    /// Discarding it would be silent data loss; forcing it into a
    /// normalised field would be silent data corruption.
    /// </summary>
    public IReadOnlyDictionary<string, string> ManufacturerAttributes { get; init; } = new Dictionary<string, string>();

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The date from which this record's own values are effective, where the source states one. <see langword="null"/> if it does not.</summary>
    public DateOnly? EffectiveDate { get; init; }
}
