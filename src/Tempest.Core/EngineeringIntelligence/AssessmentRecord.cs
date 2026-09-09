using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// One deterministic assessment, together with who ran it and when.
/// </summary>
/// <remarks>
/// <para>
/// <b>The split is the point.</b> <see cref="Evaluations"/> is a pure
/// function of the rules, the subject and the resolved constants — re-run
/// those and you get an equal value, which is what makes reproducibility
/// testable. <see cref="AssessedAt"/> and
/// <see cref="AssessedByPrincipalId"/> are not, and are deliberately kept
/// out of the deterministic part rather than being allowed to make every
/// result unique. Mirrors <see cref="Calculations.CalculationRecord{T}"/>,
/// which separates the calculated result from the execution around it for
/// the same reason.
/// </para>
/// <para>
/// <b>An assessment is not an approval.</b> This type deliberately has no
/// "approved" state and no approver. `P02` produces assessments,
/// recommendations, findings and candidate sets;
/// <see cref="RequiresHumanDecision"/> says an engineer must still decide,
/// and it is true whenever any rule said so, whenever anything was
/// inconclusive, and whenever a defect was found. Its being false is not a
/// statement that no engineer need look — only that nothing in the
/// assessment itself demands one.
/// </para>
/// </remarks>
/// <param name="SubjectId">The subject assessed.</param>
/// <param name="SubjectDisplayName">What the subject is called, for reading the record later.</param>
/// <param name="SubjectPin">The subject's pinned reference-data revision, where the subject is a reference record.</param>
/// <param name="Evaluations">Every rule that ran, in the order it ran. Never <see langword="null"/>.</param>
/// <param name="AssessedAt">When the assessment ran.</param>
/// <param name="AssessedByPrincipalId">Who ran it.</param>
public sealed record AssessmentRecord(
    string SubjectId,
    string SubjectDisplayName,
    ReferencePin? SubjectPin,
    IReadOnlyList<RuleEvaluation> Evaluations,
    DateTimeOffset AssessedAt,
    string AssessedByPrincipalId)
{
    /// <summary>Every rule that ran, in the order it ran.</summary>
    public IReadOnlyList<RuleEvaluation> Evaluations { get; } = Evaluations ?? throw new ArgumentNullException(nameof(Evaluations));

    /// <summary>
    /// What the assessment concluded overall — the worst thing any rule
    /// said. Never a pass unless every applicable rule passed.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public AssessmentOutcome Outcome => AssessmentOutcomes.Aggregate(Evaluations.Select(e => e.Outcome));

    /// <summary>Every binding rule whose condition did not hold — the defects.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<RuleEvaluation> Defects => Evaluations.Where(e => e.IsDefect).ToList();

    /// <summary>Every rule that could not reach a conclusion — the gaps a reviewer works through.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<RuleEvaluation> Gaps => Evaluations.Where(e => AssessmentOutcomes.IsGap(e.Outcome)).ToList();

    /// <summary>
    /// Whether an engineer must decide before this assessment is acted on.
    /// </summary>
    /// <remarks>
    /// True whenever any rule asked for human review, whenever any rule
    /// found a defect, and whenever anything was inconclusive. False means
    /// only that nothing in the assessment itself demands a decision —
    /// never that the design is approved, which `P02` has no authority to
    /// say.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresHumanDecision =>
        Evaluations.Any(e => e.RequiresHumanReview || e.IsDefect || !AssessmentOutcomes.IsConclusive(e.Outcome));

    /// <summary>
    /// Every reference-data revision this assessment depends on, without
    /// duplicates — the complete answer to "what would have to change for
    /// this conclusion to change?".
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReferencePin> AllPins =>
        Evaluations
            .SelectMany(e => e.AllPins)
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

    /// <summary>How many rules reached each outcome — the summary a reviewer reads first.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<AssessmentOutcome, int> OutcomeCounts =>
        AssessmentOutcomes.All
            .Select(outcome => (outcome, count: Evaluations.Count(e => e.Outcome == outcome)))
            .Where(pair => pair.count > 0)
            .ToDictionary(pair => pair.outcome, pair => pair.count);
}
