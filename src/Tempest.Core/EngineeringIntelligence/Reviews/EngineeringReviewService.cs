using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Reviews;

/// <summary>
/// Carries out structured engineering reviews (`WP02.4`).
/// </summary>
/// <remarks>
/// <para>
/// <b>The system answers the criteria a rule can answer, and says so about
/// the rest.</b> A criterion naming a rule is evaluated and its finding
/// carries the evaluation; a criterion no rule can answer becomes a
/// finding of <see cref="AssessmentOutcome.EvidenceRequired"/> naming what
/// would settle it. That is what stops a review reporting "all clear" on
/// the questions that actually need an engineer.
/// </para>
/// <para>
/// <b>This does not replace an engineer, and does not approve
/// anything.</b> A completed review is evidence: what was checked, what
/// was found, and what remains open. Whether the design may proceed is a
/// decision a named engineer takes, recorded through the platform's own
/// approval mechanism.
/// </para>
/// </remarks>
public interface IEngineeringReviewService
{
    /// <summary>
    /// Carries out the released review registered under
    /// <paramref name="reviewCode"/> against <paramref name="subject"/>,
    /// answering every criterion a rule can answer.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="reviewCode"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">No review is registered under <paramref name="reviewCode"/>.</exception>
    /// <exception cref="UnreleasedReviewDefinitionException">The review exists but has not been released.</exception>
    Task<ReviewRecord> ConductAsync(string reviewCode, IAssessmentSubject subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a finding a person reached, replacing the automated or
    /// awaiting-evidence finding for that criterion.
    /// </summary>
    /// <remarks>
    /// <b>An engineer may answer any criterion, including one a rule
    /// already answered.</b> A rule that reported a defect an engineer has
    /// established is not a defect must be answerable — but the record
    /// keeps both: the manual finding replaces the automated one in the
    /// review's own list, and the automated evaluation travels with it as
    /// evidence, so nothing is quietly overwritten.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="review"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The review has no criterion with that code, or <paramref name="reason"/> is blank.</exception>
    ReviewRecord RecordFinding(
        ReviewRecord review,
        string criterionCode,
        AssessmentOutcome outcome,
        string reason,
        IReadOnlyList<EvidenceReference>? evidence = null);

    /// <summary>
    /// Re-runs a review against the exact review-definition revision a
    /// previous record pinned, so a historical review can be reproduced.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="definitionPin"/> names a library other than the review library.</exception>
    Task<ReviewRecord> ReproduceAsync(
        ReferencePin definitionPin,
        IAssessmentSubject subject,
        CancellationToken cancellationToken = default);
}

/// <summary>Thrown when a review definition exists but has not been released.</summary>
public sealed class UnreleasedReviewDefinitionException : ReferenceDataException
{
    /// <summary>Initialises a new instance of the <see cref="UnreleasedReviewDefinitionException"/> class.</summary>
    /// <param name="reviewCode">The review that was asked for.</param>
    /// <param name="state">The state it is actually in.</param>
    public UnreleasedReviewDefinitionException(string reviewCode, ReferenceValidationState state)
        : base(
            "EngineeringReviews",
            $"Review definition '{reviewCode}' is {state}, not Released. A review nobody has finished agreeing on "
            + "must not be carried out as though it were established practice.")
    {
        ReviewCode = reviewCode;
        State = state;
    }

    /// <summary>The review that was asked for.</summary>
    public string ReviewCode { get; }

    /// <summary>The state it is actually in.</summary>
    public ReferenceValidationState State { get; }
}

/// <summary>The concrete <see cref="IEngineeringReviewService"/> implementation.</summary>
public sealed class EngineeringReviewService : IEngineeringReviewService
{
    /// <summary>Recorded as the reviewer where no principal is established.</summary>
    public const string UnknownReviewerPrincipalId = "unknown";

    private readonly IReviewDefinitionCatalog _reviews;
    private readonly IRuleCatalog _rules;
    private readonly ICurrentPrincipalAccessor _principals;
    private readonly IReleasedConstantSource? _constants;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="EngineeringReviewService"/> class.</summary>
    /// <param name="reviews">The review-definition library.</param>
    /// <param name="rules">The rule library, for criteria a rule answers.</param>
    /// <param name="principals">The platform's own identity boundary, for attributing a review.</param>
    /// <param name="constants">The released-constant seam. Optional.</param>
    /// <param name="timeProvider">The clock a review is stamped with. Defaults to the system clock.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public EngineeringReviewService(
        IReviewDefinitionCatalog reviews,
        IRuleCatalog rules,
        ICurrentPrincipalAccessor principals,
        IReleasedConstantSource? constants = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(principals);

        _reviews = reviews;
        _rules = rules;
        _principals = principals;
        _constants = constants;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ReviewRecord> ConductAsync(
        string reviewCode,
        IAssessmentSubject subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewCode);
        ArgumentNullException.ThrowIfNull(subject);

        var record = await _reviews.FindByCodeAsync(reviewCode, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(_reviews.LibraryName, reviewCode);

        if (record.ValidationState != ReferenceValidationState.Released)
            throw new UnreleasedReviewDefinitionException(reviewCode, record.ValidationState);

        return await ConductRecordAsync(record, subject, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReviewRecord> ReproduceAsync(
        ReferencePin definitionPin,
        IAssessmentSubject subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitionPin);
        ArgumentNullException.ThrowIfNull(subject);

        if (!string.Equals(definitionPin.Library, _reviews.LibraryName, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Pin {definitionPin} names library '{definitionPin.Library}', and this service can only reproduce review-definition pins.",
                nameof(definitionPin));

        var record = await _reviews
            .GetRevisionAsync(definitionPin.RecordId, definitionPin.RevisionNumber, cancellationToken)
            .ConfigureAwait(false);

        return await ConductRecordAsync(record, subject, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ReviewRecord RecordFinding(
        ReviewRecord review,
        string criterionCode,
        AssessmentOutcome outcome,
        string reason,
        IReadOnlyList<EvidenceReference>? evidence = null)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentException.ThrowIfNullOrWhiteSpace(criterionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var existing = review.Findings.FirstOrDefault(f => string.Equals(f.CriterionCode, criterionCode, StringComparison.Ordinal))
            ?? throw new ArgumentException($"This review has no criterion '{criterionCode}'.", nameof(criterionCode));

        var supporting = new List<EvidenceReference>(evidence ?? []);

        // The automated finding this replaces is kept as evidence rather
        // than discarded: an engineer overriding a rule is a real and
        // legitimate act, and the record must show what the rule said.
        if (existing.Evaluation is { } evaluation)
            supporting.Add(new EvidenceReference(
                EvidenceKind.Other,
                $"Superseded by an engineer's own finding. Rule {evaluation.RuleCode} ({evaluation.RulePin}) reported "
                + $"{evaluation.Outcome}: {evaluation.Reason}",
                evaluation.RulePin));

        var updated = review.Findings
            .Select(finding => string.Equals(finding.CriterionCode, criterionCode, StringComparison.Ordinal)
                ? finding with
                {
                    Outcome = outcome,
                    Reason = reason.Trim(),
                    Evidence = supporting,
                    Evaluation = null,
                    RecordedByPrincipalId = _principals.Current?.Identity.Id ?? UnknownReviewerPrincipalId,
                }
                : finding)
            .ToList();

        return new ReviewRecord(
            review.ReviewCode, review.DefinitionPin, review.SubjectId, review.SubjectDisplayName, review.SubjectPin,
            updated, review.ReviewedAt, review.ReviewedByPrincipalId, review.ReviewerPrincipalIds, review.Notes);
    }

    private async Task<ReviewRecord> ConductRecordAsync(
        IReferenceRecord<ReviewDefinition> record,
        IAssessmentSubject subject,
        CancellationToken cancellationToken)
    {
        var definition = record.Definition;
        var findings = new List<ReviewFinding>(definition.Criteria.Count);

        // Every rule a criterion names, read once, so a rule referenced by
        // two criteria is the same rule at the same revision in both.
        var rulesByCode = new Dictionary<string, IReferenceRecord<RuleDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in definition.AutomatedCriteria.Select(c => c.RuleCode!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await _rules.FindByCodeAsync(code, cancellationToken).ConfigureAwait(false) is { } rule)
                rulesByCode[code] = rule;
        }

        var constants = _constants is null
            ? ConstantResolutionSet.Empty
            : await ConstantResolutionSet
                .ResolveForAsync(rulesByCode.Values.Select(r => r.Definition), _constants, cancellationToken)
                .ConfigureAwait(false);

        foreach (var criterion in definition.Criteria)
            findings.Add(Answer(criterion, subject, rulesByCode, constants));

        return new ReviewRecord(
            definition.Code,
            ReferencePin.For(_reviews.LibraryName, record),
            subject.SubjectId,
            subject.DisplayName,
            subject.Pin,
            findings,
            _time.GetUtcNow(),
            _principals.Current?.Identity.Id ?? UnknownReviewerPrincipalId);
    }

    private ReviewFinding Answer(
        ReviewCriterion criterion,
        IAssessmentSubject subject,
        IReadOnlyDictionary<string, IReferenceRecord<RuleDefinition>> rulesByCode,
        ConstantResolutionSet constants)
    {
        if (!criterion.IsAutomated)
            return new ReviewFinding(
                criterion.Code,
                criterion.Question,
                criterion.Area,
                criterion.Severity,
                AssessmentOutcome.EvidenceRequired,
                criterion.EvidenceExpected is { } expected
                    ? $"An engineer must answer this. What would settle it: {expected}"
                    : "An engineer must answer this; no rule can.");

        var ruleCode = criterion.RuleCode!;

        if (!rulesByCode.TryGetValue(ruleCode, out var rule))
            return new ReviewFinding(
                criterion.Code,
                criterion.Question,
                criterion.Area,
                criterion.Severity,
                AssessmentOutcome.NotEvaluated,
                $"This criterion names rule '{ruleCode}', which the rule library does not hold, so nothing was checked. "
                + "That is a gap in the review definition, not a finding about the subject.");

        if (rule.ValidationState != ReferenceValidationState.Released)
            return new ReviewFinding(
                criterion.Code,
                criterion.Question,
                criterion.Area,
                criterion.Severity,
                AssessmentOutcome.NotEvaluated,
                $"This criterion names rule '{ruleCode}', which is {rule.ValidationState} rather than Released. "
                + "An unreviewed rule must not produce a review finding.");

        var evaluation = RuleEngine.Evaluate(
            rule.Definition,
            ReferencePin.For(_rules.LibraryName, rule),
            subject,
            constants);

        return new ReviewFinding(
            criterion.Code,
            criterion.Question,
            criterion.Area,
            // The criterion's own severity governs, not the rule's: the
            // same rule may be a requirement at one review gate and an
            // advisory at an earlier one.
            criterion.Severity,
            evaluation.Outcome == AssessmentOutcome.Fail
                ? RuleSeverities.OutcomeWhenNotSatisfied(criterion.Severity)
                : evaluation.Outcome,
            evaluation.Reason,
            evaluation.Evidence,
            evaluation);
    }
}
