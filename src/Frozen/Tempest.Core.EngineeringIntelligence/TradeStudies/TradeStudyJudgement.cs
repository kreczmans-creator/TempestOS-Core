using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>Who or what settled a judgement.</summary>
public enum JudgementSource
{
    /// <summary>
    /// Settled by evaluating the consideration's condition against
    /// recorded reference data. Reproducible, and pinned to the revisions
    /// it read.
    /// </summary>
    Assessed,

    /// <summary>
    /// Settled by an engineer. Carries a person's name and their reason,
    /// and is not reproducible from data alone — which is exactly why it
    /// is marked as such rather than presented alongside assessed
    /// judgements as though it were the same kind of thing.
    /// </summary>
    Judged,

    /// <summary>
    /// Not settled. The consideration applies to this option and nothing
    /// has answered it. It is reported, never defaulted.
    /// </summary>
    Outstanding
}

/// <summary>
/// What one option was found to be, against one consideration.
/// </summary>
/// <remarks>
/// <para>
/// A judgement is not a score. It is an <see cref="AssessmentOutcome"/>
/// with a reason, and — where a person supplied it — their name. Options
/// are not ranked by summing judgements, because
/// <see cref="AssessmentOutcome"/> has no arithmetic: there is no number
/// of passes that outweighs a failed constraint, and no sensible average
/// of <see cref="AssessmentOutcome.EvidenceRequired"/> and
/// <see cref="AssessmentOutcome.Pass"/>.
/// </para>
/// <para>
/// <see cref="Comparison"/> is where a genuine relative statement lives —
/// "stiffer than option B, at three times the cost" — in the engineer's
/// words, unquantified by the framework. That is the part of a trade study
/// a weighted matrix destroys.
/// </para>
/// </remarks>
/// <param name="OptionCode">The option this judgement is about. Required.</param>
/// <param name="ConsiderationCode">The consideration this judgement is against. Required.</param>
/// <param name="Kind">What kind of consideration it was, carried here so a result reads without the definition to hand.</param>
/// <param name="Outcome">What was concluded.</param>
/// <param name="Source">Who or what settled it.</param>
/// <param name="Reason">Why. Required.</param>
/// <param name="Comparison">How this option stands against the others on this consideration, in the engineer's own words. <see langword="null"/> if not stated.</param>
/// <param name="Evaluation">The rule evaluation behind an assessed judgement. <see langword="null"/> for a judged or outstanding one.</param>
/// <param name="Evidence">What supports the judgement. Never <see langword="null"/>.</param>
/// <param name="JudgedByPrincipalId">Who judged it, where <see cref="Source"/> is <see cref="JudgementSource.Judged"/>.</param>
public sealed record TradeStudyJudgement(
    string OptionCode,
    string ConsiderationCode,
    ConsiderationKind Kind,
    AssessmentOutcome Outcome,
    JudgementSource Source,
    string Reason,
    string? Comparison = null,
    RuleEvaluation? Evaluation = null,
    IReadOnlyList<EvidenceReference>? Evidence = null,
    string? JudgedByPrincipalId = null)
{
    private readonly string _reason = RequireReason(Reason);

    /// <summary>The option this judgement is about.</summary>
    public string OptionCode { get; } = string.IsNullOrWhiteSpace(OptionCode)
        ? throw new ArgumentException("A judgement must say which option it is about.", nameof(OptionCode))
        : OptionCode.Trim();

    /// <summary>The consideration this judgement is against.</summary>
    public string ConsiderationCode { get; } = string.IsNullOrWhiteSpace(ConsiderationCode)
        ? throw new ArgumentException("A judgement must say which consideration it is against.", nameof(ConsiderationCode))
        : ConsiderationCode.Trim();

    /// <summary>Why the judgement concluded what it did.</summary>
    /// <remarks>Validated on <c>with</c> as well as on construction: a revised judgement still has to say why.</remarks>
    public string Reason
    {
        get => _reason;
        init => _reason = RequireReason(value);
    }

    /// <summary>What supports the judgement.</summary>
    public IReadOnlyList<EvidenceReference> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether this judgement takes the option out of the study.</summary>
    /// <remarks>
    /// Only an eliminating consideration can do that, and only on an
    /// adverse outcome. A criterion the option fails badly is a reason to
    /// prefer another option, not a reason to strike this one.
    /// </remarks>
    public bool IsEliminating =>
        Kind is ConsiderationKind.Requirement or ConsiderationKind.Constraint
        && AssessmentOutcomes.IsAdverse(Outcome);

    /// <summary>Whether the judgement leaves something unanswered.</summary>
    public bool IsGap => AssessmentOutcomes.IsGap(Outcome);

    /// <summary>Every reference-data revision this judgement rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        (Evaluation?.AllPins ?? [])
            .Concat(Evidence.Select(e => e.Pin).OfType<ReferencePin>())
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

    private static string RequireReason(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("A trade-study judgement must say why it concluded what it did.", nameof(reason))
            : reason.Trim();
}

/// <summary>
/// Everything the study found about one option.
/// </summary>
/// <remarks>
/// There is no overall figure of merit here, and that is deliberate. What
/// the framework can say about an option is: whether it remains
/// admissible, what is still unanswered about it, and how it fared on each
/// consideration. Which admissible option to build is not a property of
/// the option — it is a decision, and it belongs to a person.
/// </remarks>
/// <param name="Option">The option. Required.</param>
/// <param name="Judgements">What was found against each consideration. Never <see langword="null"/>.</param>
/// <param name="SubjectPin">The reference-data revision the option's record was read at. <see langword="null"/> where the option is not catalogued.</param>
public sealed record TradeStudyOptionResult(
    TradeStudyOption Option,
    IReadOnlyList<TradeStudyJudgement> Judgements,
    ReferencePin? SubjectPin = null)
{
    /// <summary>The option.</summary>
    public TradeStudyOption Option { get; } = Option ?? throw new ArgumentNullException(nameof(Option));

    /// <summary>What was found against each consideration.</summary>
    public IReadOnlyList<TradeStudyJudgement> Judgements { get; init; } = Judgements ?? [];

    /// <summary>
    /// Whether the option remains admissible, and if not, why not.
    /// </summary>
    /// <remarks>
    /// Determined only by the eliminating considerations. An option that
    /// satisfies every requirement and constraint is
    /// <see cref="CandidateStanding.ConstraintsSatisfied"/> even if it
    /// fares poorly on every criterion — that is a judgement for the
    /// decision-maker, not grounds for the framework to discard it.
    /// </remarks>
    public CandidateStanding Standing
    {
        get
        {
            var eliminating = Judgements.Where(j => j.Kind is ConsiderationKind.Requirement or ConsiderationKind.Constraint).ToList();

            if (eliminating.Count == 0)
                return Judgements.Count == 0 ? CandidateStanding.NotAssessed : CandidateStanding.ConstraintsSatisfied;

            if (eliminating.Any(j => j.IsEliminating))
                return CandidateStanding.Eliminated;

            return eliminating.Any(j => j.IsGap) ? CandidateStanding.Unresolved : CandidateStanding.ConstraintsSatisfied;
        }
    }

    /// <summary>The considerations that eliminated this option. Empty if none did.</summary>
    public IReadOnlyList<TradeStudyJudgement> EliminatingJudgements =>
        Judgements.Where(j => j.IsEliminating).ToList();

    /// <summary>Everything still unanswered about this option. Empty if nothing is.</summary>
    public IReadOnlyList<TradeStudyJudgement> OpenGaps =>
        Judgements.Where(j => j.IsGap).ToList();

    /// <summary>How this option fared on the considerations that discriminate between admissible options.</summary>
    public IReadOnlyList<TradeStudyJudgement> CriterionJudgements =>
        Judgements.Where(j => j.Kind is ConsiderationKind.Criterion).ToList();

    /// <summary>Every reference-data revision the findings about this option rest on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Judgements
            .SelectMany(j => j.AllPins)
            .Concat(SubjectPin is null ? [] : new[] { SubjectPin })
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

    /// <summary>Returns the judgement against <paramref name="considerationCode"/>, or <see langword="null"/> if there is none.</summary>
    public TradeStudyJudgement? FindJudgement(string considerationCode) =>
        Judgements.FirstOrDefault(j => string.Equals(j.ConsiderationCode, considerationCode, StringComparison.OrdinalIgnoreCase));
}
