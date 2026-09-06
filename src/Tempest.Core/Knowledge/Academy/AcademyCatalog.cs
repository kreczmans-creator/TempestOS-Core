using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Knowledge.Academy;

/// <summary>A deterministic filter over the Academy.</summary>
public sealed record AcademyQuery
{
    /// <summary>Matches any node whose reference, title or summary contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<AcademyNodeKind> Kinds { get; init; } = [];

    /// <summary>Matches only nodes directly beneath this one. <see langword="null"/> to match any.</summary>
    public string? ParentReference { get; init; }

    /// <summary>Matches nodes applying to this enquiry. <see langword="null"/> to leave every dimension open.</summary>
    public KnowledgeEnquiry? Enquiry { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The engineering Academy.</summary>
public interface IAcademyCatalog : IReferenceDataCatalog<AcademyNode>
{
    /// <summary>Returns the node registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<AcademyNode>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered node matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<AcademyNode>>> SearchAsync(AcademyQuery query, CancellationToken cancellationToken = default);

    /// <summary>The nodes directly beneath <paramref name="parentReference"/>. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="parentReference"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<AcademyNode>>> FindChildrenAsync(string parentReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// The path from the top of the hierarchy down to
    /// <paramref name="reference"/>, broadest first.
    /// </summary>
    /// <remarks>
    /// Stops at a node whose parent the library does not hold, and at a
    /// cycle, rather than looping. A malformed hierarchy is reported by
    /// validation, and a read must not hang because of one.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<AcademyNode>> FindPathToAsync(string reference, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IAcademyCatalog"/> implementation.</summary>
public sealed class AcademyCatalog : ReferenceDataCatalog<AcademyNode>, IAcademyCatalog
{
    /// <summary>How deep the hierarchy may be walked before a read gives up, guarding against a cycle.</summary>
    public const int MaximumPathDepth = 16;

    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every node's own backing document carries.</summary>
    public const string AcademyNodeDocumentKind = "KnowledgeAcademyNode";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string AcademyLibraryName = "KnowledgeAcademy";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>nodeId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "KnowledgeAcademy.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each node reference to the <c>nodeId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "KnowledgeAcademy.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="AcademyCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own nodes are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public AcademyCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => AcademyLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => AcademyNodeDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<AcademyNode>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(AcademyNode.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<AcademyNode>>> SearchAsync(
        AcademyQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<AcademyNode>>> FindChildrenAsync(
        string parentReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentReference);

        var parent = parentReference.Trim();

        var children = await FilterAsync(
            record => string.Equals(record.Definition.ParentReference, parent, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);

        return children
            .OrderBy(r => AcademyNodeKinds.Depth(r.Definition.Kind))
            .ThenBy(r => r.Definition.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AcademyNode>> FindPathToAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        var path = new List<AcademyNode>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = reference.Trim();

        while (!string.IsNullOrWhiteSpace(current) && seen.Add(current) && path.Count < MaximumPathDepth)
        {
            var record = await FindByReferenceAsync(current, cancellationToken).ConfigureAwait(false);

            if (record is null)
                break;

            path.Add(record.Definition);
            current = record.Definition.ParentReference ?? string.Empty;
        }

        path.Reverse();

        return path;
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(AcademyNode definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(AcademyNode definition) => $"Academy node reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<AcademyNode> record, AcademyQuery query)
    {
        var node = record.Definition;

        if (query.TextContains is { } text
            && !node.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !node.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !(node.Summary?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        if (query.Kinds.Count > 0 && !query.Kinds.Contains(node.Kind))
            return false;

        if (query.ParentReference is { } parent
            && !string.Equals(node.ParentReference, parent, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Enquiry is { } enquiry && !node.Applicability.AppliesTo(enquiry))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes F2's validation service reports.</summary>
public static class AcademyValidationRules
{
    /// <summary>The node names a parent the Academy does not hold.</summary>
    public const string ParentMustResolve = "TEMPEST-KNA-001";

    /// <summary>The node sits beneath a parent that cannot contain it.</summary>
    public const string InvalidHierarchyPlacement = "TEMPEST-KNA-002";

    /// <summary>The node is its own ancestor.</summary>
    public const string HierarchyContainsACycle = "TEMPEST-KNA-003";

    /// <summary>The node names a prerequisite the Academy does not hold.</summary>
    public const string PrerequisiteMustResolve = "TEMPEST-KNA-004";

    /// <summary>The node is its own prerequisite, directly or through a chain.</summary>
    public const string PrerequisiteCycle = "TEMPEST-KNA-005";

    /// <summary>A node a learner sits down to states no learning outcome.</summary>
    public const string DeliverableNodeHasNoOutcomes = "TEMPEST-KNA-006";

    /// <summary>An outcome nothing assesses.</summary>
    public const string OutcomeIsUnassessed = "TEMPEST-KNA-007";

    /// <summary>An activity serves an outcome the node does not state.</summary>
    public const string ActivityOutcomeUnresolved = "TEMPEST-KNA-008";

    /// <summary>Two outcomes or activities share one reference.</summary>
    public const string DuplicateAcademyReference = "TEMPEST-KNA-009";

    /// <summary>A node a learner sits down to has no activities.</summary>
    public const string DeliverableNodeHasNoActivities = "TEMPEST-KNA-010";

    /// <summary>A prerequisite is pitched harder than the node requiring it.</summary>
    public const string PrerequisiteIsHarderThanTheNode = "TEMPEST-KNA-011";

    /// <summary>An activity cites a worked example or challenge the libraries do not hold.</summary>
    public const string CitedKnowledgeMustResolve = "TEMPEST-KNA-012";
}

/// <summary>Governance of the Academy itself.</summary>
public interface IAcademyValidationService : IReferenceValidationService<AcademyNode>
{
}

/// <summary>The concrete <see cref="IAcademyValidationService"/> implementation.</summary>
/// <remarks>
/// The findings are about whether the structure teaches anything. An
/// outcome nothing assesses is a promise nobody checks; a prerequisite
/// harder than the thing requiring it is a curriculum nobody can follow;
/// a cycle is a curriculum nobody can start.
/// </remarks>
public sealed class AcademyValidationService : ReferenceValidationService<AcademyNode>, IAcademyValidationService
{
    private readonly IAcademyCatalog _academy;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="AcademyValidationService"/> class.</summary>
    /// <param name="catalog">The Academy whose nodes this service validates.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public AcademyValidationService(IAcademyCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _academy = catalog;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        AcademyNode definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Academy node '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        KnowledgeGovernanceValidation.EvaluateDuplicateReferences(
            definition.Outcomes.Select(o => o.Reference),
            $"{subject} has two outcomes sharing the reference",
            errors);

        KnowledgeGovernanceValidation.EvaluateDuplicateReferences(
            definition.Activities.Select(a => a.Reference),
            $"{subject} has two activities sharing the reference",
            errors);

        if (definition.IsDeliverable && definition.Outcomes.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                AcademyValidationRules.DeliverableNodeHasNoOutcomes,
                $"{subject} is something a learner sits down to and states no learning outcome, so nobody can say what "
                + "it is for."));

        if (definition.IsDeliverable && definition.Activities.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                AcademyValidationRules.DeliverableNodeHasNoActivities,
                $"{subject} is something a learner sits down to and gives them nothing to do."));

        foreach (var outcome in definition.UnassessedOutcomes)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                AcademyValidationRules.OutcomeIsUnassessed,
                $"{subject} promises outcome '{outcome.Reference}' and nothing tests it."));

        var outcomeReferences = definition.Outcomes.Select(o => o.Reference).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (activity, cited) in definition.Activities
                     .SelectMany(a => a.OutcomeReferences.Select(r => (Activity: a, Cited: r)))
                     .Where(pair => !outcomeReferences.Contains(pair.Cited)))
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                AcademyValidationRules.ActivityOutcomeUnresolved,
                $"{subject} activity '{activity.Reference}' serves outcome '{cited}', which the node does not state."));

        await EvaluateHierarchyAsync(definition, subject, errors, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluatePrerequisitesAsync(definition, subject, errors, warnings, cancellationToken).ConfigureAwait(false);

        KnowledgeGovernanceValidation.Evaluate(
            definition.Provenance,
            definition.Applicability,
            subject,
            today,
            errors,
            warnings);
    }

    private async Task EvaluateHierarchyAsync(
        AcademyNode definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (definition.ParentReference is not { } parentReference)
            return;

        if (string.Equals(parentReference, definition.Reference, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                AcademyValidationRules.HierarchyContainsACycle,
                $"{subject} names itself as its own parent."));
            return;
        }

        var parent = await _academy.FindByReferenceAsync(parentReference, cancellationToken).ConfigureAwait(false);

        if (parent is null)
        {
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                AcademyValidationRules.ParentMustResolve,
                $"{subject} sits beneath '{parentReference}', which the Academy does not hold."));
            return;
        }

        if (!parent.Definition.CanContain(definition))
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                AcademyValidationRules.InvalidHierarchyPlacement,
                $"{subject} is a {definition.Kind} sitting beneath '{parentReference}', which is a "
                + $"{parent.Definition.Kind}. A {parent.Definition.Kind} cannot contain a {definition.Kind}."));

        var path = await _academy.FindPathToAsync(parentReference, cancellationToken).ConfigureAwait(false);

        if (path.Any(n => string.Equals(n.Reference, definition.Reference, StringComparison.OrdinalIgnoreCase)))
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                AcademyValidationRules.HierarchyContainsACycle,
                $"{subject} appears in its own chain of ancestors."));
    }

    private async Task EvaluatePrerequisitesAsync(
        AcademyNode definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var prerequisiteReference in definition.PrerequisiteReferences)
        {
            if (string.Equals(prerequisiteReference, definition.Reference, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                    AcademyValidationRules.PrerequisiteCycle,
                    $"{subject} lists itself as its own prerequisite."));
                continue;
            }

            var prerequisite = await _academy.FindByReferenceAsync(prerequisiteReference, cancellationToken).ConfigureAwait(false);

            if (prerequisite is null)
            {
                warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                    AcademyValidationRules.PrerequisiteMustResolve,
                    $"{subject} requires '{prerequisiteReference}' first, which the Academy does not hold."));
                continue;
            }

            if (prerequisite.Definition.PrerequisiteReferences.Contains(definition.Reference, StringComparer.OrdinalIgnoreCase))
                errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                    AcademyValidationRules.PrerequisiteCycle,
                    $"{subject} and '{prerequisiteReference}' each require the other first. A learner can start neither."));

            if (definition.Applicability.Level != KnowledgeLevel.Unspecified
                && prerequisite.Definition.Applicability.Level != KnowledgeLevel.Unspecified
                && prerequisite.Definition.Applicability.Level > definition.Applicability.Level)
                warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                    AcademyValidationRules.PrerequisiteIsHarderThanTheNode,
                    $"{subject} is pitched at {definition.Applicability.Level} but requires "
                    + $"'{prerequisiteReference}' first, which is pitched at {prerequisite.Definition.Applicability.Level}."));
        }
    }
}
