namespace Tempest.Core.BusinessGovernance.Operating;

/// <summary>How a measured value is compared against a gate's threshold.</summary>
public enum GateComparator
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>The value must reach or exceed the threshold.</summary>
    AtLeast,

    /// <summary>The value must not exceed the threshold.</summary>
    AtMost,

    /// <summary>The value must exceed the threshold.</summary>
    GreaterThan,

    /// <summary>The value must be below the threshold.</summary>
    LessThan
}

/// <summary>
/// What a gate's condition currently says.
/// </summary>
/// <remarks>
/// <b>Four states, and none of them is a decision.</b> Even
/// <see cref="ConditionMet"/> says only that the number crossed the line
/// somebody drew; whether to act on it is a judgement about the market,
/// the pipeline, the cash position and everything else that is not in the
/// gate.
/// </remarks>
public enum GateStatus
{
    /// <summary>Nothing has been measured, so nothing can be said.</summary>
    NotMeasured,

    /// <summary>The measurement is too old to rely on.</summary>
    MeasurementStale,

    /// <summary>The condition is not met.</summary>
    ConditionNotMet,

    /// <summary>The condition is met. A prompt for a decision, not a decision.</summary>
    ConditionMet
}

/// <summary>
/// A threshold the organisation has agreed to look at, and what it agreed
/// to consider when the threshold is crossed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The architecture reports; the responsible person decides.</b> A gate
/// holds a condition, a measured value, a review date and the name of
/// whoever decides. It has no field for the decision itself and no method
/// that reaches one, because a dashboard that quietly becomes the
/// decision-maker is exactly what C7 must not build.
/// </para>
/// <para>
/// The value is recorded rather than computed. Where it comes from a
/// finance scenario or a pipeline report, a caller reads that report and
/// records the figure with its date; the gate does not reach into other
/// packages, which keeps it deterministic and keeps C7 from acquiring
/// dependencies on every other work package.
/// </para>
/// </remarks>
/// <param name="Code">The gate's own identifier. Required.</param>
/// <param name="Question">What the gate is asking — "Should we hire a second stress engineer?". Required.</param>
/// <param name="MeasureName">What is measured. Required.</param>
/// <param name="Comparator">How the measurement is compared against the threshold.</param>
/// <param name="Threshold">The line somebody drew.</param>
/// <param name="Unit">What the measurement and threshold are in. Required.</param>
/// <param name="CurrentValue">The most recent measurement. <see langword="null"/> where nothing has been measured.</param>
/// <param name="MeasuredOn">When it was measured. <see langword="null"/> where nothing has been.</param>
/// <param name="DecisionOwnerPrincipalId">Who decides when the condition is met. Required — a gate nobody owns will not be acted on.</param>
/// <param name="ProposedAction">What the organisation agreed to consider. Required.</param>
/// <param name="ReviewBy">When the gate itself should be looked at, met or not. <see langword="null"/> where it is only event-driven.</param>
/// <param name="EvidenceRequired">What the decision-maker would need in front of them. Never <see langword="null"/>.</param>
public sealed record DecisionGate(
    string Code,
    string Question,
    string MeasureName,
    GateComparator Comparator,
    decimal Threshold,
    string Unit,
    decimal? CurrentValue = null,
    DateOnly? MeasuredOn = null,
    string DecisionOwnerPrincipalId = "",
    string ProposedAction = "",
    DateOnly? ReviewBy = null,
    IReadOnlyList<string>? EvidenceRequired = null)
{
    /// <summary>How old a measurement may be before the gate stops trusting it.</summary>
    public const int MeasurementStaleAfterDays = 90;

    /// <summary>The gate's own identifier.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A decision gate must carry its own code.", nameof(Code))
        : Code.Trim();

    /// <summary>What the gate is asking.</summary>
    public string Question { get; } = string.IsNullOrWhiteSpace(Question)
        ? throw new ArgumentException("A decision gate must state the question it exists to prompt.", nameof(Question))
        : Question.Trim();

    /// <summary>What is measured.</summary>
    public string MeasureName { get; } = string.IsNullOrWhiteSpace(MeasureName)
        ? throw new ArgumentException("A decision gate must say what it measures.", nameof(MeasureName))
        : MeasureName.Trim();

    /// <summary>What the measurement and threshold are in.</summary>
    public string Unit { get; } = string.IsNullOrWhiteSpace(Unit)
        ? throw new ArgumentException("A decision gate must state its unit. A bare threshold is not comparable to anything.", nameof(Unit))
        : Unit.Trim();

    /// <summary>Who decides when the condition is met.</summary>
    public string DecisionOwnerPrincipalId { get; } = string.IsNullOrWhiteSpace(DecisionOwnerPrincipalId)
        ? throw new ArgumentException(
            "A decision gate must name the person who decides. A threshold nobody owns will be crossed and nothing will happen.",
            nameof(DecisionOwnerPrincipalId))
        : DecisionOwnerPrincipalId.Trim();

    /// <summary>What the organisation agreed to consider.</summary>
    public string ProposedAction { get; } = string.IsNullOrWhiteSpace(ProposedAction)
        ? throw new ArgumentException(
            "A decision gate must say what it proposes considering. A trigger with no proposed action is a number on a screen.",
            nameof(ProposedAction))
        : ProposedAction.Trim();

    /// <summary>What the decision-maker would need in front of them.</summary>
    public IReadOnlyList<string> EvidenceRequired { get; init; } = EvidenceRequired ?? [];

    /// <summary>
    /// What the gate's condition says as at <paramref name="asAt"/>.
    /// </summary>
    /// <remarks>
    /// Pure and deterministic: the same gate and date always give the same
    /// status. A measurement older than
    /// <see cref="MeasurementStaleAfterDays"/> is reported as stale rather
    /// than acted on, because a gate acting on a figure from two quarters
    /// ago is worse than one acting on nothing.
    /// </remarks>
    public GateStatus StatusAt(DateOnly asAt)
    {
        if (CurrentValue is not { } value || MeasuredOn is not { } measured)
            return GateStatus.NotMeasured;

        if (measured.AddDays(MeasurementStaleAfterDays) < asAt)
            return GateStatus.MeasurementStale;

        var met = Comparator switch
        {
            GateComparator.AtLeast => value >= Threshold,
            GateComparator.AtMost => value <= Threshold,
            GateComparator.GreaterThan => value > Threshold,
            GateComparator.LessThan => value < Threshold,
            _ => false,
        };

        return met ? GateStatus.ConditionMet : GateStatus.ConditionNotMet;
    }

    /// <summary>Whether the gate is due to be looked at as at <paramref name="asAt"/>, whatever its condition says.</summary>
    public bool IsReviewDueAt(DateOnly asAt) => ReviewBy is { } due && due <= asAt;

    /// <summary>
    /// What the gate says, in the words a person should read.
    /// </summary>
    /// <remarks>
    /// Every phrasing stops short of instructing. A met condition reads as
    /// something for the owner to consider, never as something the
    /// organisation should now do.
    /// </remarks>
    public string Describe(DateOnly asAt) => StatusAt(asAt) switch
    {
        GateStatus.NotMeasured =>
            $"'{Question}' cannot be answered: {MeasureName} has never been measured against the {Threshold} {Unit} threshold.",
        GateStatus.MeasurementStale =>
            $"'{Question}' rests on a {MeasureName} of {CurrentValue} {Unit} measured on {MeasuredOn:O}, which is too old to rely on.",
        GateStatus.ConditionNotMet =>
            $"'{Question}': {MeasureName} is {CurrentValue} {Unit} against a threshold of {Threshold}. The condition is not met.",
        GateStatus.ConditionMet =>
            $"'{Question}': {MeasureName} is {CurrentValue} {Unit}, past the {Threshold} {Unit} threshold. "
            + $"'{DecisionOwnerPrincipalId}' is asked to consider: {ProposedAction}. The threshold being crossed is not itself a decision.",
        _ => $"'{Question}' is in an unrecognised state.",
    };

    /// <summary>The case-insensitive key the gate is found by within its model.</summary>
    public string CodeKey => Code.ToUpperInvariant();
}
