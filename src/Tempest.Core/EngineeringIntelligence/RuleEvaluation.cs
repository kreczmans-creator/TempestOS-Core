namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// What one sub-condition concluded, and why — the unit of explanation.
/// </summary>
/// <param name="Condition">The condition as it was written, so the explanation reads in the rule's own terms.</param>
/// <param name="Outcome">What it concluded.</param>
/// <param name="Reason">Why, in plain engineering language. Required.</param>
/// <param name="Children">The sub-conditions beneath this one, where it combined others. Never <see langword="null"/>; empty for a leaf.</param>
public sealed record ConditionResult(
    string Condition,
    AssessmentOutcome Outcome,
    string Reason,
    IReadOnlyList<ConditionResult>? Children = null)
{
    /// <summary>Why, in plain engineering language.</summary>
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("A condition result must say why it concluded what it did.", nameof(Reason))
        : Reason.Trim();

    /// <summary>The sub-conditions beneath this one.</summary>
    public IReadOnlyList<ConditionResult> Children { get; init; } = Children ?? [];

    /// <summary>This result and every result beneath it, depth-first — the full explanation.</summary>
    public IReadOnlyList<ConditionResult> Flatten()
    {
        var all = new List<ConditionResult>();
        Collect(this, all);
        return all;

        static void Collect(ConditionResult result, List<ConditionResult> into)
        {
            into.Add(result);
            foreach (var child in result.Children)
                Collect(child, into);
        }
    }
}

/// <summary>
/// What happened when one rule was evaluated against one subject.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deterministic by construction.</b> Nothing here is a timestamp, a
/// principal or a sequence number. Given the same rule revision, the same
/// subject at the same reference-data revision and the same resolved
/// constants, two evaluations produce two equal values — which is exactly
/// what makes the reproducibility claim testable rather than asserted.
/// Who ran it and when belongs to <see cref="AssessmentRecord"/>, which
/// wraps this.
/// </para>
/// <para>
/// <b>Everything needed to reconstruct the reasoning travels with the
/// result.</b> Which rule, at which revision; which subject, at which
/// reference-data revision; which constants were resolved, at which
/// revisions; which conditions held and which did not; and what the rule's
/// severity turned that into.
/// </para>
/// <para>
/// <b>A pass is not an approval.</b> <see cref="Outcome"/> is an
/// assessment by a deterministic rule against recorded data.
/// <see cref="RequiresHumanReview"/> is set wherever the rule's author
/// said so, wherever applicability could not be fully decided, and
/// wherever the rule is safety-critical — but its being false is never a
/// statement that no engineer need look. Nothing in P02 approves anything.
/// </para>
/// </remarks>
/// <param name="RuleCode">The rule's own engineering identifier.</param>
/// <param name="RulePin">The exact rule record and revision that produced this result.</param>
/// <param name="Severity">The rule's severity at that revision — recorded here so the result stays readable after the rule is superseded.</param>
/// <param name="SubjectId">The subject assessed.</param>
/// <param name="SubjectPin">The subject's pinned reference-data revision, where the subject is a reference record. <see langword="null"/> otherwise.</param>
/// <param name="Outcome">What the rule concluded.</param>
/// <param name="Reason">Why, in one sentence.</param>
/// <param name="ConditionResult">The full condition breakdown. <see langword="null"/> where the rule was never evaluated — because it did not apply, or because it has no condition.</param>
/// <param name="Evidence">What supports the conclusion. Never <see langword="null"/>.</param>
/// <param name="ConstantPins">The `A6` constants resolved during evaluation, each at its own revision. Never <see langword="null"/>.</param>
/// <param name="RequiresHumanReview">Whether this result must not be acted on without a person looking at it.</param>
public sealed record RuleEvaluation(
    string RuleCode,
    ReferencePin RulePin,
    RuleSeverity Severity,
    string SubjectId,
    ReferencePin? SubjectPin,
    AssessmentOutcome Outcome,
    string Reason,
    ConditionResult? ConditionResult = null,
    IReadOnlyList<EvidenceReference>? Evidence = null,
    IReadOnlyList<ReferencePin>? ConstantPins = null,
    bool RequiresHumanReview = false)
{
    /// <summary>Why, in one sentence.</summary>
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("A rule evaluation must say why it concluded what it did.", nameof(Reason))
        : Reason.Trim();

    /// <summary>What supports the conclusion.</summary>
    public IReadOnlyList<EvidenceReference> Evidence { get; init; } = Evidence ?? [];

    /// <summary>The `A6` constants resolved during evaluation.</summary>
    public IReadOnlyList<ReferencePin> ConstantPins { get; init; } = ConstantPins ?? [];

    /// <summary>Whether the rule found a defect — a binding rule whose condition did not hold.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDefect => AssessmentOutcomes.IsAdverse(Outcome) && RuleSeverities.IsBinding(Severity);

    /// <summary>
    /// Every reference-data revision this result depends on — the rule's
    /// own, the subject's, and every constant resolved. The complete answer
    /// to "what would have to change for this result to change?".
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReferencePin> AllPins =>
        new[] { RulePin }
            .Concat(SubjectPin is null ? [] : new[] { SubjectPin })
            .Concat(ConstantPins)
            .ToList();
}
