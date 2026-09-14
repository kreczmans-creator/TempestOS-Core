using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>What kind of thing an <see cref="EvidenceReference"/> points at.</summary>
/// <remarks>
/// Closed, because the point of the enum is that a reader can tell how
/// much weight a piece of evidence carries. A measurement and an
/// engineer's judgement are both legitimate evidence and are not
/// interchangeable, and collapsing them into "a note" would lose exactly
/// the distinction a later reviewer needs.
/// </remarks>
public enum EvidenceKind
{
    /// <summary>Not recorded. Never read as "no evidence exists" — only as "nobody said".</summary>
    Unspecified,

    /// <summary>A record in one of the `P01` reference libraries, pinned to a revision.</summary>
    ReferenceDataRecord,

    /// <summary>A published standard, identified through `A2`.</summary>
    Standard,

    /// <summary>A released engineering constant, from `A6`.</summary>
    EngineeringConstant,

    /// <summary>A requirement the platform holds.</summary>
    Requirement,

    /// <summary>A recorded calculation execution.</summary>
    Calculation,

    /// <summary>A recorded verification.</summary>
    Verification,

    /// <summary>A test report or measurement result.</summary>
    Measurement,

    /// <summary>A document held elsewhere in the platform or outside it.</summary>
    Document,

    /// <summary>
    /// A named engineer's own judgement. Legitimate evidence, and
    /// deliberately labelled as what it is so nobody later mistakes it for
    /// a measurement.
    /// </summary>
    EngineeringJudgement,

    /// <summary>Evidence of a kind this taxonomy does not classify.</summary>
    Other
}

/// <summary>
/// One thing that supports an assessment, finding or decision.
/// </summary>
/// <remarks>
/// <para>
/// Evidence is what makes a P02 result reconstructable rather than merely
/// assertive. A result saying "Fail" is an opinion; a result saying "Fail,
/// because the yield strength recorded in Materials/mat-17 at revision 3
/// is below the requirement" is engineering.
/// </para>
/// <para>
/// <see cref="Pin"/> is set wherever the evidence is a reference-data
/// record, so the exact revision travels with the claim. Where the
/// evidence lives outside the reference libraries — a document, a
/// measurement, a named engineer's judgement —
/// <see cref="Reference"/> identifies it in whatever terms that source
/// uses, and the pin stays null rather than being faked.
/// </para>
/// </remarks>
/// <param name="Kind">What kind of evidence this is.</param>
/// <param name="Description">What the evidence says, in plain engineering language. Required.</param>
/// <param name="Pin">The pinned reference-data record this evidence is, where it is one. <see langword="null"/> otherwise.</param>
/// <param name="Reference">An identifier for evidence held outside the reference libraries — a document number, a report reference, a principal id. <see langword="null"/> if none applies.</param>
public sealed record EvidenceReference(
    EvidenceKind Kind,
    string Description,
    ReferencePin? Pin = null,
    string? Reference = null)
{
    /// <summary>What the evidence says.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("Evidence must say what it shows.", nameof(Description))
        : Description.Trim();

    /// <summary>Whether this evidence resolves to a pinned reference-data revision, and so is reconstructable exactly.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsReconstructable => Pin is not null;

    /// <summary>Whether this evidence is a person's judgement rather than a recorded fact.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsJudgement => Kind == EvidenceKind.EngineeringJudgement;

    /// <summary>Evidence that is a pinned reference-data record.</summary>
    public static EvidenceReference FromReferenceData(ReferencePin pin, string description)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return new EvidenceReference(EvidenceKind.ReferenceDataRecord, description, pin);
    }

    /// <summary>Evidence that is a named engineer's judgement, labelled as such.</summary>
    public static EvidenceReference FromJudgement(string principalId, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);

        return new EvidenceReference(EvidenceKind.EngineeringJudgement, description, Reference: principalId.Trim());
    }
}
