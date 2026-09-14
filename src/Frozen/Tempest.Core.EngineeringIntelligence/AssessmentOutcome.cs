namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// What an engineering assessment concluded — the single outcome
/// vocabulary every P02 capability reports in.
/// </summary>
/// <remarks>
/// <para>
/// <b>One vocabulary, not five.</b> A material criterion, a decision-tree
/// condition, a design rule, a review finding and a trade-study
/// assessment are different engineering acts, but the honest set of
/// things any of them can conclude is the same, and five parallel enums
/// would make a result from one capability unreadable by another.
/// </para>
/// <para>
/// <b>Only <see cref="Pass"/> is affirmative.</b> Six of the eight members
/// below exist because engineering assessment fails in more ways than
/// "no". The whole point of separating them is that none of them may be
/// read as suitability: a criterion nobody could evaluate has not been
/// satisfied, and a property nobody recorded is not a property that meets
/// the requirement. <see cref="AssessmentOutcomes.IsAffirmative"/> is the
/// only permitted test for "this held", and it is true for
/// <see cref="Pass"/> and nothing else.
/// </para>
/// <para>
/// This is not <see cref="EngineeringDomain.IValidationResult"/>'s
/// errors-and-warnings shape, and deliberately so: that shape is scoped
/// to <see cref="EngineeringDomain.IEngineeringObject"/> structural
/// integrity (`ADR-0084`), has two states where engineering reasoning
/// needs eight, and cannot distinguish "does not apply" from "nobody
/// checked" — the distinction P02 exists to preserve.
/// </para>
/// </remarks>
public enum AssessmentOutcome
{
    /// <summary>
    /// Nothing has been attempted yet. The honest default for an
    /// unevaluated assessment, and never a conclusion in its own right.
    /// </summary>
    NotEvaluated,

    /// <summary>The criterion was evaluated and held. <b>The only affirmative outcome.</b></summary>
    Pass,

    /// <summary>The criterion was evaluated and did not hold.</summary>
    Fail,

    /// <summary>
    /// The criterion was evaluated, did not fail, and produced something a
    /// person should look at — a marginal result, a qualification, or a
    /// condition the source attached. Never a quiet pass: a concern is
    /// reported, and <see cref="AssessmentOutcomes.IsAffirmative"/> is
    /// false for it.
    /// </summary>
    Concern,

    /// <summary>
    /// The criterion does not apply to this subject at all — a yield
    /// strength on a brittle material, a draft angle on a machined part.
    /// Nothing is missing; there is nothing to assess.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// The criterion applies, but the data it needs is not recorded.
    /// A data gap in the reference library, not a property of the subject,
    /// and <b>never</b> read as zero, as absent, or as satisfied.
    /// </summary>
    NotRecorded,

    /// <summary>
    /// The criterion cannot be concluded from recorded data alone and
    /// needs evidence a person must supply — a test report, a supplier
    /// confirmation, a calculation. Distinct from
    /// <see cref="NotRecorded"/>: the gap is not one the reference library
    /// could close by holding more data.
    /// </summary>
    EvidenceRequired,

    /// <summary>
    /// The criterion was evaluated and the available information supports
    /// no conclusion either way — contradictory sources, or a comparison
    /// the recorded values cannot decide. Distinct from
    /// <see cref="Concern"/>, which is a conclusion with a caveat.
    /// </summary>
    Indeterminate
}

/// <summary>Questions about an <see cref="AssessmentOutcome"/>, answered in one place.</summary>
/// <remarks>
/// Every question a caller could otherwise get wrong by writing its own
/// comparison. In particular there is no <c>IsNotFail</c> and there never
/// will be: absence of failure is not suitability, and offering a helper
/// that reads that way would defeat the reason the eight members exist.
/// </remarks>
public static class AssessmentOutcomes
{
    /// <summary>Every outcome, in the order a report should present them.</summary>
    public static IReadOnlyList<AssessmentOutcome> All { get; } =
    [
        AssessmentOutcome.Pass,
        AssessmentOutcome.Concern,
        AssessmentOutcome.Fail,
        AssessmentOutcome.Indeterminate,
        AssessmentOutcome.EvidenceRequired,
        AssessmentOutcome.NotRecorded,
        AssessmentOutcome.NotApplicable,
        AssessmentOutcome.NotEvaluated,
    ];

    /// <summary>
    /// Whether the outcome says the criterion <em>held</em>. True for
    /// <see cref="AssessmentOutcome.Pass"/> and nothing else.
    /// </summary>
    public static bool IsAffirmative(AssessmentOutcome outcome) => outcome == AssessmentOutcome.Pass;

    /// <summary>
    /// Whether the outcome says the criterion did <em>not</em> hold.
    /// True for <see cref="AssessmentOutcome.Fail"/> and nothing else —
    /// a concern is not a failure, and neither is a gap.
    /// </summary>
    public static bool IsAdverse(AssessmentOutcome outcome) => outcome == AssessmentOutcome.Fail;

    /// <summary>
    /// Whether the outcome is a conclusion at all, as opposed to a
    /// statement that no conclusion was reached.
    /// </summary>
    public static bool IsConclusive(AssessmentOutcome outcome) =>
        outcome is AssessmentOutcome.Pass or AssessmentOutcome.Fail or AssessmentOutcome.Concern or AssessmentOutcome.NotApplicable;

    /// <summary>
    /// Whether the outcome reports a gap someone must close before the
    /// assessment can conclude — the set a reviewer works through.
    /// </summary>
    public static bool IsGap(AssessmentOutcome outcome) =>
        outcome is AssessmentOutcome.NotRecorded or AssessmentOutcome.EvidenceRequired
            or AssessmentOutcome.Indeterminate or AssessmentOutcome.NotEvaluated;

    /// <summary>
    /// The outcome of a set of criteria taken together, by the only rule
    /// that is defensible: the worst thing any of them said.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ordered so that a single failure dominates, a gap outranks a
    /// concern, and a concern outranks a pass. A set that is entirely
    /// <see cref="AssessmentOutcome.NotApplicable"/> aggregates to
    /// <see cref="AssessmentOutcome.NotApplicable"/> — nothing applied, so
    /// nothing held; an empty set aggregates to
    /// <see cref="AssessmentOutcome.NotEvaluated"/>, because assessing
    /// nothing is not a pass.
    /// </para>
    /// <para>
    /// <b>This never produces a pass from a set containing anything but
    /// passes</b> (and inapplicable criteria, which contribute nothing).
    /// That is the aggregation rule P02's own charter demands: a candidate
    /// is not recommended because one numerical criterion passed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="outcomes"/> is <see langword="null"/>.</exception>
    public static AssessmentOutcome Aggregate(IEnumerable<AssessmentOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var worst = (AssessmentOutcome?)null;
        var sawApplicable = false;

        foreach (var outcome in outcomes)
        {
            if (outcome != AssessmentOutcome.NotApplicable)
                sawApplicable = true;

            if (worst is null || Rank(outcome) > Rank(worst.Value))
                worst = outcome;
        }

        if (worst is null)
            return AssessmentOutcome.NotEvaluated;

        return sawApplicable ? worst.Value : AssessmentOutcome.NotApplicable;
    }

    /// <summary>
    /// How much attention an outcome demands, for aggregation. Higher
    /// dominates. Exposed so the ordering is inspectable rather than
    /// buried in <see cref="Aggregate"/>.
    /// </summary>
    public static int Rank(AssessmentOutcome outcome) => outcome switch
    {
        AssessmentOutcome.NotApplicable => 0,
        AssessmentOutcome.Pass => 1,
        AssessmentOutcome.Concern => 2,
        AssessmentOutcome.NotEvaluated => 3,
        AssessmentOutcome.NotRecorded => 4,
        AssessmentOutcome.EvidenceRequired => 5,
        AssessmentOutcome.Indeterminate => 6,
        AssessmentOutcome.Fail => 7,
        _ => 7
    };
}
