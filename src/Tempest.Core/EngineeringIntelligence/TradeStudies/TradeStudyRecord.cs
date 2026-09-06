namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>
/// A trade study as carried out: the question, the options, what was found
/// about each, and — once a person has taken it — the decision.
/// </summary>
/// <remarks>
/// <para>
/// A record with no <see cref="Decision"/> is a complete and useful
/// artefact: it is the study, done, awaiting a decision. The framework
/// never fills it in.
/// </para>
/// <para>
/// The record carries <see cref="DefinitionPin"/> and every option's own
/// pins, so the whole study can be re-run later against exactly the
/// reference-data revisions it was originally run against — and, more
/// usefully, re-run against current data to see whether anything it rested
/// on has changed.
/// </para>
/// </remarks>
/// <param name="StudyCode">The study this is a run of. Required.</param>
/// <param name="DefinitionPin">The study-definition revision this run used. Required.</param>
/// <param name="Options">What was found about each option, in the order they were offered. Never <see langword="null"/>.</param>
/// <param name="AssessedAt">When the assessment was carried out.</param>
/// <param name="AssessedByPrincipalId">Who ran the assessment. Required.</param>
/// <param name="Decision">The decision a person took, once they have taken it. <see langword="null"/> until then.</param>
/// <param name="Notes">Anything else the study wants on the record. <see langword="null"/> if nothing.</param>
public sealed record TradeStudyRecord(
    string StudyCode,
    ReferencePin DefinitionPin,
    IReadOnlyList<TradeStudyOptionResult> Options,
    DateTimeOffset AssessedAt,
    string AssessedByPrincipalId,
    TradeStudyDecision? Decision = null,
    string? Notes = null)
{
    /// <summary>The study this is a run of.</summary>
    public string StudyCode { get; } = string.IsNullOrWhiteSpace(StudyCode)
        ? throw new ArgumentException("A trade-study record must name the study it is a run of.", nameof(StudyCode))
        : StudyCode.Trim();

    /// <summary>The study-definition revision this run used.</summary>
    public ReferencePin DefinitionPin { get; } = DefinitionPin ?? throw new ArgumentNullException(nameof(DefinitionPin));

    /// <summary>What was found about each option.</summary>
    public IReadOnlyList<TradeStudyOptionResult> Options { get; init; } = Options ?? [];

    /// <summary>Who ran the assessment.</summary>
    public string AssessedByPrincipalId { get; } = string.IsNullOrWhiteSpace(AssessedByPrincipalId)
        ? throw new ArgumentException("A trade-study record must say who ran the assessment.", nameof(AssessedByPrincipalId))
        : AssessedByPrincipalId.Trim();

    /// <summary>Options that satisfied every eliminating consideration that could be settled.</summary>
    /// <remarks>
    /// <b>Not a shortlist of recommendations.</b> This is the set of
    /// options the study did not rule out. Choosing among them is the
    /// engineering decision.
    /// </remarks>
    public IReadOnlyList<TradeStudyOptionResult> AdmissibleOptions =>
        Options.Where(o => o.Standing is CandidateStanding.ConstraintsSatisfied).ToList();

    /// <summary>Options with an eliminating consideration still unsettled, which are therefore neither in nor out.</summary>
    public IReadOnlyList<TradeStudyOptionResult> UnresolvedOptions =>
        Options.Where(o => o.Standing is CandidateStanding.Unresolved).ToList();

    /// <summary>Options ruled out by an eliminating consideration.</summary>
    public IReadOnlyList<TradeStudyOptionResult> EliminatedOptions =>
        Options.Where(o => o.Standing is CandidateStanding.Eliminated).ToList();

    /// <summary>Everything still unanswered, across every option.</summary>
    public IReadOnlyList<TradeStudyJudgement> OpenGaps =>
        Options.SelectMany(o => o.OpenGaps).ToList();

    /// <summary>Whether a person still has to take the decision this study exists to inform.</summary>
    /// <remarks>
    /// True until a decision is recorded — always, and regardless of how
    /// few options survived. A study that eliminated all but one option
    /// has narrowed the field; it has not decided, and an engineer may
    /// still conclude that none of the options is acceptable.
    /// </remarks>
    public bool RequiresHumanDecision => Decision is null;

    /// <summary>Whether the study has been decided.</summary>
    public bool IsDecided => Decision is not null;

    /// <summary>
    /// Whether the recorded decision selected an option the study had
    /// ruled out or left unresolved without recording an override for it.
    /// </summary>
    /// <remarks>
    /// Not an error, and not blocked: an engineer may overrule the study,
    /// and that is their prerogative. It is surfaced so a reviewer sees it
    /// immediately rather than having to reconcile the decision against
    /// the judgements by hand.
    /// </remarks>
    public bool DecisionDepartsFromAssessment
    {
        get
        {
            if (Decision is not { } decision)
                return false;

            var selected = FindOption(decision.SelectedOptionCode);

            if (selected is null)
                return true;

            if (selected.Standing is CandidateStanding.ConstraintsSatisfied)
                return false;

            return selected.EliminatingJudgements
                .Any(j => decision.FindOverride(j.ConsiderationCode) is null);
        }
    }

    /// <summary>Every reference-data revision this study rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Options
            .SelectMany(o => o.AllPins)
            .Append(DefinitionPin)
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

    /// <summary>Returns the result for the option registered under <paramref name="optionCode"/>, or <see langword="null"/> if there is none.</summary>
    public TradeStudyOptionResult? FindOption(string optionCode) =>
        Options.FirstOrDefault(o => string.Equals(o.Option.Code, optionCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>How many judgements reached each outcome, across every option.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<AssessmentOutcome, int> OutcomeCounts =>
        AssessmentOutcomes.All
            .Select(outcome => (outcome, count: Options.SelectMany(o => o.Judgements).Count(j => j.Outcome == outcome)))
            .Where(pair => pair.count > 0)
            .ToDictionary(pair => pair.outcome, pair => pair.count);
}
