using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The shared collaborators every <see cref="EngineeringObjectBase"/>
/// instance and factory needs — bundled to keep per-Kind constructors
/// small — and the transaction boundary every durable change to an
/// engineering object passes through (`ADR-0145`).
/// </summary>
/// <remarks>
/// <para>
/// <b>One store is authoritative.</b> An engineering object's truth used
/// to live in four places at once — the instance's own fields, the
/// object-state record, the document store's documents, revisions and
/// references, and the in-memory relationship repository — with no
/// transaction spanning them. A write that was refused or failed part way
/// left memory and disk disagreeing, and the whole apparatus of
/// <c>RollBackOnFailureAsync</c>, <c>MutationRollbackPoint</c>,
/// <c>DurableRecordAlreadyShowsThisStateAsync</c>, the attachment
/// write-intent marker and the reconciliation sweep existed to make that
/// disagreement smaller rather than impossible (`TD-135`, `TD-136`,
/// `TD-140`–`TD-150`).
/// </para>
/// <para>
/// Since `ADR-0145` there is one durable authority and one write path:
/// <see cref="ExecuteWriteAsync"/> takes the domain write lock, opens one
/// transaction on <see cref="PersistenceStore"/>, and everything a single
/// logical change touches — the object-state record, any document,
/// revision or reference record, any attachment bytes, and the audit row
/// — is written through that one transaction. Graph invariants are
/// checked inside it. The in-memory repositories are mutated only after
/// it commits. There is therefore nothing to roll back, and the
/// compensation above is deleted rather than kept.
/// </para>
/// <para>
/// <b>The lock is domain-wide, and deliberately not project-scoped.</b>
/// TempestOS is a single-user desktop system of record: one person, one
/// process, one database file, whose writes are user-initiated actions
/// seconds apart. A finer-grained lock — per project, per object — would
/// buy concurrency nobody is waiting for and cost a lock-ordering
/// discipline that the cycle check makes genuinely hard, because
/// <c>GuardAgainstCircularParentAsync</c> and the live-children check
/// both read an unbounded slice of the graph and would each have to
/// acquire an unbounded, order-sensitive set of locks to be correct. One
/// lock is the honest shape for the product this is; `ADR-0145` records
/// that as a decision rather than an oversight.
/// </para>
/// <para>
/// The lock also cannot be taken inside the transaction body, and is
/// taken outside it here: SQLite's <c>BEGIN IMMEDIATE</c> already holds
/// the database's single write lock, so a second writer waiting on the
/// domain lock while a transaction holds the database lock is one queue,
/// not two, and cannot invert.
/// </para>
/// </remarks>
public sealed class EngineeringDomainContext
{
    /// <summary>
    /// The one domain-wide write lock (`ADR-0145`). A
    /// <see cref="SemaphoreSlim"/> rather than a monitor because the whole
    /// critical section is asynchronous, and not reentrant — no code
    /// inside <see cref="ExecuteWriteAsync"/> may call back into a
    /// mutator.
    /// </summary>
    private readonly SemaphoreSlim _domainWriteLock = new(1, 1);

    /// <summary>Initialises a new instance of the <see cref="EngineeringDomainContext"/> class.</summary>
    /// <param name="persistenceStore">The single durable store every engineering write commits through.</param>
    /// <param name="store">The document store, built over <paramref name="persistenceStore"/>.</param>
    /// <param name="repository">The in-memory object cache, populated from committed state only.</param>
    /// <param name="relationshipRepository">The in-memory relationship cache, populated from committed state only.</param>
    /// <param name="lifecycleTable">The permitted lifecycle transitions.</param>
    /// <param name="validationRuleSet">The validation rules every object is checked against.</param>
    /// <param name="evidenceComposer">The digital-thread evidence composer.</param>
    /// <param name="currentPrincipalAccessor">The service the acting principal is resolved from.</param>
    /// <param name="objectStateStore">
    /// The object-state store. <see langword="null"/> — the default —
    /// builds one over <paramref name="persistenceStore"/>, which is what
    /// every caller wants and what the composition root passes explicitly.
    /// </param>
    /// <param name="attachmentContentStore">
    /// The attachment content store. <see langword="null"/> — the default
    /// — builds one over <paramref name="persistenceStore"/>, which must
    /// then also be the platform's byte store (both shipped
    /// implementations are).
    /// </param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Any parameter other than the three optional ones is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="attachmentContentStore"/> was not supplied and
    /// <paramref name="persistenceStore"/> is not also an
    /// <see cref="IBinaryPersistenceStore"/>.
    /// </exception>
    public EngineeringDomainContext(
        IQueryablePersistenceStore persistenceStore,
        IEngineeringDocumentStore store,
        IEngineeringObjectRepository repository,
        IEngineeringRelationshipRepository relationshipRepository,
        ILifecycleTransitionTable lifecycleTable,
        IValidationRuleSet validationRuleSet,
        IEvidenceComposer evidenceComposer,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IEngineeringObjectStateStore? objectStateStore = null,
        IAttachmentContentStore? attachmentContentStore = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(relationshipRepository);
        ArgumentNullException.ThrowIfNull(lifecycleTable);
        ArgumentNullException.ThrowIfNull(validationRuleSet);
        ArgumentNullException.ThrowIfNull(evidenceComposer);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        PersistenceStore = persistenceStore;
        Store = store;
        Repository = repository;
        RelationshipRepository = relationshipRepository;
        LifecycleTable = lifecycleTable;
        ValidationRuleSet = validationRuleSet;
        EvidenceComposer = evidenceComposer;
        CurrentPrincipalAccessor = currentPrincipalAccessor;
        ObjectStateStore = objectStateStore ?? new EngineeringObjectStateStore(persistenceStore, migrations: null, logger);
        AttachmentContentStore = attachmentContentStore ?? new AttachmentContentStore(
            persistenceStore as IBinaryPersistenceStore
            ?? throw new ArgumentException(
                "No attachment content store was supplied and the persistence store is not also the platform's byte " +
                "store, so one cannot be built over it. Pass an IAttachmentContentStore explicitly.",
                nameof(attachmentContentStore)),
            logger);

        DocumentWriter = Require<ITransactionalDocumentWriter>(Store, nameof(store));
        AttachmentWriter = Require<ITransactionalAttachmentWriter>(AttachmentContentStore, nameof(attachmentContentStore));
        StateWriter = Require<ITransactionalStateWriter>(ObjectStateStore, nameof(objectStateStore));
    }

    /// <summary>The single durable store every engineering write commits through (`ADR-0145`).</summary>
    public IQueryablePersistenceStore PersistenceStore { get; }

    /// <summary>The document store — the read surface for documents, revisions and references.</summary>
    public IEngineeringDocumentStore Store { get; }

    /// <summary>The in-memory object cache. Populated from committed state only; never a co-equal writer.</summary>
    public IEngineeringObjectRepository Repository { get; }

    /// <summary>The in-memory relationship cache. Populated from committed state only; never a co-equal writer.</summary>
    public IEngineeringRelationshipRepository RelationshipRepository { get; }

    /// <summary>The permitted lifecycle transitions.</summary>
    public ILifecycleTransitionTable LifecycleTable { get; }

    /// <summary>The validation rules every object is checked against.</summary>
    public IValidationRuleSet ValidationRuleSet { get; }

    /// <summary>The digital-thread evidence composer.</summary>
    public IEvidenceComposer EvidenceComposer { get; }

    /// <summary>The service the acting principal is resolved from.</summary>
    public ICurrentPrincipalAccessor CurrentPrincipalAccessor { get; }

    /// <summary>The durable engineering-object state store (`TD-85`) — the read surface for rehydration.</summary>
    public IEngineeringObjectStateStore ObjectStateStore { get; }

    /// <summary>The durable attachment-content store (`TD-31`) — the read surface for attachment bytes.</summary>
    public IAttachmentContentStore AttachmentContentStore { get; }

    internal ITransactionalDocumentWriter DocumentWriter { get; }

    internal ITransactionalAttachmentWriter AttachmentWriter { get; }

    internal ITransactionalStateWriter StateWriter { get; }

    /// <summary>
    /// The acting principal's id, or the store's own "unknown" where none
    /// is established — never a fabricated one.
    /// </summary>
    public string ResolveCurrentPrincipalId() =>
        CurrentPrincipalAccessor.Current?.Identity.Id ?? EngineeringDocumentStore.UnknownAuthorPrincipalId;

    /// <summary>
    /// Runs <paramref name="work"/> as one durable engineering change:
    /// under the domain write lock, inside one transaction on
    /// <see cref="PersistenceStore"/> (`ADR-0145`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything <paramref name="work"/> writes — object state, document
    /// records, revisions, references, attachment bytes, the audit row —
    /// lands together or not at all. Everything it reads it reads through
    /// the same transaction handle, so a graph invariant checked inside is
    /// checked against the state that will actually be committed.
    /// </para>
    /// <para>
    /// <b>It must not touch memory.</b> The caller computes the next
    /// state, commits it here, and only then applies it to its own fields
    /// and to the repositories. That ordering is what makes a failed
    /// commit leave nothing behind — in memory or on disk — without any
    /// undo step, which is the whole point of `ADR-0145`.
    /// </para>
    /// </remarks>
    /// <param name="work">The unit of durable work.</param>
    /// <param name="cancellationToken">Cancels the wait for the lock, and the transaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    internal async Task ExecuteWriteAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await _domainWriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistenceStore.ExecuteInTransactionAsync(work, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _domainWriteLock.Release();
        }
    }

    private static TWriter Require<TWriter>(object candidate, string parameterName)
        where TWriter : class =>
        candidate as TWriter
        ?? throw new ArgumentException(
            $"'{candidate.GetType().Name}' does not implement '{typeof(TWriter).Name}', so it cannot take part in the " +
            "one transaction every engineering change commits through (`ADR-0145`). Use the store this platform " +
            "ships, or implement the writer contract.",
            parameterName);
}
