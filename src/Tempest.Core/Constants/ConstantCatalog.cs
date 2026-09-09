using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>The concrete <see cref="IConstantCatalog"/> implementation.</summary>
/// <remarks>
/// Everything about storing, revising, governing and superseding a
/// constant record comes from
/// <see cref="ReferenceDataCatalog{TDefinition}"/>, shared with every Group
/// A library (`ADR-0126`). What this class adds is constants-specific: the
/// symbol uniqueness key, the lookup that reads it, the constants query,
/// and the released-only seam.
/// </remarks>
public sealed class ConstantCatalog : ReferenceDataCatalog<ConstantDefinition>, IConstantCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every constant record's own backing document carries.</summary>
    public const string ConstantDocumentKind = "EngineeringConstant";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>constantId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "Constants.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each symbol to the <c>constantId</c> holding it.</summary>
    public const string SymbolIndexCollection = "Constants.SymbolIndex";

    /// <summary>
    /// Initialises a new instance of the <see cref="ConstantCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own constant records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ConstantCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "Constants";

    /// <inheritdoc />
    public override string DocumentKind => ConstantDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => SymbolIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<ConstantDefinition>?> FindBySymbolAsync(string symbol, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(ConstantDefinition.SymbolKeyFor(symbol), cancellationToken);

    /// <inheritdoc />
    public async Task<ReleasedConstant?> FindReleasedAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var record = await FindBySymbolAsync(symbol, cancellationToken).ConfigureAwait(false);

        // Not released is reported exactly as not registered. A consumer
        // must not be able to distinguish "there is a value here you may
        // not use" from "there is no value here", because the first
        // invites using it anyway.
        if (record is null || record.ValidationState != ReferenceValidationState.Released)
            return null;

        // A released record with no value would be a governance failure
        // upstream — validation refuses to let one reach Validated — but
        // handing back a null value would push that failure into a
        // calculation, so it is reported as absent here too.
        if (record.Definition.Value is not { } value)
            return null;

        return new ReleasedConstant(
            record.Definition.Symbol,
            record.Definition.Name,
            value,
            record.Id,
            record.RevisionNumber);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<ConstantDefinition>>> SearchAsync(ConstantQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => ConstantQueryEvaluator.Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(ConstantDefinition definition) => definition.SymbolKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(ConstantDefinition definition) => $"Symbol '{definition.Symbol}'";
}
