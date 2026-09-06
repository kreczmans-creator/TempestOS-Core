using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Fasteners;

/// <summary>The concrete <see cref="IFastenerCatalog"/> implementation.</summary>
/// <remarks>
/// Everything about storing, revising, governing and superseding a fastener
/// record comes from <see cref="ReferenceDataCatalog{TDefinition}"/>, shared
/// with every Group A library (`ADR-0126`). What this class adds is
/// fastener-specific: the identity uniqueness key, the lookups that read
/// it, and the fastener query.
/// </remarks>
public sealed class FastenerCatalog : ReferenceDataCatalog<FastenerDefinition>, IFastenerCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every fastener record's own backing document carries.</summary>
    public const string FastenerDocumentKind = "FastenerReference";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>fastenerId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "Fasteners.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each fastener identity key to the <c>fastenerId</c> holding it.</summary>
    public const string IdentityIndexCollection = "Fasteners.IdentityIndex";

    /// <summary>
    /// Initialises a new instance of the <see cref="FastenerCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own fastener records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public FastenerCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "Fasteners";

    /// <inheritdoc />
    public override string DocumentKind => FastenerDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => IdentityIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<FastenerDefinition>?> FindByDesignationAsync(
        string designation,
        string? manufacturer = null,
        CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(FastenerDefinition.IdentityKeyFor(manufacturer, partNumber: null, designation), cancellationToken);

    /// <inheritdoc />
    public Task<IReferenceRecord<FastenerDefinition>?> FindByPartNumberAsync(
        string manufacturer,
        string partNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);

        // The designation is not part of a part-number key, but
        // IdentityKeyFor requires one to build any key at all; a
        // placeholder is passed and discarded by the part-number branch.
        return FindBySecondaryKeyAsync(FastenerDefinition.IdentityKeyFor(manufacturer, partNumber, designation: partNumber), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<FastenerDefinition>>> SearchAsync(FastenerQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => FastenerQueryEvaluator.Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(FastenerDefinition definition) => definition.IdentityKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(FastenerDefinition definition) =>
        definition.ManufacturerPartNumber is null
            ? $"Designation '{definition.Designation}'"
            : $"Manufacturer '{definition.Manufacturer}' part number '{definition.ManufacturerPartNumber}'";
}
