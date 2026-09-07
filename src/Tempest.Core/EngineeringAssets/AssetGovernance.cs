using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.EngineeringAssets;

/// <summary>Who is accountable for an engineering asset.</summary>
/// <remarks>
/// Distinct from authorship on the same reasoning `WP 9.1A` applied to
/// requirements: ownership may change, authorship never does.
/// </remarks>
/// <param name="OwnerPrincipalId">The person currently accountable. Required.</param>
/// <param name="RoleOrTitle">The capacity they hold it in. <see langword="null"/> where unrecorded.</param>
public sealed record AssetOwnership(string OwnerPrincipalId, string? RoleOrTitle = null)
{
    /// <summary>The person currently accountable.</summary>
    public string OwnerPrincipalId { get; } = string.IsNullOrWhiteSpace(OwnerPrincipalId)
        ? throw new ArgumentException("An engineering asset's ownership must name the person accountable for it.", nameof(OwnerPrincipalId))
        : OwnerPrincipalId.Trim();
}

/// <summary>Who made an engineering asset, and when.</summary>
/// <param name="AuthoredByPrincipalId">The person who produced it. Required.</param>
/// <param name="AuthoredOn">When. <see langword="null"/> where unrecorded.</param>
public sealed record AssetAuthorship(string AuthoredByPrincipalId, DateOnly? AuthoredOn = null)
{
    /// <summary>The person who produced it.</summary>
    public string AuthoredByPrincipalId { get; } = string.IsNullOrWhiteSpace(AuthoredByPrincipalId)
        ? throw new ArgumentException("Authorship must name the person who produced the asset.", nameof(AuthoredByPrincipalId))
        : AuthoredByPrincipalId.Trim();
}

/// <summary>What a reviewer concluded about an engineering asset.</summary>
/// <remarks>
/// Deliberately not a Boolean. "Reviewed with comments" and "reviewed and
/// accepted" are different outcomes, and "reviewed and rejected" is not
/// the absence of a review.
/// </remarks>
public enum AssetReviewOutcome
{
    /// <summary>Nobody has reviewed it.</summary>
    NotReviewed,

    /// <summary>A review is under way.</summary>
    InProgress,

    /// <summary>Reviewed, with findings the author must address.</summary>
    ReviewedWithFindings,

    /// <summary>Reviewed and found sound.</summary>
    Accepted,

    /// <summary>Reviewed and rejected.</summary>
    Rejected
}

/// <summary>A review somebody performed on an engineering asset.</summary>
/// <remarks>
/// A review is not an approval. `ADR-0136` keeps the two apart: a
/// reviewer says whether the work is sound, and an approver commits the
/// organisation to it. Approval, where it applies, is a
/// <see cref="BusinessAuthorisation"/> a named person constructs.
/// </remarks>
/// <param name="ReviewedByPrincipalId">Who reviewed it. Required.</param>
/// <param name="Outcome">What they concluded.</param>
/// <param name="ReviewedOn">When. <see langword="null"/> where unrecorded.</param>
/// <param name="Commentary">What they said. <see langword="null"/> where nothing was written.</param>
public sealed record AssetReview(
    string ReviewedByPrincipalId,
    AssetReviewOutcome Outcome,
    DateOnly? ReviewedOn = null,
    string? Commentary = null)
{
    /// <summary>Who reviewed it.</summary>
    public string ReviewedByPrincipalId { get; } = string.IsNullOrWhiteSpace(ReviewedByPrincipalId)
        ? throw new ArgumentException(
            "A review must name the reviewer. An unattributable review is not a review.",
            nameof(ReviewedByPrincipalId))
        : ReviewedByPrincipalId.Trim();

    /// <summary>Whether the reviewer found the work sound.</summary>
    public bool IsAccepted => Outcome == AssetReviewOutcome.Accepted;

    /// <summary>Whether anything is outstanding for the author.</summary>
    public bool HasOutstandingFindings => Outcome == AssetReviewOutcome.ReviewedWithFindings;
}

/// <summary>
/// The governance facts every engineering asset carries, whatever kind of
/// asset it is.
/// </summary>
/// <remarks>
/// Composed into each asset type rather than inherited, on the same
/// reasoning `P07` used for <c>BusinessGovernanceFacts</c>: the five asset
/// kinds share these facts and share nothing else, and a base class would
/// force a hierarchy the domain does not have.
/// </remarks>
public sealed record AssetGovernanceFacts
{
    /// <summary>Who is accountable. <see langword="null"/> where nobody is named.</summary>
    public AssetOwnership? Ownership { get; init; }

    /// <summary>Who produced it. <see langword="null"/> where unrecorded.</summary>
    public AssetAuthorship? Authorship { get; init; }

    /// <summary>Who has reviewed it, most recent last. Never <see langword="null"/>.</summary>
    public IReadOnlyList<AssetReview> Reviews { get; init; } = [];

    /// <summary>
    /// The approval, where the organisation has formally approved the
    /// asset. <see langword="null"/> until a named person does.
    /// </summary>
    /// <remarks>
    /// Reuses `P07`'s <see cref="BusinessAuthorisation"/>, which refuses
    /// construction without a person, a capacity, a date and a basis.
    /// Nothing in `P05` constructs one.
    /// </remarks>
    public BusinessAuthorisation? Approval { get; init; }

    /// <summary>How sensitive the asset is.</summary>
    public ConfidentialityClassification Classification { get; init; } = ConfidentialityClassification.Unclassified;

    /// <summary>Supporting material. Never <see langword="null"/>.</summary>
    public IReadOnlyList<EngineeringEvidence> Evidence { get; init; } = [];

    /// <summary>The most recent review, or <see langword="null"/> where nobody has reviewed it.</summary>
    public AssetReview? LatestReview => Reviews.Count > 0 ? Reviews[^1] : null;

    /// <summary>Whether a reviewer has found the asset sound.</summary>
    public bool IsReviewed => LatestReview?.IsAccepted ?? false;

    /// <summary>Whether the organisation has formally approved it.</summary>
    /// <remarks>
    /// Approval is an act of authority a person performs. TempestOS
    /// records it and never confers it.
    /// </remarks>
    public bool IsApproved => Approval is not null;

    /// <summary>Whether a review left findings nobody has closed.</summary>
    public bool HasOutstandingFindings => LatestReview?.HasOutstandingFindings ?? false;
}
