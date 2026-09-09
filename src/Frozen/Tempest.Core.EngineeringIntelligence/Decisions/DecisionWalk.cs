using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Decisions;

/// <summary>One step a walk took through a decision tree.</summary>
/// <param name="NodeId">The node the walk was at.</param>
/// <param name="Question">The question that node asked.</param>
/// <param name="BranchLabel">The branch taken. <see langword="null"/> at a terminal node, and where no branch could be taken.</param>
/// <param name="BranchRationale">Why that branch exists, where the author recorded it.</param>
/// <param name="ConditionResult">What the branch condition concluded, and why. <see langword="null"/> at a terminal node.</param>
/// <param name="EvaluatedBranches">Every branch condition evaluated at this node, in order, including the ones that did not select — so a reader can see what was ruled out, not only what was chosen.</param>
public sealed record DecisionStep(
    string NodeId,
    string Question,
    string? BranchLabel,
    string? BranchRationale,
    ConditionResult? ConditionResult,
    IReadOnlyList<ConditionResult>? EvaluatedBranches = null)
{
    /// <summary>Every branch condition evaluated at this node, in order.</summary>
    public IReadOnlyList<ConditionResult> EvaluatedBranches { get; init; } = EvaluatedBranches ?? [];
}

/// <summary>Why a walk stopped.</summary>
public enum DecisionWalkTermination
{
    /// <summary>The walk reached a terminal node and the tree concluded.</summary>
    ReachedOutcome,

    /// <summary>
    /// No branch's condition was satisfied and the node declared no
    /// default. The tree does not cover this case — a real and reportable
    /// finding about the tree, not about the subject.
    /// </summary>
    NoBranchApplied,

    /// <summary>
    /// A branch condition could not be evaluated, so continuing would mean
    /// deciding on a basis nobody has established. The walk stops and says
    /// what was missing.
    /// </summary>
    InformationMissing,

    /// <summary>A branch led to a node the tree does not contain, or the tree named a root it does not contain.</summary>
    TreeIsBroken,

    /// <summary>
    /// The walk revisited a node it had already been at. A decision tree
    /// with a cycle would otherwise walk forever; the walk stops and
    /// reports the tree as broken.
    /// </summary>
    CycleDetected
}

/// <summary>
/// What happened when a decision tree was walked for one subject.
/// </summary>
/// <remarks>
/// <para>
/// <b>The path is the explanation.</b> Every step records which node was
/// reached, which question it asked, which branch was taken and why — and
/// every branch condition that was evaluated at that node, including the
/// ones that did not select. An engineer disputing the conclusion can see
/// exactly where the tree went a way they would not have.
/// </para>
/// <para>
/// Deterministic, like <see cref="RuleEvaluation"/> and for the same
/// reason: given the same tree revision, subject revision and resolved
/// constants, two walks are equal values. Who walked it and when belongs
/// to the caller, not here.
/// </para>
/// </remarks>
/// <param name="TreeCode">The tree walked.</param>
/// <param name="TreePin">The exact tree record and revision walked.</param>
/// <param name="SubjectId">The subject the conditions were evaluated against.</param>
/// <param name="SubjectPin">The subject's pinned reference-data revision, where it is a reference record.</param>
/// <param name="Path">The steps taken, in order. Never <see langword="null"/>.</param>
/// <param name="Termination">Why the walk stopped.</param>
/// <param name="Outcome">What the tree concluded. <see langword="null"/> unless the walk reached a terminal node.</param>
/// <param name="Explanation">Why the walk ended as it did, in one sentence.</param>
/// <param name="ConstantPins">The `A6` constants resolved during the walk. Never <see langword="null"/>.</param>
public sealed record DecisionWalk(
    string TreeCode,
    ReferencePin TreePin,
    string SubjectId,
    ReferencePin? SubjectPin,
    IReadOnlyList<DecisionStep> Path,
    DecisionWalkTermination Termination,
    DecisionOutcome? Outcome,
    string Explanation,
    IReadOnlyList<ReferencePin>? ConstantPins = null)
{
    /// <summary>The steps taken, in order.</summary>
    public IReadOnlyList<DecisionStep> Path { get; } = Path ?? throw new ArgumentNullException(nameof(Path));

    /// <summary>Why the walk ended as it did.</summary>
    public string Explanation { get; } = string.IsNullOrWhiteSpace(Explanation)
        ? throw new ArgumentException("A decision walk must explain why it ended where it did.", nameof(Explanation))
        : Explanation.Trim();

    /// <summary>The `A6` constants resolved during the walk.</summary>
    public IReadOnlyList<ReferencePin> ConstantPins { get; init; } = ConstantPins ?? [];

    /// <summary>Whether the walk reached a conclusion at all.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Concluded => Termination == DecisionWalkTermination.ReachedOutcome;

    /// <summary>
    /// Whether an engineer must decide before this walk is acted on.
    /// </summary>
    /// <remarks>
    /// True unless the walk both reached an outcome and that outcome said
    /// it needs no decision. A walk that stopped short always needs one,
    /// and so does an outcome naming several candidate processes: choosing
    /// between them is the judgement `P02` does not make.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresHumanDecision => !Concluded || (Outcome?.RequiresHumanDecision ?? true);

    /// <summary>The path as a readable trail, for a report or a log.</summary>
    public string DescribePath() =>
        string.Join(" → ", Path.Select(step => step.BranchLabel is null ? step.NodeId : $"{step.NodeId} [{step.BranchLabel}]"));

    /// <summary>Every reference-data revision this walk depends on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReferencePin> AllPins =>
        new[] { TreePin }
            .Concat(SubjectPin is null ? [] : new[] { SubjectPin })
            .Concat(ConstantPins)
            .Distinct()
            .ToList();
}
