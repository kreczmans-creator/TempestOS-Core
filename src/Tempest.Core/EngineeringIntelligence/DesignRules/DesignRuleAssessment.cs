namespace Tempest.Core.EngineeringIntelligence.DesignRules;

/// <summary>
/// One subject assessed against a set of design rules, with the scope of
/// the assessment stated.
/// </summary>
/// <remarks>
/// <para>
/// Wraps <see cref="AssessmentRecord"/> — which holds the deterministic
/// evaluations and the who-and-when — with the one thing a rule-set
/// assessment needs that a single rule evaluation does not: an explicit
/// statement of what was covered.
/// </para>
/// <para>
/// <b>An empty rule library produces an empty assessment, not a pass.</b>
/// <see cref="Outcome"/> is <see cref="AssessmentOutcome.NotEvaluated"/>
/// where nothing ran, and <see cref="Scope"/> says so in words. A system
/// that reported "no problems found" against a library holding no rules
/// would be the single most misleading thing `P02` could do.
/// </para>
/// </remarks>
/// <param name="Record">The deterministic evaluations, and who ran them when.</param>
/// <param name="Scope">What the assessment covered.</param>
public sealed record DesignRuleAssessment(AssessmentRecord Record, AssessmentScopeStatement Scope)
{
    /// <summary>The deterministic evaluations, and who ran them when.</summary>
    public AssessmentRecord Record { get; } = Record ?? throw new ArgumentNullException(nameof(Record));

    /// <summary>What the assessment covered.</summary>
    public AssessmentScopeStatement Scope { get; } = Scope ?? throw new ArgumentNullException(nameof(Scope));

    /// <summary>What the assessment concluded overall — never a pass unless every rule that ran passed.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public AssessmentOutcome Outcome => Record.Outcome;

    /// <summary>Every binding rule whose condition did not hold.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<RuleEvaluation> Defects => Record.Defects;

    /// <summary>Every rule that raised something short of a defect — a warning or a recommendation not met.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<RuleEvaluation> Concerns =>
        Record.Evaluations.Where(e => e.Outcome == AssessmentOutcome.Concern).ToList();

    /// <summary>Every rule that could not reach a conclusion.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<RuleEvaluation> Gaps => Record.Gaps;

    /// <summary>Whether an engineer must decide before this assessment is acted on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresHumanDecision => Record.RequiresHumanDecision || Scope.ApplicableRuleCount == 0;

    /// <summary>
    /// The assessment as an engineer would read it: what was covered, what
    /// was found, and what remains open.
    /// </summary>
    public string Explain()
    {
        var lines = new List<string>
        {
            $"Subject: {Record.SubjectDisplayName}"
                + (Record.SubjectPin is { } pin ? $" ({pin})" : string.Empty),
            $"Scope: {Scope.Describe()}",
        };

        if (Record.Evaluations.Count == 0)
        {
            lines.Add("Result: nothing was assessed. This is not a pass.");
        }
        else
        {
            lines.Add($"Result: {Record.Outcome}"
                + $" ({string.Join(", ", Record.OutcomeCounts.Select(pair => $"{pair.Value} {pair.Key}"))}).");

            foreach (var evaluation in Record.Evaluations.Where(e => e.Outcome != AssessmentOutcome.Pass))
                lines.Add($"  [{evaluation.Outcome}] {evaluation.RuleCode} ({evaluation.Severity}, {evaluation.RulePin}): {evaluation.Reason}");
        }

        lines.Add(RequiresHumanDecision
            ? "An engineer must review this before it is acted on."
            : "Nothing in this assessment itself demands a decision. That is not an approval.");

        return string.Join(Environment.NewLine, lines);
    }
}
