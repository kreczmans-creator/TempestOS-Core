using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Manufacturing;

/// <summary>The concrete <see cref="IProcessCatalog"/> implementation.</summary>
/// <remarks>
/// Everything about storing, revising, governing and superseding a process
/// record comes from <see cref="ReferenceDataCatalog{TDefinition}"/>,
/// shared with every Group A library (`ADR-0126`). What this class adds is
/// process-specific: the identity uniqueness key, the lookup that reads it,
/// and the process query.
/// </remarks>
public sealed class ProcessCatalog : ReferenceDataCatalog<ProcessDefinition>, IProcessCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every process record's own backing document carries.</summary>
    /// <remarks>
    /// Deliberately not <c>ManufacturingOperation</c>: that Kind is the
    /// workspace's own canonical object for an operation performed on a
    /// real part, and this is a reference description of a process in
    /// general. One value, one meaning.
    /// </remarks>
    public const string ProcessDocumentKind = "ManufacturingProcessReference";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>processId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "Manufacturing.ProcessIndex";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each process identity key to the <c>processId</c> holding it.</summary>
    public const string IdentityIndexCollection = "Manufacturing.ProcessIdentityIndex";

    /// <summary>
    /// Initialises a new instance of the <see cref="ProcessCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own process records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ProcessCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "Manufacturing";

    /// <inheritdoc />
    public override string DocumentKind => ProcessDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => IdentityIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<ProcessDefinition>?> FindByNameAsync(
        ProcessFamily family,
        string name,
        string? variant = null,
        CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(ProcessDefinition.IdentityKeyFor(family, name, variant), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<ProcessDefinition>>> SearchAsync(ProcessQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => ProcessQueryEvaluator.Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(ProcessDefinition definition) => definition.IdentityKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(ProcessDefinition definition) =>
        definition.Variant is null
            ? $"Process '{definition.Family} / {definition.Name}'"
            : $"Process '{definition.Family} / {definition.Name}' variant '{definition.Variant}'";
}
