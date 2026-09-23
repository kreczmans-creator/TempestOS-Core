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
    public Task<IEngineeringObject> CreateAsync(string initialContent, CancellationToken cancellationToken = default) =>
        CreateAsync(initialContent, projectScopeId: null, cancellationToken);

    /// <summary>
    /// As <see cref="CreateAsync(string, CancellationToken)"/>, and additionally enforces `TD-38`'s
    /// uniqueness rule — a business identifier unique among live objects of this <see cref="Kind"/>
    /// within one project — for the Kinds <see cref="BusinessIdentifierScope.EnforcedKinds"/> names.
    /// </summary>
    /// <param name="initialContent">The new object's own revision-1 content.</param>
    /// <param name="projectScopeId">
    /// The project this object is about to be placed under — resolved by the caller from its own
    /// intended parent, via <see cref="BusinessIdentifierScope.ResolveProjectId"/>, before this object
    /// exists to resolve its own ancestry from. <see langword="null"/> for "outside any project".
    /// Ignored for a Kind <see cref="BusinessIdentifierScope.EnforcedKinds"/> does not name.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait for the write lock, and the transaction.</param>
    /// <exception cref="DuplicateBusinessIdentifierException">
    /// A different, still-live object of this <see cref="Kind"/> already holds the new object's own
    /// <see cref="IEngineeringObject.BusinessIdentifier"/> within <paramref name="projectScopeId"/>.
    /// </exception>
    public async Task<IEngineeringObject> CreateAsync(string initialContent, Guid? projectScopeId, CancellationToken cancellationToken = default)
    {
        var documentId = Guid.NewGuid();
        var enforced = BusinessIdentifierScope.EnforcedKinds.Contains(Kind);
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

                // `TD-38`. Checked here, inside the transaction and before
                // anything durable is written for this object, so a refusal
                // leaves nothing behind — the identical "project" step
                // `ADR-0145` uses throughout; the claim itself is not made
                // until `afterCommit`, below.
                if (enforced)
                {
                    BusinessIdentifierScope.EnsureAvailable(
                        _context.BusinessIdentifierIndex, _context.Repository, Kind, projectScopeId, candidate.BusinessIdentifier, documentId);
                }

                await candidate.WriteCreationAsync(transaction, token).ConfigureAwait(false);

                instance = candidate;
            },
            // Committed, and still under the write lock. Nothing before
            // this point put the instance anywhere a second caller could
            // find it, and nothing after the lock is released can find it
            // missing.
            afterCommit: () =>
            {
                _context.Repository.Register(instance!);

                if (enforced)
                    _context.BusinessIdentifierIndex.Claim(Kind, projectScopeId, instance!.BusinessIdentifier, documentId);
            },
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
    /// <para>
    /// The reference record and its audit row are one transaction; the
    /// in-memory relationship cache learns of the link only after it
    /// commits, so a failed write leaves the cache and the store agreeing
    /// that nothing happened (`TD-140`).
    /// </para>
    /// <para>
    /// <b>Refuses either end that is already superseded (`TD-141`).</b>
    /// <see cref="EngineeringObjectBase.LinkAsync"/> guards a durable write
    /// through a specific, retired instance <em>handle</em> with its own
    /// private <c>ThrowIfSuperseded</c> check; this factory takes raw ids
    /// instead, which carry no handle to go stale, so it checks the
    /// durable signal a raw id <em>can</em> carry — the resolved object's
    /// own <see cref="IHasLifecycle.Status"/>. An id that currently
    /// resolves to an object whose lifecycle has already moved to
    /// <see cref="LifecycleState.Superseded"/> is refused with the same
    /// <see cref="SupersededEngineeringObjectException"/>
    /// <c>ThrowIfSuperseded</c> throws, before either end is resolved for
    /// the write below - so a refusal, like every other guard on this
    /// write path, leaves nothing durable.
    /// </para>
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
                var source = await _context.Repository.FindAsync(sourceId, token).ConfigureAwait(false);
                ThrowIfSuperseded(source, sourceId);

                var target = await _context.Repository.FindAsync(targetId, token).ConfigureAwait(false);
                ThrowIfSuperseded(target, targetId);

                await _context.DocumentWriter.LinkAsync(transaction, sourceId, targetId, RelationshipKind, token).ConfigureAwait(false);

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

    /// <summary>Refuses (`TD-141`) when <paramref name="candidate"/> was resolved and its own lifecycle is already <see cref="LifecycleState.Superseded"/>. A <see langword="null"/> candidate (not yet resolvable) is not this guard's concern - the write below leaves that refusal to the document store's own existence check.</summary>
    private static void ThrowIfSuperseded(IEngineeringObject? candidate, Guid id)
    {
        if (candidate is IHasLifecycle { Status: LifecycleState.Superseded })
            throw new SupersededEngineeringObjectException(id, candidate.CurrentRevisionNumber);
    }
}
