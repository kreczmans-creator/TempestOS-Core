using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.DesignReviews;

/// <summary>Which review in a project's life this is.</summary>
public enum DesignReviewKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Requirements review — is the problem right?</summary>
    Requirements,

    /// <summary>Concept review — is the approach right?</summary>
    Concept,

    /// <summary>Preliminary design review.</summary>
    Preliminary,

    /// <summary>Critical design review.</summary>
    Critical,

    /// <summary>Manufacturing readiness.</summary>
    ManufacturingReadiness,

    /// <summary>Test readiness.</summary>
    TestReadiness,

    /// <summary>Review before release to a customer or to production.</summary>
    Release,

    /// <summary>A review after the fact.</summary>
    PostProject,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>What part somebody played in a review.</summary>
public enum ReviewParticipantRole
{
    /// <summary>Ran the review.</summary>
    Chair,

    /// <summary>Presented the work.</summary>
    Presenter,

    /// <summary>Reviewed the work.</summary>
    Reviewer,

    /// <summary>Attended without a formal part.</summary>
    Observer,

    /// <summary>Recorded the review.</summary>
    Recorder,

    /// <summary>Attended for a particular specialism.</summary>
    SpecialistAdvisor
}

/// <summary>Somebody who took part in a review.</summary>
/// <param name="PrincipalId">Who. Required.</param>
/// <param name="Role">What part they played.</param>
/// <param name="Organisation">Who they were there for, where not the organisation itself. <see langword="null"/> otherwise.</param>
/// <param name="WasPresent">Whether they actually attended.</param>
public sealed record ReviewParticipant(
    string PrincipalId,
    ReviewParticipantRole Role = ReviewParticipantRole.Reviewer,
    string? Organisation = null,
    bool WasPresent = true)
{
    /// <summary>Who.</summary>
    public string PrincipalId { get; } = string.IsNullOrWhiteSpace(PrincipalId)
        ? throw new ArgumentException("A review participant must be named.", nameof(PrincipalId))
        : PrincipalId.Trim();
}

/// <summary>How serious an observation is.</summary>
/// <remarks>
/// Graded rather than Boolean because a review that cannot distinguish a
/// typographical error from a structural concern produces a list nobody
/// reads.
/// </remarks>
public enum ObservationSeverity
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A remark, requiring nothing.</summary>
    Comment,

    /// <summary>Worth improving.</summary>
    Minor,

    /// <summary>Should be addressed before the work proceeds.</summary>
    Major,

    /// <summary>Must be addressed; the work cannot proceed.</summary>
    Critical
}

/// <summary>
/// Something a reviewer noticed.
/// </summary>
/// <remarks>
/// <b>An observation is not an action and not a decision.</b> A reviewer
/// may notice something and propose nothing; the meeting may agree an
/// action; the organisation may later decide something different. `E4`
/// keeps the three as separate records precisely because collapsing them
/// is how "somebody mentioned it" becomes "it was agreed" (`ADR-0139`).
/// </remarks>
/// <param name="Reference">The observation's own identifier within the pack. Required.</param>
/// <param name="Statement">What was noticed. Required.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="RaisedByPrincipalId">Who noticed it. <see langword="null"/> where unrecorded.</param>
/// <param name="Area">What it concerns — a subsystem, a document, a requirement. <see langword="null"/> where general.</param>
/// <param name="Recommendation">What the reviewer suggests, where they suggested anything. <see langword="null"/> otherwise.</param>
public sealed record ReviewObservation(
    string Reference,
    string Statement,
    ObservationSeverity Severity = ObservationSeverity.Unspecified,
    string? RaisedByPrincipalId = null,
    string? Area = null,
    string? Recommendation = null)
{
    /// <summary>The observation's own identifier within the pack.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A review observation must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What was noticed.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A review observation must say what was noticed.", nameof(Statement))
        : Statement.Trim();

    /// <summary>Whether the reviewer proposed anything.</summary>
    /// <remarks>
    /// An observation without a recommendation is entirely legitimate:
    /// noticing a problem and knowing what to do about it are different
    /// competencies.
    /// </remarks>
    public bool HasRecommendation => !string.IsNullOrWhiteSpace(Recommendation);

    /// <summary>Whether the observation is one that should stop the work proceeding.</summary>
    public bool IsBlocking => Severity is ObservationSeverity.Critical;
}

/// <summary>Where an action has got to.</summary>
public enum ReviewActionState
{
    /// <summary>Agreed and not started.</summary>
    Open,

    /// <summary>Being worked on.</summary>
    InProgress,

    /// <summary>Done, awaiting confirmation by whoever raised it.</summary>
    AwaitingVerification,

    /// <summary>Done and confirmed.</summary>
    Closed,

    /// <summary>Deliberately not being done, with a reason.</summary>
    Waived
}

/// <summary>
/// Something somebody agreed to do about an observation.
/// </summary>
/// <remarks>
/// An action is a commitment by a person, which is why it names one. An
/// action with no owner is a wish.
/// </remarks>
/// <param name="Reference">The action's own identifier within the pack. Required.</param>
/// <param name="Description">What is to be done. Required.</param>
/// <param name="OwnerPrincipalId">Who agreed to do it. <see langword="null"/> where nobody did.</param>
/// <param name="DueBy">When by. <see langword="null"/> where nobody said.</param>
/// <param name="State">Where it has got to.</param>
/// <param name="ObservationReferences">The observations it answers. Never <see langword="null"/>.</param>
/// <param name="ClosureNote">What was done, or why it was waived. <see langword="null"/> where nothing was written.</param>
public sealed record ReviewAction(
    string Reference,
    string Description,
    string? OwnerPrincipalId = null,
    DateOnly? DueBy = null,
    ReviewActionState State = ReviewActionState.Open,
    IReadOnlyList<string>? ObservationReferences = null,
    string? ClosureNote = null)
{
    /// <summary>The action's own identifier within the pack.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A review action must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is to be done.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A review action must say what is to be done.", nameof(Description))
        : Description.Trim();

    /// <summary>The observations it answers.</summary>
    public IReadOnlyList<string> ObservationReferences { get; init; } = ObservationReferences ?? [];

    /// <summary>Whether the action still needs somebody's attention.</summary>
    public bool IsOutstanding => State is ReviewActionState.Open
        or ReviewActionState.InProgress
        or ReviewActionState.AwaitingVerification;

    /// <summary>Whether anybody has committed to doing it.</summary>
    public bool IsOwned => !string.IsNullOrWhiteSpace(OwnerPrincipalId);

    /// <summary>Whether it was waived without a stated reason.</summary>
    public bool IsUnexplainedWaiver => State == ReviewActionState.Waived && string.IsNullOrWhiteSpace(ClosureNote);
}

/// <summary>What the review concluded about whether the work may proceed.</summary>
/// <remarks>
/// A review outcome is the reviewers' collective engineering judgement.
/// It is not an approval: approval commits the organisation and is an act
/// of authority a named person performs (`ADR-0139`).
/// </remarks>
public enum ReviewOutcome
{
    /// <summary>The review has not concluded.</summary>
    NotConcluded,

    /// <summary>The work may proceed.</summary>
    Proceed,

    /// <summary>The work may proceed once the actions are closed.</summary>
    ProceedWithActions,

    /// <summary>The work may not proceed; the review must be repeated.</summary>
    DoNotProceed,

    /// <summary>The review could not conclude — insufficient material, insufficient attendance.</summary>
    Inconclusive
}

/// <summary>
/// A decision taken at, or arising from, a review.
/// </summary>
/// <remarks>
/// <b>A decision is not an observation and not an action.</b> Recorded
/// separately, and separately again from
/// <see cref="DesignReviewPack.Approval"/>, because a review may decide
/// something the organisation has not approved and may approve something
/// the review did not decide.
/// </remarks>
/// <param name="Reference">The decision's own identifier within the pack. Required.</param>
/// <param name="Statement">What was decided. Required.</param>
/// <param name="Rationale">Why. Required — a decision nobody can explain cannot be reviewed.</param>
/// <param name="DecidedByPrincipalId">Who decided. Required.</param>
/// <param name="DecidedOn">When. <see langword="null"/> where unrecorded.</param>
/// <param name="ObservationReferences">The observations it responds to. Never <see langword="null"/>.</param>
public sealed record ReviewDecision(
    string Reference,
    string Statement,
    string Rationale,
    string DecidedByPrincipalId,
    DateOnly? DecidedOn = null,
    IReadOnlyList<string>? ObservationReferences = null)
{
    /// <summary>The decision's own identifier within the pack.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A review decision must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What was decided.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A review decision must say what was decided.", nameof(Statement))
        : Statement.Trim();

    /// <summary>Why.</summary>
    public string Rationale { get; } = string.IsNullOrWhiteSpace(Rationale)
        ? throw new ArgumentException(
            "A review decision must record why it was taken. A decision without a stated reason cannot be reviewed later.",
            nameof(Rationale))
        : Rationale.Trim();

    /// <summary>Who decided.</summary>
    public string DecidedByPrincipalId { get; } = string.IsNullOrWhiteSpace(DecidedByPrincipalId)
        ? throw new ArgumentException(
            "A review decision must name the person who took it. TempestOS records decisions and takes none.",
            nameof(DecidedByPrincipalId))
        : DecidedByPrincipalId.Trim();

    /// <summary>The observations it responds to.</summary>
    public IReadOnlyList<string> ObservationReferences { get; init; } = ObservationReferences ?? [];
}

/// <summary>
/// Everything a design review looked at, found, agreed and decided.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pack is the artefact, not the process.</b> `E4` structures what
/// a review produced. Running the review, chasing the actions and
/// obtaining the approval are organisational activities that belong to
/// later operational integration, and nothing here drives them
/// (`ADR-0139`).
/// </para>
/// <para>
/// Six things are kept apart: what was <b>reviewed</b>, what was
/// <b>observed</b>, what was <b>recommended</b> (a field on the
/// observation, because it is the observer's suggestion), what was
/// <b>actioned</b>, what was <b>decided</b>, and what was
/// <b>approved</b>. Collapsing any pair of them loses information the
/// record exists to hold.
/// </para>
/// </remarks>
public sealed record DesignReviewPack
{
    /// <summary>The reference the pack is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What was reviewed. Required.</summary>
    public required string Subject { get; init; }

    /// <summary>Which review this is.</summary>
    public DesignReviewKind Kind { get; init; } = DesignReviewKind.Unspecified;

    /// <summary>When it was held. <see langword="null"/> where not yet, or unrecorded.</summary>
    public DateOnly? HeldOn { get; init; }

    /// <summary>Who took part. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReviewParticipant> Participants { get; init; } = [];

    /// <summary>The requirements in scope, by requirement Id. Never <see langword="null"/>.</summary>
    public IReadOnlyList<Guid> RequirementIds { get; init; } = [];

    /// <summary>The documents put before the review. Never <see langword="null"/>.</summary>
    public IReadOnlyList<Guid> DocumentIds { get; init; } = [];

    /// <summary>The `E2` calculation packs put before the review, by reference. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> CalculationPackReferences { get; init; } = [];

    /// <summary>The `E3` verification artefacts put before the review, by reference. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> VerificationArtefactReferences { get; init; } = [];

    /// <summary>Governed records the review relied on, at the revisions relied on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> SourcePins { get; init; } = [];

    /// <summary>What reviewers noticed. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReviewObservation> Observations { get; init; } = [];

    /// <summary>What was agreed to be done. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReviewAction> Actions { get; init; } = [];

    /// <summary>What was decided. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReviewDecision> Decisions { get; init; } = [];

    /// <summary>What the review concluded.</summary>
    public ReviewOutcome Outcome { get; init; } = ReviewOutcome.NotConcluded;

    /// <summary>Why it concluded that. <see langword="null"/> where nothing was written.</summary>
    public string? OutcomeRationale { get; init; }

    /// <summary>
    /// The organisation's approval, where a named person has given one.
    /// <see langword="null"/> until they do.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Outcome"/>. A review can conclude
    /// <see cref="ReviewOutcome.Proceed"/> and the organisation still not
    /// have approved anything; TempestOS records the approval and never
    /// confers it.
    /// </remarks>
    public BusinessAuthorisation? Approval { get; init; }

    /// <summary>The template the pack was produced from, at the revision worked from. <see langword="null"/> where none was used.</summary>
    public Templates.TemplateUsage? TemplateUsage { get; init; }

    /// <summary>Where and when it applies.</summary>
    public AssetApplicability Applicability { get; init; } = AssetApplicability.Unrestricted;

    /// <summary>Who owns it, who wrote it, who checked it.</summary>
    public AssetGovernanceFacts Governance { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Observations serious enough to stop the work.</summary>
    public IReadOnlyList<ReviewObservation> BlockingObservations => Observations.Where(o => o.IsBlocking).ToList();

    /// <summary>Actions still needing somebody's attention.</summary>
    public IReadOnlyList<ReviewAction> OutstandingActions => Actions.Where(a => a.IsOutstanding).ToList();

    /// <summary>Actions nobody has committed to.</summary>
    public IReadOnlyList<ReviewAction> UnownedActions => Actions.Where(a => !a.IsOwned).ToList();

    /// <summary>Whether anybody actually attended.</summary>
    public bool WasAttended => Participants.Any(p => p.WasPresent);

    /// <summary>Whether somebody other than the presenters reviewed the work.</summary>
    /// <remarks>
    /// A "review" attended only by the people who did the work is a
    /// meeting. Reported, never prevented — in a small team it happens,
    /// and what matters is that the record says so.
    /// </remarks>
    public bool HasIndependentReviewer => Participants.Any(p =>
        p.WasPresent && p.Role is ReviewParticipantRole.Reviewer or ReviewParticipantRole.SpecialistAdvisor);

    /// <summary>
    /// Whether the review concluded that the work may proceed while
    /// blocking observations stand unanswered.
    /// </summary>
    /// <remarks>
    /// Legitimate where somebody decided to accept them, which is why
    /// this reports rather than prevents — and why the validation service
    /// looks for a decision covering each one.
    /// </remarks>
    public bool ProceedsOverBlockingObservations =>
        Outcome is ReviewOutcome.Proceed
        && BlockingObservations.Any(o => !IsAnswered(o.Reference));

    /// <summary>Whether an action or a decision answers <paramref name="observationReference"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="observationReference"/> is null, empty, or whitespace.</exception>
    public bool IsAnswered(string observationReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observationReference);

        var reference = observationReference.Trim();

        return Actions.Any(a => a.ObservationReferences.Contains(reference, StringComparer.OrdinalIgnoreCase))
               || Decisions.Any(d => d.ObservationReferences.Contains(reference, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Observations nothing answers.</summary>
    public IReadOnlyList<ReviewObservation> UnansweredObservations =>
        Observations.Where(o => !IsAnswered(o.Reference)).ToList();

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
