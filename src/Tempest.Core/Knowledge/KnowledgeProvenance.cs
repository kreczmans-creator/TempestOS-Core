using Tempest.Core.ReferenceData;

namespace Tempest.Core.Knowledge;

/// <summary>Where a piece of knowledge came from.</summary>
/// <remarks>
/// <para>
/// The most important vocabulary in `P06`. Knowledge is only as good as
/// its origin, and a library that cannot distinguish a textbook from a
/// guess will eventually present the guess as a textbook.
/// </para>
/// <para>
/// <see cref="MachineGenerated"/> exists because it must. Content an AI
/// produced is legitimate raw material and is never authoritative on its
/// own; recording it as its own origin is what stops it quietly becoming
/// so (`ADR-0139`).
/// </para>
/// </remarks>
public enum KnowledgeOrigin
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Written by a named person in the organisation.</summary>
    Authored,

    /// <summary>Taken from a published standard.</summary>
    Standard,

    /// <summary>Taken from a textbook, handbook or published reference.</summary>
    PublishedReference,

    /// <summary>Taken from a paper or technical journal.</summary>
    AcademicSource,

    /// <summary>Taken from a manufacturer's own literature.</summary>
    ManufacturerLiterature,

    /// <summary>Drawn from what the organisation actually did and observed.</summary>
    OrganisationalExperience,

    /// <summary>A named expert's opinion, with nothing published behind it.</summary>
    ExpertJudgement,

    /// <summary>Produced by a machine — a model, a generator, a script.</summary>
    MachineGenerated,

    /// <summary>Invented for testing, and never true of the world.</summary>
    FictionalFixture
}

/// <summary>What <see cref="KnowledgeOrigin"/> permits.</summary>
public static class KnowledgeOrigins
{
    /// <summary>
    /// Whether content of this origin may be presented as authoritative
    /// once somebody has reviewed it.
    /// </summary>
    /// <remarks>
    /// <see cref="KnowledgeOrigin.FictionalFixture"/> can never be, review
    /// or no review — a reviewed fiction is still a fiction, and this is
    /// the single guard that keeps test data out of the knowledge base.
    /// <see cref="KnowledgeOrigin.Unspecified"/> cannot either: content
    /// that does not say where it came from has nothing to be trusted on.
    /// </remarks>
    public static bool CanBecomeAuthoritative(KnowledgeOrigin origin) =>
        origin is not (KnowledgeOrigin.FictionalFixture or KnowledgeOrigin.Unspecified);

    /// <summary>Whether content of this origin is independent of the organisation asserting it.</summary>
    public static bool IsExternal(KnowledgeOrigin origin) =>
        origin is KnowledgeOrigin.Standard
            or KnowledgeOrigin.PublishedReference
            or KnowledgeOrigin.AcademicSource
            or KnowledgeOrigin.ManufacturerLiterature;

    /// <summary>Whether content of this origin needs a citation to mean anything.</summary>
    public static bool RequiresCitation(KnowledgeOrigin origin) => IsExternal(origin);

    /// <summary>Whether a machine produced the content.</summary>
    public static bool IsMachineGenerated(KnowledgeOrigin origin) => origin == KnowledgeOrigin.MachineGenerated;
}

/// <summary>How far a piece of knowledge has got through review.</summary>
/// <remarks>
/// Separate from <see cref="KnowledgeOrigin"/> and from the record
/// lifecycle. Origin says where it came from, this says who has checked
/// it, and the lifecycle says how far the record got through governance.
/// All three are needed: a Released record of Authored content that
/// nobody has reviewed is an accurate description of a real situation.
/// </remarks>
public enum KnowledgeReviewState
{
    /// <summary>Nobody has looked at it.</summary>
    Unreviewed,

    /// <summary>Somebody is looking.</summary>
    InReview,

    /// <summary>Reviewed, with changes the author must make.</summary>
    ChangesRequested,

    /// <summary>Reviewed by somebody competent and found sound.</summary>
    Reviewed,

    /// <summary>Reviewed and rejected.</summary>
    Rejected,

    /// <summary>Once sound and no longer maintained.</summary>
    Deprecated,

    /// <summary>Replaced by something later.</summary>
    Superseded
}

/// <summary>What <see cref="KnowledgeReviewState"/> permits.</summary>
public static class KnowledgeReviewStates
{
    /// <summary>Whether a competent person has checked the content and found it sound.</summary>
    public static bool IsReviewed(KnowledgeReviewState state) => state == KnowledgeReviewState.Reviewed;

    /// <summary>Whether the content should still be offered to a learner or a reader.</summary>
    public static bool IsCurrent(KnowledgeReviewState state) =>
        state is KnowledgeReviewState.Unreviewed
            or KnowledgeReviewState.InReview
            or KnowledgeReviewState.ChangesRequested
            or KnowledgeReviewState.Reviewed;

    /// <summary>Whether the content is out of service.</summary>
    public static bool IsRetired(KnowledgeReviewState state) =>
        state is KnowledgeReviewState.Deprecated or KnowledgeReviewState.Superseded or KnowledgeReviewState.Rejected;
}

/// <summary>
/// A citation — where a piece of knowledge actually came from.
/// </summary>
/// <remarks>
/// A citation is a claim about the world and `P06` never invents one. A
/// fictional fixture carries no citation at all rather than a plausible
/// fake, and validation reports external content that cites nothing
/// (`ADR-0139`).
/// </remarks>
/// <param name="Description">What is being cited, in plain words. Required.</param>
/// <param name="Author">Who wrote it. <see langword="null"/> where unrecorded.</param>
/// <param name="Title">The work's title. <see langword="null"/> where unrecorded.</param>
/// <param name="Identifier">An ISBN, DOI, standard number or equivalent. <see langword="null"/> where there is none.</param>
/// <param name="Edition">Which edition or revision. <see langword="null"/> where unrecorded.</param>
/// <param name="Year">When it was published. <see langword="null"/> where unrecorded.</param>
/// <param name="Locator">Where in the work — a page, a clause, a section. <see langword="null"/> where unrecorded.</param>
/// <param name="StandardRecordId">The `A2` Standards Library record, where the citation is a registered standard. <see langword="null"/> otherwise.</param>
public sealed record KnowledgeCitation(
    string Description,
    string? Author = null,
    string? Title = null,
    string? Identifier = null,
    string? Edition = null,
    int? Year = null,
    string? Locator = null,
    string? StandardRecordId = null)
{
    /// <summary>What is being cited.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A citation must say what is being cited.", nameof(Description))
        : Description.Trim();

    /// <summary>
    /// Whether the citation identifies the work precisely enough for
    /// somebody to find it.
    /// </summary>
    /// <remarks>
    /// A title and an author are not enough on their own — editions
    /// differ, and an engineering value from the wrong edition is a wrong
    /// value. An identifier, or an edition with a year, makes it findable.
    /// </remarks>
    public bool IsSpecific =>
        !string.IsNullOrWhiteSpace(Identifier)
        || StandardRecordId is not null
        || (!string.IsNullOrWhiteSpace(Title) && (!string.IsNullOrWhiteSpace(Edition) || Year is not null));

    /// <summary>Whether the citation points at a registered `A2` standard.</summary>
    public bool IsRegisteredStandard => !string.IsNullOrWhiteSpace(StandardRecordId);
}

/// <summary>
/// The provenance facts every piece of `P06` knowledge carries.
/// </summary>
/// <remarks>
/// <para>
/// Composed into each knowledge type rather than inherited, on the same
/// reasoning `P05`'s <c>AssetGovernanceFacts</c> uses: the five knowledge
/// kinds share these facts and share no hierarchy.
/// </para>
/// <para>
/// <b><see cref="IsAuthoritative"/> is the property this whole type
/// exists for.</b> Three things must all hold: an origin that can become
/// authoritative, a review by a competent person, and — for external
/// content — a citation somebody can follow. Nothing sets it; it is
/// derived, so no caller can assert authority the facts do not support.
/// </para>
/// </remarks>
public sealed record KnowledgeProvenance
{
    /// <summary>Where the content came from.</summary>
    public KnowledgeOrigin Origin { get; init; } = KnowledgeOrigin.Unspecified;

    /// <summary>How far it has got through review.</summary>
    public KnowledgeReviewState ReviewState { get; init; } = KnowledgeReviewState.Unreviewed;

    /// <summary>Who wrote it. <see langword="null"/> where unrecorded.</summary>
    public string? AuthoredByPrincipalId { get; init; }

    /// <summary>When. <see langword="null"/> where unrecorded.</summary>
    public DateOnly? AuthoredOn { get; init; }

    /// <summary>Who reviewed it. <see langword="null"/> where nobody has.</summary>
    public string? ReviewedByPrincipalId { get; init; }

    /// <summary>When. <see langword="null"/> where nobody has.</summary>
    public DateOnly? ReviewedOn { get; init; }

    /// <summary>What the reviewer said. <see langword="null"/> where nothing was written.</summary>
    public string? ReviewCommentary { get; init; }

    /// <summary>Where the content actually came from. Never <see langword="null"/>.</summary>
    public IReadOnlyList<KnowledgeCitation> Citations { get; init; } = [];

    /// <summary>Governed records the content rests on, at the revisions relied on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> SourcePins { get; init; } = [];

    /// <summary>The knowledge record this one replaces, by reference. <see langword="null"/> where it replaces none.</summary>
    public string? SupersedesReference { get; init; }

    /// <summary>Why it was deprecated, where it has been. <see langword="null"/> otherwise.</summary>
    public string? RetirementReason { get; init; }

    /// <summary>Provenance for content invented to exercise the code.</summary>
    /// <remarks>
    /// The only way to build fixture provenance, and it can never become
    /// authoritative however it is reviewed afterwards.
    /// </remarks>
    public static KnowledgeProvenance Fictional { get; } = new()
    {
        Origin = KnowledgeOrigin.FictionalFixture,
        ReviewState = KnowledgeReviewState.Unreviewed,
    };

    /// <summary>Whether the content is fiction invented for testing.</summary>
    public bool IsFictional => Origin == KnowledgeOrigin.FictionalFixture;

    /// <summary>Whether a machine produced the content.</summary>
    public bool IsMachineGenerated => KnowledgeOrigins.IsMachineGenerated(Origin);

    /// <summary>Whether a competent person has checked it and found it sound.</summary>
    public bool IsReviewed => KnowledgeReviewStates.IsReviewed(ReviewState);

    /// <summary>Whether the content should still be offered to a reader.</summary>
    public bool IsCurrent => KnowledgeReviewStates.IsCurrent(ReviewState);

    /// <summary>Whether external content cites anything somebody could actually find.</summary>
    public bool IsCited =>
        !KnowledgeOrigins.RequiresCitation(Origin) || Citations.Any(c => c.IsSpecific);

    /// <summary>
    /// Whether the content may be presented as authoritative.
    /// </summary>
    /// <remarks>
    /// Derived, never set. Fiction is never authoritative; unreviewed
    /// content is never authoritative; external content citing nothing
    /// findable is never authoritative. Machine-generated content can
    /// become authoritative <em>once a person has reviewed it</em> — which
    /// is exactly the point: the review is what makes it so, not the
    /// generation (`ADR-0139`).
    /// </remarks>
    public bool IsAuthoritative =>
        KnowledgeOrigins.CanBecomeAuthoritative(Origin)
        && IsReviewed
        && IsCited;

    /// <summary>Whether the same person wrote and reviewed it.</summary>
    public bool IsSelfReviewed =>
        ReviewedByPrincipalId is not null
        && string.Equals(ReviewedByPrincipalId, AuthoredByPrincipalId, StringComparison.Ordinal);
}
