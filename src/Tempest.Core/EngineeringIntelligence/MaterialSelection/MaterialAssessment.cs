using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.MaterialSelection;

/// <summary>What one criterion concluded about one candidate.</summary>
/// <param name="Criterion">The criterion, as the engineer wrote it.</param>
/// <param name="Role">Whether failing it eliminates the candidate.</param>
/// <param name="Outcome">What it concluded.</param>
/// <param name="Reason">Why, in plain engineering language.</param>
public sealed record CriterionAssessment(
    string Criterion,
    MaterialCriterionRole Role,
    AssessmentOutcome Outcome,
    string Reason)
{
    /// <summary>Why, in plain engineering language.</summary>
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("A criterion assessment must say why it concluded what it did.", nameof(Reason))
        : Reason.Trim();

    /// <summary>Whether this assessment eliminates the candidate — a constraint that was not satisfied.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEliminating => Role == MaterialCriterionRole.Constraint && AssessmentOutcomes.IsAdverse(Outcome);
}

/// <summary>
/// What one material candidate was found to be, against one set of
/// requirements.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no "recommended" flag, and that is deliberate.</b>
/// <see cref="Standing"/> reports whether the candidate was eliminated,
/// whether every constraint was satisfied, or whether the assessment could
/// not conclude — and "every constraint satisfied" is a statement about
/// the criteria that were checked, not a recommendation to use the
/// material. Choosing a material is an engineering decision that weighs
/// things no criterion set contains.
/// </para>
/// <para>
/// The pin is the whole traceability story: this assessment was made
/// against <em>that</em> material at <em>that</em> revision, and a later
/// revision of the same material produces a different assessment rather
/// than silently changing this one.
/// </para>
/// </remarks>
/// <param name="MaterialId">The candidate assessed.</param>
/// <param name="DisplayName">What the candidate is called.</param>
/// <param name="Pin">The exact material record and revision assessed.</param>
/// <param name="ValidationState">The candidate's own `A1` validation state at that revision.</param>
/// <param name="CriterionAssessments">What each criterion concluded. Never <see langword="null"/>.</param>
/// <param name="RuleEvaluations">What each applicable released rule concluded. Never <see langword="null"/>.</param>
/// <param name="FamilyAssessment">What the family restriction concluded. <see langword="null"/> where none was stated.</param>
public sealed record MaterialCandidateAssessment(
    string MaterialId,
    string DisplayName,
    ReferencePin Pin,
    ReferenceValidationState ValidationState,
    IReadOnlyList<CriterionAssessment> CriterionAssessments,
    IReadOnlyList<RuleEvaluation> RuleEvaluations,
    CriterionAssessment? FamilyAssessment = null)
{
    /// <summary>What each criterion concluded.</summary>
    public IReadOnlyList<CriterionAssessment> CriterionAssessments { get; } =
        CriterionAssessments ?? throw new ArgumentNullException(nameof(CriterionAssessments));

    /// <summary>What each applicable released rule concluded.</summary>
    public IReadOnlyList<RuleEvaluation> RuleEvaluations { get; } =
        RuleEvaluations ?? throw new ArgumentNullException(nameof(RuleEvaluations));

    /// <summary>Every criterion assessment, including the family restriction where one was stated.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<CriterionAssessment> AllCriterionAssessments =>
        FamilyAssessment is null ? CriterionAssessments : [FamilyAssessment, .. CriterionAssessments];

    /// <summary>Where this candidate stands.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public CandidateStanding Standing
    {
        get
        {
            if (AllCriterionAssessments.Any(a => a.IsEliminating) || RuleEvaluations.Any(e => e.IsDefect))
                return CandidateStanding.Eliminated;

            var constraintOutcomes = AllCriterionAssessments
                .Where(a => a.Role == MaterialCriterionRole.Constraint)
                .Select(a => a.Outcome)
                .Concat(RuleEvaluations
                    .Where(e => RuleSeverities.IsBinding(e.Severity))
                    .Select(e => e.Outcome))
                .ToList();

            // Nothing was checked at all: not a pass, and not a failure.
            if (constraintOutcomes.Count == 0)
                return CandidateStanding.NotAssessed;

            // A constraint nobody could evaluate leaves the candidate
            // unresolved. It has not been eliminated, and it has not
            // satisfied anything either.
            return constraintOutcomes.All(o => o is AssessmentOutcome.Pass or AssessmentOutcome.NotApplicable)
                ? CandidateStanding.ConstraintsSatisfied
                : CandidateStanding.Unresolved;
        }
    }

    /// <summary>The overall outcome, aggregated over every criterion and rule — the worst thing anything said.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public AssessmentOutcome Outcome => AssessmentOutcomes.Aggregate(
        AllCriterionAssessments.Select(a => a.Outcome).Concat(RuleEvaluations.Select(e => e.Outcome)));

    /// <summary>Every criterion and rule that could not reach a conclusion — the gaps a reviewer must close.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> OpenGaps =>
        AllCriterionAssessments.Where(a => AssessmentOutcomes.IsGap(a.Outcome)).Select(a => a.Reason)
            .Concat(RuleEvaluations.Where(e => AssessmentOutcomes.IsGap(e.Outcome)).Select(e => e.Reason))
            .ToList();

    /// <summary>Every preference this candidate does not meet — what an engineer trades away by choosing it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<CriterionAssessment> UnmetPreferences =>
        AllCriterionAssessments
            .Where(a => a.Role == MaterialCriterionRole.Preference && !AssessmentOutcomes.IsAffirmative(a.Outcome))
            .ToList();

    /// <summary>Every reference-data revision this assessment depends on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReferencePin> AllPins =>
        new[] { Pin }
            .Concat(RuleEvaluations.SelectMany(e => e.AllPins))
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ToList();
}


/// <summary>
/// The result of assessing a set of material candidates against one
/// application's requirements.
/// </summary>
/// <param name="ApplicationDescription">What the material is for, carried through from the requirements.</param>
/// <param name="Candidates">Every candidate assessed, in ascending material-Id order. Never <see langword="null"/>.</param>
/// <param name="AssessedAt">When the assessment ran.</param>
/// <param name="AssessedByPrincipalId">Who ran it.</param>
public sealed record MaterialSelectionResult(
    string ApplicationDescription,
    IReadOnlyList<MaterialCandidateAssessment> Candidates,
    DateTimeOffset AssessedAt,
    string AssessedByPrincipalId)
{
    /// <summary>Every candidate assessed.</summary>
    public IReadOnlyList<MaterialCandidateAssessment> Candidates { get; } =
        Candidates ?? throw new ArgumentNullException(nameof(Candidates));

    /// <summary>
    /// The candidates that satisfied every constraint that could be
    /// evaluated. <b>Not a recommendation, and not ranked</b> — ordering
    /// them would imply a preference the criteria do not establish.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MaterialCandidateAssessment> SatisfyingCandidates =>
        Candidates.Where(c => c.Standing == CandidateStanding.ConstraintsSatisfied).ToList();

    /// <summary>The candidates neither eliminated nor cleared, because something could not be concluded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MaterialCandidateAssessment> UnresolvedCandidates =>
        Candidates.Where(c => c.Standing == CandidateStanding.Unresolved).ToList();

    /// <summary>The candidates a constraint or a binding rule ruled out.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MaterialCandidateAssessment> EliminatedCandidates =>
        Candidates.Where(c => c.Standing == CandidateStanding.Eliminated).ToList();

    /// <summary>
    /// Whether an engineer must decide before this result is acted on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always true.</b> Choosing a material is an engineering decision,
    /// and `P02` does not make it. Even where exactly one candidate
    /// satisfied every stated constraint, the result is a statement about
    /// the criteria that were checked and is silent about everything that
    /// was not — cost, availability, the supplier's actual stock, whether
    /// the criteria were the right ones. A result that reported "no
    /// decision needed" would be claiming otherwise.
    /// </para>
    /// <para>
    /// The narrowing information an engineer actually wants is in
    /// <see cref="SatisfyingCandidates"/>,
    /// <see cref="UnresolvedCandidates"/> and
    /// <see cref="HasOutstandingQuestions"/>, which say what the
    /// assessment established rather than what to do about it.
    /// </para>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresHumanDecision => true;

    /// <summary>
    /// Whether something the assessment tried to settle is still open —
    /// an unresolved candidate, or a rule that asked for human review.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="RequiresHumanDecision"/>: this is about
    /// the assessment being incomplete, not about who takes the decision.
    /// An assessment with nothing outstanding still needs an engineer.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasOutstandingQuestions =>
        UnresolvedCandidates.Count > 0
        || Candidates.Any(c => c.RuleEvaluations.Any(e => e.RequiresHumanReview));
}
