using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Procurement;

/// <summary>A deterministic filter over the sourcing-requirement library.</summary>
public sealed record SourcingRequirementQuery
{
    /// <summary>Matches any requirement whose reference or subject contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches only requirements needed by this date or earlier. <see langword="null"/> for no ceiling.</summary>
    public DateOnly? RequiredBy { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>What the organisation needs sourced.</summary>
public interface ISourcingRequirementCatalog : IReferenceDataCatalog<SourcingRequirement>
{
    /// <summary>Returns the requirement registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<SourcingRequirement>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered requirement matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<SourcingRequirement>>> SearchAsync(SourcingRequirementQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ISourcingRequirementCatalog"/> implementation.</summary>
public sealed class SourcingRequirementCatalog : ReferenceDataCatalog<SourcingRequirement>, ISourcingRequirementCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every requirement's own backing document carries.</summary>
    public const string RequirementDocumentKind = "CommercialSourcingRequirement";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string RequirementLibraryName = "CommercialSourcingRequirements";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>requirementId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "CommercialSourcingRequirements.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each requirement reference to the <c>requirementId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "CommercialSourcingRequirements.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="SourcingRequirementCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own requirements are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public SourcingRequirementCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => RequirementLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => RequirementDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<SourcingRequirement>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(SourcingRequirement.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<SourcingRequirement>>> SearchAsync(
        SourcingRequirementQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(SourcingRequirement definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(SourcingRequirement definition) => $"Sourcing requirement reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<SourcingRequirement> record, SourcingRequirementQuery query)
    {
        var requirement = record.Definition;

        if (query.TextContains is { } text
            && !requirement.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !requirement.Subject.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.RequiredBy is { } by && (requirement.RequiredBy is not { } needed || needed > by))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>A deterministic filter over the sourcing-comparison library.</summary>
public sealed record SourcingComparisonQuery
{
    /// <summary>Matches any comparison whose reference or requirement reference contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches only comparisons against this requirement. <see langword="null"/> to match any.</summary>
    public string? RequirementReference { get; init; }

    /// <summary>Matches any of these decision states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<SourcingDecisionState> DecisionStates { get; init; } = [];

    /// <summary>Matches only comparisons naming this candidate, whether recommended, chosen or excluded. <see langword="null"/> to match any.</summary>
    public string? MentionsSupplierRecordId { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>What the organisation compared, and what it decided.</summary>
public interface ISourcingComparisonCatalog : IReferenceDataCatalog<SourcingComparison>
{
    /// <summary>Returns the comparison registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<SourcingComparison>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered comparison matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<SourcingComparison>>> SearchAsync(SourcingComparisonQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every comparison still waiting on a person, oldest first.
    /// </summary>
    /// <remarks>
    /// The queue the platform exists to surface rather than to work
    /// through: these are the decisions nobody has taken, and TempestOS
    /// will not take them.
    /// </remarks>
    Task<IReadOnlyList<IReferenceRecord<SourcingComparison>>> FindAwaitingDecisionAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ISourcingComparisonCatalog"/> implementation.</summary>
public sealed class SourcingComparisonCatalog : ReferenceDataCatalog<SourcingComparison>, ISourcingComparisonCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every comparison's own backing document carries.</summary>
    public const string ComparisonDocumentKind = "CommercialSourcingComparison";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string ComparisonLibraryName = "CommercialSourcingComparisons";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>comparisonId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "CommercialSourcingComparisons.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each comparison reference to the <c>comparisonId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "CommercialSourcingComparisons.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="SourcingComparisonCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own comparisons are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public SourcingComparisonCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => ComparisonLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => ComparisonDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<SourcingComparison>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(SourcingComparison.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<SourcingComparison>>> SearchAsync(
        SourcingComparisonQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<SourcingComparison>>> FindAwaitingDecisionAsync(CancellationToken cancellationToken = default)
    {
        var awaiting = await FilterAsync(
            record => record.Definition.DecisionState
                is SourcingDecisionState.AwaitingHumanDecision
                or SourcingDecisionState.MoreInformationRequested,
            cancellationToken).ConfigureAwait(false);

        return awaiting
            .OrderBy(r => r.Definition.PreparedOn ?? DateOnly.MaxValue)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(SourcingComparison definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(SourcingComparison definition) => $"Sourcing comparison reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<SourcingComparison> record, SourcingComparisonQuery query)
    {
        var comparison = record.Definition;

        if (query.TextContains is { } text
            && !comparison.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !comparison.RequirementReference.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.RequirementReference is { } requirement
            && !string.Equals(comparison.RequirementReference, requirement, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.DecisionStates.Count > 0 && !query.DecisionStates.Contains(comparison.DecisionState))
            return false;

        if (query.MentionsSupplierRecordId is { } supplier
            && !comparison.Candidates.Any(c => string.Equals(c.SupplierRecordId, supplier, StringComparison.Ordinal)))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}
