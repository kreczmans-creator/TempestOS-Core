using Tempest.Core.Concurrency;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;

namespace Tempest.Core.EngineeringDomain;

/// <summary>The shared collaborators every <see cref="EngineeringObjectBase"/> instance and factory needs — bundled to keep per-Kind constructors small.</summary>
public sealed class EngineeringDomainContext
{
    /// <summary>
    /// Serialises each engineering object's own capture-then-persist
    /// sequence, keyed by object Id (`WP 16.4B-R3`) — see
    /// <see cref="EngineeringObjectBase.PersistStateAsync"/> for the lost
    /// update this closes and why the key is the object's Id rather than
    /// any one instance. Shared by construction across every
    /// <see cref="EngineeringObjectBase"/> instance for the same Id,
    /// because <see cref="EngineeringObjectBase.ReviseAsync"/> proves more
    /// than one live instance can answer to the same Id at once (the
    /// original and its revised successor, both registered and both
    /// reachable) — an instance-level lock would not serialise those two
    /// against each other, but a lock keyed by Id, held here where every
    /// instance already shares one <see cref="EngineeringDomainContext"/>,
    /// does.
    /// </summary>
    private readonly AsyncKeyedLock _objectWriteLock = new();

    public IEngineeringDocumentStore Store { get; }
    public IEngineeringObjectRepository Repository { get; }
    public IEngineeringRelationshipRepository RelationshipRepository { get; }
    public ILifecycleTransitionTable LifecycleTable { get; }
    public IValidationRuleSet ValidationRuleSet { get; }
    public IEvidenceComposer EvidenceComposer { get; }
    public ICurrentPrincipalAccessor CurrentPrincipalAccessor { get; }

    /// <summary>
    /// The durable engineering-object state store (`TD-85`), or
    /// <see langword="null"/> where no rehydration substrate is composed.
    /// </summary>
    /// <remarks>
    /// Optional so every existing hand-assembled context in tests and
    /// samples keeps working unchanged: with no store, objects behave
    /// exactly as they did before `TD-85` (in-memory only). The production
    /// Host always supplies one.
    /// </remarks>
    public IEngineeringObjectStateStore? ObjectStateStore { get; }

    /// <summary>
    /// The durable store of attachment bytes, or <see langword="null"/>
    /// where none is configured (`TD-31`).
    /// </summary>
    /// <remarks>
    /// Optional for the same reason <see cref="ObjectStateStore"/> is: the
    /// many hand-assembled domain pipelines in this repository's own tests
    /// predate both, and must keep behaving exactly as they did. An object
    /// in a context without one can still record attachment metadata; it
    /// simply cannot hold a file, and says so rather than pretending.
    /// </remarks>
    public IAttachmentContentStore? AttachmentContentStore { get; }

    /// <summary>
    /// The durable write-intent marker store (`WP 16.4B-R2`), or
    /// <see langword="null"/> where none is configured.
    /// </summary>
    /// <remarks>
    /// Optional for the same reason <see cref="AttachmentContentStore"/>
    /// is: every hand-assembled context in this repository's own tests
    /// that predates it must keep compiling and behaving unchanged.
    /// <b>With no marker store, <see cref="EngineeringObjectBase.AttachContentAsync"/>
    /// simply skips marking</b> — it does not fail, and it does not
    /// silently reopen a bigger hole than the one before this Work
    /// Package: a context with an <see cref="AttachmentContentStore"/> but
    /// no marker store is exactly as exposed to the race as `WP 16.4B`
    /// shipped, never more so. The production Host always composes both
    /// together (<c>TempestHost</c>), so this combination is a test-only
    /// shape, not a production one.
    /// </remarks>
    public IAttachmentWriteIntentStore? AttachmentWriteIntentStore { get; }

    public EngineeringDomainContext(
        IEngineeringDocumentStore store,
        IEngineeringObjectRepository repository,
        IEngineeringRelationshipRepository relationshipRepository,
        ILifecycleTransitionTable lifecycleTable,
        IValidationRuleSet validationRuleSet,
        IEvidenceComposer evidenceComposer,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IEngineeringObjectStateStore? objectStateStore = null,
        IAttachmentContentStore? attachmentContentStore = null,
        IAttachmentWriteIntentStore? attachmentWriteIntentStore = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(relationshipRepository);
        ArgumentNullException.ThrowIfNull(lifecycleTable);
        ArgumentNullException.ThrowIfNull(validationRuleSet);
        ArgumentNullException.ThrowIfNull(evidenceComposer);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        Store = store;
        Repository = repository;
        RelationshipRepository = relationshipRepository;
        LifecycleTable = lifecycleTable;
        ValidationRuleSet = validationRuleSet;
        EvidenceComposer = evidenceComposer;
        CurrentPrincipalAccessor = currentPrincipalAccessor;
        ObjectStateStore = objectStateStore;
        AttachmentContentStore = attachmentContentStore;
        AttachmentWriteIntentStore = attachmentWriteIntentStore;
    }

    public string ResolveCurrentPrincipalId() =>
        CurrentPrincipalAccessor.Current?.Identity.Id ?? InMemoryEngineeringDocumentStore.UnknownAuthorPrincipalId;

    /// <summary>
    /// Acquires <see cref="_objectWriteLock"/> for <paramref name="objectId"/>
    /// (`WP 16.4B-R3`). Dispose the returned value to release.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Internal, and acquired from exactly six call sites, all of them
    /// inside <see cref="EngineeringObjectBase"/>
    /// (<c>grep -n AcquireObjectWriteLockAsync</c> over that file returns
    /// nine hits: these six calls and three mentions in comments). This
    /// list is maintained from
    /// the call sites rather than from intent, because it has now been
    /// wrong twice: `WP 16.4B-R3`'s version named
    /// <see cref="EngineeringObjectFactory{T}.CreateAsync"/>, which has
    /// reached this only indirectly through <c>PersistInitialStateAsync</c>
    /// since that Work Package, and omitted <c>ReviseAsync</c>;
    /// `WP 16.4B-R6`'s replacement then asserted that <c>ReviseAsync</c>,
    /// <c>AttachAsync</c> and <c>AttachContentAsync</c> all called
    /// <c>PersistStateHoldingWriteLockAsync</c>, and the review board
    /// established that only <c>AttachContentAsync</c> did —
    /// <c>ReviseAsync</c> performs no durable state write at all.
    /// Corrected here for `WP 16.4B-R6b`, and now checkable by
    /// <c>grep -n AcquireObjectWriteLockAsync</c> over that one file.
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="EngineeringObjectBase.PersistStateAsync"/> — creation, via <c>PersistInitialStateAsync</c>, and the concrete Kinds' own type-specific mutators.</description></item>
    /// <item><description><see cref="EngineeringObjectBase.ReviseAsync"/> — the whole revision hand-off, so a successor is minted and the predecessor retired atomically.</description></item>
    /// <item><description><see cref="EngineeringObjectBase.AttachAsync"/> — the supersession check, the in-memory add and the state write, together.</description></item>
    /// <item><description><see cref="EngineeringObjectBase.AttachContentAsync"/> — the whole mark/content/state/clear sequence, so an attach is atomic with respect to a revision.</description></item>
    /// <item><description><c>EngineeringObjectBase.MutateAndPersistAsync</c> (`WP 16.4B-R6b`) — the refusal, the in-memory mutation and the state write of <see cref="EngineeringObjectBase.TransitionAsync"/>, <see cref="EngineeringObjectBase.RenameAsync"/>, <see cref="EngineeringObjectBase.DeleteAsync"/> and <see cref="EngineeringObjectBase.SetBomLineAsync"/>, together, so an operation the platform reports as failed leaves nothing behind.</description></item>
    /// <item><description><see cref="EngineeringObjectBase.MoveAsync"/> (`WP 16.4B-R6b`) — the same three, plus the permanent <c>groupedUnder</c> link that has to land between the mutation and the state write, which is why it holds the lock itself instead of going through the helper.</description></item>
    /// </list>
    /// <para>
    /// This lock is not reentrant (<see cref="AsyncKeyedLock"/>), so all of
    /// those except the first reach any durable state write through
    /// <c>PersistStateHoldingWriteLockAsync</c> rather than by re-entering
    /// <see cref="EngineeringObjectBase.PersistStateAsync"/>, which would
    /// deadlock against the hold they already have.
    /// </para>
    /// </remarks>
    internal Task<IDisposable> AcquireObjectWriteLockAsync(Guid objectId, CancellationToken cancellationToken = default) =>
        _objectWriteLock.AcquireAsync(objectId.ToString("N"), cancellationToken);
}
