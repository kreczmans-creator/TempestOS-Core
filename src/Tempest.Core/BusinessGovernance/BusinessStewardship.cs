namespace Tempest.Core.BusinessGovernance;

/// <summary>
/// Who is answerable for a business record.
/// </summary>
/// <remarks>
/// Ownership is a person, never a team, a system or a role alone. A risk
/// owned by "Operations" is a risk nobody has to answer for at review; a
/// risk owned by a named person in a stated capacity is one somebody does.
/// </remarks>
/// <param name="OwnerPrincipalId">The person answerable for the record. Required.</param>
/// <param name="OwnerRoleOrTitle">The capacity they hold it in. Required.</param>
/// <param name="DeputyPrincipalId">Who covers in their absence. <see langword="null"/> where nobody does.</param>
public sealed record BusinessOwnership(string OwnerPrincipalId, string OwnerRoleOrTitle, string? DeputyPrincipalId = null)
{
    /// <summary>The person answerable for the record.</summary>
    public string OwnerPrincipalId { get; } = string.IsNullOrWhiteSpace(OwnerPrincipalId)
        ? throw new ArgumentException(
            "A governed business record must name the person answerable for it. A record owned by a department is owned by nobody.",
            nameof(OwnerPrincipalId))
        : OwnerPrincipalId.Trim();

    /// <summary>The capacity they hold it in.</summary>
    public string OwnerRoleOrTitle { get; } = string.IsNullOrWhiteSpace(OwnerRoleOrTitle)
        ? throw new ArgumentException("A record's owner must be named in a stated capacity.", nameof(OwnerRoleOrTitle))
        : OwnerRoleOrTitle.Trim();
}

/// <summary>
/// When a business record must next be looked at by a person.
/// </summary>
/// <remarks>
/// <para>
/// The single most common failure in business governance is not a wrong
/// record; it is a right record nobody has looked at since it stopped
/// being right. An insurance policy that lapsed, a rate card that predates
/// two rounds of cost inflation, a risk whose mitigation was never
/// implemented. Every one of those is visible from a review date and
/// invisible without one.
/// </para>
/// <para>
/// A schedule with no next review is legitimate — some records genuinely
/// do not need one — but it is distinguishable from a schedule nobody
/// set, and reports can say which is which.
/// </para>
/// </remarks>
/// <param name="NextReviewDue">When the record must next be reviewed. <see langword="null"/> where no review is scheduled.</param>
/// <param name="LastReviewedOn">When it was last reviewed. <see langword="null"/> where it never has been.</param>
/// <param name="LastReviewedByPrincipalId">Who last reviewed it. <see langword="null"/> where nobody has.</param>
/// <param name="IntervalMonths">How often it should be reviewed. <see langword="null"/> where there is no fixed cycle.</param>
/// <param name="Rationale">Why this cycle, or why none. <see langword="null"/> if not stated.</param>
public sealed record ReviewSchedule(
    DateOnly? NextReviewDue = null,
    DateOnly? LastReviewedOn = null,
    string? LastReviewedByPrincipalId = null,
    int? IntervalMonths = null,
    string? Rationale = null)
{
    /// <summary>How often the record should be reviewed.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="IntervalMonths"/> is not positive.</exception>
    public int? IntervalMonths { get; } = IntervalMonths is { } months && months <= 0
        ? throw new ArgumentOutOfRangeException(nameof(IntervalMonths), months, "A review interval must be a positive number of months.")
        : IntervalMonths;

    /// <summary>A schedule nobody has set. Distinct from one that deliberately has no review.</summary>
    public static ReviewSchedule NotScheduled { get; } = new();

    /// <summary>Whether anybody has ever reviewed the record.</summary>
    public bool HasBeenReviewed => LastReviewedOn is not null;

    /// <summary>Whether a next review has been scheduled at all.</summary>
    public bool IsScheduled => NextReviewDue is not null;

    /// <summary>Whether the review is past due as at <paramref name="asAt"/>.</summary>
    public bool IsOverdueAt(DateOnly asAt) => NextReviewDue is { } due && due < asAt;

    /// <summary>Whether the review falls within <paramref name="withinDays"/> of <paramref name="asAt"/>, or is already overdue.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="withinDays"/> is negative.</exception>
    public bool IsDueWithin(DateOnly asAt, int withinDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(withinDays);

        return NextReviewDue is { } due && due <= asAt.AddDays(withinDays);
    }

    /// <summary>The schedule after a review carried out on <paramref name="reviewedOn"/> by <paramref name="principalId"/>.</summary>
    /// <remarks>
    /// The next date is computed from the interval where one is set, and
    /// left unset where none is — this method never invents a cycle the
    /// record did not have.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="principalId"/> is null, empty, or whitespace.</exception>
    public ReviewSchedule Reviewed(DateOnly reviewedOn, string principalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);

        return this with
        {
            LastReviewedOn = reviewedOn,
            LastReviewedByPrincipalId = principalId.Trim(),
            NextReviewDue = IntervalMonths is { } months ? reviewedOn.AddMonths(months) : null,
        };
    }
}

/// <summary>
/// The governance every `P07` record carries, whatever its domain.
/// </summary>
/// <remarks>
/// <para>
/// Composed into each domain definition rather than inherited from, and
/// deliberately so. A contract, an insurance policy, a rate card and a
/// forecast all need an owner, a classification, a review cycle, evidence
/// and a record of who authorised what — and share almost nothing else.
/// Putting the shared part in a property gives every package the same
/// governance without forcing five unrelated domains through one base
/// class with a nullable field for each of their differences.
/// </para>
/// <para>
/// Provenance, lifecycle state, revision number and supersession are not
/// here: those come from the shared reference-data record the definition
/// is registered as, exactly as they do in `Group A` and `Group B`.
/// </para>
/// </remarks>
public sealed record BusinessGovernanceFacts
{
    /// <summary>Who is answerable for the record. Required.</summary>
    public required BusinessOwnership Ownership { get; init; }

    /// <summary>How sensitive the record is.</summary>
    public ConfidentialityClassification Classification { get; init; } = ConfidentialityClassification.Unclassified;

    /// <summary>When it must next be looked at.</summary>
    public ReviewSchedule Review { get; init; } = ReviewSchedule.NotScheduled;

    /// <summary>Acts of authority already exercised over it. Never <see langword="null"/>.</summary>
    public IReadOnlyList<BusinessAuthorisation> Authorisations { get; init; } = [];

    /// <summary>Authority the record still needs. Never <see langword="null"/>.</summary>
    public IReadOnlyList<AuthorityRequirement> OutstandingAuthorities { get; init; } = [];

    /// <summary>What supports the record. Never <see langword="null"/>.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = [];

    /// <summary>Anything else that belongs on the record. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether an authority of <paramref name="kind"/> has been exercised.</summary>
    public bool HasAuthority(BusinessAuthorityKind kind) => Authorisations.Any(a => a.Kind == kind);

    /// <summary>Returns the authorisation of <paramref name="kind"/>, or <see langword="null"/> if none was given.</summary>
    public BusinessAuthorisation? FindAuthority(BusinessAuthorityKind kind) =>
        Authorisations.FirstOrDefault(a => a.Kind == kind);

    /// <summary>Whether anything is still waiting on somebody's authority.</summary>
    public bool HasOutstandingAuthorities => OutstandingAuthorities.Count > 0;

    /// <summary>Whether any evidence at all supports the record.</summary>
    public bool HasEvidence => Evidence.Count > 0;

    /// <summary>Whether every recorded piece of evidence can actually be retrieved and checked.</summary>
    public bool AllEvidenceIsLocatable => Evidence.All(e => e.IsLocatable);
}
