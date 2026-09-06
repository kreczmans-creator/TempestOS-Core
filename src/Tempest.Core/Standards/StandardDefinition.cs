using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>
/// The canonical engineering description of one standard: its
/// bibliographic identity, its standing with its own publisher, and how it
/// relates to other standards.
/// </summary>
/// <remarks>
/// <para>
/// <b>A2 is a register of standards, not a copy of them.</b> There is no
/// field here for a standard's clauses, tables, figures or requirements,
/// and there deliberately never will be: that content is the copyright of
/// the issuing body, and reproducing it would be both unlawful and a
/// category error — TempestOS would then be asserting technical
/// requirements it has no authority to state.
/// <see cref="ScopeSummary"/> is a summary written by whoever recorded the
/// standard, in their own words, and validation flags one long enough to
/// look like reproduced text.
/// </para>
/// <para>
/// <b>Registering a standard is not claiming compliance with it.</b>
/// Nothing on this record, and nothing anywhere in Group A, asserts that
/// any item conforms to any standard. A citing library's own
/// <see cref="StandardReference"/> records only that a source cited the
/// standard.
/// </para>
/// <para>
/// Carries no TempestOS identity, no provenance, no validation state and
/// no revision number — those belong to the registered record
/// (<see cref="IReferenceRecord{TDefinition}"/>), because they are
/// catalogue governance rather than description of the standard.
/// </para>
/// </remarks>
public sealed record StandardDefinition
{
    /// <summary>The organisation that publishes the standard. Required — a designation without a body is ambiguous between publishers.</summary>
    public required StandardsBody Body { get; init; }

    /// <summary>
    /// The standard's own designation, without the body prefix where the
    /// body is already named (for example the number and any part
    /// suffix). Required.
    /// </summary>
    public required string Designation { get; init; }

    /// <summary>The standard's published title, for bibliographic citation. <see langword="null"/> if not recorded.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// The edition, year or version this record describes.
    /// <see langword="null"/> where none is recorded — which makes the
    /// record uncitable with precision, and is warned about rather than
    /// silently accepted.
    /// </summary>
    /// <remarks>
    /// Part of the uniqueness key: two editions of one standard are two
    /// records, related by supersession, not one record edited in place.
    /// An edition that superseded another is exactly the history a legacy
    /// design review needs to read back.
    /// </remarks>
    public string? Edition { get; init; }

    /// <summary>The part number, where the standard is one part of a multi-part standard. <see langword="null"/> if it is not, or if not recorded.</summary>
    public string? PartNumber { get; init; }

    /// <summary>What kind of document the standard is.</summary>
    public StandardClassification Classification { get; init; } = StandardClassification.Unspecified;

    /// <summary>
    /// The engineering subjects the standard covers. Never
    /// <see langword="null"/>; empty means no discipline was recorded,
    /// never that the standard covers none.
    /// </summary>
    public IReadOnlyList<StandardDiscipline> Disciplines { get; init; } = [];

    /// <summary>
    /// The source's own classification wording, verbatim — the honest home
    /// for a standard whose kind or publisher this taxonomy classifies as
    /// <c>Other</c>. <see langword="null"/> if the source gave none.
    /// </summary>
    public string? SourceClassification { get; init; }

    /// <summary>The issuing body's own status for the standard. Never derived from, and never conflated with, the record's own validation state.</summary>
    public StandardPublicationStatus PublicationStatus { get; init; } = StandardPublicationStatus.Unknown;

    /// <summary>The date the issuing body published this edition. <see langword="null"/> if not recorded.</summary>
    public DateOnly? PublicationDate { get; init; }

    /// <summary>The date from which the standard takes effect, where the body states one separately from publication. <see langword="null"/> if not recorded.</summary>
    public DateOnly? EffectiveDate { get; init; }

    /// <summary>The date the issuing body withdrew the standard. <see langword="null"/> while it remains in force, or where the date is not recorded.</summary>
    public DateOnly? WithdrawalDate { get; init; }

    /// <summary>The date the issuing body last confirmed the edition without change. <see langword="null"/> if not recorded or not applicable.</summary>
    public DateOnly? ConfirmationDate { get; init; }

    /// <summary>
    /// A short summary, <b>in the recorder's own words</b>, of what the
    /// standard covers. Never the standard's own scope clause reproduced:
    /// see this type's own remarks. <see langword="null"/> if none was
    /// written.
    /// </summary>
    public string? ScopeSummary { get; init; }

    /// <summary>
    /// The designations this edition replaces, as the issuing body stated
    /// them. Never <see langword="null"/>; empty if none.
    /// </summary>
    /// <remarks>
    /// The publisher's claim, recorded as data. TempestOS's own record of
    /// which registered record replaced which is a separate, governed act
    /// (<see cref="IReferenceDataCatalog{TDefinition}.SupersedeAsync"/>),
    /// and the two are deliberately not merged: a standard can replace one
    /// nobody has registered here, and A2 must be able to record that
    /// without inventing a record to point at.
    /// </remarks>
    public IReadOnlyList<string> ReplacesDesignations { get; init; } = [];

    /// <summary>Equivalent, adopted or transposed standards, as sources stated them. Never <see langword="null"/>; empty if none.</summary>
    public IReadOnlyList<StandardEquivalence> Equivalences { get; init; } = [];

    /// <summary>
    /// The standards this one normatively references. Never
    /// <see langword="null"/>; empty if none is recorded — never a claim
    /// that the standard references none.
    /// </summary>
    public IReadOnlyList<StandardReference> NormativeReferences { get; init; } = [];

    /// <summary>The language this edition is published in, where recorded. <see langword="null"/> otherwise.</summary>
    public string? Language { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The full designation as a reader would cite it, body prefix included.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string FullDesignation => Edition is null
        ? $"{Body.Code} {Designation}"
        : $"{Body.Code} {Designation}:{Edition}";

    /// <summary>The key standard identity uniqueness is enforced on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string DesignationKey => DesignationKeyFor(Body.Code, Designation, Edition);

    /// <summary>
    /// Builds the uniqueness key from a body, designation and edition that
    /// are not (yet) a record — the lookup path.
    /// </summary>
    /// <remarks>
    /// The edition is part of the key because two editions of one standard
    /// are two distinct pieces of reference data, both of which must be
    /// holdable at once. Where no edition is recorded the key collapses to
    /// body and designation, so the library holds at most one undated
    /// record per standard — the right answer, since a second undated
    /// record of the same standard could not be told apart from the first.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="bodyCode"/> or <paramref name="designation"/> is null, empty, or whitespace.</exception>
    public static string DesignationKeyFor(string bodyCode, string designation, string? edition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(designation);

        return $"{bodyCode.Trim()} {designation.Trim()}:{edition?.Trim() ?? string.Empty}".ToUpperInvariant();
    }
}
