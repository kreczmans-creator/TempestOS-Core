using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The shared plumbing every concrete canonical object class derives from — implements every facet
/// interface generically (ADR-0075's composition rule governs the <i>contracts</i>; this base class is
/// ordinary implementation reuse, orthogonal to it, the same way <see cref="Modules.ModuleLifecycleBase"/>
/// gives every module its own shared no-op lifecycle plumbing). A concrete Kind class only ever declares
/// the specific facets its own interface actually composes — inheriting the rest here costs nothing extra.
/// </summary>
public abstract class EngineeringObjectBase :
    IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions,
    IHasRelationships, ITraceable, IValidatable, IHasAttachments, ISearchable,
    IRenamable, IHasParent, IDeletable, IHasBomLine
{
    private readonly EngineeringDomainContext _context;
    private readonly List<ILifecycleTransitionRecord> _history = new();
    private readonly List<IAttachment> _attachments = new();
    private readonly object _lifecycleLock = new();
    private readonly object _structuralLock = new();
    // `WP 16.4B-R6`. Takes the predecessor's own captured state, so a
    // successor is built by the Kind's own state *reader*
    // (`IRehydratable{TSelf}.Rehydrate`) rather than by a closure over the
    // values the original construction call happened to pass. Before this,
    // the closure was the only thing that carried type-specific fields
    // across a revision, and it only ever knew their construction-time
    // values — every later mutation of one was silently dropped. See
    // `ReviseAsync`.
    private Func<IEngineeringDocument, IDocumentRevision, EngineeringObjectState, EngineeringObjectBase>? _selfFactory;

    // `WP 16.4B-R4`. Non-null once `ReviseAsync` has built a successor for
    // this Id and registered it in place of this instance. Written and read
    // only inside the per-object write lock, which is what makes it a real
    // ordering point rather than a hint: a competing durable write either
    // acquires the lock before the revision (and is therefore carried into
    // the successor's snapshot) or after it (and is refused). It is not
    // `volatile` precisely because every access is already inside that lock.
    private EngineeringObjectBase? _supersededBy;
    private LifecycleState _status = LifecycleState.Draft;
    private string _displayName;
    private Guid? _parentId;
    private bool _isDeleted;
    private decimal _quantity = 1m;
    private string? _unitOfMeasure;
    private string? _findNumber;
    private string? _itemNumber;
    private string? _referenceDesignator;

    protected EngineeringObjectBase(
        IEngineeringDocument document,
        IDocumentRevision currentRevision,
        EngineeringDomainContext context,
        string? identifier,
        string displayName,
        EngineeringObjectMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(currentRevision);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(metadata);

        Document = document;
        CurrentRevision = currentRevision;
        _context = context;
        Identifier = identifier;
        _displayName = displayName;
        Metadata = metadata;
    }

    protected IEngineeringDocument Document { get; }
    protected IDocumentRevision CurrentRevision { get; }
    protected EngineeringObjectMetadata Metadata { get; }
    protected EngineeringDomainContext Context => _context;

    /// <summary>
    /// Called once by the factory or rehydrator that constructed this
    /// instance, so <see cref="ReviseAsync"/> can produce a correctly-typed
    /// successor from a captured <see cref="EngineeringObjectState"/>
    /// (`WP 16.4B-R6`).
    /// </summary>
    /// <remarks>
    /// Both callers supply the same thing — the Kind's own
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> — so "revise" and
    /// "restart" reconstruct an object through one reader, and a
    /// type-specific field can no longer survive one and be dropped by the
    /// other.
    /// </remarks>
    internal void AttachSelfFactory(Func<IEngineeringDocument, IDocumentRevision, EngineeringObjectState, EngineeringObjectBase> selfFactory) =>
        _selfFactory = selfFactory;

    // ----------------------------------------------------------------
    // Durable object state (`TD-85`)
    // ----------------------------------------------------------------

    /// <summary>
    /// Captures this object's own complete state for persistence
    /// (`TD-85`) — everything that must come back after a restart for
    /// this to be the same object. Always stamped with
    /// <see cref="EngineeringObjectStateStore.CurrentSchemaVersion"/>
    /// (`TD-87`, `ADR-0120`) — an object in memory has exactly one shape,
    /// the current one, whether it arrived via a factory or a rehydrator;
    /// migration is a read-path concern only.
    /// </summary>
    internal EngineeringObjectState CaptureState()
    {
        var typeState = new Dictionary<string, string?>(StringComparer.Ordinal);
        CaptureTypeState(typeState);

        lock (_lifecycleLock)
        {
            lock (_structuralLock)
            {
                return new EngineeringObjectState(
                    EngineeringObjectStateStore.CurrentSchemaVersion,
                    Id,
                    Kind,
                    Identifier,
                    _displayName,
                    Metadata,
                    _status,
                    _parentId,
                    _isDeleted,
                    new EngineeringObjectBomLineState(_quantity, _unitOfMeasure, _findNumber, _itemNumber, _referenceDesignator),
                    _history.Select(h => new EngineeringObjectTransitionState(h.From, h.To, h.ActorPrincipalId, h.OccurredAt, h.ApprovalId)).ToList(),
                    CaptureAttachmentState(),
                    typeState);
            }
        }
    }

    /// <summary>
    /// Projects <c>_attachments</c> under the monitor its own writers use.
    /// </summary>
    /// <remarks>
    /// `WP 16.4B-R5`. Every mutator of <c>_attachments</c> writes under
    /// <c>lock (_attachments)</c>, but <see cref="CaptureState"/> read it
    /// under <c>_structuralLock</c> — a different monitor, so the read was
    /// not synchronised against the writes at all. The release review board
    /// reproduced a <see cref="NullReferenceException"/> thrown from inside
    /// the projection (a torn read of the list's backing array) on a
    /// concurrent attach and rename, twice in three hundred attempts, with
    /// no revision involved. It predates the whole `WP 16.4B` chain — it
    /// arrived with `TD-85`'s original <see cref="CaptureState"/> — but it
    /// also made `WP 16.4B-R4`'s claim of "an atomic capture" untrue in
    /// general, which is why it is closed here rather than deferred.
    /// <para>
    /// Nesting order: this is taken innermost, inside
    /// <c>_lifecycleLock</c> and <c>_structuralLock</c>. That is safe
    /// because no site anywhere in this type holds <c>_attachments</c>
    /// while acquiring either of the other two — every other use is a
    /// short, non-nested critical section — so no lock-order inversion is
    /// introduced.
    /// </para>
    /// </remarks>
    private List<EngineeringObjectAttachmentState> CaptureAttachmentState()
    {
        lock (_attachments)
        {
            return _attachments
                .Select(a => new EngineeringObjectAttachmentState(a.Id, a.FileName, a.ContentType, a.SizeInBytes, a.ContentHash))
                .ToList();
        }
    }

    /// <summary>
    /// Restores the mutable state a constructor cannot carry (`TD-85`) —
    /// applied immediately after reconstructing an instance, so the object
    /// is fully itself before any caller can observe it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Identifier, display name, metadata and <b>every type-specific
    /// field</b> arrive through the Kind's own
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> constructor, which is
    /// the reader for the <see cref="EngineeringObjectState.TypeState"/>
    /// half of the record; this method restores what lives in mutable base
    /// fields instead: lifecycle state and its history, structural parent,
    /// deletion, BOM line, and attachments. It deliberately does not read
    /// <see cref="EngineeringObjectState.TypeState"/> — there is no
    /// base-class writer for a type's own fields, only that type's own
    /// constructor.
    /// </para>
    /// <para>
    /// <b>That division is only sound while every caller pairs the two
    /// halves (`WP 16.4B-R6`).</b> The release review board found
    /// <see cref="ReviseAsync"/> calling this method on a successor built
    /// from a plain construction closure rather than from the captured
    /// state, so the whole <c>TypeState</c> half was silently dropped and
    /// then persisted — an <see cref="EngineeringTask"/>'s assignee, work
    /// state, priority and due date reverted by editing its description.
    /// <see cref="ReviseAsync"/> now builds its successor through the same
    /// reader the rehydrator uses, so the two halves are never applied
    /// apart.
    /// </para>
    /// </remarks>
    internal void RestoreState(EngineeringObjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_lifecycleLock)
        {
            _status = state.Status;
            _history.Clear();
            foreach (var transition in state.History)
                _history.Add(new LifecycleTransitionRecord(transition.From, transition.To, transition.ActorPrincipalId, transition.OccurredAt, transition.ApprovalId));
        }

        lock (_attachments)
        {
            _attachments.Clear();
            foreach (var attachment in state.Attachments)
                _attachments.Add(new Attachment(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeInBytes, attachment.ContentHash));
        }

        lock (_structuralLock)
        {
            _displayName = state.DisplayName;
            _parentId = state.ParentId;
            _isDeleted = state.IsDeleted;
            _quantity = state.BomLine.Quantity;
            _unitOfMeasure = state.BomLine.UnitOfMeasure;
            _findNumber = state.BomLine.FindNumber;
            _itemNumber = state.BomLine.ItemNumber;
            _referenceDesignator = state.BomLine.ReferenceDesignator;
        }
    }

    /// <summary>
    /// Writes this concrete type's own state into <paramref name="state"/>
    /// (`TD-85`). A type with fields beyond the shared facets overrides
    /// this and writes them; its own <see cref="IRehydratable{TSelf}.Rehydrate"/>
    /// reads them back. Each type therefore owns its own persistence,
    /// rather than a central switch knowing every type's fields.
    /// </summary>
    protected virtual void CaptureTypeState(IDictionary<string, string?> state)
    {
    }

    /// <summary>Writes a list of values into type state, as JSON.</summary>
    protected static void WriteList(IDictionary<string, string?> state, string key, IEnumerable<string>? values) =>
        state[key] = values is null ? null : System.Text.Json.JsonSerializer.Serialize(values.ToList());

    /// <summary>Writes a list of <see cref="Guid"/> values into type state, as JSON.</summary>
    protected static void WriteGuidList(IDictionary<string, string?> state, string key, IEnumerable<Guid>? values) =>
        WriteList(state, key, values?.Select(v => v.ToString()));

    /// <summary>Writes an arbitrary serialisable value into type state, as JSON — for a type whose own field is neither a scalar nor a list of scalars.</summary>
    protected static void WriteJson<TValue>(IDictionary<string, string?> state, string key, TValue? value) =>
        state[key] = value is null ? null : System.Text.Json.JsonSerializer.Serialize(value);

    /// <summary>
    /// Persists this object's own current state (`TD-85`) — called after
    /// every mutation, and once at creation. A no-op where no state store
    /// is composed, so every pre-`TD-85` hand-assembled context keeps
    /// working exactly as it did.
    /// </summary>
    /// <remarks>
    /// <b>The lost update this closes (`WP 16.4B-R3`).</b>
    /// <see cref="CaptureState"/> reads the live fields under short-lived
    /// per-field locks and <see cref="EngineeringObjectStateStore.SaveAsync"/>
    /// is an unconditional whole-record overwrite with no version check —
    /// nothing used to serialise the capture-then-save pair as a whole.
    /// Two concurrent mutations could have their saves land in the
    /// opposite order to their captures, and the later-landing, earlier
    /// captured snapshot silently dropped the other's change from disk —
    /// including an attachment reference, which
    /// <see cref="AttachmentContentReconciliationService.SweepAsync"/> then
    /// permanently deletes as an orphan, because it is behaving exactly as
    /// designed against durable state that no longer names a file the
    /// caller was told exists. Found by the independent post-remediation
    /// review that reproduced it against the real classes; the
    /// `WP 16.4B-R2` write-intent marker does not touch this — both
    /// concurrent writes complete and clear their markers correctly, and
    /// the marker's own window is separate from this one.
    /// <para>
    /// <b>Why the lock is keyed by <see cref="Id"/>, not held on this
    /// instance.</b> An instance-level lock only serialises callers that
    /// share this exact <see cref="EngineeringObjectBase"/> object, and
    /// that is not always true for one Id: <see cref="ReviseAsync"/>
    /// constructs a second, independently-mutable instance for the same
    /// <see cref="Id"/> and registers it in place of this one, while
    /// nothing requires every caller holding a reference to <em>this</em>
    /// instance to have noticed the replacement — a caller that mutates
    /// the original after a concurrent revision is racing the revised
    /// successor for the identical durable record. <see cref="Context"/>
    /// (<see cref="EngineeringDomainContext"/>) is the one collaborator
    /// every instance for a given Id is guaranteed to share (it is
    /// threaded through every constructor and every self-factory/rehydrator
    /// closure), so the lock lives there, keyed by <see cref="Id"/> rather
    /// than by any one instance —
    /// <see cref="EngineeringDomainContext.AcquireObjectWriteLockAsync"/>.
    /// </para>
    /// <para>
    /// <b>Keying it by <see cref="Id"/> was necessary and not sufficient
    /// (`WP 16.4B-R4`).</b> The paragraph above identified the
    /// <see cref="ReviseAsync"/> multi-instance hazard correctly and then
    /// under-solved it: a shared lock <em>orders</em> the predecessor's and
    /// successor's writes, but ordering alone does not stop the second from
    /// discarding the first, because the successor's snapshot was taken at
    /// revision time and never learns of a predecessor write that lands
    /// after it. The release review reproduced exactly that, against the
    /// real classes. What closes it is <see cref="ReviseAsync"/> performing
    /// its capture-and-handoff inside this same lock and retiring the
    /// predecessor there, so a later predecessor write is refused with
    /// <see cref="SupersededEngineeringObjectException"/> instead of
    /// silently overwriting. This note is left in place rather than
    /// rewritten, because the gap between a correctly-identified hazard and
    /// a sufficient fix is the whole lesson.
    /// </para>
    /// <para>
    /// <b>Re-entrancy.</b> <see cref="Concurrency.AsyncKeyedLock"/> is not
    /// reentrant, so no caller of this method may already hold this
    /// object's write lock — a call that did would deadlock against
    /// itself.
    /// </para>
    /// <para>
    /// <b>Who actually calls this, at this commit (`WP 16.4B-R6b`).</b>
    /// The list is short, and is written from the call sites rather than
    /// from intent, because the review board found the previous version of
    /// this paragraph — added by `WP 16.4B-R6` to correct an earlier wrong
    /// comment — wrong in two of its three claims: it named
    /// <see cref="ReviseAsync"/>, which performs no durable state write at
    /// all and calls neither this method nor
    /// <see cref="PersistStateHoldingWriteLockAsync"/>, and
    /// <see cref="AttachAsync"/>, which at that commit reached
    /// <c>store.SaveAsync</c> directly.
    /// <list type="bullet">
    /// <item><description><c>PersistInitialStateAsync</c> — the one durable write <see cref="EngineeringObjectFactory{T}.CreateAsync"/> performs at creation.</description></item>
    /// <item><description>The type-specific mutators on the concrete Kinds in this namespace (for example <c>EngineeringTask.AssignAsync</c> and the <c>GovernanceRisk</c> setters), which mutate their own fields under their own monitor and then call this.</description></item>
    /// </list>
    /// Every mutator declared on <em>this</em> type — the two attachment
    /// entry points, <see cref="TransitionAsync"/>,
    /// <see cref="RenameAsync"/>, <see cref="MoveAsync"/>,
    /// <see cref="DeleteAsync"/> and <see cref="SetBomLineAsync"/> —
    /// acquires the lock itself and calls
    /// <see cref="PersistStateHoldingWriteLockAsync"/> instead, so that the
    /// supersession refusal happens before the mutation rather than after
    /// it (<see cref="MutateAndPersistAsync"/>). That is what makes a
    /// refused write leave nothing behind; a mutator that reaches its
    /// durable write through <em>this</em> method has already mutated
    /// itself by the time the refusal is raised.
    /// </para>
    /// </remarks>
    protected async Task PersistStateAsync(CancellationToken cancellationToken = default)
    {
        if (_context.ObjectStateStore is not { } store)
            return;

        using (await _context.AcquireObjectWriteLockAsync(Id, cancellationToken).ConfigureAwait(false))
        {
            await PersistStateHoldingWriteLockAsync(store, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The body of <see cref="PersistStateAsync"/>, for a caller that
    /// already holds this object's write lock (`WP 16.4B-R6`).
    /// </summary>
    /// <remarks>
    /// <see cref="Concurrency.AsyncKeyedLock"/> is not reentrant, so a
    /// method that must do more than persist inside one uninterrupted hold
    /// of the lock cannot call <see cref="PersistStateAsync"/> and must
    /// call this instead. Two things need that hold: keeping another
    /// durable step atomic with the state write
    /// (<see cref="AttachContentAsync"/>'s content write,
    /// <see cref="MoveAsync"/>'s <c>groupedUnder</c> link), and — since
    /// `WP 16.4B-R6b` — keeping the supersession <em>refusal</em> atomic
    /// with the mutation it is refusing, which is every mutator declared on
    /// this type. <b>Every caller of this method must already hold
    /// <see cref="EngineeringDomainContext.AcquireObjectWriteLockAsync"/>
    /// for <see cref="Id"/>.</b>
    /// </remarks>
    private Task PersistStateHoldingWriteLockAsync(IEngineeringObjectStateStore store, CancellationToken cancellationToken)
    {
        ThrowIfSuperseded();
        return store.SaveAsync(CaptureState(), cancellationToken);
    }

    /// <summary>
    /// Refuses a durable write through an instance <see cref="ReviseAsync"/>
    /// has already retired (`WP 16.4B-R4`).
    /// </summary>
    /// <remarks>
    /// <b>Must be called while holding this object's write lock.</b> Read
    /// outside it, this would be a race of its own: a caller could pass the
    /// check, block on the lock while <see cref="ReviseAsync"/> completes,
    /// then wake and overwrite the record with a snapshot the successor has
    /// never seen. Inside, the lock orders the two absolutely — whichever
    /// of {this write, the revision} acquires first wins, and if the
    /// revision won, this write never happened.
    /// </remarks>
    private void ThrowIfSuperseded()
    {
        if (_supersededBy is { } successor)
            throw new SupersededEngineeringObjectException(Id, successor.CurrentRevisionNumber);
    }

    /// <summary>
    /// Runs <paramref name="mutate"/> and the durable write that records it
    /// inside one hold of this object's write lock, <b>after</b> refusing a
    /// superseded instance (`WP 16.4B-R6b`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The invariant this exists to hold: an operation the platform
    /// reports as failed must not become durable, and must not change this
    /// instance either.</b> Every mutator on this type used to mutate its
    /// in-memory field first and only then call
    /// <see cref="PersistStateAsync"/>, which is where the write lock and
    /// <see cref="ThrowIfSuperseded"/> live. A concurrent
    /// <see cref="ReviseAsync"/> that took the lock in between captured the
    /// mutation into the successor, became the live registered object, and
    /// only then made the mutator's own write throw — so the caller was
    /// told the change did not happen while it had in fact landed on the
    /// only instance that still answers for this Id, and became durable on
    /// that successor's next write. `WP 16.4B-R6` closed this for the two
    /// attachment entry points by hoisting the check above the mutation;
    /// this method is the same three lines, made available to every other
    /// mutator so the treatment is uniform rather than per-method.
    /// </para>
    /// <para>
    /// <b>Why the window could not simply be narrowed.</b> It is not the
    /// few instructions between the mutation and the semaphore enqueue: a
    /// revision already queued ahead of the mutator makes the window
    /// exactly as long as the current lock hold, which
    /// <c>AttachContentAsync</c>'s content write can make arbitrarily long.
    /// Deciding {refuse, or apply and record} once, before anything has
    /// been touched, removes it instead of shrinking it.
    /// </para>
    /// <para>
    /// <b>Ordering inside the lock.</b> <see cref="ThrowIfSuperseded"/>
    /// first, so a retired instance refuses before it validates, mutates or
    /// writes anything; then <paramref name="mutate"/>, which may itself
    /// reject the operation on this object's own state (an impermissible
    /// lifecycle transition, say) and is free to throw — nothing durable
    /// has happened yet either way; then the persist. Because the whole
    /// sequence is one uninterrupted hold, <c>_supersededBy</c> cannot
    /// change between the check and the write.
    /// </para>
    /// <para>
    /// <b>Re-entrancy.</b> <see cref="Concurrency.AsyncKeyedLock"/> is not
    /// reentrant, so this calls
    /// <see cref="PersistStateHoldingWriteLockAsync"/> and never
    /// <see cref="PersistStateAsync"/>, which would deadlock against the
    /// lock this method already holds. <paramref name="mutate"/> is
    /// therefore also required to be a plain synchronous field update that
    /// never re-enters this type's public surface.
    /// </para>
    /// <para>
    /// <b>No state store, no change in behaviour.</b> A context without an
    /// <see cref="EngineeringDomainContext.ObjectStateStore"/> has no
    /// durable record, no supersession semantics to enforce and nothing to
    /// serialise against — the pre-`TD-85` in-memory-only shape every
    /// hand-assembled context in this repository's tests and samples still
    /// uses. It mutates without taking the lock, exactly as it did before,
    /// which is the same branch <see cref="AttachAsync"/> and
    /// <see cref="AttachContentAsync"/> already take.
    /// </para>
    /// </remarks>
    private async Task MutateAndPersistAsync(Action mutate, CancellationToken cancellationToken)
    {
        if (_context.ObjectStateStore is not { } store)
        {
            mutate();
            return;
        }

        using (await _context.AcquireObjectWriteLockAsync(Id, cancellationToken).ConfigureAwait(false))
        {
            ThrowIfSuperseded();

            mutate();

            await PersistStateHoldingWriteLockAsync(store, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Persists a freshly-created object's initial state (`TD-85`)
    /// through the same per-object write lock as every later mutation
    /// (`WP 16.4B-R3` — see <see cref="PersistStateAsync"/>).
    /// </summary>
    /// <remarks>
    /// Exists only for <see cref="EngineeringObjectFactory{T}.CreateAsync"/>:
    /// that type is not part of this hierarchy, so it cannot reach the
    /// <see langword="protected"/> <see cref="PersistStateAsync"/>
    /// directly — the same reason <see cref="CaptureState"/> itself is
    /// <see langword="internal"/> rather than <see langword="protected"/>.
    /// Before `WP 16.4B-R3` the factory captured and saved this object's
    /// state directly, unprotected by any lock, after already registering
    /// the instance in the repository — a window, however narrow, in
    /// which a concurrent caller that found the object through the
    /// repository could race the factory's own initial save exactly as
    /// two mutators could race each other. Routing it through this same
    /// locked path closes that window too, rather than leaving one
    /// capture-then-save call outside the serialisation this Work Package
    /// exists to add.
    /// </remarks>
    internal Task PersistInitialStateAsync(CancellationToken cancellationToken = default) =>
        PersistStateAsync(cancellationToken);

    // IEngineeringObject
    public Guid Id => Document.Id;
    public string Kind => Document.Kind;
    public int CurrentRevisionNumber => CurrentRevision.RevisionNumber;
    public DateTimeOffset CreatedAt => Document.CreatedAt;

    // IHasBusinessIdentifier
    public string? Identifier { get; }

    public string DisplayName
    {
        get { lock (_structuralLock) { return _displayName; } }
    }

    // IHasMetadata
    public string? Category => Metadata.Category;
    public string? Discipline => Metadata.Discipline;
    public string? Owner => Metadata.Owner;
    public IReadOnlyList<string> Tags => Metadata.TagsOrEmpty;
    public string? Classification => Metadata.Classification;
    public string? Notes => Metadata.Notes;

    // IHasLifecycle
    public LifecycleState Status
    {
        get { lock (_lifecycleLock) { return _status; } }
    }

    public IReadOnlyList<ILifecycleTransitionRecord> History
    {
        get { lock (_lifecycleLock) { return _history.ToList(); } }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>A refused transition writes no history entry (`WP 16.4B-R6b`).</b>
    /// This used to append the <see cref="LifecycleTransitionRecord"/> and
    /// move <c>_status</c> under <c>_lifecycleLock</c> and only then call
    /// <see cref="PersistStateAsync"/>, which is where the supersession
    /// check lived — so a transition the caller was told had been
    /// <em>rejected</em> had already stamped an actor principal id and a
    /// wall-clock timestamp into the append-only history this platform
    /// treats as its governance record, and a concurrent
    /// <see cref="ReviseAsync"/> then carried that fabricated entry into the
    /// successor and made it durable. There is no removal path for a
    /// history entry, by design, so the refusal has to happen before the
    /// entry exists rather than be compensated afterwards. It now runs
    /// inside the same hold of the write lock that performs the write —
    /// see <see cref="MutateAndPersistAsync"/>.
    /// </remarks>
    public Task TransitionAsync(LifecycleState target, CancellationToken cancellationToken = default) =>
        // A lifecycle change is state (`TD-85`) — persisted, so it survives
        // restart rather than living only in this instance.
        MutateAndPersistAsync(
            () =>
            {
                lock (_lifecycleLock)
                {
                    if (!_context.LifecycleTable.IsPermitted(_status, target))
                        throw new InvalidLifecycleTransitionException(_status, target);

                    _history.Add(new LifecycleTransitionRecord(_status, target, _context.ResolveCurrentPrincipalId(), DateTimeOffset.UtcNow, approvalId: null));
                    _status = target;
                }
            },
            cancellationToken);

    // IHasRevisions
    public string Content => CurrentRevision.Content;
    public string AuthorPrincipalId => CurrentRevision.AuthorPrincipalId;

    public async Task<IHasRevisions> ReviseAsync(string newContent, string? changeSummary, CancellationToken cancellationToken = default)
    {
        if (_selfFactory is not { } selfFactory)
            throw new InvalidOperationException($"'{GetType().Name}' was constructed without a self-factory attached — it cannot revise itself.");

        // A revision is a new *instance* of the same object, so it must
        // carry the same object's whole state. A freshly constructed
        // successor starts at `Draft` with no history and no attachments —
        // which, before `TD-85`, silently reverted a revised object's
        // lifecycle in memory (`WP 9.0B` corrected only the structural half
        // of this: rename, parent, delete, BOM line).
        //
        // `TD-85` made that in-memory loss durable: the next mutation on
        // the revised instance persists it, overwriting a recorded
        // lifecycle state and its entire transition history on disk. Found
        // by the `TD-85` closure audit and addressed by carrying the full
        // captured state rather than a hand-picked subset.
        // `WP 16.4B-R4`: the handoff is performed under this object's own
        // durable-write lock, and the predecessor is retired inside it.
        //
        // The independent release review reproduced a permanent data-loss
        // path here against the real classes. Capturing outside the lock let
        // a concurrent mutation on *this* instance land durably after the
        // snapshot was taken but before the successor became live; the
        // successor's next mutation then wrote its own whole-record snapshot
        // and silently discarded that write. Where the discarded field was
        // an attachment reference, the reconciliation sweep afterwards saw
        // content that was present, unmarked and unreferenced, and deleted
        // the file's bytes as a genuine orphan — behaving exactly as
        // designed against durable state that had quietly lost the truth.
        //
        // `WP 16.4B-R3` keyed the write lock by Id rather than by instance
        // *because* of this multi-instance hazard, and its own remarks on
        // `PersistStateAsync` name it. But serialising the two writes was
        // never sufficient: an ordered pair of writes still loses the first
        // if the second carries a snapshot taken before it. Closing it needs
        // both halves — an atomic capture, and a predecessor that refuses to
        // write again afterwards rather than overwriting from a stale view.
        //
        // `WP 16.4B-R6` closes two further holes the fifth review board
        // found here, and the whole method now runs inside the lock:
        //
        // 1. *At most one live successor.* This assignment used to be
        //    unconditional, so revising one predecessor twice minted two
        //    successors and retired the predecessor only once — the second
        //    successor overwrote the first's accepted durable writes from a
        //    snapshot that never saw them, which is verbatim the `TD-136`
        //    lost update this guard exists to prevent, reproducible in
        //    program order with no threads at all. `ThrowIfSuperseded`
        //    below makes a second revision of an already-revised instance a
        //    refusal instead. It is checked inside the lock, before the
        //    document store is asked for a new revision, so a durable
        //    revision record is never minted for a revision that is then
        //    refused. (Revising a *successor* is untouched — the guard is
        //    per-instance, and each successor is its own unrevised
        //    instance.)
        //
        // 2. *The successor is built by the Kind's own state reader.*
        //    `RestoreState` restores the base class's mutable fields and,
        //    by design, never touches `TypeState` — a type's own fields are
        //    written by `CaptureTypeState` and read back only by that
        //    type's `IRehydratable{TSelf}.Rehydrate`. Building the
        //    successor from a construction closure and then calling
        //    `RestoreState` therefore applied one half of the record and
        //    dropped the other: every type-specific field reverted to its
        //    construction-time value and was then persisted by the
        //    successor's next write. Passing the captured state to the
        //    self-factory makes "revise" reconstruct exactly the way
        //    "restart" does, through one reader, for all of the Kinds — so
        //    there really is one definition of "this object's state", which
        //    is what the note above always claimed.
        using (await _context.AcquireObjectWriteLockAsync(Id, cancellationToken).ConfigureAwait(false))
        {
            ThrowIfSuperseded();

            var newRevision = await _context.Store.ReviseAsync(Id, newContent, changeSummary, cancellationToken).ConfigureAwait(false);
            var refreshedDocument = new EngineeringDocument(Document.Id, Document.Kind, newRevision.RevisionNumber, Document.CreatedAt);

            var state = CaptureState();

            var revised = selfFactory(refreshedDocument, newRevision, state);
            revised.AttachSelfFactory(selfFactory);
            revised.RestoreState(state);

            _supersededBy = revised;
            _context.Repository.Register(revised);

            return revised;
        }
    }

    public async Task<IReadOnlyList<IRevisionRecord>> GetRevisionHistoryAsync(CancellationToken cancellationToken = default)
    {
        var revisions = await _context.Store.GetRevisionHistoryAsync(Id, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<IRevisionRecord> records = revisions
            .Select(r => (IRevisionRecord)new RevisionRecord(r.RevisionNumber, r.Content, r.ChangeSummary, r.AuthorPrincipalId, r.CreatedAt))
            .ToList();

        return records;
    }

    // IHasRelationships
    public async Task LinkAsync(Guid targetId, string relationshipKind, CancellationToken cancellationToken = default)
    {
        if (targetId == Id)
            throw new SelfReferentialRelationshipException(Id);

        await _context.Store.LinkAsync(Id, targetId, relationshipKind, cancellationToken).ConfigureAwait(false);

        var category = RelationshipKindCategoryMap.InferCategory(relationshipKind);
        _context.RelationshipRepository.Record(
            new EngineeringRelationship(Id, targetId, relationshipKind, category, _context.ResolveCurrentPrincipalId(), DateTimeOffset.UtcNow));
    }

    public Task<IReadOnlyList<IEngineeringRelationship>> GetRelationshipsAsync(CancellationToken cancellationToken = default) =>
        _context.RelationshipRepository.GetOutgoingAsync(Id, cancellationToken);

    // ITraceable
    public Task<IEvidence> GetEvidenceAsync(CancellationToken cancellationToken = default) =>
        _context.EvidenceComposer.ComposeAsync(Id, cancellationToken);

    // IValidatable
    public Task<IValidationResult> ValidateAsync(CancellationToken cancellationToken = default) =>
        _context.ValidationRuleSet.ValidateAsync(this, cancellationToken);

    // IHasAttachments

    /// <inheritdoc />
    /// <remarks>
    /// `WP 16.4B-R6`. The supersession check and the in-memory add happen
    /// inside one hold of this object's write lock, in that order, so a
    /// refused attach never leaves the instance claiming an attachment it
    /// does not have. Before this the add ran first and unconditionally:
    /// the caller was told the write was refused and the instance disagreed
    /// for ever, and a concurrent <see cref="ReviseAsync"/> could carry the
    /// phantom into the successor. `WP 16.4B-R5` compensated
    /// <see cref="AttachContentAsync"/> for exactly this and left the
    /// metadata-only entry point untouched.
    /// </remarks>
    public async Task AttachAsync(IAttachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        // No durable state means no supersession semantics to enforce and
        // nothing to serialise against — the pre-`TD-85` in-memory-only
        // shape every hand-assembled context still uses, unchanged.
        if (_context.ObjectStateStore is not { } store)
        {
            lock (_attachments) { _attachments.Add(attachment); }
            return;
        }

        using (await _context.AcquireObjectWriteLockAsync(Id, cancellationToken).ConfigureAwait(false))
        {
            ThrowIfSuperseded();

            lock (_attachments) { _attachments.Add(attachment); }

            // `WP 16.4B-R6b`: the same capture-and-save this always did,
            // routed through the one helper every mutator on this type now
            // uses, so there is a single durable-write body rather than one
            // method reaching past it to `store.SaveAsync` directly. Its
            // repeat of `ThrowIfSuperseded` is redundant here and
            // deliberately not special-cased — the answer cannot have
            // changed inside one hold of the lock.
            await PersistStateHoldingWriteLockAsync(store, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<IReadOnlyList<IAttachment>> GetAttachmentsAsync(CancellationToken cancellationToken = default)
    {
        lock (_attachments)
        {
            IReadOnlyList<IAttachment> snapshot = _attachments.ToList();
            return Task.FromResult(snapshot);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Write-intent marker (`WP 16.4B-R2`).</b> A marker for
    /// <c>attachmentId</c> is recorded before the content write and
    /// cleared only after the state write that references it succeeds —
    /// bracketing both writes without reordering either of them.
    /// <c>ADR-0114</c> Decision 4 (content before the state that names it)
    /// is unchanged: the marker is additional, durable information a
    /// sweep can consult, never a change to what gets written when. See
    /// <see cref="IAttachmentWriteIntentStore"/> for why a marker can only
    /// ever prevent a sweep from collecting content, never cause it to.
    /// Skipped entirely (no marker, no failure) when this domain has no
    /// <see cref="EngineeringDomainContext.AttachmentWriteIntentStore"/>
    /// configured — see that property's own remarks for why that is not a
    /// regression.
    /// </para>
    /// <para>
    /// <b>The whole sequence is one hold of this object's write lock
    /// (`WP 16.4B-R6`).</b> That is what makes an attach atomic with
    /// respect to <see cref="ReviseAsync"/>, and it is the reason there is
    /// no rollback here to get wrong. The cost is that a large content
    /// write now holds this <em>one object's</em> write lock while it runs;
    /// the alternative was a window in which a concurrent revision could
    /// adopt an attachment whose durable write was then refused, which the
    /// release review board turned into permanent, silent destruction of
    /// content the live successor referenced.
    /// </para>
    /// </remarks>
    public async Task<IAttachment> AttachContentAsync(
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var contentStore = _context.AttachmentContentStore
            ?? throw new InvalidOperationException(
                "This engineering domain has no attachment content store configured, so file content cannot be stored. " +
                "Use AttachAsync to record attachment metadata alone.");

        var writeIntentStore = _context.AttachmentWriteIntentStore;
        var store = _context.ObjectStateStore;
        var attachmentId = Guid.NewGuid();

        // `WP 16.4B-R6`: the lock is taken *first*, and held across all four
        // durable steps, rather than being taken by `PersistStateAsync` at
        // the third of them.
        //
        // `WP 16.4B-R4` taught `PersistStateAsync` to throw
        // `SupersededEngineeringObjectException` when a concurrent
        // `ReviseAsync` retires this instance mid-call. That put an
        // *ordinary*, non-crash exception between the content write and the
        // state write, where before only a process crash could land, and
        // stranded the marker (`TD-139`). `WP 16.4B-R5` answered it with a
        // compensation that deleted the content bytes on that one exception
        // type, arguing that because the supersession guard throws strictly
        // before `store.SaveAsync`, "nothing was written" and the delete is
        // a true rollback.
        //
        // The fifth review board falsified that argument, three times
        // independently, against the real classes. "Nothing was written" is
        // a statement about *this call's own* save. It says nothing about
        // the durable record for this `Id`, which the `ReviseAsync`
        // successor also owns: the in-memory add below used to happen
        // *before* the lock, so a revision that acquired the lock in between
        // captured the pending attachment into the successor, became the
        // live registered object, and only then made this call's own write
        // throw. The compensation then deleted the bytes of an attachment
        // the live successor holds and persists — a dangling reference
        // nothing repairs, because the reconciliation sweep hunts content
        // nothing references and never a reference to content that is gone.
        //
        // Taking the lock here removes the window rather than compensating
        // for it, which is why this is not a wider `catch`. The alternatives
        // considered and rejected: (a) adding to `_attachments` inside the
        // lock but leaving the content write outside still lets a refused
        // attach leave written bytes behind and still needs a rollback to
        // decide about; (b) making the compensation conditional on the
        // attachment not having been inherited requires proving a negative
        // about every live instance and every durable record for this Id,
        // after the fact, from inside a failure path. Under the ordering
        // below, {refuse, or write everything} is decided once, before
        // anything durable exists, and the two failure classes the board
        // named collapse into one line each:
        //
        //   (a) nothing durable was written — the refusal below happens
        //       before the marker, so there is nothing to roll back at all;
        //   (b) a successor legitimately inherited the attachment — only
        //       reachable once this call's state write has committed, at
        //       which point this method has already returned successfully
        //       and no compensation exists to destroy anything.
        //
        // Note the marker is now set *inside* the lock. The `TD-139`
        // stranding the board could still reach by cancelling while waiting
        // for the lock (`P2-2`) is closed by that alone: a cancelled lock
        // wait now throws before the marker exists.
        using var writeLock = store is null
            ? null
            : await _context.AcquireObjectWriteLockAsync(Id, cancellationToken).ConfigureAwait(false);

        // Refuse before anything durable exists. A context with no state
        // store has no durable record, no supersession semantics and
        // nothing to serialise against — the pre-`TD-85` in-memory-only
        // shape, unchanged.
        if (store is not null)
            ThrowIfSuperseded();

        // Mark first: any sweep that can see this attachment's content
        // from this point forward must also be able to see that it is
        // still being written, and skip it.
        if (writeIntentStore is not null)
            await writeIntentStore.MarkAsync(attachmentId, cancellationToken).ConfigureAwait(false);

        // Content first: a crash between the two writes leaves unreferenced
        // bytes, not an attachment promising content nobody stored.
        string contentHash;

        try
        {
            contentHash = await contentStore.SaveAsync(attachmentId, content, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // `WP 16.4B-R6`. The content write failed, so the marker is
            // protecting nothing: `attachmentId` is a fresh Guid that has
            // never left this method, nothing in memory or on disk names it,
            // and this instance has not yet been told about it. Clearing the
            // marker therefore cannot expose a referenced attachment — it
            // demotes whatever a half-finished write left behind to an
            // ordinary unreferenced orphan the sweep can collect, instead of
            // bytes a marker protects for ever. Nothing is *deleted* here:
            // what is withdrawn is a guard, never content.
            if (writeIntentStore is not null)
            {
                try
                {
                    await writeIntentStore.ClearAsync(attachmentId, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Deliberately swallowed, and deliberately conservative:
                    // the marker stays set and the bytes stay uncollectable —
                    // a bounded leak, now reported by
                    // `AttachmentContentReconciliationReport.SkippedByMarker`.
                    // Rethrowing would replace the real cause with a cleanup
                    // failure.
                }
            }

            throw;
        }

        var attachment = new Attachment(attachmentId, fileName, contentType, content.Length, contentHash);

        lock (_attachments) { _attachments.Add(attachment); }

        // Still inside the same lock acquisition, so `_supersededBy` cannot
        // have changed since the refusal check above: this call either owns
        // the record for the whole sequence or never wrote to it.
        //
        // A failure *here* is the case `WP 16.4B-R5` correctly refused to
        // compensate and this Work Package still refuses to: an I/O or
        // serialisation fault can be raised after the record has landed, in
        // which case the attachment is live and referenced and deleting its
        // bytes would be real data loss rather than cleanup. The outcome is
        // the disclosed, conservative `TD-97` one — a stale marker, content
        // left uncollected, no deletion — and it is now visible to an
        // operator through the reconciliation report rather than silent.
        if (store is not null)
            await PersistStateHoldingWriteLockAsync(store, cancellationToken).ConfigureAwait(false);

        // Clear last, only once the state that references this attachment
        // is itself durable — a crash before this point leaves a stale
        // marker, whose only effect is that this content is never swept
        // (the pre-existing, disclosed `TD-97` outcome), never data loss.
        //
        // `CancellationToken.None`, matching the compensation above
        // (`WP 16.4B-R6`, board finding `P2-3`): by this line the state
        // write has already landed, so honouring a cancellation here would
        // strand a marker on a successfully attached, live, referenced
        // attachment. Cancellation is respected everywhere it can still
        // prevent work, and nowhere it could only leave one half-done.
        if (writeIntentStore is not null)
            await writeIntentStore.ClearAsync(attachmentId, CancellationToken.None).ConfigureAwait(false);

        return attachment;
    }

    /// <inheritdoc />
    public async Task<AttachmentContentResult> ReadAttachmentContentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        IAttachment? attachment;
        lock (_attachments) { attachment = _attachments.FirstOrDefault(a => a.Id == attachmentId); }

        // An attachment this object does not have holds no content for it.
        // Reported as Missing rather than thrown: asking about the wrong id
        // is the same passive read as asking about one whose bytes were
        // never stored, and neither is a failure of this object.
        if (attachment is null)
            return AttachmentContentResult.Missing();

        if (_context.AttachmentContentStore is not { } contentStore)
            return AttachmentContentResult.Missing();

        return await contentStore
            .ReadAsync(attachment.Id, attachment.ContentHash, attachment.SizeInBytes, cancellationToken)
            .ConfigureAwait(false);
    }

    // ISearchable
    public virtual string SearchableText =>
        string.Join(' ', new[] { DisplayName, Identifier, Category, Content }.Where(s => !string.IsNullOrWhiteSpace(s)));

    // IRenamable (WP 9.0A — additive; see StructuralMutation.cs)

    /// <inheritdoc />
    /// <remarks>
    /// Argument validation stays outside the lock: a malformed name is a
    /// contract violation of this call, answered before any collaborator is
    /// involved, exactly as it was. The rename itself and the durable write
    /// that records it are one hold of the write lock, refusing first
    /// (`WP 16.4B-R6b`) — see <see cref="MutateAndPersistAsync"/>.
    /// </remarks>
    public Task RenameAsync(string newDisplayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newDisplayName);

        return MutateAndPersistAsync(
            () => { lock (_structuralLock) { _displayName = newDisplayName; } },
            cancellationToken);
    }

    // IHasParent (WP 9.0A — additive; see StructuralMutation.cs)
    public Guid? ParentId
    {
        get { lock (_structuralLock) { return _parentId; } }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A refused move records no relationship (`WP 16.4B-R6b`).</b> The
    /// <c>groupedUnder</c> link below is permanent and append-only — the
    /// platform has no path that removes one — and it used to be written
    /// before <see cref="PersistStateAsync"/>, which is where the
    /// supersession check lived. A move the caller was told had been
    /// refused therefore left a durable relationship claiming it happened,
    /// and the reparent itself leaked into a concurrent
    /// <see cref="ReviseAsync"/>'s successor. The refusal now precedes
    /// both, inside one hold of the write lock that also performs them.
    /// </para>
    /// <para>
    /// <b>Why <see cref="GuardAgainstCircularParentAsync"/> runs outside
    /// that lock.</b> It mutates nothing — it walks other objects' parent
    /// edges through the repository purely to decide whether to throw — so
    /// there is nothing about it that has to be atomic with this object's
    /// own write, and a caller it rejects has changed nothing either way.
    /// Against that, moving it inside would lengthen the hold by an
    /// unbounded chain walk (the widened-window failure the review board
    /// raised against `WP 16.4B-R6`), and it calls
    /// <see cref="IHasParent.ParentId"/> on arbitrary other objects,
    /// including types outside this assembly, from inside a lock that is
    /// not reentrant — a deadlock surface that does not exist today. It
    /// therefore stays where it was. The consequence, unchanged by this
    /// Work Package and pre-existing, is that two concurrent moves can
    /// still each pass the guard and between them form a cycle; that is a
    /// separate defect and is recorded, not fixed here.
    /// </para>
    /// </remarks>
    public async Task MoveAsync(Guid? newParentId, CancellationToken cancellationToken = default)
    {
        if (newParentId is { } candidateParentId)
            await GuardAgainstCircularParentAsync(candidateParentId, cancellationToken).ConfigureAwait(false);

        // Kept inline rather than routed through `MutateAndPersistAsync`,
        // because the durable link has to land inside the same hold of the
        // lock, between the mutation and the persist. The lock ordering is
        // the one `ReviseAsync` already established and the board verified:
        // object write lock, then `EngineeringDocumentStore`'s persistence
        // keys. `LinkAsync` makes no callback into this domain and never
        // re-enters `AcquireObjectWriteLockAsync`, so there is no inversion
        // and no re-entrancy.
        if (_context.ObjectStateStore is not { } store)
        {
            lock (_structuralLock) { _parentId = newParentId; }

            if (newParentId is { } unpersistedParentId)
                await LinkAsync(unpersistedParentId, "groupedUnder", cancellationToken).ConfigureAwait(false);

            return;
        }

        using (await _context.AcquireObjectWriteLockAsync(Id, cancellationToken).ConfigureAwait(false))
        {
            ThrowIfSuperseded();

            lock (_structuralLock)
            {
                _parentId = newParentId;
            }

            // Permanent, append-only audit trail — the old "groupedUnder" link (if
            // any) is never removed, so a full move history survives even though
            // ParentId itself only ever reflects the latest move (WP 9.0A).
            if (newParentId is { } parentId)
                await LinkAsync(parentId, "groupedUnder", cancellationToken).ConfigureAwait(false);

            // The structural parent is the edge that makes an object belong to
            // a project — it must survive restart (`TD-85`).
            await PersistStateHoldingWriteLockAsync(store, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task GuardAgainstCircularParentAsync(Guid candidateParentId, CancellationToken cancellationToken)
    {
        if (candidateParentId == Id)
            throw new CircularParentAssignmentException(Id, candidateParentId);

        var current = candidateParentId;
        var visited = new HashSet<Guid> { Id };

        while (visited.Add(current))
        {
            var candidate = await _context.Repository.FindAsync(current, cancellationToken).ConfigureAwait(false);

            if (candidate is not IHasParent { ParentId: { } nextParentId })
                return;

            if (nextParentId == Id)
                throw new CircularParentAssignmentException(Id, candidateParentId);

            current = nextParentId;
        }
    }

    // IDeletable (WP 9.0A — additive; see StructuralMutation.cs)
    public bool IsDeleted
    {
        get { lock (_structuralLock) { return _isDeleted; } }
    }

    /// <remarks>
    /// <b>`TD-97` closure — attachment content is released on delete.</b>
    /// This object's metadata (<see cref="IAttachment"/> records) is never
    /// erased — deletion is soft, and the platform's own append-only,
    /// nothing-silently-destroyed ethos keeps every attachment's history
    /// intact for a deleted object exactly as for a live one. The
    /// <em>bytes</em> a deleted object's attachments held are a different
    /// matter: nothing can ever view them again through this object, so
    /// they are released via <see cref="IAttachmentContentStore.DeleteAsync"/>
    /// once <see cref="IsDeleted"/> is durably recorded — after, not
    /// before, so a crash between the two leaves the object durably
    /// deleted with its content merely unreleased yet (the pre-existing,
    /// disclosed `TD-97` state — closed the rest of the way by the
    /// content sweep, never by reordering this write ahead of the
    /// deletion it depends on).
    /// <para>
    /// <b>A refused delete deletes nothing (`WP 16.4B-R6b`).</b>
    /// <c>_isDeleted</c> was set under <c>_structuralLock</c> before
    /// <see cref="PersistStateAsync"/>, which is where the supersession
    /// check lived, so a delete the caller was told had been refused still
    /// left the object soft-deleted — in memory, and durably once a
    /// concurrent <see cref="ReviseAsync"/>'s successor had inherited the
    /// flag and written it. That is not recoverable divergence:
    /// <c>_isDeleted</c> has no writer anywhere that clears it (the field
    /// is set here and restored by <see cref="RestoreState"/>, and there is
    /// no undelete on any facet), and every read model filters
    /// <c>IDeletable { IsDeleted: true }</c> out — so an operation reported
    /// as <em>failed</em> removed the object from the whole product with no
    /// supported way back, and skipped the `TD-97` byte release on the way
    /// past. The refusal now happens before the flag is set.
    /// </para>
    /// <para>
    /// The byte release below is deliberately left exactly where it was:
    /// after the state write, outside the write lock, on the success path
    /// only. A refusal throws above it and therefore never reaches it —
    /// which is the correct outcome for bytes belonging to an object that
    /// is, after the refusal, not deleted.
    /// </para>
    /// </remarks>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        var liveChildren = all.Count(o =>
            o is IHasParent { ParentId: { } parentId } && parentId == Id &&
            o is not IDeletable { IsDeleted: true });

        if (liveChildren > 0)
            throw new EngineeringObjectHasChildrenException(Id, liveChildren);

        await MutateAndPersistAsync(
            () => { lock (_structuralLock) { _isDeleted = true; } },
            cancellationToken)
            .ConfigureAwait(false);

        if (_context.AttachmentContentStore is { } contentStore)
        {
            List<IAttachment> attachmentsSnapshot;
            lock (_attachments) { attachmentsSnapshot = _attachments.ToList(); }

            foreach (var attachment in attachmentsSnapshot)
                await contentStore.DeleteAsync(attachment.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    // IHasBomLine (WP 9.0B — additive; see BillOfMaterials.cs)
    public decimal Quantity
    {
        get { lock (_structuralLock) { return _quantity; } }
    }

    public string? UnitOfMeasure
    {
        get { lock (_structuralLock) { return _unitOfMeasure; } }
    }

    public string? FindNumber
    {
        get { lock (_structuralLock) { return _findNumber; } }
    }

    public string? ItemNumber
    {
        get { lock (_structuralLock) { return _itemNumber; } }
    }

    public string? ReferenceDesignator
    {
        get { lock (_structuralLock) { return _referenceDesignator; } }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Argument validation stays outside the lock, as it was: a
    /// non-positive quantity is a contract violation of this call, not a
    /// question about this object's durable state. The five fields and the
    /// write that records them are one hold of the write lock, refusing
    /// first (`WP 16.4B-R6b`) — see <see cref="MutateAndPersistAsync"/>.
    /// </remarks>
    public Task SetBomLineAsync(
        decimal quantity, string? unitOfMeasure = null, string? findNumber = null,
        string? itemNumber = null, string? referenceDesignator = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, $"Quantity must be positive ({StructuralValidationRules.QuantityMustBePositive}).");

        return MutateAndPersistAsync(
            () =>
            {
                lock (_structuralLock)
                {
                    _quantity = quantity;
                    _unitOfMeasure = unitOfMeasure;
                    _findNumber = findNumber;
                    _itemNumber = itemNumber;
                    _referenceDesignator = referenceDesignator;
                }
            },
            cancellationToken);
    }
}
