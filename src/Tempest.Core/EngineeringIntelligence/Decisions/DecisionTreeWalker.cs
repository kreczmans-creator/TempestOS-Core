using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Decisions;

/// <summary>
/// Walks a decision tree for one subject, deterministically.
/// </summary>
/// <remarks>
/// <para>
/// A pure function of a tree, a subject and a set of pre-resolved
/// constants — no store, no clock, no principal — for the same reason
/// <see cref="RuleEngine"/> is: reproducibility that can be asserted by a
/// test rather than argued for.
/// </para>
/// <para>
/// <b>Every stopping condition is reported, never thrown.</b> A tree with
/// a dangling branch, a cycle, an uncovered case or a condition the data
/// cannot decide are all things a real tree library contains, and each
/// produces a walk that says what happened and how far it got.
/// </para>
/// </remarks>
public static class DecisionTreeWalker
{
    /// <summary>
    /// The greatest number of steps a walk will take before declaring the
    /// tree broken.
    /// </summary>
    /// <remarks>
    /// A backstop behind the cycle check, not a substitute for it: the
    /// cycle check catches a node revisited, and this catches a tree
    /// pathological in some way the cycle check does not model. Generous
    /// enough that no real engineering decision tree approaches it.
    /// </remarks>
    public const int MaximumSteps = 256;

    /// <summary>Walks <paramref name="tree"/> for <paramref name="subject"/>.</summary>
    /// <param name="tree">The tree to walk, at the revision <paramref name="treePin"/> names.</param>
    /// <param name="treePin">The exact tree record and revision being walked.</param>
    /// <param name="subject">The subject branch conditions are evaluated against.</param>
    /// <param name="constants">Constants resolved ahead of the walk. Pass <see cref="ConstantResolutionSet.Empty"/> where the tree needs none.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static DecisionWalk Walk(
        DecisionTree tree,
        ReferencePin treePin,
        IAssessmentSubject subject,
        ConstantResolutionSet constants)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(treePin);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(constants);

        var path = new List<DecisionStep>();
        var used = new List<ResolvedConstant>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentId = tree.RootNodeId;

        for (var step = 0; step < MaximumSteps; step++)
        {
            if (!visited.Add(currentId))
                return Stop(
                    DecisionWalkTermination.CycleDetected,
                    $"The walk returned to node '{currentId}', which it had already visited. "
                    + "A decision tree that loops cannot conclude, and this one is not usable until it is corrected.");

            if (tree.FindNode(currentId) is not { } node)
                return Stop(
                    DecisionWalkTermination.TreeIsBroken,
                    $"The walk was directed to node '{currentId}', which tree '{tree.Code}' does not contain.");

            if (node.Outcome is { } outcome)
            {
                path.Add(new DecisionStep(node.NodeId, node.Question, null, null, null));

                return new DecisionWalk(
                    tree.Code, treePin, subject.SubjectId, subject.Pin, path,
                    DecisionWalkTermination.ReachedOutcome, outcome,
                    $"The tree concluded: {outcome.Summary}",
                    used.Select(c => c.Pin).Distinct().ToList());
            }

            if (node.Branches.Count == 0)
                return Stop(
                    DecisionWalkTermination.TreeIsBroken,
                    $"Node '{node.NodeId}' neither asks a question nor states an outcome, so the walk has nowhere to go.");

            var evaluated = new List<ConditionResult>();
            DecisionBranch? taken = null;
            ConditionResult? takenResult = null;

            foreach (var branch in node.Branches)
            {
                var result = EvaluateBranch(branch, subject, constants, used);
                evaluated.Add(result);

                if (result.Outcome == AssessmentOutcome.Pass)
                {
                    taken = branch;
                    takenResult = result;
                    break;
                }

                // A branch that could not be evaluated is not a branch
                // that did not apply. Continuing past it would mean
                // deciding that it did not apply, which nobody knows.
                if (AssessmentOutcomes.IsGap(result.Outcome))
                {
                    path.Add(new DecisionStep(node.NodeId, node.Question, null, null, result, evaluated));

                    return new DecisionWalk(
                        tree.Code, treePin, subject.SubjectId, subject.Pin, path,
                        DecisionWalkTermination.InformationMissing, null,
                        $"At '{node.Question}', branch '{branch.Label}' could not be evaluated, so the walk cannot "
                        + $"continue past it without assuming it does not apply: {result.Reason}",
                        used.Select(c => c.Pin).Distinct().ToList());
                }
            }

            if (taken is null)
            {
                // No branch was satisfied and none was undecidable.
                if (node.DefaultBranchLabel is { } defaultLabel
                    && node.Branches.FirstOrDefault(b => string.Equals(b.Label, defaultLabel, StringComparison.Ordinal)) is { } fallback)
                {
                    path.Add(new DecisionStep(
                        node.NodeId, node.Question, fallback.Label, fallback.Rationale,
                        new ConditionResult(
                            $"default branch '{fallback.Label}'",
                            AssessmentOutcome.Pass,
                            "No branch condition was satisfied, so the node's declared default was taken."),
                        evaluated));

                    currentId = fallback.TargetNodeId;
                    continue;
                }

                path.Add(new DecisionStep(node.NodeId, node.Question, null, null, null, evaluated));

                return new DecisionWalk(
                    tree.Code, treePin, subject.SubjectId, subject.Pin, path,
                    DecisionWalkTermination.NoBranchApplied, null,
                    $"At '{node.Question}', no branch applied and the node declares no default. "
                    + "The tree does not cover this case — which is a finding about the tree, not about the subject.",
                    used.Select(c => c.Pin).Distinct().ToList());
            }

            path.Add(new DecisionStep(node.NodeId, node.Question, taken.Label, taken.Rationale, takenResult, evaluated));
            currentId = taken.TargetNodeId;
        }

        return Stop(
            DecisionWalkTermination.TreeIsBroken,
            $"The walk exceeded {MaximumSteps} steps without concluding. Tree '{tree.Code}' is not usable until it is corrected.");

        DecisionWalk Stop(DecisionWalkTermination termination, string explanation) => new(
            tree.Code, treePin, subject.SubjectId, subject.Pin, path, termination, null, explanation,
            used.Select(c => c.Pin).Distinct().ToList());
    }

    private static ConditionResult EvaluateBranch(
        DecisionBranch branch,
        IAssessmentSubject subject,
        ConstantResolutionSet constants,
        List<ResolvedConstant> used)
    {
        // Branch conditions go through the same evaluator rule conditions
        // do, so a missing property means the same thing in a tree as it
        // does in a rule. A probe rule is the honest way to reach it: the
        // engine's condition evaluation is not a separate, second
        // implementation.
        var probe = new RuleDefinition
        {
            Code = "BRANCH",
            Name = branch.Label,
            Statement = branch.Label,
            Severity = RuleSeverity.Requirement,
            Condition = branch.Condition,
        };

        var evaluation = RuleEngine.Evaluate(probe, new ReferencePin("DecisionTrees", "branch", 1), subject, constants);

        foreach (var pin in evaluation.ConstantPins)
        {
            if (constants.All.FirstOrDefault(c => c.Pin == pin) is { } resolved
                && !used.Any(u => u.Pin == pin))
                used.Add(resolved);
        }

        return evaluation.ConditionResult
            ?? new ConditionResult(branch.Condition.Describe(), evaluation.Outcome, evaluation.Reason);
    }
}
