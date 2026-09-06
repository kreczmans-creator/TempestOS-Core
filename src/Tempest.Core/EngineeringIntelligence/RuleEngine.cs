using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// One `A6` constant, already resolved, ready for a rule to compare
/// against.
/// </summary>
/// <param name="Symbol">The symbol the rule cited.</param>
/// <param name="Value">The released constant's own value.</param>
/// <param name="Pin">The constant record and revision the value came from.</param>
public sealed record ResolvedConstant(string Symbol, ReferenceQuantityValue Value, ReferencePin Pin);

/// <summary>
/// The constants a rule evaluation may compare against, resolved before
/// evaluation begins.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolved up front, deliberately.</b> The engine itself performs no
/// I/O: it is a pure function of a rule, a subject and this set. That is
/// what makes an evaluation reproducible — re-running it with the same
/// three inputs cannot reach a different answer because a catalogue
/// changed underneath it — and it is what makes the engine testable
/// without a store.
/// </para>
/// <para>
/// A symbol absent from this set is not an error and is not a zero: the
/// condition citing it reports
/// <see cref="AssessmentOutcome.EvidenceRequired"/>, because the evidence
/// the rule needs — a released constant — is not available. That is the
/// same answer whether the constant does not exist or exists only as an
/// unreleased draft, which is exactly what
/// <see cref="IReleasedConstantSource"/> already guarantees.
/// </para>
/// </remarks>
public sealed class ConstantResolutionSet
{
    private readonly Dictionary<string, ResolvedConstant> _bySymbol;

    /// <summary>An empty set — every constant-backed threshold will report that its evidence is missing.</summary>
    public static ConstantResolutionSet Empty { get; } = new([]);

    /// <summary>Initialises a new instance of the <see cref="ConstantResolutionSet"/> class.</summary>
    /// <param name="constants">The resolved constants. A symbol appearing twice is an error: one symbol has one value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="constants"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A symbol appears more than once.</exception>
    public ConstantResolutionSet(IEnumerable<ResolvedConstant> constants)
    {
        ArgumentNullException.ThrowIfNull(constants);

        _bySymbol = new Dictionary<string, ResolvedConstant>(StringComparer.Ordinal);

        foreach (var constant in constants)
        {
            if (!_bySymbol.TryAdd(constant.Symbol, constant))
                throw new ArgumentException(
                    $"Constant symbol '{constant.Symbol}' was resolved twice. One symbol has one value, or a rule could compare against either.",
                    nameof(constants));
        }
    }

    /// <summary>Every constant in this set, ordered by symbol.</summary>
    public IReadOnlyList<ResolvedConstant> All => _bySymbol.Values.OrderBy(c => c.Symbol, StringComparer.Ordinal).ToList();

    /// <summary>Returns the resolved constant for <paramref name="symbol"/>, or <see langword="null"/> if it was not resolved.</summary>
    public ResolvedConstant? Find(string symbol) =>
        string.IsNullOrWhiteSpace(symbol) ? null : _bySymbol.GetValueOrDefault(symbol.Trim());

    /// <summary>
    /// Resolves every constant <paramref name="rules"/> needs, through the
    /// released-only seam, so an evaluation can then run without I/O.
    /// </summary>
    /// <remarks>
    /// A symbol the seam does not return is simply absent from the result:
    /// there is nothing to record about a constant that is not released,
    /// and the condition citing it will say so at evaluation time.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public static async Task<ConstantResolutionSet> ResolveForAsync(
        IEnumerable<RuleDefinition> rules,
        IReleasedConstantSource constants,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(constants);

        var symbols = rules
            .Select(rule => rule.Condition)
            .OfType<RuleExpression>()
            .SelectMany(condition => condition.RequiredConstantSymbols)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(symbol => symbol, StringComparer.Ordinal)
            .ToList();

        var resolved = new List<ResolvedConstant>(symbols.Count);

        foreach (var symbol in symbols)
        {
            var constant = await constants.FindReleasedAsync(symbol, cancellationToken).ConfigureAwait(false);
            if (constant is not null)
                resolved.Add(new ResolvedConstant(
                    symbol,
                    constant.Value,
                    new ReferencePin("Constants", constant.RecordId, constant.RevisionNumber)));
        }

        return new ConstantResolutionSet(resolved);
    }
}

/// <summary>
/// Evaluates a rule against a subject, deterministically.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pure function, and the only place a rule's meaning is decided.</b>
/// No store, no clock, no principal, no randomness, no ambient state. The
/// same rule, the same subject and the same resolved constants always
/// produce an equal <see cref="RuleEvaluation"/> — which is what
/// `P02`'s own reproducibility requirement means, and what its tests
/// assert directly rather than by inspection.
/// </para>
/// <para>
/// <b>Every path through this class is an outcome, never an exception.</b>
/// A missing property, an unresolvable constant, a dimension mismatch and
/// an undecidable applicability are all engineering situations a real
/// assessment meets, and each has an outcome that says what happened.
/// Throwing would lose the explanation and stop the rest of the rule set
/// running.
/// </para>
/// </remarks>
public static class RuleEngine
{
    /// <summary>
    /// The relative tolerance an equality comparison uses, applied to the
    /// larger of the two magnitudes.
    /// </summary>
    /// <remarks>
    /// Exact floating-point equality between a transcribed threshold and a
    /// value converted from another unit is not achievable and asserting
    /// it would make equality rules fail at random. The tolerance is
    /// relative rather than absolute so it behaves identically whether the
    /// comparison is in metres or micrometres, and it is exposed so a test
    /// can pin the boundary rather than guess at it.
    /// </remarks>
    public const double RelativeComparisonTolerance = 1e-9;

    /// <summary>Evaluates <paramref name="rule"/> against <paramref name="subject"/>.</summary>
    /// <param name="rule">The rule to evaluate, at the revision <paramref name="rulePin"/> names.</param>
    /// <param name="rulePin">The exact rule record and revision being evaluated.</param>
    /// <param name="subject">The subject to evaluate it against.</param>
    /// <param name="constants">Constants resolved ahead of evaluation. Pass <see cref="ConstantResolutionSet.Empty"/> where the rule needs none.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static RuleEvaluation Evaluate(
        RuleDefinition rule,
        ReferencePin rulePin,
        IAssessmentSubject subject,
        ConstantResolutionSet constants)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(rulePin);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(constants);

        // Applicability first: a rule that does not apply is never
        // evaluated, and is never reported as having passed.
        var applicability = rule.Applicability.DecideFor(subject);

        if (applicability == ApplicabilityDecision.DoesNotApply)
            return Result(
                rule, rulePin, subject,
                AssessmentOutcome.NotApplicable,
                $"The rule applies where {rule.Applicability.Describe()}; {subject.DisplayName} is outside that.",
                condition: null,
                usedConstants: []);

        if (applicability == ApplicabilityDecision.Unknown)
            return Result(
                rule, rulePin, subject,
                AssessmentOutcome.Indeterminate,
                $"Whether the rule applies cannot be determined: it applies where {rule.Applicability.Describe()}, "
                + $"and {subject.DisplayName} does not record enough to decide.",
                condition: null,
                usedConstants: [],
                requiresHumanReview: true);

        if (rule.Condition is not { } condition)
            return Result(
                rule, rulePin, subject,
                AssessmentOutcome.NotEvaluated,
                $"Rule '{rule.Code}' states no condition, so there is nothing to evaluate.",
                condition: null,
                usedConstants: [],
                requiresHumanReview: true);

        var used = new List<ResolvedConstant>();
        var conditionResult = EvaluateExpression(condition, subject, constants, used);

        var outcome = conditionResult.Outcome switch
        {
            AssessmentOutcome.Pass => AssessmentOutcome.Pass,

            // The one place severity turns a condition into a verdict. A
            // prohibition that is not satisfied is a defect; a
            // recommendation that is not satisfied is a concern. The
            // condition itself never knows the difference.
            AssessmentOutcome.Fail => RuleSeverities.OutcomeWhenNotSatisfied(rule.Severity),

            // Everything else is a gap, and a gap is reported as itself
            // rather than being converted into a verdict the data cannot
            // support.
            var gap => gap,
        };

        var reason = outcome == AssessmentOutcome.Pass
            ? $"{rule.Statement} — satisfied."
            : $"{rule.Statement} — {conditionResult.Reason}";

        return Result(
            rule, rulePin, subject, outcome, reason, conditionResult, used,
            requiresHumanReview: rule.RequiresHumanReview
                || rule.IsSafetyCritical
                || rule.Applicability.NeedsHumanConfirmation
                || !AssessmentOutcomes.IsConclusive(outcome));
    }

    private static RuleEvaluation Result(
        RuleDefinition rule,
        ReferencePin rulePin,
        IAssessmentSubject subject,
        AssessmentOutcome outcome,
        string reason,
        ConditionResult? condition,
        IReadOnlyList<ResolvedConstant> usedConstants,
        bool requiresHumanReview = false)
    {
        var evidence = new List<EvidenceReference>();

        if (subject.Pin is { } subjectPin)
            evidence.Add(EvidenceReference.FromReferenceData(
                subjectPin,
                $"{subject.DisplayName}, as recorded at {subjectPin}."));

        foreach (var constant in usedConstants)
            evidence.Add(new EvidenceReference(
                EvidenceKind.EngineeringConstant,
                $"Constant '{constant.Symbol}' = {constant.Value.Value}, as released at {constant.Pin}.",
                constant.Pin));

        foreach (var standard in rule.Standards)
            evidence.Add(new EvidenceReference(
                EvidenceKind.Standard,
                $"Rule authority cited as '{standard.Designation}'"
                + (standard.Edition is null ? "." : $", edition {standard.Edition}.")
                + " Citing a standard is not a claim of compliance with it.",
                Reference: standard.StandardId));

        return new RuleEvaluation(
            rule.Code,
            rulePin,
            rule.Severity,
            subject.SubjectId,
            subject.Pin,
            outcome,
            reason,
            condition,
            evidence,
            usedConstants.Select(c => c.Pin).ToList(),
            requiresHumanReview);
    }

    private static ConditionResult EvaluateExpression(
        RuleExpression expression,
        IAssessmentSubject subject,
        ConstantResolutionSet constants,
        List<ResolvedConstant> used) => expression switch
        {
            QuantityComparisonExpression comparison => EvaluateComparison(comparison, subject, constants, used),
            TextMatchExpression match => EvaluateTextMatch(match, subject),
            PropertyRecordedExpression recorded => EvaluateRecorded(recorded, subject),
            EvidenceRequiredExpression evidence => new ConditionResult(
                evidence.Describe(),
                AssessmentOutcome.EvidenceRequired,
                $"This cannot be concluded from recorded data: {evidence.Requirement}"),
            StatedExpression stated => new ConditionResult(
                stated.Describe(),
                stated.Holds ? AssessmentOutcome.Pass : AssessmentOutcome.Fail,
                stated.Holds ? $"{stated.Statement} — stated as holding." : $"{stated.Statement} — stated as not holding."),
            AllOfExpression allOf => EvaluateAllOf(allOf, subject, constants, used),
            AnyOfExpression anyOf => EvaluateAnyOf(anyOf, subject, constants, used),
            NotExpression not => EvaluateNot(not, subject, constants, used),
            _ => new ConditionResult(
                expression.Describe(),
                AssessmentOutcome.Indeterminate,
                "This build does not recognise the condition form, so it cannot be evaluated. "
                + "The rule may have been written by a later version."),
        };

    private static ConditionResult EvaluateComparison(
        QuantityComparisonExpression comparison,
        IAssessmentSubject subject,
        ConstantResolutionSet constants,
        List<ResolvedConstant> used)
    {
        var description = comparison.Describe();
        var subjectValue = subject.GetQuantity(comparison.PropertyName);

        if (subjectValue.Availability != ReferencePropertyAvailability.Recorded || subjectValue.Value is null)
            return new ConditionResult(
                description,
                subjectValue.AbsenceOutcome,
                subjectValue.Availability == ReferencePropertyAvailability.NotApplicable
                    ? $"{comparison.PropertyName} does not apply to {subject.DisplayName}, so there is nothing to compare."
                    : $"{comparison.PropertyName} is not recorded for {subject.DisplayName}. "
                      + "An unrecorded property is not a satisfied one, and is never read as zero.");

        ReferenceQuantityValue threshold;

        if (comparison.Threshold.ConstantSymbol is { } symbol)
        {
            var resolved = constants.Find(symbol);

            if (resolved is null)
                return new ConditionResult(
                    description,
                    AssessmentOutcome.EvidenceRequired,
                    $"The rule compares against constant '{symbol}', which is not available as a released engineering constant. "
                    + "An unreleased constant is not a value this comparison may use.");

            if (!used.Any(c => string.Equals(c.Symbol, symbol, StringComparison.Ordinal)))
                used.Add(resolved);

            threshold = resolved.Value;
        }
        else
        {
            threshold = comparison.Threshold.Literal!;
        }

        if (!string.Equals(subjectValue.Value.DimensionName, threshold.DimensionName, StringComparison.Ordinal))
            return new ConditionResult(
                description,
                AssessmentOutcome.Indeterminate,
                $"{comparison.PropertyName} is recorded as a {subjectValue.Value.DimensionName} and the rule compares it "
                + $"against a {threshold.DimensionName}. These are not comparable, so the rule states nothing about this subject.");

        var actual = subjectValue.Value.CanonicalValue;
        var limit = threshold.CanonicalValue;
        var tolerance = Math.Max(Math.Abs(actual), Math.Abs(limit)) * RelativeComparisonTolerance;

        var satisfied = comparison.Comparator switch
        {
            QuantityComparator.AtLeast => actual >= limit - tolerance,
            QuantityComparator.AtMost => actual <= limit + tolerance,
            QuantityComparator.GreaterThan => actual > limit + tolerance,
            QuantityComparator.LessThan => actual < limit - tolerance,
            QuantityComparator.EqualTo => Math.Abs(actual - limit) <= tolerance,
            QuantityComparator.NotEqualTo => Math.Abs(actual - limit) > tolerance,
            _ => false,
        };

        return new ConditionResult(
            description,
            satisfied ? AssessmentOutcome.Pass : AssessmentOutcome.Fail,
            $"{comparison.PropertyName} is {subjectValue.Value.Value}, and the rule requires it to be "
            + $"{DescribeComparator(comparison.Comparator)} {threshold.Value}. {(satisfied ? "It is." : "It is not.")}");
    }

    private static string DescribeComparator(QuantityComparator comparator) => comparator switch
    {
        QuantityComparator.AtLeast => "at least",
        QuantityComparator.AtMost => "at most",
        QuantityComparator.GreaterThan => "greater than",
        QuantityComparator.LessThan => "less than",
        QuantityComparator.EqualTo => "equal to",
        QuantityComparator.NotEqualTo => "different from",
        _ => "compared to",
    };

    private static ConditionResult EvaluateTextMatch(TextMatchExpression match, IAssessmentSubject subject)
    {
        var description = match.Describe();
        var value = subject.GetText(match.AttributeName);

        if (value.Availability != ReferencePropertyAvailability.Recorded || value.Value is null)
            return new ConditionResult(
                description,
                value.AbsenceOutcome,
                value.Availability == ReferencePropertyAvailability.NotApplicable
                    ? $"{match.AttributeName} does not apply to {subject.DisplayName}, so there is nothing to match."
                    : $"{match.AttributeName} is not recorded for {subject.DisplayName}. An unrecorded attribute matches nothing.");

        var inSet = match.AcceptedValues.Any(accepted =>
            string.Equals(accepted, value.Value.Trim(), StringComparison.OrdinalIgnoreCase));
        var satisfied = match.Negated ? !inSet : inSet;

        return new ConditionResult(
            description,
            satisfied ? AssessmentOutcome.Pass : AssessmentOutcome.Fail,
            $"{match.AttributeName} is '{value.Value}'. The rule accepts "
            + $"{(match.Negated ? "anything but " : string.Empty)}[{string.Join(", ", match.AcceptedValues)}]. "
            + $"{(satisfied ? "It matches." : "It does not.")}");
    }

    private static ConditionResult EvaluateRecorded(PropertyRecordedExpression recorded, IAssessmentSubject subject)
    {
        var description = recorded.Describe();
        var value = subject.GetQuantity(recorded.PropertyName);

        return value.Availability switch
        {
            ReferencePropertyAvailability.Recorded => new ConditionResult(
                description,
                AssessmentOutcome.Pass,
                $"{recorded.PropertyName} is recorded for {subject.DisplayName}."),

            // A completeness check is the one condition for which an
            // absent value is a genuine, conclusive failure rather than a
            // gap: being unrecorded is exactly what it tests for.
            ReferencePropertyAvailability.NotRecorded => new ConditionResult(
                description,
                AssessmentOutcome.Fail,
                $"{recorded.PropertyName} is not recorded for {subject.DisplayName}, and the rule requires it to be."),

            _ => new ConditionResult(
                description,
                AssessmentOutcome.NotApplicable,
                $"{recorded.PropertyName} does not apply to {subject.DisplayName}, so it cannot be required."),
        };
    }

    private static ConditionResult EvaluateAllOf(
        AllOfExpression allOf,
        IAssessmentSubject subject,
        ConstantResolutionSet constants,
        List<ResolvedConstant> used)
    {
        // Every operand is evaluated, never short-circuited: an engineer
        // reading the result should see every reason at once.
        var children = allOf.Operands.Select(o => EvaluateExpression(o, subject, constants, used)).ToList();
        var outcome = AssessmentOutcomes.Aggregate(children.Select(c => c.Outcome));

        var reason = outcome == AssessmentOutcome.Pass
            ? "every condition is satisfied."
            : string.Join(" ", children.Where(c => c.Outcome != AssessmentOutcome.Pass).Select(c => c.Reason));

        return new ConditionResult(allOf.Describe(), outcome, reason, children);
    }

    private static ConditionResult EvaluateAnyOf(
        AnyOfExpression anyOf,
        IAssessmentSubject subject,
        ConstantResolutionSet constants,
        List<ResolvedConstant> used)
    {
        var children = anyOf.Operands.Select(o => EvaluateExpression(o, subject, constants, used)).ToList();

        if (children.Any(c => c.Outcome == AssessmentOutcome.Pass))
            return new ConditionResult(
                anyOf.Describe(),
                AssessmentOutcome.Pass,
                "at least one alternative is satisfied: "
                + children.First(c => c.Outcome == AssessmentOutcome.Pass).Reason,
                children);

        // Nothing passed. If some alternative could not be evaluated, the
        // set has not been ruled out — reporting a failure would claim
        // more than the data supports.
        var gaps = children.Where(c => AssessmentOutcomes.IsGap(c.Outcome)).ToList();

        if (gaps.Count > 0)
            return new ConditionResult(
                anyOf.Describe(),
                AssessmentOutcomes.Aggregate(gaps.Select(c => c.Outcome)),
                "no alternative is satisfied, and at least one could not be evaluated, so the alternatives are not ruled out: "
                + string.Join(" ", gaps.Select(c => c.Reason)),
                children);

        return new ConditionResult(
            anyOf.Describe(),
            AssessmentOutcome.Fail,
            "no alternative is satisfied: " + string.Join(" ", children.Select(c => c.Reason)),
            children);
    }

    private static ConditionResult EvaluateNot(
        NotExpression not,
        IAssessmentSubject subject,
        ConstantResolutionSet constants,
        List<ResolvedConstant> used)
    {
        var child = EvaluateExpression(not.Operand, subject, constants, used);

        // Negating a gap leaves a gap. Not knowing whether something holds
        // is not knowing that it does not.
        var outcome = child.Outcome switch
        {
            AssessmentOutcome.Pass => AssessmentOutcome.Fail,
            AssessmentOutcome.Fail => AssessmentOutcome.Pass,
            var other => other,
        };

        var reason = outcome switch
        {
            AssessmentOutcome.Pass => $"the negated condition does not hold, as required: {child.Reason}",
            AssessmentOutcome.Fail => $"the negated condition holds, and the rule requires it not to: {child.Reason}",
            _ => $"the negated condition could not be evaluated, so its negation cannot be either: {child.Reason}",
        };

        return new ConditionResult(not.Describe(), outcome, reason, [child]);
    }
}
