using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Decisions;

/// <summary>
/// One branch out of a decision node: the condition that selects it, and
/// where it leads.
/// </summary>
/// <remarks>
/// <para>
/// Branches are evaluated in the order they are declared and the first
/// whose condition is satisfied is taken, so a tree's behaviour is fixed
/// by its own structure rather than by evaluation order chance. Where no
/// branch is satisfied the node's
/// <see cref="DecisionNode.DefaultBranchLabel"/> decides, and where that is
/// absent the walk stops with an explicit "no branch applies" outcome
/// rather than falling through silently.
/// </para>
/// <para>
/// A branch whose condition could not be <em>evaluated</em> — missing
/// data, an unresolved constant — is not skipped as though it had failed.
/// The walk stops there and reports the gap, because taking a later branch
/// would mean deciding on the basis that an earlier one did not apply
/// when nobody knows whether it did.
/// </para>
/// </remarks>
/// <param name="Label">A short name for the branch, used in the path a walk reports. Required.</param>
/// <param name="Condition">The condition that selects this branch. Required.</param>
/// <param name="TargetNodeId">The node this branch leads to. Required.</param>
/// <param name="Rationale">Why this branch exists, in the author's own words. <see langword="null"/> if not recorded.</param>
public sealed record DecisionBranch(
    string Label,
    RuleExpression Condition,
    string TargetNodeId,
    string? Rationale = null)
{
    /// <summary>A short name for the branch.</summary>
    public string Label { get; } = string.IsNullOrWhiteSpace(Label)
        ? throw new ArgumentException("A decision branch must be labelled, or a path through the tree cannot be read.", nameof(Label))
        : Label.Trim();

    /// <summary>The condition that selects this branch.</summary>
    public RuleExpression Condition { get; } = Condition ?? throw new ArgumentNullException(nameof(Condition));

    /// <summary>The node this branch leads to.</summary>
    public string TargetNodeId { get; } = string.IsNullOrWhiteSpace(TargetNodeId)
        ? throw new ArgumentException("A decision branch must lead somewhere.", nameof(TargetNodeId))
        : TargetNodeId.Trim();
}

/// <summary>What a terminal node concludes.</summary>
/// <remarks>
/// <b>Candidates, never a choice.</b> A terminal node names the processes
/// that reached it and says why; it does not pick one. Which of several
/// viable processes to use depends on cost, lead time, tooling already
/// owned and supplier relationships — none of which a decision tree over
/// reference data holds.
/// </remarks>
/// <param name="Summary">What this outcome means, in plain engineering language. Required.</param>
/// <param name="CandidateProcessFamilies">The `A7` process families this outcome points at, by name. Never <see langword="null"/>; empty where the outcome is a dead end rather than a candidate set.</param>
/// <param name="Advice">What the engineer should do next. <see langword="null"/> if the summary says it.</param>
/// <param name="RequiresHumanDecision">Whether reaching this outcome always needs an engineer's decision. Defaults to <see langword="true"/>, because process choice is one.</param>
public sealed record DecisionOutcome(
    string Summary,
    IReadOnlyList<string>? CandidateProcessFamilies = null,
    string? Advice = null,
    bool RequiresHumanDecision = true)
{
    /// <summary>What this outcome means.</summary>
    public string Summary { get; } = string.IsNullOrWhiteSpace(Summary)
        ? throw new ArgumentException("A decision outcome must say what it means.", nameof(Summary))
        : Summary.Trim();

    /// <summary>The `A7` process families this outcome points at.</summary>
    public IReadOnlyList<string> CandidateProcessFamilies { get; init; } = CandidateProcessFamilies ?? [];
}

/// <summary>One node in a decision tree — either a question or an answer.</summary>
/// <remarks>
/// <para>
/// A node with branches is a question; a node with an outcome is an
/// answer. A node with both, or neither, is refused at construction:
/// a question that also concludes is ambiguous about where the walk
/// stops, and a node that does neither is a hole in the tree.
/// </para>
/// <para>
/// Multi-way decisions are the ordinary case — a node may declare as many
/// branches as the engineering question has answers — and a binary
/// decision is simply a node with two.
/// </para>
/// </remarks>
public sealed record DecisionNode
{
    /// <summary>The node's own identity within its tree. Required.</summary>
    public required string NodeId { get; init; }

    /// <summary>The question this node asks, or the conclusion it states. Required.</summary>
    public required string Question { get; init; }

    /// <summary>The branches out of this node, in evaluation order. Never <see langword="null"/>; empty for a terminal node.</summary>
    public IReadOnlyList<DecisionBranch> Branches { get; init; } = [];

    /// <summary>What this node concludes. <see langword="null"/> for a decision node.</summary>
    public DecisionOutcome? Outcome { get; init; }

    /// <summary>
    /// The branch taken when no branch's condition is satisfied.
    /// <see langword="null"/> where no branch applying is itself a result
    /// worth reporting rather than a case to absorb.
    /// </summary>
    public string? DefaultBranchLabel { get; init; }

    /// <summary>Why this node asks what it asks. <see langword="null"/> if not recorded.</summary>
    public string? Rationale { get; init; }

    /// <summary>Whether this node concludes rather than asks.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsTerminal => Outcome is not null;

    /// <summary>Whether this node is well-formed — exactly one of "asks" and "concludes".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsWellFormed => (Branches.Count > 0) != (Outcome is not null);
}

/// <summary>
/// A structured, deterministic manufacturing decision tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a tree and not nested conditionals in application code.</b> An
/// engineering decision buried in <c>if</c> statements cannot be
/// inspected, reviewed, versioned, superseded, or explained back to the
/// engineer who is affected by it — and nobody can tell whether it changed
/// between two releases. Held as data, all of that follows, and the tree
/// itself becomes something an engineer can read and dispute.
/// </para>
/// <para>
/// <b>A tree definition is a governed record</b>, held by
/// <see cref="DecisionTreeCatalog"/> on `P01`'s shared reference-data
/// layer, exactly as a rule is: authored, sourced, reviewed, released,
/// immutable once released, superseded rather than edited. A walk through
/// a released tree pins the tree's own revision, so a decision taken
/// months ago can be reconstructed against the tree as it then stood.
/// </para>
/// <para>
/// <b>What a tree does not do.</b> It does not plan a route, sequence
/// operations, estimate cost or cycle time, or select a supplier. It
/// narrows to candidate process families from what the reference libraries
/// record, and stops.
/// </para>
/// </remarks>
public sealed record DecisionTree
{
    /// <summary>The tree's own engineering identifier, as an engineer would cite it. Required, and unique across the library.</summary>
    public required string Code { get; init; }

    /// <summary>A short name for the tree. Required.</summary>
    public required string Name { get; init; }

    /// <summary>What question the tree answers, in plain engineering language. Required.</summary>
    public required string Purpose { get; init; }

    /// <summary>The node a walk starts at. Required.</summary>
    public required string RootNodeId { get; init; }

    /// <summary>Every node in the tree. Never <see langword="null"/>.</summary>
    public IReadOnlyList<DecisionNode> Nodes { get; init; } = [];

    /// <summary>The subject kind a walk evaluates conditions against. <see langword="null"/> where the tree is not tied to one.</summary>
    public string? SubjectKind { get; init; }

    /// <summary>Why the tree decides the way it does. <see langword="null"/> if not recorded.</summary>
    public string? Rationale { get; init; }

    /// <summary>The standards the tree's authority derives from. Never <see langword="null"/>; empty if none.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>The author's own classification wording, verbatim. <see langword="null"/> if none.</summary>
    public string? SourceClassification { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>Returns the node with <paramref name="nodeId"/>, or <see langword="null"/> if the tree has none.</summary>
    public DecisionNode? FindNode(string? nodeId) =>
        nodeId is null ? null : Nodes.FirstOrDefault(n => string.Equals(n.NodeId, nodeId, StringComparison.Ordinal));

    /// <summary>The root node, or <see langword="null"/> where the tree names a root it does not contain.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DecisionNode? Root => FindNode(RootNodeId);

    /// <summary>Every node no branch leads to, other than the root — the parts of the tree a walk can never reach.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> UnreachableNodeIds
    {
        get
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            Walk(RootNodeId, reachable);

            return Nodes.Select(n => n.NodeId).Where(id => !reachable.Contains(id)).ToList();

            void Walk(string? nodeId, HashSet<string> seen)
            {
                if (nodeId is null || !seen.Add(nodeId))
                    return;

                foreach (var branch in FindNode(nodeId)?.Branches ?? [])
                    Walk(branch.TargetNodeId, seen);
            }
        }
    }

    /// <summary>Every branch target that names a node the tree does not contain — a walk reaching one has nowhere to go.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> DanglingTargets =>
        Nodes.SelectMany(n => n.Branches)
            .Select(b => b.TargetNodeId)
            .Where(target => FindNode(target) is null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>The key tree-code uniqueness is enforced on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string CodeKey => CodeKeyFor(Code);

    /// <summary>Builds the uniqueness key from a code that is not (yet) a record — the lookup path.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    public static string CodeKeyFor(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return code.Trim().ToUpperInvariant();
    }
}
