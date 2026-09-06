namespace Tempest.Core.EngineeringIntelligence.Reviews;

/// <summary>What was decided about a finding.</summary>
/// <remarks>
/// <b>Separate from the finding itself, deliberately.</b> What a review
/// found and what the programme decided to do about it are two different
/// facts with two different authors, and merging them lets a disposition
/// quietly rewrite history: a finding "closed" is not a finding that never
/// existed.
/// </remarks>
public enum FindingDisposition
{
    /// <summary>Nobody has decided yet. The honest default.</summary>
    Open,

    /// <summary>The finding is accepted and an action has been raised to resolve it.</summary>
    ActionRaised,

    /// <summary>The finding was addressed and the reviewer confirmed it.</summary>
    Resolved,

    /// <summary>
    /// The finding stands and the programme has accepted it, with a named
    /// person accepting and a stated reason. Never a way to make a finding
    /// go quietly.
    /// </summary>
    Accepted,

    /// <summary>The finding was examined and found not to hold — the criterion did apply, and the concern did not.</summary>
    Rejected,

    /// <summary>The finding was raised against something outside this review's scope, and belongs elsewhere.</summary>
    OutOfScope
}

/// <summary>
/// What one review criterion found about one subject.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never an unexplained boolean.</b> A finding carries the criterion it
/// answers, what it concluded, why, what evidence supports it, how binding
/// the criterion was, who recorded it and what was decided. An engineer
/// reading it six months later can tell what was checked, why it was
/// checked, what evidence was used and what happened as a result — which
/// is the whole obligation a review record carries.
/// </para>
/// <para>
/// A finding produced by a rule carries the <see cref="RuleEvaluation"/>
/// that produced it, so the rule's own revision, the reference-data
/// revisions it read, and its full condition breakdown are all reachable
/// from the finding.
/// </para>
/// </remarks>
/// <param name="CriterionCode">The criterion this answers.</param>
/// <param name="Question">What was checked, carried forward so the finding reads on its own.</param>
/// <param name="Area">What area of the design it is about.</param>
/// <param name="Severity">How binding the criterion was.</param>
/// <param name="Outcome">What was found.</param>
/// <param name="Reason">Why, in plain engineering language. Required.</param>
/// <param name="Evidence">What supports the finding. Never <see langword="null"/>.</param>
/// <param name="Evaluation">The rule evaluation that produced this finding, where a rule did. <see langword="null"/> for a finding a person recorded.</param>
/// <param name="RecordedByPrincipalId">Who recorded the finding. <see langword="null"/> where a rule produced it unattended.</param>
/// <param name="Disposition">What was decided about it.</param>
/// <param name="DispositionNote">Why that was decided, and by whom where the disposition needs a name. <see langword="null"/> while open.</param>
/// <param name="ActionReference">The action raised to resolve it, in whatever terms the programme tracks actions. <see langword="null"/> if none.</param>
public sealed record ReviewFinding(
    string CriterionCode,
    string Question,
    ReviewArea Area,
    RuleSeverity Severity,
    AssessmentOutcome Outcome,
    string Reason,
    IReadOnlyList<EvidenceReference>? Evidence = null,
    RuleEvaluation? Evaluation = null,
    string? RecordedByPrincipalId = null,
    FindingDisposition Disposition = FindingDisposition.Open,
    string? DispositionNote = null,
    string? ActionReference = null)
{
    private readonly string _reason = RequireReason(Reason);

    /// <summary>Why, in plain engineering language.</summary>
    /// <remarks>
    /// Validated on <c>with</c> as well as on construction, so a finding
    /// revised by an engineer still has to say why.
    /// </remarks>
    public string Reason
    {
        get => _reason;
        init => _reason = RequireReason(value);
    }

    private static string RequireReason(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("A review finding must say why it concluded what it did.", nameof(reason))
            : reason.Trim();

    /// <summary>What supports the finding.</summary>
    public IReadOnlyList<EvidenceReference> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether this finding is a defect — a binding criterion that was not satisfied.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDefect => AssessmentOutcomes.IsAdverse(Outcome) && RuleSeverities.IsBinding(Severity);

    /// <summary>
    /// Whether this finding still needs work — it found something, and
    /// nobody has resolved, accepted or rejected it.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsOutstanding =>
        !AssessmentOutcomes.IsAffirmative(Outcome)
        && Outcome != AssessmentOutcome.NotApplicable
        && Disposition is FindingDisposition.Open or FindingDisposition.ActionRaised;

    /// <summary>Whether a person, rather than a rule, recorded this finding.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsManual => Evaluation is null;

    /// <summary>Every reference-data revision this finding rests on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReferencePin> AllPins =>
        (Evaluation?.AllPins ?? [])
            .Concat(Evidence.Select(e => e.Pin).OfType<ReferencePin>())
            .Distinct()
            .ToList();
}
