using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.TechnicalDocumentation;

/// <summary>What sort of technical document this is.</summary>
/// <remarks>
/// A closed vocabulary because the type decides what governance applies:
/// a drawing needs an issue state and a change note, a report needs an
/// author and a reviewer, and a procedure needs both plus an effective
/// date.
/// </remarks>
public enum TechnicalDocumentType
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>An engineering drawing.</summary>
    Drawing,

    /// <summary>A specification.</summary>
    Specification,

    /// <summary>A requirements document.</summary>
    RequirementsDocument,

    /// <summary>A design report or design justification.</summary>
    DesignReport,

    /// <summary>A calculation report.</summary>
    CalculationReport,

    /// <summary>A test or inspection procedure.</summary>
    Procedure,

    /// <summary>A test report.</summary>
    TestReport,

    /// <summary>A user or maintenance manual.</summary>
    Manual,

    /// <summary>A data sheet.</summary>
    DataSheet,

    /// <summary>A parts list or bill of materials.</summary>
    PartsList,

    /// <summary>A change note or engineering change request.</summary>
    ChangeNote,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>
/// Where a technical document has got to.
/// </summary>
/// <remarks>
/// A second axis from <see cref="ReferenceValidationState"/>, on the
/// reasoning `ADR-0129` set out for `P07`: the lifecycle state says how
/// far the <em>record</em> got through governance, this says where the
/// <em>document</em> has got to. A released, validated record of a draft
/// drawing must be expressible.
/// </remarks>
public enum DocumentStatus
{
    /// <summary>Being written.</summary>
    Draft,

    /// <summary>Written and awaiting a reviewer.</summary>
    InReview,

    /// <summary>Reviewed and awaiting approval.</summary>
    Approved,

    /// <summary>Issued for use.</summary>
    Issued,

    /// <summary>Issued, and since replaced by a later revision.</summary>
    Superseded,

    /// <summary>Withdrawn without replacement.</summary>
    Withdrawn,

    /// <summary>No longer maintained, and not withdrawn.</summary>
    Obsolete
}

/// <summary>What <see cref="DocumentStatus"/> means for use.</summary>
public static class DocumentStatuses
{
    /// <summary>Whether the document may be worked from.</summary>
    /// <remarks>
    /// True for <see cref="DocumentStatus.Issued"/> alone. An approved
    /// document that has not been issued is not yet in circulation, and a
    /// superseded one must not be picked up by mistake.
    /// </remarks>
    public static bool IsInForce(DocumentStatus status) => status == DocumentStatus.Issued;

    /// <summary>Whether the document has ever been in circulation.</summary>
    public static bool HasBeenIssued(DocumentStatus status) =>
        status is DocumentStatus.Issued or DocumentStatus.Superseded
            or DocumentStatus.Withdrawn or DocumentStatus.Obsolete;

    /// <summary>Whether the document is still being worked on.</summary>
    public static bool IsInPreparation(DocumentStatus status) =>
        status is DocumentStatus.Draft or DocumentStatus.InReview or DocumentStatus.Approved;
}

/// <summary>
/// One relationship this document has to another.
/// </summary>
/// <remarks>
/// Reuses <see cref="EngineeringData.DocumentReference"/>'s own
/// convention — a directed link with a caller-declared kind — and does
/// not invent a second relationship model. <see cref="Kinds"/> names the
/// relationships `E5` itself reasons about; anything else is still a
/// valid kind and is simply carried.
/// </remarks>
/// <param name="RelationshipKind">What the relationship is. Required.</param>
/// <param name="TargetDocumentId">The document at the other end, where it is a document. <see langword="null"/> otherwise.</param>
/// <param name="TargetReference">The other end by reference, where it is not a document TempestOS holds. <see langword="null"/> otherwise.</param>
/// <param name="Note">Anything else about the link. <see langword="null"/> if nothing.</param>
public sealed record DocumentRelationship(
    string RelationshipKind,
    Guid? TargetDocumentId = null,
    string? TargetReference = null,
    string? Note = null)
{
    /// <summary>What the relationship is.</summary>
    public string RelationshipKind { get; } = string.IsNullOrWhiteSpace(RelationshipKind)
        ? throw new ArgumentException("A document relationship must say what the relationship is.", nameof(RelationshipKind))
        : RelationshipKind.Trim();

    /// <summary>Whether the link names anything at the other end.</summary>
    public bool IsResolvable => TargetDocumentId is not null || !string.IsNullOrWhiteSpace(TargetReference);

    /// <summary>The relationship kinds `E5` itself reasons about.</summary>
    /// <remarks>
    /// <para>
    /// Not a closed set. <see cref="EngineeringData.DocumentReference"/>
    /// deliberately enforces no vocabulary, and `E5` does not impose one
    /// on it; these are the values `E5`'s own validation understands.
    /// </para>
    /// <para>
    /// <b>Supersession is not declared here.</b> It is platform-wide and
    /// canonically owned by
    /// <see cref="EngineeringDomain.GovernanceRelationshipKinds.Supersedes"/>
    /// — one value, one declaring class (`ADR-0105`). Use that constant
    /// for a supersession link; <see cref="All"/> includes it.
    /// </para>
    /// </remarks>
    public static class Kinds
    {
        /// <summary>This document is derived from the target.</summary>
        public const string DerivedFrom = "derivedFrom";

        /// <summary>This document implements the target.</summary>
        public const string Implements = "implements";

        /// <summary>This document verifies the target.</summary>
        public const string Verifies = "verifies";

        /// <summary>This document refers to the target without depending on it.</summary>
        public const string References = "references";

        /// <summary>This document is part of the target.</summary>
        public const string PartOf = "partOf";

        /// <summary>Every kind `E5` reasons about, including the canonically owned supersession value.</summary>
        public static IReadOnlyList<string> All { get; } =
        [
            EngineeringDomain.GovernanceRelationshipKinds.Supersedes,
            DerivedFrom,
            Implements,
            Verifies,
            References,
            PartOf,
        ];
    }
}

/// <summary>
/// A technical document, as a governed engineering asset.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a second document-management system.</b> The bytes, the
/// revision history and the document-to-document links belong to
/// <c>Tempest.Core.EngineeringData</c> and stay there. `E5` records what
/// the organisation needs to <em>govern</em> a document — what it is,
/// what state it is in, who owns it, when it takes effect, and what it
/// replaces — and points at the underlying document by Id (`ADR-0137`).
/// </para>
/// <para>
/// <b>Status is separate from the record lifecycle.</b> A Released,
/// Validated record of a Draft drawing is a perfectly accurate record,
/// and the two axes never collapse into one.
/// </para>
/// </remarks>
public sealed record TechnicalDocument
{
    /// <summary>The document number the organisation knows it by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What to call it. Required.</summary>
    public required string Title { get; init; }

    /// <summary>What sort of document it is.</summary>
    public TechnicalDocumentType Type { get; init; } = TechnicalDocumentType.Unspecified;

    /// <summary>Where the document has got to.</summary>
    public DocumentStatus Status { get; init; } = DocumentStatus.Draft;

    /// <summary>
    /// The organisation's own issue or sheet revision, as printed on the
    /// document. <see langword="null"/> where it carries none.
    /// </summary>
    /// <remarks>
    /// Deliberately a string and deliberately distinct from the record's
    /// own <c>RevisionNumber</c>. Drawings are issued at "A", "B", "P1";
    /// the platform counts 1, 2, 3; and conflating them would make it
    /// impossible to say which issue somebody actually holds.
    /// </remarks>
    public string? IssueRevision { get; init; }

    /// <summary>The underlying engineering document holding the content. <see langword="null"/> where the content lives outside TempestOS.</summary>
    public Guid? DocumentId { get; init; }

    /// <summary>Where the content lives, when it is not a document TempestOS holds. <see langword="null"/> otherwise.</summary>
    public string? ExternalLocation { get; init; }

    /// <summary>The project this document belongs to. <see langword="null"/> where it belongs to none.</summary>
    public string? ProjectIdentifier { get; init; }

    /// <summary>How this document relates to others. Never <see langword="null"/>.</summary>
    public IReadOnlyList<DocumentRelationship> Relationships { get; init; } = [];

    /// <summary>The document reference this one replaces. <see langword="null"/> where it replaces none.</summary>
    public string? SupersedesReference { get; init; }

    /// <summary>When the document takes effect and, where known, stops. <see langword="null"/> where nobody said.</summary>
    public EffectivePeriod? Effectivity { get; init; }

    /// <summary>When it was issued. <see langword="null"/> until it is.</summary>
    public DateOnly? IssuedOn { get; init; }

    /// <summary>Where and to whom it applies.</summary>
    public AssetApplicability Applicability { get; init; } = AssetApplicability.Unrestricted;

    /// <summary>Who owns it, who wrote it, who reviewed it, who approved it.</summary>
    public AssetGovernanceFacts Governance { get; init; } = new();

    /// <summary>The template it was produced from, at the revision worked from. <see langword="null"/> where none was used.</summary>
    public Templates.TemplateUsage? TemplateUsage { get; init; }

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the document may be worked from.</summary>
    public bool IsInForce => DocumentStatuses.IsInForce(Status);

    /// <summary>Whether the document has ever been in circulation.</summary>
    public bool HasBeenIssued => DocumentStatuses.HasBeenIssued(Status);

    /// <summary>Whether anybody can actually get at the content.</summary>
    /// <remarks>
    /// A governed record of a document nobody can open is a card in a
    /// catalogue with no book behind it.
    /// </remarks>
    public bool IsRetrievable => DocumentId is not null || !string.IsNullOrWhiteSpace(ExternalLocation);

    /// <summary>Whether the document is in force on <paramref name="asAt"/>.</summary>
    public bool IsInForceAt(DateOnly asAt) =>
        IsInForce && (Effectivity is not { } effectivity || effectivity.Contains(asAt));

    /// <summary>Whether it has run past its own effectivity as at <paramref name="asAt"/>.</summary>
    public bool HasExpiredAt(DateOnly asAt) => Effectivity?.HasExpiredBy(asAt) ?? false;

    /// <summary>The relationships of <paramref name="relationshipKind"/>. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="relationshipKind"/> is null, empty, or whitespace.</exception>
    public IReadOnlyList<DocumentRelationship> RelationshipsOfKind(string relationshipKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipKind);

        return Relationships
            .Where(r => string.Equals(r.RelationshipKind, relationshipKind.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// The <see cref="EngineeringData.DocumentReference"/> rows this
    /// document's relationships correspond to, for a caller writing them
    /// into the document store.
    /// </summary>
    /// <remarks>
    /// The seam that keeps `E5` from becoming a second relationship
    /// model: relationships are authored here and are expressible as the
    /// platform's own links, rather than living only in `E5`.
    /// Relationships whose other end is not a TempestOS document are
    /// skipped, having nothing to point at.
    /// </remarks>
    /// <param name="createdByPrincipalId">Who recorded the links. <see langword="null"/> to leave provenance honestly absent.</param>
    /// <param name="createdAt">When. <see langword="null"/> to leave it absent.</param>
    /// <exception cref="InvalidOperationException">The document has no <see cref="DocumentId"/> to be the source of a link.</exception>
    public IReadOnlyList<EngineeringData.DocumentReference> ToDocumentReferences(
        string? createdByPrincipalId = null,
        DateTimeOffset? createdAt = null)
    {
        if (DocumentId is not { } sourceId)
            throw new InvalidOperationException(
                $"Technical document '{Reference}' holds no engineering document, so its relationships have no source "
                + "document to be recorded against.");

        return Relationships
            .Where(r => r.TargetDocumentId is not null)
            .Select(r => new EngineeringData.DocumentReference(
                sourceId,
                r.TargetDocumentId!.Value,
                r.RelationshipKind,
                createdByPrincipalId,
                createdAt))
            .ToList();
    }

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
