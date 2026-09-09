using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>The concrete <see cref="IStandardCatalog"/> implementation.</summary>
/// <remarks>
/// Everything about storing, revising, governing and superseding a standard
/// record comes from <see cref="ReferenceDataCatalog{TDefinition}"/>, shared
/// with every Group A library (`ADR-0126`). What this class adds is
/// standards-specific: the body-designation-edition uniqueness key, the
/// lookups that read it, the standards query, and the
/// <see cref="IStandardResolver"/> implementation every citing library
/// resolves through.
/// </remarks>
public sealed class StandardCatalog : ReferenceDataCatalog<StandardDefinition>, IStandardCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every standard record's own backing document carries.</summary>
    public const string StandardDocumentKind = "StandardReference";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>standardId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "Standards.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each body-designation-edition key to the <c>standardId</c> holding it.</summary>
    public const string DesignationIndexCollection = "Standards.DesignationIndex";

    /// <summary>
    /// Initialises a new instance of the <see cref="StandardCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own standard records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public StandardCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "Standards";

    /// <inheritdoc />
    public override string DocumentKind => StandardDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => DesignationIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<StandardDefinition>?> FindByDesignationAsync(
        string bodyCode,
        string designation,
        string? edition = null,
        CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(StandardDefinition.DesignationKeyFor(bodyCode, designation, edition), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<StandardDefinition>>> FindEditionsAsync(
        string bodyCode,
        string designation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(designation);

        var body = bodyCode.Trim();
        var number = designation.Trim();

        return FilterAsync(
            record => string.Equals(record.Definition.Body.Code, body, StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Definition.Designation, number, StringComparison.OrdinalIgnoreCase),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<StandardDefinition>>> SearchAsync(StandardQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => StandardQueryEvaluator.Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The whole of <see cref="IStandardResolver"/>: a citing library asks
    /// only whether the record it cites exists. Deliberately not "and is
    /// released" — a citation of a Draft record is a citation of a
    /// standard TempestOS has not finished checking, which is a governance
    /// observation for the citing library's own validation to make with
    /// the record in hand, not a reason for this seam to report the
    /// standard as absent.
    /// </remarks>
    public async Task<bool> ExistsAsync(string standardId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(standardId);

        return await FindAsync(standardId, cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(StandardDefinition definition) => definition.DesignationKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(StandardDefinition definition) =>
        $"Standard '{definition.FullDesignation}'";
}
