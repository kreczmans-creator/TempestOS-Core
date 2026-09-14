using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringAssets;

namespace Tempest.Core.BusinessOperations.Records;

/// <summary>What sort of business record this is.</summary>
/// <remarks>
/// Business records, not engineering documents. A drawing is `P05`'s
/// `E5`; an invoice, a certificate, a signed contract copy and an audit
/// report are these. The two libraries never overlap, and a record that
/// belongs in `E5` should be there instead (`ADR-0142`).
/// </remarks>
public enum BusinessRecordKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>An invoice, credit note or statement.</summary>
    FinancialRecord,

    /// <summary>A signed agreement or its variation.</summary>
    ContractualRecord,

    /// <summary>A certificate of conformity, calibration or approval.</summary>
    Certificate,

    /// <summary>An audit report or finding.</summary>
    AuditRecord,

    /// <summary>Something sent to or received from a customer, supplier or authority.</summary>
    Correspondence,

    /// <summary>A meeting record or minute.</summary>
    Minutes,

    /// <summary>A policy, procedure or work instruction.</summary>
    PolicyOrProcedure,

    /// <summary>Something a regulator or scheme requires be kept.</summary>
    StatutoryRecord,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>
/// Why a record is kept, and for how long.
/// </summary>
/// <remarks>
/// <para>
/// <b>The platform holds no retention law.</b> Statutory retention
/// periods differ by jurisdiction, by record type and over time, and a
/// platform that shipped them would be giving legal advice it cannot
/// stand behind. `WP04.6` records the period <em>the organisation has
/// decided on</em> and the basis it decided on, and computes nothing from
/// a rule of its own (`ADR-0142`).
/// </para>
/// <para>
/// <see cref="Basis"/> is free text for exactly that reason: "Companies
/// Act, six years" is the organisation's statement, not the platform's.
/// </para>
/// </remarks>
/// <param name="RetainUntil">The date the organisation has decided to keep it until. <see langword="null"/> where kept indefinitely.</param>
/// <param name="Basis">Why that period, in the organisation's own words. <see langword="null"/> where nobody said.</param>
/// <param name="IsIndefinite">Whether the organisation has decided to keep it permanently.</param>
/// <param name="DisposalMethod">How it is to be destroyed when the time comes. <see langword="null"/> where nobody said.</param>
public sealed record RetentionTerms(
    DateOnly? RetainUntil = null,
    string? Basis = null,
    bool IsIndefinite = false,
    string? DisposalMethod = null)
{
    /// <summary>Retention nobody has decided.</summary>
    public static RetentionTerms Undecided { get; } = new();

    /// <summary>Whether the organisation has decided anything at all about how long to keep this.</summary>
    public bool IsDecided => IsIndefinite || RetainUntil is not null;

    /// <summary>Whether the record is past the date the organisation decided to keep it to.</summary>
    /// <remarks>
    /// Reports; it never deletes. Disposal is an act with legal
    /// consequence and `P04` performs none of it.
    /// </remarks>
    public bool IsDueForReviewAt(DateOnly asAt) => !IsIndefinite && RetainUntil is { } until && until <= asAt;
}

/// <summary>
/// A business record the organisation keeps.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a second document store.</b> The bytes live in
/// <c>EngineeringData</c> exactly as `P05`'s `E5` arranges; `WP04.6`
/// holds the business metadata a document store does not — why the record
/// is kept, for how long, who it concerns, and what it relates to
/// (`ADR-0142`).
/// </para>
/// <para>
/// <b>Not `E5`.</b> `E5` governs technical documents: drawings,
/// specifications, procedures issued at a revision. This governs business
/// records: invoices, certificates, correspondence. A record belonging in
/// one should not be in the other, and the two libraries share no
/// document kind.
/// </para>
/// </remarks>
public sealed record BusinessRecord
{
    /// <summary>The reference the record is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What it is. Required.</summary>
    public required string Title { get; init; }

    /// <summary>What sort of record it is.</summary>
    public BusinessRecordKind Kind { get; init; } = BusinessRecordKind.Unspecified;

    /// <summary>The engineering document holding the content. <see langword="null"/> where the content lives outside TempestOS.</summary>
    public Guid? DocumentId { get; init; }

    /// <summary>Where the content lives, when it is not a document TempestOS holds. <see langword="null"/> otherwise.</summary>
    public string? ExternalLocation { get; init; }

    /// <summary>When the record was created or received. <see langword="null"/> where unrecorded.</summary>
    public DateOnly? RecordedOn { get; init; }

    /// <summary>How sensitive it is.</summary>
    public ConfidentialityClassification Classification { get; init; } = ConfidentialityClassification.Internal;

    /// <summary>How long the organisation has decided to keep it.</summary>
    public RetentionTerms Retention { get; init; } = RetentionTerms.Undecided;

    /// <summary>Who it concerns. <see langword="null"/> where it concerns nobody outside.</summary>
    public PartyReference? Party { get; init; }

    /// <summary>The project it belongs to. <see langword="null"/> where it belongs to none.</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>What else it relates to, by reference and kind. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// Reuses `P05`'s
    /// <see cref="EngineeringAssets.TechnicalDocumentation.DocumentRelationship"/>
    /// rather than declaring a second relationship model — the shape of a
    /// document-to-document link is the same whichever library holds the
    /// document.
    /// </remarks>
    public IReadOnlyList<EngineeringAssets.TechnicalDocumentation.DocumentRelationship> Relationships { get; init; } = [];

    /// <summary>Supporting material. Never <see langword="null"/>.</summary>
    public IReadOnlyList<EngineeringEvidence> Evidence { get; init; } = [];

    /// <summary>Who owns it, and where it stands.</summary>
    public OperationalFacts Facts { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether anybody can actually get at the content.</summary>
    public bool IsRetrievable => DocumentId is not null || !string.IsNullOrWhiteSpace(ExternalLocation);

    /// <summary>Whether the organisation has decided how long to keep it.</summary>
    public bool HasRetentionDecision => Retention.IsDecided;

    /// <summary>Whether the record has passed the date the organisation decided to keep it to.</summary>
    /// <remarks>
    /// A prompt for a person to review, never an instruction to delete.
    /// `P04` disposes of nothing.
    /// </remarks>
    public bool IsDueForRetentionReviewAt(DateOnly asAt) => Retention.IsDueForReviewAt(asAt);

    /// <summary>
    /// Whether a record likely to carry a statutory retention period has
    /// no retention decision recorded against it.
    /// </summary>
    public bool NeedsRetentionDecision =>
        !HasRetentionDecision
        && Kind is BusinessRecordKind.FinancialRecord
            or BusinessRecordKind.ContractualRecord
            or BusinessRecordKind.StatutoryRecord
            or BusinessRecordKind.Certificate;

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}
