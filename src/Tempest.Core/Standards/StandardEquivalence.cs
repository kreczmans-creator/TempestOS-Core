using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>The degree to which one standard is equivalent to another, as the source stated it.</summary>
public enum StandardEquivalenceKind
{
    /// <summary>The source asserted a relationship but did not qualify it.</summary>
    Unspecified,

    /// <summary>Technically identical, adopted without change.</summary>
    Identical,

    /// <summary>Technically equivalent, with editorial or presentational differences only.</summary>
    Equivalent,

    /// <summary>Adopted with stated technical deviations.</summary>
    Modified,

    /// <summary>Related in subject but not equivalent — recorded so a reader is not left to assume otherwise.</summary>
    NotEquivalent
}

/// <summary>
/// A relationship between one standard and another published elsewhere —
/// the national adoption, regional transposition or cross-body equivalence
/// that lets one physical requirement appear under several designations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recorded, never asserted.</b> <see cref="Origin"/> says who claimed
/// the equivalence. TempestOS never decides that two standards are
/// equivalent: that is a technical judgement belonging to the bodies that
/// published them, and an equivalence carrying
/// <see cref="ReferenceValueOrigin.DerivedByTempestOS"/> is flagged by
/// validation rather than presented as reference data.
/// </para>
/// <para>
/// <b>Data, not a document relationship.</b> This is what a source said
/// about the standard, so it lives in the definition — where
/// <see cref="StandardId"/> resolves it to a registered record when one
/// exists, and <see cref="Designation"/> preserves the claim when none
/// does. Supersession, by contrast, is TempestOS's own governance act and
/// goes through the catalogue's own
/// <see cref="IReferenceDataCatalog{TDefinition}.SupersedeAsync"/> and the
/// platform's existing <c>supersedes</c> relationship. A2 introduces no
/// relationship kind of its own (`ADR-0073`).
/// </para>
/// </remarks>
/// <param name="Designation">The other standard's own designation as the source writes it. Required.</param>
/// <param name="Kind">How equivalent the source said the two standards are.</param>
/// <param name="StandardId">The registered A2 record this resolves to, where one exists. <see langword="null"/> otherwise.</param>
/// <param name="Body">The organisation that publishes the other standard. <see langword="null"/> if the source did not name one.</param>
/// <param name="Origin">Who claimed the equivalence.</param>
/// <param name="Notes">The deviations the source stated, or any other qualification, verbatim. <see langword="null"/> if none.</param>
public sealed record StandardEquivalence(
    string Designation,
    StandardEquivalenceKind Kind = StandardEquivalenceKind.Unspecified,
    string? StandardId = null,
    string? Body = null,
    ReferenceValueOrigin Origin = ReferenceValueOrigin.Unknown,
    string? Notes = null)
{
    /// <summary>The other standard's own designation.</summary>
    public string Designation { get; } = string.IsNullOrWhiteSpace(Designation)
        ? throw new ArgumentException("A standard equivalence must name the other standard's designation.", nameof(Designation))
        : Designation.Trim();

    /// <summary>Whether this equivalence resolves to a registered A2 record.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsResolved => !string.IsNullOrWhiteSpace(StandardId);

    /// <summary>Whether TempestOS itself, rather than a source, claimed the equivalence.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDerived => Origin == ReferenceValueOrigin.DerivedByTempestOS;
}
