namespace Tempest.Core.EngineeringIntelligence.Decisions;

/// <summary>What one requirement concluded about one candidate process.</summary>
/// <param name="Requirement">The requirement, in plain engineering language.</param>
/// <param name="Outcome">What it concluded.</param>
/// <param name="Reason">Why.</param>
public sealed record ProcessRequirementAssessment(string Requirement, AssessmentOutcome Outcome, string Reason)
{
    /// <summary>Why.</summary>
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("A process requirement assessment must say why it concluded what it did.", nameof(Reason))
        : Reason.Trim();
}

/// <summary>What one candidate process was found to be, against one part's requirements.</summary>
/// <remarks>
/// <b>Screening, not selection.</b> A process whose published bands cover
/// every stated requirement is a candidate. Which candidate to use depends
/// on tooling already owned, supplier availability, cost and lead time —
/// none of which `A7` records and none of which this assesses.
/// </remarks>
/// <param name="ProcessId">The candidate assessed.</param>
/// <param name="DisplayName">What the candidate is called.</param>
/// <param name="Family">The `A7` process family.</param>
/// <param name="Pin">The exact process record and revision assessed.</param>
/// <param name="Assessments">What each stated requirement concluded. Never <see langword="null"/>.</param>
public sealed record ProcessCandidateAssessment(
    string ProcessId,
    string DisplayName,
    string Family,
    ReferencePin Pin,
    IReadOnlyList<ProcessRequirementAssessment> Assessments)
{
    /// <summary>What each stated requirement concluded.</summary>
    public IReadOnlyList<ProcessRequirementAssessment> Assessments { get; } =
        Assessments ?? throw new ArgumentNullException(nameof(Assessments));

    /// <summary>The overall outcome — the worst thing any requirement said.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public AssessmentOutcome Outcome => AssessmentOutcomes.Aggregate(Assessments.Select(a => a.Outcome));

    /// <summary>Where this candidate stands.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public CandidateStanding Standing
    {
        get
        {
            if (Assessments.Any(a => AssessmentOutcomes.IsAdverse(a.Outcome)))
                return CandidateStanding.Eliminated;

            if (Assessments.Count == 0)
                return CandidateStanding.NotAssessed;

            return Assessments.All(a => a.Outcome is AssessmentOutcome.Pass or AssessmentOutcome.NotApplicable)
                ? CandidateStanding.ConstraintsSatisfied
                : CandidateStanding.Unresolved;
        }
    }

    /// <summary>Every requirement that could not be concluded — the gaps in `A7`'s own record of this process.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ProcessRequirementAssessment> Gaps =>
        Assessments.Where(a => AssessmentOutcomes.IsGap(a.Outcome)).ToList();
}

/// <summary>
/// The result of screening candidate manufacturing processes against one
/// part's requirements.
/// </summary>
/// <param name="PartDescription">What is being made, carried through from the requirements.</param>
/// <param name="StatedRequirements">Which requirements the engineer actually stated — what the screening could test against.</param>
/// <param name="Candidates">Every candidate assessed, in ascending process-Id order. Never <see langword="null"/>.</param>
/// <param name="Walk">The decision-tree walk that produced or narrowed the candidate set. <see langword="null"/> where screening ran without a tree.</param>
/// <param name="ScreenedAt">When the screening ran.</param>
/// <param name="ScreenedByPrincipalId">Who ran it.</param>
public sealed record ProcessScreeningResult(
    string PartDescription,
    IReadOnlyList<string> StatedRequirements,
    IReadOnlyList<ProcessCandidateAssessment> Candidates,
    DecisionWalk? Walk,
    DateTimeOffset ScreenedAt,
    string ScreenedByPrincipalId)
{
    /// <summary>Every candidate assessed.</summary>
    public IReadOnlyList<ProcessCandidateAssessment> Candidates { get; } =
        Candidates ?? throw new ArgumentNullException(nameof(Candidates));

    /// <summary>The candidates whose published bands cover every stated requirement. Not ranked, and not a recommendation.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ProcessCandidateAssessment> ViableCandidates =>
        Candidates.Where(c => c.Standing == CandidateStanding.ConstraintsSatisfied).ToList();

    /// <summary>The candidates neither ruled out nor cleared, because `A7` does not record enough about them.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ProcessCandidateAssessment> UnresolvedCandidates =>
        Candidates.Where(c => c.Standing == CandidateStanding.Unresolved).ToList();

    /// <summary>The candidates a stated requirement ruled out.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ProcessCandidateAssessment> EliminatedCandidates =>
        Candidates.Where(c => c.Standing == CandidateStanding.Eliminated).ToList();

    /// <summary>
    /// Whether an engineer must decide before this result is acted on.
    /// Always true: choosing a manufacturing process weighs tooling,
    /// supply, cost and lead time that no reference library holds.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresHumanDecision => true;
}
