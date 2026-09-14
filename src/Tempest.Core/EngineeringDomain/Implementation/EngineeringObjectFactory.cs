using Tempest.Core.Audit;
using Tempest.Core.EngineeringData;
using Tempest.Core.Events;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// One generic factory type serving every Kind — each <em>instance</em> is still responsible for exactly one
/// declared <see cref="Kind"/> (WP8.2B Dependency Rules.md §7), constructed once per Kind by the composition
/// root rather than resolved from any registry (§8: no registry contract is proposed by WP8.2B).
/// </summary>
public sealed class EngineeringObjectFactory<T> : IEngineeringObjectFactory
    where T : EngineeringObjectBase, IRehydratable<T>
{
    private readonly EngineeringDomainContext _context;
    private readonly Func<IEngineeringDocument, IDocumentRevision, T> _constructor;

    /// <summary>Initialises a new instance of the <see cref="EngineeringObjectFactory{T}"/> class.</summary>
    public EngineeringObjectFactory(string kind, EngineeringDomainContext context, Func<IEngineeringDocument, IDocumentRevision, T> constructor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(constructor);

        Kind = kind;
        _context = context;
        _constructor = constructor;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>One transaction, then registration (`ADR-0145`, `TD-147`).</b> The
    /// document, its revision 1, the object's own state record and the
    /// creation audit row are written through one transaction; the
    /// instance reaches
    /// <see cref="IEngineeringObjectRepository.Register"/> only after that
    /// transaction commits. A creation whose write fails therefore leaves
    /// nothing in the repository and nothing on disk — which is `TD-147`,
    /// where the instance used to be registered first and stayed
    /// registered, live and mutable, after its initial write threw.
    /// </para>
    /// <para>
    /// The document Id is minted here, before the transaction, so the
    /// instance can be constructed from a document record the transaction
    /// has already written and read back nothing.
    /// </para>
    /// </remarks>
    public async Task<IEngineeringObject> CreateAsync(string initialContent, CancellationToken cancellationToken = default)
    {
        var documentId = Guid.NewGuid();
        T? instance = null;

        await _context.ExecuteWriteAsync(
            async (transaction, token) =>
            {
                var created = await _context.DocumentWriter
                    .CreateAsync(transaction, documentId, Kind, initialContent, token)
                    .ConfigureAwait(false);

                var candidate = _constructor(created.Document, created.Revision);

                // `WP 16.4B-R6`. The successor `ReviseAsync` builds is
                // produced by this type's own state reader, given the state
                // captured at the moment of the revision — not by re-running
                // this factory's construction closure, which only ever knew
                // the values passed to *this* call and therefore reverted
                // every type-specific field a caller had changed since.
                candidate.AttachSelfFactory((doc, rev, state) => T.Rehydrate(doc, rev, _context, state));

                await candidate.WriteCreationAsync(transaction, token).ConfigureAwait(false);

                instance = candidate;
            },
            // Committed, and still under the write lock. Nothing before
            // this point put the instance anywhere a second caller could
            // find it, and nothing after the lock is released can find it
            // missing.
            afterCommit: () => _context.Repository.Register(instance!),
            cancellationToken,
            touched: () => [new WorkspaceChangeEntry(documentId, Kind, WorkspaceChangeType.Created)]).ConfigureAwait(false);

        return instance!;
    }
}

/// <summary>
/// Creates a typed relationship between two existing objects, as one
/// transaction (`ADR-0145`).
/// </summary>
public sealed class EngineeringRelationshipFactory : IEngineeringRelationshipFactory
{
    private readonly EngineeringDomainContext _context;
    private readonly RelationshipCategory _category;

    /// <summary>Initialises a new instance of the <see cref="EngineeringRelationshipFactory"/> class.</summary>
    public EngineeringRelationshipFactory(string relationshipKind, RelationshipCategory category, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipKind);
        ArgumentNullException.ThrowIfNull(context);

        RelationshipKind = relationshipKind;
        _category = category;
        _context = context;
    }

    /// <inheritdoc />
    public string RelationshipKind { get; }

    /// <inheritdoc />
    /// <remarks>
    /// The reference record and its audit row are one transaction; the
    /// in-memory relationship cache learns of the link only after it
    /// commits, so a failed write leaves the cache and the store agreeing
    /// that nothing happened (`TD-140`).
    /// </remarks>
    public async Task<IEngineeringRelationship> CreateAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        if (sourceId == targetId)
            throw new SelfReferentialRelationshipException(sourceId);

        var principalId = _context.ResolveCurrentPrincipalId();
        var createdAt = DateTimeOffset.UtcNow;
        var relationship = new EngineeringRelationship(sourceId, targetId, RelationshipKind, _category, principalId, createdAt);
        string? sourceKind = null;

        await _context.ExecuteWriteAsync(
            async (transaction, token) =>
            {
                await _context.DocumentWriter.LinkAsync(transaction, sourceId, targetId, RelationshipKind, token).ConfigureAwait(false);

                var source = await _context.Repository.FindAsync(sourceId, token).ConfigureAwait(false);
                sourceKind = source?.Kind ?? "Unknown";

                await AuditTransactionWriter.WriteAsync(
                    transaction,
                    sourceId,
                    sourceKind,
                    EngineeringAuditActions.Linked,
                    principalId,
                    $"{RelationshipKind} to '{targetId:N}'.",
                    createdAt,
                    token).ConfigureAwait(false);
            },
            afterCommit: () => _context.RelationshipRepository.Record(relationship),
            cancellationToken,
            touched: () => [new WorkspaceChangeEntry(sourceId, sourceKind!, WorkspaceChangeType.Updated)]).ConfigureAwait(false);

        return relationship;
    }
}
