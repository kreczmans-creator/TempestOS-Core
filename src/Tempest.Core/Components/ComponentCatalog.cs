using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Components;

/// <summary>The concrete <see cref="IComponentCatalog"/> implementation.</summary>
/// <remarks>
/// Everything about storing, revising, governing and superseding a
/// component record comes from
/// <see cref="ReferenceDataCatalog{TDefinition}"/>, shared with every Group
/// A library (`ADR-0126`). What this class adds is component-specific: the
/// identity uniqueness key, the lookups that read it, and the component
/// query.
/// </remarks>
public sealed class ComponentCatalog : ReferenceDataCatalog<ComponentDefinition>, IComponentCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every component record's own backing document carries.</summary>
    public const string ComponentDocumentKind = "ComponentReference";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>componentId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "Components.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each component identity key to the <c>componentId</c> holding it.</summary>
    public const string IdentityIndexCollection = "Components.IdentityIndex";

    /// <summary>
    /// Initialises a new instance of the <see cref="ComponentCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own component records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ComponentCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "Components";

    /// <inheritdoc />
    public override string DocumentKind => ComponentDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => IdentityIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<ComponentDefinition>?> FindByDesignationAsync(
        string designation,
        string? manufacturer = null,
        CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(ComponentDefinition.IdentityKeyFor(manufacturer, partNumber: null, designation), cancellationToken);

    /// <inheritdoc />
    public Task<IReferenceRecord<ComponentDefinition>?> FindByPartNumberAsync(
        string manufacturer,
        string partNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);

        return FindBySecondaryKeyAsync(ComponentDefinition.IdentityKeyFor(manufacturer, partNumber, designation: partNumber), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<ComponentDefinition>>> SearchAsync(ComponentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => ComponentQueryEvaluator.Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(ComponentDefinition definition) => definition.IdentityKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(ComponentDefinition definition) =>
        definition.ManufacturerPartNumber is null
            ? $"Designation '{definition.Designation}'"
            : $"Manufacturer '{definition.Manufacturer}' part number '{definition.ManufacturerPartNumber}'";
}
