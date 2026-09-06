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
    private Func<IEngineeringDocument, IDocumentRevision, EngineeringObjectBase>? _selfFactory;

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

    /// <summary>Called once by the factory that constructed this instance, so <see cref="ReviseAsync"/> can produce a correctly-typed successor.</summary>
    internal void AttachSelfFactory(Func<IEngineeringDocument, IDocumentRevision, EngineeringObjectBase> selfFactory) =>
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
    /// Restores the mutable state a constructor cannot carry (`TD-85`) —
    /// applied by the rehydrator immediately after reconstructing an
    /// instance, so the object is fully itself before any caller can
    /// observe it.
    /// </summary>
    /// <remarks>
    /// Identifier, display name, metadata and every type-specific field
    /// arrive through the rehydrating constructor; this method restores
    /// what lives in mutable fields instead: lifecycle state and its
    /// history, structural parent, deletion, BOM line, and attachments.
    /// </remarks>
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
    /// reentrant. Every mutator on this hierarchy calls this method at
    /// most once, and never while already holding this object's write
    /// lock (audited across the whole <c>EngineeringDomain</c> tree and
    /// every composed caller in <c>Tempest.App</c> for `WP 16.4B-R3`) — a
    /// method that performs more than one durable step (for example
    /// <see cref="MoveAsync"/>'s link followed by its own persist) always
    /// completes its non-locking steps first and calls this method
    /// exactly once, last.
    /// </para>
    /// </remarks>
    protected async Task PersistStateAsync(CancellationToken cancellationToken = default)
    {
        if (_context.ObjectStateStore is not { } store)
            return;

        using (await _context.AcquireObjectWriteLockAsync(Id, cancellationToken).ConfigureAwait(false))
        {
            // `WP 16.4B-R4`. Checked *inside* the lock, never before it.
            // Checked outside, this would be a race of its own: a caller
            // could pass the check, block on the lock while `ReviseAsync`
            // completes, then wake and overwrite the record with a snapshot
            // the successor has never seen. Inside, the lock orders the two
            // absolutely — whichever of {this write, the revision} acquires
            // first wins, and if the revision won, this write never happened.
            if (_supersededBy is { } successor)
                throw new SupersededEngineeringObjectException(Id, successor.CurrentRevisionNumber);

            await store.SaveAsync(CaptureState(), cancellationToken).ConfigureAwait(false);
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

    public Task TransitionAsync(LifecycleState target, CancellationToken cancellationToken = default)
    {
        lock (_lifecycleLock)
        {
            if (!_context.LifecycleTable.IsPermitted(_status, target))
                throw new InvalidLifecycleTransitionException(_status, target);

            _history.Add(new LifecycleTransitionRecord(_status, target, _context.ResolveCurrentPrincipalId(), DateTimeOffset.UtcNow, approvalId: null));
            _status = target;
        }

        // A lifecycle change is state (`TD-85`) — persisted here, so it
        // survives restart rather than living only in this instance.
        return PersistStateAsync(cancellationToken);
    }

    // IHasRevisions
    public string Content => CurrentRevision.Content;
    public string AuthorPrincipalId => CurrentRevision.AuthorPrincipalId;

    public async Task<IHasRevisions> ReviseAsync(string newContent, string? changeSummary, CancellationToken cancellationToken = default)
    {
        if (_selfFactory is null)
            throw new InvalidOperationException($"'{GetType().Name}' was constructed without a self-factory attached — it cannot revise itself.");

        var newRevision = await _context.Store.ReviseAsync(Id, newContent, changeSummary, cancellationToken).ConfigureAwait(false);
        var refreshedDocument = new EngineeringDocument(Document.Id, Document.Kind, newRevision.RevisionNumber, Document.CreatedAt);

        var revised = _selfFactory(refreshedDocument, newRevision);
        revised.AttachSelfFactory(_selfFactory);

        // A revision is a new *instance* of the same object, so it must
        // carry the same object's whole state. `_selfFactory` only ever
        // knew the values passed to the original factory call, so a freshly
        // constructed successor starts at `Draft` with no history and no
        // attachments — which, before `TD-85`, silently reverted a revised
        // object's lifecycle in memory (`WP 9.0B` corrected only the
        // structural half of this: rename, parent, delete, BOM line).
        //
        // `TD-85` made that in-memory loss durable: the next mutation on
        // the revised instance persists it, overwriting a recorded
        // lifecycle state and its entire transition history on disk.
        // Found by the `TD-85` closure audit and fixed here by carrying the
        // full captured state rather than a hand-picked subset — the same
        // capture/restore pair rehydration already uses, so there is
        // exactly one definition of "this object's state" and a field added
        // to it can never again be forgotten by one of two copy paths.
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
        using (await _context.AcquireObjectWriteLockAsync(Id, cancellationToken).ConfigureAwait(false))
        {
            revised.RestoreState(CaptureState());
            _supersededBy = revised;
            _context.Repository.Register(revised);
        }

        return revised;
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
    public Task AttachAsync(IAttachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        lock (_attachments) { _attachments.Add(attachment); }
        return PersistStateAsync(cancellationToken);
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
        var attachmentId = Guid.NewGuid();

        // Mark first: any sweep that can see this attachment's content
        // from this point forward must also be able to see that it is
        // still being written, and skip it.
        if (writeIntentStore is not null)
            await writeIntentStore.MarkAsync(attachmentId, cancellationToken).ConfigureAwait(false);

        // Content first: a crash between the two writes leaves unreferenced
        // bytes, not an attachment promising content nobody stored.
        var contentHash = await contentStore.SaveAsync(attachmentId, content, cancellationToken).ConfigureAwait(false);

        var attachment = new Attachment(attachmentId, fileName, contentType, content.Length, contentHash);

        lock (_attachments) { _attachments.Add(attachment); }

        // `WP 16.4B-R5`: the state write is compensated, not merely awaited.
        //
        // Until `WP 16.4B-R4`, only a process crash could interrupt between
        // the marker being set and its being cleared, and the comment below
        // reasoned from exactly that: a crash leaves a stale marker whose
        // only effect is that this content is never swept — the disclosed
        // `TD-97` outcome, never data loss. `WP 16.4B-R4` then taught
        // `PersistStateAsync` to throw `SupersededEngineeringObjectException`
        // when a concurrent `ReviseAsync` retires this instance mid-call,
        // which put an *ordinary*, non-crash exception inside that window and
        // silently falsified the premise. The release review board
        // reproduced the result: marker set, bytes durably written, state
        // write refused, `ClearAsync` never reached — a marker stranded set
        // for ever, and content the sweep must therefore refuse to collect
        // for ever. A permanent leak, reachable without any crash at all.
        //
        // The compensation restores the pre-call state rather than leaving
        // the caller half-applied. Content is deleted *before* the marker is
        // cleared, never after: if the delete itself fails, the marker stays
        // set and the bytes stay uncollectable, which is the conservative
        // end of the trade — a bounded leak rather than content the sweep
        // would delete while something still believed it existed. The
        // original exception always wins; a failure inside the compensation
        // is suppressed rather than allowed to mask why the write failed.
        //
        // <b>It catches this one exception type and no other, and that is
        // the whole safety argument.</b> The supersession guard throws
        // strictly *before* `store.SaveAsync`, so on this path — and only on
        // this path — it is known that nothing was written and deleting the
        // content is a true rollback. Any other exception may have been
        // raised *after* the state landed durably, in which case the
        // attachment is live and referenced and deleting its bytes would be
        // real data loss, not cleanup. The first version of this fix caught
        // everything, and `SweepAsync_AStaleMarker_LeavesContentUncollected‐
        // RatherThanErroring` — which simulates a failure after a successful
        // save — caught it doing exactly that. For those cases the original,
        // conservative behaviour stands: a stale marker, content left
        // uncollected, and no deletion.
        try
        {
            await PersistStateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SupersededEngineeringObjectException)
        {
            lock (_attachments) { _attachments.Remove(attachment); }

            try
            {
                await contentStore.DeleteAsync(attachmentId, CancellationToken.None).ConfigureAwait(false);

                if (writeIntentStore is not null)
                    await writeIntentStore.ClearAsync(attachmentId, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Deliberately swallowed. The marker remains set and the
                // bytes remain uncollectable, which is safe; re-throwing
                // here would replace the real cause with a cleanup failure.
            }

            throw;
        }

        // Clear last, only once the state that references this attachment
        // is itself durable — a crash before this point leaves a stale
        // marker, whose only effect is that this content is never swept
        // (the pre-existing, disclosed `TD-97` outcome), never data loss.
        if (writeIntentStore is not null)
            await writeIntentStore.ClearAsync(attachmentId, cancellationToken).ConfigureAwait(false);

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
    public Task RenameAsync(string newDisplayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newDisplayName);

        lock (_structuralLock)
        {
            _displayName = newDisplayName;
        }

        return PersistStateAsync(cancellationToken);
    }

    // IHasParent (WP 9.0A — additive; see StructuralMutation.cs)
    public Guid? ParentId
    {
        get { lock (_structuralLock) { return _parentId; } }
    }

    public async Task MoveAsync(Guid? newParentId, CancellationToken cancellationToken = default)
    {
        if (newParentId is { } candidateParentId)
            await GuardAgainstCircularParentAsync(candidateParentId, cancellationToken).ConfigureAwait(false);

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
        await PersistStateAsync(cancellationToken).ConfigureAwait(false);
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
    /// </remarks>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        var liveChildren = all.Count(o =>
            o is IHasParent { ParentId: { } parentId } && parentId == Id &&
            o is not IDeletable { IsDeleted: true });

        if (liveChildren > 0)
            throw new EngineeringObjectHasChildrenException(Id, liveChildren);

        lock (_structuralLock)
        {
            _isDeleted = true;
        }

        await PersistStateAsync(cancellationToken).ConfigureAwait(false);

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

    public Task SetBomLineAsync(
        decimal quantity, string? unitOfMeasure = null, string? findNumber = null,
        string? itemNumber = null, string? referenceDesignator = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, $"Quantity must be positive ({StructuralValidationRules.QuantityMustBePositive}).");

        lock (_structuralLock)
        {
            _quantity = quantity;
            _unitOfMeasure = unitOfMeasure;
            _findNumber = findNumber;
            _itemNumber = itemNumber;
            _referenceDesignator = referenceDesignator;
        }

        return PersistStateAsync(cancellationToken);
    }
}
