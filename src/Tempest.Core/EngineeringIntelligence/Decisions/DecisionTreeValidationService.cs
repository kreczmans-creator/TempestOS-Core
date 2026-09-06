using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Decisions;

/// <summary>The diagnostic codes <see cref="IDecisionTreeValidationService"/> reports.</summary>
/// <remarks>
/// <b>Rules about trees, not about subjects.</b> These decide whether an
/// authored tree is fit to be released as guidance. The rules about being
/// a governed record at all live in
/// <see cref="ReferenceValidationRules"/>'s <c>TEMPEST-REF-</c> series.
/// </remarks>
public static class DecisionTreeValidationRules
{
    /// <summary>The tree names a root node it does not contain, so no walk can start.</summary>
    public const string RootMustExist = "TEMPEST-EID-001";

    /// <summary>A node neither asks a question nor states an outcome, or does both.</summary>
    public const string NodeMustAskOrConclude = "TEMPEST-EID-002";

    /// <summary>A branch leads to a node the tree does not contain.</summary>
    public const string BranchTargetMustExist = "TEMPEST-EID-003";

    /// <summary>Two nodes share one node id, so a branch naming it is ambiguous.</summary>
    public const string DuplicateNodeId = "TEMPEST-EID-004";

    /// <summary>Two branches out of one node share a label, so a recorded path cannot be read back unambiguously.</summary>
    public const string DuplicateBranchLabel = "TEMPEST-EID-005";

    /// <summary>A node declares a default branch label no branch out of it carries.</summary>
    public const string DefaultBranchMustExist = "TEMPEST-EID-006";

    /// <summary>The tree contains a node no walk can reach.</summary>
    public const string UnreachableNode = "TEMPEST-EID-007";

    /// <summary>The tree can loop, so a walk may never conclude.</summary>
    public const string TreeMustNotCycle = "TEMPEST-EID-008";

    /// <summary>The tree contains no terminal node, so no walk can ever conclude.</summary>
    public const string TreeMustReachAnOutcome = "TEMPEST-EID-009";

    /// <summary>A decision node declares no default, so a subject matching no branch produces no answer.</summary>
    public const string NodeShouldDeclareADefault = "TEMPEST-EID-010";

    /// <summary>The tree records no rationale, so a later engineer cannot tell why it decides the way it does.</summary>
    public const string RationaleShouldBeRecorded = "TEMPEST-EID-011";

    /// <summary>An outcome names a candidate process family `A7` does not recognise.</summary>
    public const string UnknownProcessFamily = "TEMPEST-EID-012";

    /// <summary>Two trees share one tree code.</summary>
    public const string DuplicateTreeCode = "TEMPEST-EID-013";

    /// <summary>The tree names a subject kind no reference library produces.</summary>
    public const string UnknownSubjectKind = "TEMPEST-EID-014";
}

/// <summary>Governance of decision trees themselves — whether an authored tree is fit to be released.</summary>
/// <remarks>
/// <b>A structurally broken tree must never be released.</b> A tree with a
/// dangling branch, a cycle or no terminal node does not produce a wrong
/// answer — it produces no answer, halfway through a decision an engineer
/// was relying on. Every one of those is an error here, so such a tree
/// cannot reach Validated and therefore cannot reach Released.
/// </remarks>
public interface IDecisionTreeValidationService : IReferenceValidationService<DecisionTree>
{
}

/// <summary>The concrete <see cref="IDecisionTreeValidationService"/> implementation.</summary>
public sealed class DecisionTreeValidationService : ReferenceValidationService<DecisionTree>, IDecisionTreeValidationService
{
    private readonly IReadOnlySet<string> _knownProcessFamilies;
    private readonly IReadOnlySet<string> _knownSubjectKinds;

    /// <summary>Initialises a new instance of the <see cref="DecisionTreeValidationService"/> class.</summary>
    /// <param name="catalog">The tree library whose records this service validates.</param>
    /// <param name="standardResolver">Resolves a cited standard against `A2`. Optional.</param>
    public DecisionTreeValidationService(IDecisionTreeCatalog catalog, IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog: null, standardResolver)
    {
        _knownProcessFamilies = Enum.GetNames<Manufacturing.ProcessFamily>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        _knownSubjectKinds = AssessmentSubjectKinds.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        DecisionTree definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateStructure(definition, errors, warnings);
        EvaluateNodes(definition, errors, warnings);
        EvaluateOutcomes(definition, warnings);

        if (definition.SubjectKind is { } kind && !_knownSubjectKinds.Contains(kind))
            warnings.Add(Diagnostic(
                DecisionTreeValidationRules.UnknownSubjectKind,
                $"Tree '{definition.Code}' is written for subject kind '{kind}', which no reference library produces. "
                + $"The kinds in use are: {string.Join(", ", AssessmentSubjectKinds.All)}."));

        if (string.IsNullOrWhiteSpace(definition.Rationale))
            warnings.Add(Diagnostic(
                DecisionTreeValidationRules.RationaleShouldBeRecorded,
                $"Tree '{definition.Code}' records no rationale. A tree nobody can justify is a tree nobody can safely revise."));

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<DecisionTree> record,
        IReadOnlyList<IReferenceRecord<DecisionTree>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var key = record.Definition.CodeKey;
        var others = library ?? await Catalog.ListAsync(cancellationToken).ConfigureAwait(false);

        var collisions = others
            .Where(other => !string.Equals(other.Id, record.Id, StringComparison.Ordinal))
            .Where(other => string.Equals(other.Definition.CodeKey, key, StringComparison.Ordinal))
            .Select(other => other.Id)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                DecisionTreeValidationRules.DuplicateTreeCode,
                $"Tree code '{record.Definition.Code}' is also registered as: {string.Join(", ", collisions)}."));
    }

    private static void EvaluateStructure(DecisionTree tree, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (tree.Root is null)
            errors.Add(Diagnostic(
                DecisionTreeValidationRules.RootMustExist,
                $"Tree '{tree.Code}' names root node '{tree.RootNodeId}', which it does not contain. No walk can start."));

        foreach (var dangling in tree.DanglingTargets)
            errors.Add(Diagnostic(
                DecisionTreeValidationRules.BranchTargetMustExist,
                $"Tree '{tree.Code}' has a branch leading to node '{dangling}', which it does not contain. "
                + "A walk reaching that branch stops halfway through a decision."));

        var duplicateIds = tree.Nodes
            .GroupBy(n => n.NodeId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicate in duplicateIds)
            errors.Add(Diagnostic(
                DecisionTreeValidationRules.DuplicateNodeId,
                $"Tree '{tree.Code}' declares node '{duplicate}' more than once, so a branch naming it is ambiguous."));

        if (tree.Nodes.Count > 0 && !tree.Nodes.Any(n => n.IsTerminal))
            errors.Add(Diagnostic(
                DecisionTreeValidationRules.TreeMustReachAnOutcome,
                $"Tree '{tree.Code}' contains no terminal node, so no walk can ever conclude."));

        if (HasCycle(tree))
            errors.Add(Diagnostic(
                DecisionTreeValidationRules.TreeMustNotCycle,
                $"Tree '{tree.Code}' can return to a node it has already visited. A walk that loops never concludes."));

        foreach (var unreachable in tree.UnreachableNodeIds)
            warnings.Add(Diagnostic(
                DecisionTreeValidationRules.UnreachableNode,
                $"Tree '{tree.Code}' contains node '{unreachable}', which no branch leads to. "
                + "It is either dead weight or a branch somebody forgot to connect."));
    }

    private static void EvaluateNodes(DecisionTree tree, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        foreach (var node in tree.Nodes)
        {
            if (!node.IsWellFormed)
                errors.Add(Diagnostic(
                    DecisionTreeValidationRules.NodeMustAskOrConclude,
                    $"Node '{node.NodeId}' in tree '{tree.Code}' "
                    + (node.Branches.Count > 0
                        ? "both asks a question and states an outcome, so where the walk stops is ambiguous."
                        : "neither asks a question nor states an outcome, so a walk reaching it has nowhere to go.")));

            var duplicateLabels = node.Branches
                .GroupBy(b => b.Label, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            foreach (var label in duplicateLabels)
                errors.Add(Diagnostic(
                    DecisionTreeValidationRules.DuplicateBranchLabel,
                    $"Node '{node.NodeId}' in tree '{tree.Code}' has more than one branch labelled '{label}', "
                    + "so a recorded path through it cannot be read back unambiguously."));

            if (node.DefaultBranchLabel is { } defaultLabel
                && !node.Branches.Any(b => string.Equals(b.Label, defaultLabel, StringComparison.Ordinal)))
                errors.Add(Diagnostic(
                    DecisionTreeValidationRules.DefaultBranchMustExist,
                    $"Node '{node.NodeId}' in tree '{tree.Code}' declares default branch '{defaultLabel}', which it does not have."));

            if (node.Branches.Count > 0 && node.DefaultBranchLabel is null)
                warnings.Add(Diagnostic(
                    DecisionTreeValidationRules.NodeShouldDeclareADefault,
                    $"Node '{node.NodeId}' in tree '{tree.Code}' declares no default branch. "
                    + "A subject matching none of its branches produces no answer, which may be the intent — "
                    + "an uncovered case is worth reporting — but is worth confirming."));
        }
    }

    private void EvaluateOutcomes(DecisionTree tree, List<IValidationDiagnostic> warnings)
    {
        foreach (var node in tree.Nodes.Where(n => n.Outcome is not null))
        {
            foreach (var family in node.Outcome!.CandidateProcessFamilies)
            {
                if (!_knownProcessFamilies.Contains(family))
                    warnings.Add(Diagnostic(
                        DecisionTreeValidationRules.UnknownProcessFamily,
                        $"Node '{node.NodeId}' in tree '{tree.Code}' names candidate process family '{family}', "
                        + "which `A7` does not recognise. The outcome will point at nothing findable."));
            }
        }
    }

    private static bool HasCycle(DecisionTree tree)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var settled = new HashSet<string>(StringComparer.Ordinal);

        return tree.Nodes.Any(node => Visit(node.NodeId));

        bool Visit(string nodeId)
        {
            if (settled.Contains(nodeId))
                return false;

            if (!visiting.Add(nodeId))
                return true;

            foreach (var branch in tree.FindNode(nodeId)?.Branches ?? [])
            {
                if (Visit(branch.TargetNodeId))
                    return true;
            }

            visiting.Remove(nodeId);
            settled.Add(nodeId);
            return false;
        }
    }
}
