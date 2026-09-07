using Tempest.Core.ReferenceData;

namespace Tempest.Core.Components;

/// <summary>
/// The canonical engineering description of one mechanical component:
/// everything a source said about it, structured, dimensioned and
/// attributable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared governance, per-family engineering.</b> The identity,
/// dimensions, ratings, material link and standards below are common to
/// every family. The engineering detail that is not common is held in one
/// of three typed records — <see cref="Spring"/>, <see cref="Gear"/>,
/// <see cref="DriveElement"/> — and
/// <see cref="ComponentFamilyTraits"/> decides which a family may carry.
/// A gear detail on a spring is a modelling error and validation reports
/// it as one; families outside those three (couplings, collars, keys,
/// seals, plain bearings, guides, ball screws) legitimately carry none,
/// and that is a fact rather than a gap.
/// </para>
/// <para>
/// <b>What is deliberately absent.</b> No spring design or optimisation,
/// no gear rating, no drive selection, no centre-distance or ratio
/// calculation, no life prediction, no suitability judgement. A5 records
/// what components exist and what their sources published.
/// </para>
/// <para>
/// Carries no TempestOS identity, no provenance, no validation state and
/// no revision number — those belong to the registered record
/// (<see cref="IReferenceRecord{TDefinition}"/>).
/// </para>
/// </remarks>
public sealed record ComponentDefinition
{
    /// <summary>The component's own family. Required — this is what makes the rest of the record interpretable.</summary>
    public required ComponentFamily Family { get; init; }

    /// <summary>The designation the component is known by, as the source writes it. Required.</summary>
    public required string Designation { get; init; }

    /// <summary>The manufacturer or supplier of record. <see langword="null"/> where the record describes a generic item.</summary>
    public string? Manufacturer { get; init; }

    /// <summary>The manufacturer's own ordering part number. <see langword="null"/> if the source gives only a designation.</summary>
    public string? ManufacturerPartNumber { get; init; }

    /// <summary>The component's own display name, where the source gives one distinct from the designation. <see langword="null"/> otherwise.</summary>
    public string? Name { get; init; }

    /// <summary>Spring geometry and rate. <see langword="null"/> unless the family is a spring, or unless nothing was recorded.</summary>
    public SpringDetail? Spring { get; init; }

    /// <summary>Gear tooth geometry. <see langword="null"/> unless the family is a gear, or unless nothing was recorded.</summary>
    public GearDetail? Gear { get; init; }

    /// <summary>Belt, chain, pulley or sprocket geometry. <see langword="null"/> unless the family is a drive element, or unless nothing was recorded.</summary>
    public DriveElementDetail? DriveElement { get; init; }

    /// <summary>Envelope and mounting dimensions. Never <see langword="null"/>; an all-absent instance means nothing was recorded.</summary>
    public ComponentDimensions Dimensions { get; init; } = new();

    /// <summary>Published limits. Never <see langword="null"/>; an all-absent instance means nothing was recorded.</summary>
    public ComponentRatings Ratings { get; init; } = new();

    /// <summary>
    /// The <c>materialId</c> of the registered A1 material record this
    /// component is made from. <see langword="null"/> where the material is
    /// not registered — <see cref="MaterialDesignation"/> then still
    /// records what the source said.
    /// </summary>
    public string? MaterialId { get; init; }

    /// <summary>The material as the source designates it, verbatim. <see langword="null"/> if the source gave none.</summary>
    public string? MaterialDesignation { get; init; }

    /// <summary>The surface treatment, plating or coating as the source designates it, verbatim. <see langword="null"/> if none is recorded.</summary>
    public string? SurfaceTreatment { get; init; }

    /// <summary>Every standard this record's own information is stated against. Never <see langword="null"/>; empty if none is cited.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>
    /// The source's own classification wording, verbatim — the honest home
    /// for a family this taxonomy classifies as
    /// <see cref="ComponentFamily.Other"/>, and for a figure the source
    /// quoted in a form this model has no field for. <see langword="null"/>
    /// if the source gave none.
    /// </summary>
    public string? SourceClassification { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The date from which this record's own values are effective, where the source states one. <see langword="null"/> if it does not.</summary>
    public DateOnly? EffectiveDate { get; init; }

    /// <summary>The broad group this component's own family belongs to.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ComponentGroup Group => ComponentFamilyTraits.GroupOf(Family);

    /// <summary>The key component identity uniqueness is enforced on.</summary>
    /// <remarks>
    /// A manufacturer part number is not an identity — the TempestOS record
    /// Id is always caller-assigned and never derived from here. Where a
    /// manufacturer and part number are both recorded they form the key,
    /// exactly as A3 and A4 do; otherwise the designation alone does.
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
