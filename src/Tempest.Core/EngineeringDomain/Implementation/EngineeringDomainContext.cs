using Tempest.Core.EngineeringData;
using Tempest.Core.Events;
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
    /// <param name="workspaceChanges">
    /// Where a committed write's touched set is announced (`WP 18.1A`).
    /// <see langword="null"/> — the default — is a legitimate, silent
    /// no-op: a caller that has no feed to publish to (most tests) simply
    /// gets none, rather than being forced to supply a stub.
    /// </param>
    /// <exception cref="ArgumentNullException">Any parameter other than the four optional ones is <see langword="null"/>.</exception>
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
        ILogger? logger = null,
        IWorkspaceChangePublisher? workspaceChanges = null)
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
        WorkspaceChanges = workspaceChanges;
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
    /// Where <see cref="ExecuteWriteAsync"/> announces a committed write's
    /// touched set (`WP 18.1A`). <see langword="null"/> when this context
    /// was built without a feed to publish to.
    /// </summary>
    internal IWorkspaceChangePublisher? WorkspaceChanges { get; }

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
    /// <b><paramref name="work"/> must not touch memory.</b> It computes
    /// the next state and commits it; memory is written by
    /// <paramref name="afterCommit"/>, which runs only if the transaction
    /// committed. That ordering is what makes a failed commit leave
    /// nothing behind — in memory or on disk — without any undo step,
    /// which is the whole point of `ADR-0145`.
    /// </para>
    /// <para>
    /// <b><paramref name="afterCommit"/> runs while this method still
    /// holds the write lock, and that is load-bearing.</b> A mutator
    /// projects its next state from the object's own in-memory fields, so
    /// if memory were updated after the lock were released there would be
    /// a window in which a second writer had taken the lock and projected
    /// from fields the first writer had committed but not yet applied. The
    /// second writer would then commit a state derived from a value the
    /// store had already replaced, and the first writer's change would be
    /// durably lost — which is exactly the lost update `WP 16.4B-R3`
    /// closed with its per-object lock, and it would return the moment
    /// "apply after commit" stopped meaning "apply before releasing".
    /// Commit and apply are therefore one critical section, and no mutator
    /// may apply its own change outside this call.
    /// </para>
    /// <para>
    /// <paramref name="afterCommit"/> is synchronous and must not block,
    /// await, or call back into a mutator: it runs under a non-reentrant
    /// lock, so re-entry would deadlock rather than misbehave visibly.
    /// Assigning fields and calling <c>Register</c>/<c>Record</c> on the
    /// in-memory repositories is all it is for.
    /// </para>
    /// </remarks>
    /// <param name="work">The unit of durable work. Must not touch memory.</param>
    /// <param name="afterCommit">Applies the committed change to memory, under the same lock hold.</param>
    /// <param name="cancellationToken">Cancels the wait for the lock, and the transaction.</param>
    /// <param name="touched">
    /// Computes the objects this write touched, for <see cref="WorkspaceChanges"/>
    /// (`WP 18.1A`). Invoked after <paramref name="afterCommit"/>, under
    /// the same lock hold, so it may read a value the mutator captured
    /// inside <paramref name="work"/> exactly as <paramref name="afterCommit"/>
    /// does — it exists as a delegate rather than a precomputed list for
    /// that reason: a relationship write, for one, does not know the
    /// source object's own Kind until <paramref name="work"/> has read it.
    /// <see langword="null"/>, or a delegate returning an empty list, is
    /// legitimate — nothing is published, exactly as if this write had no
    /// subscriber at all.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    internal async Task ExecuteWriteAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work,
        Action? afterCommit = null,
        CancellationToken cancellationToken = default,
        Func<IReadOnlyList<WorkspaceChangeEntry>>? touched = null)
    {
        ArgumentNullException.ThrowIfNull(work);

        await _domainWriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistenceStore.ExecuteInTransactionAsync(work, cancellationToken).ConfigureAwait(false);

            // Committed. Memory is updated here, before the lock is
            // released, so the next writer projects from it.
            afterCommit?.Invoke();

            // Raised only now — after memory agrees with the commit, and
            // still under the write lock, so the sequence this reports can
            // never be superseded by a second writer's commit before a
            // subscriber ever hears about this one (`WP 18.1A`).
            if (touched?.Invoke() is { Count: > 0 } entries && WorkspaceChanges is not null)
                WorkspaceChanges.Publish(new WorkspaceChange(PersistenceStore.CurrentSequence, entries));
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
