using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Reviews;

/// <summary>
/// One engineering review, carried out against one subject.
/// </summary>
/// <remarks>
/// <para>
/// <b>A review record is not an approval, and there is no field here that
/// could be mistaken for one.</b> <see cref="IsComplete"/> says every
/// criterion has a finding; <see cref="OutstandingFindings"/> says what is
/// still open. Whether the design may proceed is a decision a named
/// engineer takes, recorded through the platform's own approval mechanism
/// (<c>IApproval</c>), which this record is evidence <em>for</em> rather
/// than a substitute for.
/// </para>
/// <para>
/// The pins are the traceability: this review checked <em>that</em>
/// subject at <em>that</em> revision, against <em>that</em> review
/// definition at <em>that</em> revision, using rules at their own
/// revisions. Every one of those can be read back exactly as it stood.
/// </para>
/// </remarks>
/// <param name="ReviewCode">The review definition carried out.</param>
/// <param name="DefinitionPin">The exact review definition and revision carried out.</param>
/// <param name="SubjectId">The subject reviewed.</param>
/// <param name="SubjectDisplayName">What the subject is called.</param>
/// <param name="SubjectPin">The subject's pinned reference-data revision, where it is a reference record.</param>
/// <param name="Findings">One finding per criterion, in the definition's own order. Never <see langword="null"/>.</param>
/// <param name="ReviewedAt">When the review ran.</param>
/// <param name="ReviewedByPrincipalId">Who ran it.</param>
/// <param name="ReviewerPrincipalIds">Every engineer taking part, where more than one did. Never <see langword="null"/>.</param>
/// <param name="Notes">Anything the reviewers recorded that no criterion covers. <see langword="null"/> if none.</param>
public sealed record ReviewRecord(
    string ReviewCode,
    ReferencePin DefinitionPin,
    string SubjectId,
    string SubjectDisplayName,
    ReferencePin? SubjectPin,
    IReadOnlyList<ReviewFinding> Findings,
    DateTimeOffset ReviewedAt,
    string ReviewedByPrincipalId,
    IReadOnlyList<string>? ReviewerPrincipalIds = null,
    string? Notes = null)
{
    /// <summary>One finding per criterion.</summary>
    public IReadOnlyList<ReviewFinding> Findings { get; } = Findings ?? throw new ArgumentNullException(nameof(Findings));

    /// <summary>Every engineer taking part.</summary>
    public IReadOnlyList<string> ReviewerPrincipalIds { get; init; } = ReviewerPrincipalIds ?? [];

    /// <summary>What the review concluded overall — the worst thing any criterion found.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public AssessmentOutcome Outcome => AssessmentOutcomes.Aggregate(Findings.Select(f => f.Outcome));

    /// <summary>Every finding that is a defect — a binding criterion not satisfied.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReviewFinding> Defects => Findings.Where(f => f.IsDefect).ToList();

    /// <summary>Every finding still needing work.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReviewFinding> OutstandingFindings => Findings.Where(f => f.IsOutstanding).ToList();

    /// <summary>Every criterion still waiting for a person to answer it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReviewFinding> AwaitingEvidence =>
        Findings.Where(f => f.Outcome == AssessmentOutcome.EvidenceRequired).ToList();

    /// <summary>
    /// Whether every criterion has been answered — no finding is still
    /// waiting for evidence, and none is unevaluated.
    /// </summary>
    /// <remarks>
    /// Completeness is not a verdict. A review can be complete and still
    /// hold defects; it can be complete and still need a decision. What it
    /// cannot be is complete with questions nobody answered.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsComplete =>
        Findings.Count > 0
        && Findings.All(f => f.Outcome is not (AssessmentOutcome.EvidenceRequired or AssessmentOutcome.NotEvaluated));

    /// <summary>
    /// Whether an engineer must decide before this review is acted on.
    /// True whenever anything is outstanding, whenever the review is
    /// incomplete, and whenever any finding is a defect.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresHumanDecision => !IsComplete || OutstandingFindings.Count > 0 || Defects.Count > 0;

    /// <summary>How many findings reached each outcome — the summary a reviewer reads first.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<AssessmentOutcome, int> OutcomeCounts =>
        AssessmentOutcomes.All
            .Select(outcome => (outcome, count: Findings.Count(f => f.Outcome == outcome)))
            .Where(pair => pair.count > 0)
            .ToDictionary(pair => pair.outcome, pair => pair.count);

    /// <summary>Every reference-data revision this review rests on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReferencePin> AllPins =>
        new[] { DefinitionPin }
            .Concat(SubjectPin is null ? [] : new[] { SubjectPin })
            .Concat(Findings.SelectMany(f => f.AllPins))
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Records a disposition against one finding, returning a new review
    /// record. The original is unchanged.
    /// </summary>
    /// <remarks>
    /// A finding is never edited and never removed: dispositioning it
    /// produces a new record in which the finding still says what it
    /// found, and additionally says what was decided. That is what stops a
    /// disposition rewriting the review's own history.
    /// </remarks>
    /// <param name="criterionCode">The finding to disposition.</param>
    /// <param name="disposition">What was decided.</param>
    /// <param name="note">Why, and by whom where the disposition needs a name. Required for an accepted finding.</param>
    /// <param name="actionReference">The action raised, where one was.</param>
    /// <exception cref="ArgumentException">No finding answers <paramref name="criterionCode"/>, or an accepted finding is dispositioned without a note.</exception>
    public ReviewRecord WithDisposition(
        string criterionCode,
        FindingDisposition disposition,
        string? note = null,
        string? actionReference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(criterionCode);

        if (!Findings.Any(f => string.Equals(f.CriterionCode, criterionCode, StringComparison.Ordinal)))
            throw new ArgumentException($"This review has no finding for criterion '{criterionCode}'.", nameof(criterionCode));

        // Accepting a finding that stands is the one disposition that
        // leaves a real engineering concern in place. It must be
        // attributable, or the acceptance is nobody's.
        if (disposition == FindingDisposition.Accepted && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException(
                "Accepting a finding leaves a real engineering concern in place, so it must record who accepted it and why.",
                nameof(note));

        var updated = Findings
            .Select(finding => string.Equals(finding.CriterionCode, criterionCode, StringComparison.Ordinal)
                ? finding with { Disposition = disposition, DispositionNote = note, ActionReference = actionReference }
                : finding)
            .ToList();

        return new ReviewRecord(
            ReviewCode, DefinitionPin, SubjectId, SubjectDisplayName, SubjectPin,
            updated, ReviewedAt, ReviewedByPrincipalId, ReviewerPrincipalIds, Notes);
    }
}
