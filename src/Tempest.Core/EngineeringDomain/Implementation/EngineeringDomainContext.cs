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
    /// Internal, and acquired from exactly four places inside
    /// <see cref="EngineeringObjectBase"/> — corrected here for
    /// `WP 16.4B-R6` after the review board found the previous note wrong
    /// in both halves (it named <see cref="EngineeringObjectFactory{T}.CreateAsync"/>,
    /// which has reached this only indirectly through
    /// <c>PersistInitialStateAsync</c> since `WP 16.4B-R3`, and omitted
    /// <c>ReviseAsync</c>, which has acquired it directly since
    /// `WP 16.4B-R4`):
    /// <list type="bullet">
    /// <item><description><see cref="EngineeringObjectBase.PersistStateAsync"/> — every ordinary mutation, and, via <c>PersistInitialStateAsync</c>, every creation.</description></item>
    /// <item><description><see cref="EngineeringObjectBase.ReviseAsync"/> — the whole revision hand-off, so a successor is minted and the predecessor retired atomically.</description></item>
    /// <item><description><see cref="EngineeringObjectBase.AttachAsync"/> — the supersession check and the in-memory add, together.</description></item>
    /// <item><description><see cref="EngineeringObjectBase.AttachContentAsync"/> — the whole mark/content/state/clear sequence, so an attach is atomic with respect to a revision.</description></item>
    /// </list>
    /// This lock is not reentrant (<see cref="AsyncKeyedLock"/>), so those
    /// last three use <c>PersistStateHoldingWriteLockAsync</c> rather than
    /// re-entering <see cref="EngineeringObjectBase.PersistStateAsync"/>.
    /// </remarks>
    internal Task<IDisposable> AcquireObjectWriteLockAsync(Guid objectId, CancellationToken cancellationToken = default) =>
        _objectWriteLock.AcquireAsync(objectId.ToString("N"), cancellationToken);
}
