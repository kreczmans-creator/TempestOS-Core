using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Decisions;

/// <summary>A deterministic filter over the decision-tree library.</summary>
public sealed record DecisionTreeQuery
{
    /// <summary>Matches any tree whose code contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CodeContains { get; init; }

    /// <summary>Matches any tree whose name or purpose contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches trees written for this subject kind, and trees not tied to one. <see langword="null"/> to match any.</summary>
    public string? SubjectKind { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>Matches trees by whether they are structurally sound enough to walk. <see langword="null"/> to match any.</summary>
    public bool? IsWalkable { get; init; }
}

/// <summary>The library of governed decision trees.</summary>
/// <remarks>
/// A decision tree is an authored, sourced, reviewed, revisioned record —
/// the same shape a rule is, and the same shape
/// <see cref="ReferenceDataCatalog{TDefinition}"/> already governs
/// (`ADR-0128`). A walk pins the tree's revision, so a decision taken
/// against one version of a tree can be reconstructed after the tree has
/// been revised.
/// </remarks>
public interface IDecisionTreeCatalog : IReferenceDataCatalog<DecisionTree>
{
    /// <summary>Returns the tree registered under <paramref name="code"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<DecisionTree>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Every registered tree matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<DecisionTree>>> SearchAsync(DecisionTreeQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IDecisionTreeCatalog"/> implementation.</summary>
/// <remarks>
/// Storage, revision, lifecycle and supersession all come from
/// <see cref="ReferenceDataCatalog{TDefinition}"/>. What this class adds is
/// the tree-code uniqueness key and the tree query.
/// </remarks>
public sealed class DecisionTreeCatalog : ReferenceDataCatalog<DecisionTree>, IDecisionTreeCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every decision-tree record's own backing document carries.</summary>
    public const string DecisionTreeDocumentKind = "EngineeringDecisionTree";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>treeId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "DecisionTrees.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each tree code to the <c>treeId</c> holding it.</summary>
    public const string CodeIndexCollection = "DecisionTrees.CodeIndex";

    /// <summary>Initialises a new instance of the <see cref="DecisionTreeCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own tree records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public DecisionTreeCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "DecisionTrees";

    /// <inheritdoc />
    public override string DocumentKind => DecisionTreeDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => CodeIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<DecisionTree>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(DecisionTree.CodeKeyFor(code), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<DecisionTree>>> SearchAsync(
        DecisionTreeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(DecisionTree definition) => definition.CodeKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(DecisionTree definition) => $"Decision tree code '{definition.Code}'";

    private static bool Matches(IReferenceRecord<DecisionTree> record, DecisionTreeQuery query)
    {
        var tree = record.Definition;

        if (query.CodeContains is not null && !tree.Code.Contains(query.CodeContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.TextContains is { } text
            && !tree.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !tree.Purpose.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        // A tree not tied to a subject kind is usable with any, so it
        // matches a filter for a particular one.
        if (query.SubjectKind is { } kind
            && tree.SubjectKind is not null
            && !string.Equals(tree.SubjectKind, kind, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        if (query.IsWalkable is { } walkable)
        {
            var isWalkable = tree.Root is not null
                && tree.DanglingTargets.Count == 0
                && tree.Nodes.All(n => n.IsWellFormed);

            if (isWalkable != walkable)
                return false;
        }

        return true;
    }
}
