using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Bearings;

/// <summary>The concrete <see cref="IBearingCatalog"/> implementation.</summary>
/// <remarks>
/// <para>
/// Everything about storing, revising, governing and superseding a bearing
/// record comes from <see cref="ReferenceDataCatalog{TDefinition}"/>,
/// shared with every Group A library (`ADR-0126`): a typed index over
/// <see cref="IEngineeringDocumentStore"/> with <c>Kind =
/// "BearingReference"</c>, plus an <see cref="IPersistenceStore"/> index of
/// its own, plus the provenance-gated lifecycle.
/// </para>
/// <para>
/// What this class adds is bearing-specific and nothing else: the
/// manufacturer-and-part-number uniqueness key the shared secondary index
/// enforces, the lookup that reads it, and the bearing query.
/// </para>
/// </remarks>
public sealed class BearingCatalog : ReferenceDataCatalog<BearingDefinition>, IBearingCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every bearing record's own backing document carries.</summary>
    public const string BearingDocumentKind = "BearingReference";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>bearingId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "Bearings.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each manufacturer-and-part-number key to the <c>bearingId</c> holding it.</summary>
    public const string PartNumberIndexCollection = "Bearings.PartNumberIndex";

    /// <summary>
    /// Initialises a new instance of the <see cref="BearingCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own bearing records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own two indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public BearingCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "Bearings";

    /// <inheritdoc />
    public override string DocumentKind => BearingDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => PartNumberIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<BearingDefinition>?> FindByPartNumberAsync(
        string manufacturer,
        string partNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manufacturer);
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);

        return FindBySecondaryKeyAsync(BearingIdentity.PartNumberKeyFor(manufacturer, partNumber), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<BearingDefinition>>> SearchAsync(BearingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => BearingQueryEvaluator.Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(BearingDefinition definition) => definition.Identity.PartNumberKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(BearingDefinition definition) =>
        $"Manufacturer '{definition.Identity.Manufacturer}' part number '{definition.Identity.ManufacturerPartNumber}'";
}
