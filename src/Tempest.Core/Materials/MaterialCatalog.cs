using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Materials;

/// <summary>The concrete <see cref="IMaterialCatalog"/> implementation.</summary>
/// <remarks>
/// <para>
/// Everything about storing, revising, governing and superseding a material
/// record comes from <see cref="ReferenceDataCatalog{TDefinition}"/>,
/// shared with every Group A library (`ADR-0126`). `ADR-0055` established
/// this pattern here first — a typed index over
/// <see cref="IEngineeringDocumentStore"/> with <c>Kind =
/// "MaterialSpecification"</c> plus an <see cref="IPersistenceStore"/> index
/// of its own — and the shared base is that same decision, generalised
/// rather than replaced. The document kind is unchanged, so records written
/// before the generalisation are still this library's own.
/// </para>
/// <para>
/// What this class adds is materials-specific: the designation uniqueness
/// key, the lookup that reads it, and the material query.
/// </para>
/// </remarks>
public sealed class MaterialCatalog : ReferenceDataCatalog<MaterialDefinition>, IMaterialCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every material record's own backing document carries.</summary>
    public const string MaterialSpecificationDocumentKind = "MaterialSpecification";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>materialId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "Materials.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each supplier-and-designation key to the <c>materialId</c> holding it.</summary>
    public const string DesignationIndexCollection = "Materials.DesignationIndex";

    /// <summary>
    /// Initialises a new instance of the <see cref="MaterialCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own material records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public MaterialCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "Materials";

    /// <inheritdoc />
    public override string DocumentKind => MaterialSpecificationDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => DesignationIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<MaterialDefinition>?> FindByDesignationAsync(
        string designation,
        string? supplier = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designation);

        return FindBySecondaryKeyAsync(MaterialDefinition.DesignationKeyFor(supplier, designation), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<MaterialDefinition>>> SearchAsync(MaterialQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => MaterialQueryEvaluator.Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(MaterialDefinition definition) => definition.DesignationKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(MaterialDefinition definition) =>
        definition.Supplier is null
            ? $"Designation '{definition.Designation}'"
            : $"Supplier '{definition.Supplier}' designation '{definition.Designation}'";
}
