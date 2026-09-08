using Tempest.Core.Audit;
using Tempest.Core.EngineeringData;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The shared plumbing every concrete canonical object class derives from — implements every facet
/// interface generically (ADR-0075's composition rule governs the <i>contracts</i>; this base class is
/// ordinary implementation reuse, orthogonal to it, the same way <see cref="Modules.ModuleLifecycleBase"/>
/// gives every module its own shared no-op lifecycle plumbing). A concrete Kind class only ever declares
/// the specific facets its own interface actually composes — inheriting the rest here costs nothing extra.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every mutator has the same three steps, in this order (`ADR-0145`):
/// project, commit, apply.</b> A mutator computes the object's next
/// <see cref="EngineeringObjectState"/> as a value, commits it — with any
/// document, revision, reference or attachment bytes it also needs, and
/// its audit row — through one transaction on the platform's single
/// store, and only then writes the result into this instance's own fields
/// and into the in-memory repositories.
/// </para>
/// <para>
/// <b>That ordering is why this type has no undo.</b> Until `WP 17.1B`
/// every mutator changed its fields first and wrote afterwards, so a
/// refused or failed write left the instance carrying a change the caller
/// had been told did not happen — and the whole of
/// <c>MutationRollbackPoint</c>, <c>CaptureRollbackPoint</c>,
/// <c>RollBackTo</c>, <c>RollBackOnFailureAsync</c> and
/// <c>DurableRecordAlreadyShowsThisStateAsync</c> existed to undo it,
/// conditionally, on evidence that could not always be obtained. None of
/// it is here any more, because nothing is mutated before the commit and
/// there is therefore nothing to put back. What those five members
/// promised conditionally, the transaction gives unconditionally.
/// </para>
/// </remarks>
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
    // values the original construction call happened to pass. See
    // `ReviseAsync`.
    private Func<IEngineeringDocument, IDocumentRevision, EngineeringObjectState, EngineeringObjectBase>? _selfFactory;

    // Non-null once `ReviseAsync` has committed a successor for this Id and
    // registered it in place of this instance. Read and written only inside
    // the domain write lock, which is what makes it an ordering point
    // rather than a hint.
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

    /// <summary>Initialises a new instance of the <see cref="EngineeringObjectBase"/> class.</summary>
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

    /// <summary>This object's own document — its identity, Kind and creation instant.</summary>
    protected IEngineeringDocument Document { get; }

    /// <summary>The revision this instance answers for.</summary>
    protected IDocumentRevision CurrentRevision { get; }

    /// <summary>The metadata this object was created or rehydrated with.</summary>
    protected EngineeringObjectMetadata Metadata { get; }

    /// <summary>The shared collaborators and the transaction boundary.</summary>
    protected EngineeringDomainContext Context => _context;

    /// <summary>
    /// Called once by the factory or rehydrator that constructed this
    /// instance, so <see cref="ReviseAsync"/> can produce a correctly-typed
    /// successor from a captured <see cref="EngineeringObjectState"/>
    /// (`WP 16.4B-R6`).
    /// </summary>
    internal void AttachSelfFactory(Func<IEngineeringDocument, IDocumentRevision, EngineeringObjectState, EngineeringObjectBase> selfFactory) =>
        _selfFactory = selfFactory;

    // ================================================================
    // State capture and application
    // ================================================================

    /// <summary>
    /// Captures this object's own complete state (`TD-85`) — everything
    /// that must come back after a restart for this to be the same object.
    /// Always stamped with
    /// <see cref="EngineeringObjectStateStore.CurrentSchemaVersion"/>
    /// (`TD-87`, `ADR-0120`).
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
    /// Projects <c>_attachments</c> under the monitor its own writers use
    /// (`WP 16.4B-R5`) — taken innermost, inside <c>_lifecycleLock</c> and
    /// <c>_structuralLock</c>, and never held while either of those is
    /// acquired, so no lock-order inversion is introduced.
    /// </summary>
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
    /// Identifier, display name, metadata and <b>every type-specific
    /// field</b> arrive through the Kind's own
    /// <see cref="IRehydratable{TSelf}.Rehydrate"/> constructor, which is
    /// the reader for the <see cref="EngineeringObjectState.TypeState"/>
    /// half of the record; this restores what lives in mutable base fields
    /// instead. It deliberately does not read
    /// <see cref="EngineeringObjectState.TypeState"/>: there is no
    /// base-class writer for a type's own fields, only that type's own
    /// constructor and its <see cref="ApplyTypeState"/> override.
    /// </remarks>
    internal void RestoreState(EngineeringObjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        ApplyBaseState(state, attachments: null);
    }

    /// <summary>
    /// Writes a committed <paramref name="state"/> into this instance —
    /// the third and last step of every mutator (`ADR-0145`).
    /// </summary>
    /// <remarks>
    /// Called only after the transaction that made <paramref name="state"/>
    /// durable has committed, so this instance and its record cannot
    /// disagree. <paramref name="attachments"/> lets an attach path keep
    /// the caller's own <see cref="IAttachment"/> instances rather than
    /// swapping them for equal-valued copies rebuilt from the record;
    /// everywhere else it is <see langword="null"/> and the record is the
    /// only source.
    /// </remarks>
    private void ApplyCommittedState(EngineeringObjectState state, IReadOnlyList<IAttachment>? attachments = null)
    {
        ApplyBaseState(state, attachments);
        ApplyTypeState(state);
    }

    private void ApplyBaseState(EngineeringObjectState state, IReadOnlyList<IAttachment>? attachments)
    {
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

            if (attachments is not null)
            {
                _attachments.AddRange(attachments);
            }
            else
            {
                foreach (var attachment in state.Attachments)
                    _attachments.Add(new Attachment(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeInBytes, attachment.ContentHash));
            }
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
    /// reads them back.
    /// </summary>
    protected virtual void CaptureTypeState(IDictionary<string, string?> state)
    {
    }

    /// <summary>
    /// Reads this concrete type's own fields back out of a <b>committed</b>
    /// <paramref name="state"/> — the writer that pairs with
    /// <see cref="CaptureTypeState"/> for an object that is already live
    /// (`ADR-0145`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Kind whose own mutators go through
    /// <see cref="MutateTypeStateAndPersistAsync"/> overrides this so the
    /// committed value reaches its fields; a Kind with no mutators of its
    /// own does not need to, because its type state never changes after
    /// construction. <see cref="IRehydratable{TSelf}.Rehydrate"/> remains
    /// the reader for a <em>new</em> instance; this is the reader for an
    /// existing one, and the two must agree.
    /// </para>
    /// <para>
    /// Called only from <see cref="ApplyCommittedState"/>, i.e. only after
    /// the state it is given is durable.
    /// </para>
    /// </remarks>
    protected virtual void ApplyTypeState(EngineeringObjectState state)
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

    // ================================================================
    // The one write path (`ADR-0145`)
    // ================================================================

    /// <summary>
    /// Refuses a durable write through an instance <see cref="ReviseAsync"/>
    /// has already retired (`WP 16.4B-R4`).
    /// </summary>
    /// <remarks>
    /// <b>Called only inside the domain write lock</b>, where the answer
    /// cannot change between the check and the commit: whichever of {this
    /// write, the revision} takes the lock first wins, and if the revision
    /// won, this write never happens.
    /// </remarks>
    private void ThrowIfSuperseded()
    {
        if (_supersededBy is { } successor)
            throw new SupersededEngineeringObjectException(Id, successor.CurrentRevisionNumber);
    }

    /// <summary>
    /// Project, commit, apply — the shape of every mutator declared on
    /// this type (`ADR-0145`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="project"/> runs <b>inside</b> the transaction,
    /// against the state as it stands, and is free to throw to refuse the
    /// operation: an impermissible lifecycle transition, a cycle, a delete
    /// with live children. Nothing has been touched at that point, in
    /// memory or on disk, so a refusal simply is not an operation.
    /// </para>
    /// <para>
    /// The state it returns and the audit row are written through the same
    /// transaction, along with anything <paramref name="alsoWrite"/> adds.
    /// The instance is updated only after that transaction commits.
    /// </para>
    /// </remarks>
    private async Task<EngineeringObjectState> MutateAndPersistAsync(
        Func<EngineeringObjectState, EngineeringObjectState> project,
        string auditAction,
        string? auditDetail,
        CancellationToken cancellationToken,
        Func<IPersistenceTransaction, EngineeringObjectState, CancellationToken, Task>? alsoWrite = null)
    {
        EngineeringObjectState? committed = null;

        await _context.ExecuteWriteAsync(
            async (transaction, token) =>
            {
                ThrowIfSuperseded();

                var next = project(CaptureState());

                await _context.StateWriter.SaveAsync(transaction, next, token).ConfigureAwait(false);

                if (alsoWrite is not null)
                    await alsoWrite(transaction, next, token).ConfigureAwait(false);

                await WriteAuditAsync(transaction, auditAction, auditDetail, token).ConfigureAwait(false);

                committed = next;
            },
            cancellationToken).ConfigureAwait(false);

        return committed!;
    }

    /// <summary>
    /// The Kind-specific counterpart of
    /// <see cref="MutateAndPersistAsync"/>: a concrete Kind's own mutator
    /// declares what it is about to change as type state, commits it, and
    /// applies it to its own fields afterwards (`ADR-0145`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="projectTypeState"/> runs <b>inside</b> the
    /// transaction. It may throw to refuse — an impermissible issue-status
    /// or work-state move — and otherwise returns the next value of every
    /// type-state key this mutation changes, merged over the record's
    /// current type state. <paramref name="apply"/> writes the same change
    /// into the Kind's own fields and runs only after the commit.
    /// </para>
    /// <para>
    /// Projection and refusal are one delegate rather than two parameters
    /// deliberately. A mutator whose next state depends on its current
    /// state — <c>DecideAsync</c> records who decided only on the move out
    /// of <c>Proposed</c> — must read that current state under the same
    /// lock and in the same transaction that commits the result. Splitting
    /// "check" from "what to write" would put the read on the wrong side
    /// of the boundary and reintroduce, in miniature, exactly the
    /// check-then-write window `ADR-0145` exists to close.
    /// </para>
    /// <para>
    /// The Kind must also override <see cref="ApplyTypeState"/>, so an
    /// instance updated by <em>another</em> committed path — a revision's
    /// successor, a rehydration — reads the same values back.
    /// </para>
    /// </remarks>
    /// <param name="projectTypeState">
    /// Refuses the operation, or returns the next value of each type-state
    /// key this mutation changes. Runs inside the transaction.
    /// </param>
    /// <param name="apply">Writes the change into the Kind's own fields. Runs after the commit.</param>
    /// <param name="auditDetail">A short description for the audit row.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    protected async Task MutateTypeStateAndPersistAsync(
        Func<IReadOnlyDictionary<string, string?>> projectTypeState,
        Action apply,
        string? auditDetail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectTypeState);
        ArgumentNullException.ThrowIfNull(apply);

        await MutateAndPersistAsync(
            current =>
            {
                var typeState = projectTypeState()
                    ?? throw new InvalidOperationException(
                        "A type-state projection returned null. Return an empty dictionary to change nothing.");

                var merged = new Dictionary<string, string?>(current.TypeState, StringComparer.Ordinal);
                foreach (var (key, value) in typeState)
                    merged[key] = value;

                return current with { TypeState = merged };
            },
            EngineeringAuditActions.StateChanged,
            auditDetail,
            cancellationToken).ConfigureAwait(false);

        apply();
    }

    /// <summary>
    /// Writes this object's creation — its state record and its audit row
    /// — inside the transaction the factory opened (`ADR-0145`, `TD-147`).
    /// </summary>
    internal Task WriteCreationAsync(IPersistenceTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return WriteCreationCoreAsync(transaction, cancellationToken);
    }

    private async Task WriteCreationCoreAsync(IPersistenceTransaction transaction, CancellationToken cancellationToken)
    {
        await _context.StateWriter.SaveAsync(transaction, CaptureState(), cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(transaction, EngineeringAuditActions.Created, $"Kind '{Kind}', revision 1.", cancellationToken).ConfigureAwait(false);
    }

    private Task WriteAuditAsync(IPersistenceTransaction transaction, string action, string? detail, CancellationToken cancellationToken) =>
        AuditTransactionWriter.WriteAsync(
            transaction, Id, Kind, action, _context.ResolveCurrentPrincipalId(), detail, DateTimeOffset.UtcNow, cancellationToken);

    // ================================================================
    // IEngineeringObject
    // ================================================================

    /// <inheritdoc />
    public Guid Id => Document.Id;

    /// <inheritdoc />
    public string Kind => Document.Kind;

    /// <inheritdoc />
    public int CurrentRevisionNumber => CurrentRevision.RevisionNumber;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt => Document.CreatedAt;

    /// <inheritdoc />
    public string? Identifier { get; }

    /// <inheritdoc />
    public string DisplayName
    {
        get { lock (_structuralLock) { return _displayName; } }
    }

    /// <inheritdoc />
    public string? Category => Metadata.Category;

    /// <inheritdoc />
    public string? Discipline => Metadata.Discipline;

    /// <inheritdoc />
    public string? Owner => Metadata.Owner;

    /// <inheritdoc />
    public IReadOnlyList<string> Tags => Metadata.TagsOrEmpty;

    /// <inheritdoc />
    public string? Classification => Metadata.Classification;

    /// <inheritdoc />
    public string? Notes => Metadata.Notes;

    /// <inheritdoc />
    public LifecycleState Status
    {
        get { lock (_lifecycleLock) { return _status; } }
    }

    /// <inheritdoc />
    public IReadOnlyList<ILifecycleTransitionRecord> History
    {
        get { lock (_lifecycleLock) { return _history.ToList(); } }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>A refused or failed transition writes no history entry.</b> The
    /// permitted-transition check and the entry are both inside the
    /// projection, which runs inside the transaction and mutates nothing;
    /// the entry reaches this object's append-only history only once the
    /// record that carries it is durable. There is no removal path for a
    /// history entry, by design, and none is needed: an entry that was
    /// never committed was never created.
    /// </remarks>
    public async Task TransitionAsync(LifecycleState target, CancellationToken cancellationToken = default)
    {
        var actor = _context.ResolveCurrentPrincipalId();
        var occurredAt = DateTimeOffset.UtcNow;
        LifecycleState from = default;

        var committed = await MutateAndPersistAsync(
            current =>
            {
                if (!_context.LifecycleTable.IsPermitted(current.Status, target))
                    throw new InvalidLifecycleTransitionException(current.Status, target);

                from = current.Status;

                return current with
                {
                    Status = target,
                    History = [.. current.History, new EngineeringObjectTransitionState(current.Status, target, actor, occurredAt, ApprovalId: null)],
                };
            },
            EngineeringAuditActions.Transitioned,
            $"{from} to {target}.",
            cancellationToken).ConfigureAwait(false);

        ApplyCommittedState(committed);
    }

    /// <inheritdoc />
    public string Content => CurrentRevision.Content;

    /// <inheritdoc />
    public string AuthorPrincipalId => CurrentRevision.AuthorPrincipalId;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// A revision is a new <em>instance</em> of the same object, carrying
    /// the same object's whole state. The new revision record, the
    /// document record that names it, this object's state and the audit
    /// row are one transaction; the successor is built and registered only
    /// after it commits, and the predecessor is retired in the same lock
    /// hold, so a later write through the predecessor is refused with
    /// <see cref="SupersededEngineeringObjectException"/> rather than
    /// overwriting the successor's record from a stale view (`WP 16.4B-R4`).
    /// </para>
    /// <para>
    /// The successor is built by the Kind's own state <em>reader</em>
    /// (<see cref="IRehydratable{TSelf}.Rehydrate"/>), so "revise" and
    /// "restart" reconstruct an object the same way and a type-specific
    /// field cannot survive one and be dropped by the other
    /// (`WP 16.4B-R6`).
    /// </para>
    /// </remarks>
    public async Task<IHasRevisions> ReviseAsync(string newContent, string? changeSummary, CancellationToken cancellationToken = default)
    {
        if (_selfFactory is not { } selfFactory)
            throw new InvalidOperationException($"'{GetType().Name}' was constructed without a self-factory attached — it cannot revise itself.");

        EngineeringObjectBase? revised = null;

        await _context.ExecuteWriteAsync(
            async (transaction, token) =>
            {
                ThrowIfSuperseded();

                var newRevision = await _context.DocumentWriter
                    .ReviseAsync(transaction, Id, newContent, changeSummary, token).ConfigureAwait(false);

                var state = CaptureState();

                await _context.StateWriter.SaveAsync(transaction, state, token).ConfigureAwait(false);
                await WriteAuditAsync(transaction, EngineeringAuditActions.Revised, $"Revision {newRevision.RevisionNumber}.", token)
                    .ConfigureAwait(false);

                var refreshedDocument = new EngineeringDocument(Document.Id, Document.Kind, newRevision.RevisionNumber, Document.CreatedAt);

                var successor = selfFactory(refreshedDocument, newRevision, state);
                successor.AttachSelfFactory(selfFactory);
                successor.RestoreState(state);

                revised = successor;
            },
            cancellationToken).ConfigureAwait(false);

        // Committed. Only now does anything outside the transaction learn
        // that this object has a successor.
        _supersededBy = revised;
        _context.Repository.Register(revised!);

        return revised!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IRevisionRecord>> GetRevisionHistoryAsync(CancellationToken cancellationToken = default)
    {
        var revisions = await _context.Store.GetRevisionHistoryAsync(Id, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<IRevisionRecord> records = revisions
            .Select(r => (IRevisionRecord)new RevisionRecord(r.RevisionNumber, r.Content, r.ChangeSummary, r.AuthorPrincipalId, r.CreatedAt))
            .ToList();

        return records;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The reference record and the audit row are one transaction; the
    /// in-memory relationship cache learns of the link only after it
    /// commits, so the cache can no longer hold a relationship the store
    /// does not (`TD-140`).
    /// </remarks>
    public async Task LinkAsync(Guid targetId, string relationshipKind, CancellationToken cancellationToken = default)
    {
        if (targetId == Id)
            throw new SelfReferentialRelationshipException(Id);

        var createdAt = DateTimeOffset.UtcNow;
        var principalId = _context.ResolveCurrentPrincipalId();

        await _context.ExecuteWriteAsync(
            async (transaction, token) =>
            {
                ThrowIfSuperseded();

                await _context.DocumentWriter.LinkAsync(transaction, Id, targetId, relationshipKind, token).ConfigureAwait(false);
                await WriteAuditAsync(transaction, EngineeringAuditActions.Linked, $"{relationshipKind} to '{targetId:N}'.", token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        RecordRelationship(targetId, relationshipKind, principalId, createdAt);
    }

    private void RecordRelationship(Guid targetId, string relationshipKind, string principalId, DateTimeOffset createdAt) =>
        _context.RelationshipRepository.Record(new EngineeringRelationship(
            Id, targetId, relationshipKind, RelationshipKindCategoryMap.InferCategory(relationshipKind), principalId, createdAt));

    /// <inheritdoc />
    public Task<IReadOnlyList<IEngineeringRelationship>> GetRelationshipsAsync(CancellationToken cancellationToken = default) =>
        _context.RelationshipRepository.GetOutgoingAsync(Id, cancellationToken);

    /// <inheritdoc />
    public Task<IEvidence> GetEvidenceAsync(CancellationToken cancellationToken = default) =>
        _context.EvidenceComposer.ComposeAsync(Id, cancellationToken);

    /// <inheritdoc />
    public Task<IValidationResult> ValidateAsync(CancellationToken cancellationToken = default) =>
        _context.ValidationRuleSet.ValidateAsync(this, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// A refused or failed attach leaves no phantom on the instance: the
    /// attachment reaches <c>_attachments</c> only after the record naming
    /// it is durable.
    /// </remarks>
    public async Task AttachAsync(IAttachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        List<IAttachment> nextAttachments;
        lock (_attachments) { nextAttachments = [.. _attachments, attachment]; }

        var committed = await MutateAndPersistAsync(
            current => current with
            {
                Attachments = [.. current.Attachments, new EngineeringObjectAttachmentState(
                    attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeInBytes, attachment.ContentHash)],
            },
            EngineeringAuditActions.Attached,
            $"'{attachment.FileName}' ({attachment.SizeInBytes:N0} bytes).",
            cancellationToken).ConfigureAwait(false);

        ApplyCommittedState(committed, nextAttachments);
    }

    /// <inheritdoc />
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
    /// <b>The bytes and the record that names them are one write
    /// (`WP 17.1B`).</b> The payload is a BLOB in the same database,
    /// written through the same transaction as the object-state record
    /// that references it, so a committed attachment can never name bytes
    /// that are not there and a transaction that does not commit leaves no
    /// orphaned bytes. `ADR-0114`'s content-before-state <em>ordering</em>
    /// is retired here rather than followed: there is no ordering between
    /// two writes that are one write.
    /// </para>
    /// <para>
    /// That is why <c>AttachmentWriteIntentStore</c>,
    /// <c>IAttachmentWriteIntentStore</c> and
    /// <c>AttachmentContentReconciliationService</c> were deleted rather
    /// than kept: they compensated for a failure mode — content on disk
    /// that no state record names — that this method can no longer
    /// produce.
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

        if (content.Length > AttachmentContentLimits.MaximumSizeInBytes)
            throw new AttachmentContentTooLargeException(fileName, content.Length);

        var attachmentId = Guid.NewGuid();
        var contentHash = AttachmentContentStore.ComputeHash(content.Span);
        var attachment = new Attachment(attachmentId, fileName, contentType, content.Length, contentHash);

        List<IAttachment> nextAttachments;
        lock (_attachments) { nextAttachments = [.. _attachments, attachment]; }

        var committed = await MutateAndPersistAsync(
            current => current with
            {
                Attachments = [.. current.Attachments, new EngineeringObjectAttachmentState(
                    attachmentId, fileName, contentType, content.Length, contentHash)],
            },
            EngineeringAuditActions.ContentAttached,
            $"'{fileName}' ({content.Length:N0} bytes).",
            cancellationToken,
            alsoWrite: async (transaction, _, token) =>
                await _context.AttachmentWriter.SaveAsync(transaction, attachmentId, content, token).ConfigureAwait(false))
            .ConfigureAwait(false);

        ApplyCommittedState(committed, nextAttachments);

        return attachment;
    }

    /// <inheritdoc />
    public async Task<AttachmentContentResult> ReadAttachmentContentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        IAttachment? attachment;
        lock (_attachments) { attachment = _attachments.FirstOrDefault(a => a.Id == attachmentId); }

        if (attachment is null)
            return AttachmentContentResult.Missing();

        return await _context.AttachmentContentStore
            .ReadAsync(attachment.Id, attachment.ContentHash, attachment.SizeInBytes, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual string SearchableText =>
        string.Join(' ', new[] { DisplayName, Identifier, Category, Content }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <inheritdoc />
    public async Task RenameAsync(string newDisplayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newDisplayName);

        var committed = await MutateAndPersistAsync(
            current => current with { DisplayName = newDisplayName },
            EngineeringAuditActions.Renamed,
            $"Renamed to '{newDisplayName}'.",
            cancellationToken).ConfigureAwait(false);

        ApplyCommittedState(committed);
    }

    /// <inheritdoc />
    public Guid? ParentId
    {
        get { lock (_structuralLock) { return _parentId; } }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>The cycle check is inside the transaction (`TD-145`).</b> It used
    /// to run before the write lock was taken, so two moves that each
    /// passed the check independently could commit and leave a cycle
    /// neither of them could see. The domain write lock now spans the
    /// check and the write, so the walk reads a graph that cannot change
    /// under it and the second move is refused.
    /// </remarks>
    public async Task MoveAsync(Guid? newParentId, CancellationToken cancellationToken = default)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var principalId = _context.ResolveCurrentPrincipalId();

        var committed = await MutateAndPersistAsync(
            current =>
            {
                if (newParentId is { } candidateParentId)
                    GuardAgainstCircularParent(candidateParentId);

                return current with { ParentId = newParentId };
            },
            EngineeringAuditActions.Moved,
            newParentId is { } parent ? $"Parent set to '{parent:N}'." : "Parent cleared.",
            cancellationToken,
            alsoWrite: async (transaction, _, token) =>
            {
                if (newParentId is { } parentId)
                {
                    await _context.DocumentWriter
                        .LinkAsync(transaction, Id, parentId, GroupedUnderRelationshipKind, token)
                        .ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

        ApplyCommittedState(committed);

        if (newParentId is { } committedParentId)
            RecordRelationship(committedParentId, GroupedUnderRelationshipKind, principalId, createdAt);
    }

    /// <summary>The relationship kind a structural move records.</summary>
    private const string GroupedUnderRelationshipKind = "groupedUnder";

    /// <summary>
    /// Walks the parent chain from <paramref name="candidateParentId"/>
    /// upward and refuses a move that would close a cycle.
    /// </summary>
    /// <remarks>
    /// Synchronous, and reads the in-memory object cache directly rather
    /// than awaiting <see cref="IEngineeringObjectRepository.FindAsync"/>:
    /// it runs inside the transaction body, where the cache is by
    /// construction a projection of committed state and cannot be written
    /// by anyone else, and a synchronous walk keeps the transaction from
    /// awaiting anything but its own store.
    /// </remarks>
    private void GuardAgainstCircularParent(Guid candidateParentId)
    {
        if (candidateParentId == Id)
            throw new CircularParentAssignmentException(Id, candidateParentId);

        var current = candidateParentId;
        var visited = new HashSet<Guid> { Id };

        while (visited.Add(current))
        {
            var candidate = _context.Repository.FindAsync(current, CancellationToken.None).GetAwaiter().GetResult();

            if (candidate is not IHasParent { ParentId: { } nextParentId })
                return;

            if (nextParentId == Id)
                throw new CircularParentAssignmentException(Id, candidateParentId);

            current = nextParentId;
        }
    }

    /// <inheritdoc />
    public bool IsDeleted
    {
        get { lock (_structuralLock) { return _isDeleted; } }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>The live-children check is inside the transaction (`TD-146`).</b>
    /// It used to run before the write, so a concurrent
    /// <see cref="MoveAsync"/> could give this object a child between the
    /// count and the commit and leave an orphan. The domain write lock now
    /// spans both, so a delete and a move that would race are ordered and
    /// the loser is refused.
    /// <para>
    /// The attachment bytes are removed in the same transaction as the
    /// state record that stops referencing them, so a delete cannot leave
    /// bytes behind and cannot remove bytes for a delete that did not
    /// commit.
    /// </para>
    /// </remarks>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> attachmentIds;
        lock (_attachments) { attachmentIds = _attachments.Select(a => a.Id).ToList(); }

        var committed = await MutateAndPersistAsync(
            current =>
            {
                var all = _context.Repository.ListAllAsync(CancellationToken.None).GetAwaiter().GetResult();

                var liveChildren = all.Count(o =>
                    o is IHasParent { ParentId: { } parentId } && parentId == Id &&
                    o is not IDeletable { IsDeleted: true });

                if (liveChildren > 0)
                    throw new EngineeringObjectHasChildrenException(Id, liveChildren);

                return current with { IsDeleted = true };
            },
            EngineeringAuditActions.Deleted,
            attachmentIds.Count == 0 ? "Deleted." : $"Deleted, with {attachmentIds.Count} attachment payload(s).",
            cancellationToken,
            alsoWrite: async (transaction, _, token) =>
            {
                foreach (var attachmentId in attachmentIds)
                    await _context.AttachmentWriter.DeleteAsync(transaction, attachmentId, token).ConfigureAwait(false);
            }).ConfigureAwait(false);

        ApplyCommittedState(committed);
    }

    /// <inheritdoc />
    public decimal Quantity
    {
        get { lock (_structuralLock) { return _quantity; } }
    }

    /// <inheritdoc />
    public string? UnitOfMeasure
    {
        get { lock (_structuralLock) { return _unitOfMeasure; } }
    }

    /// <inheritdoc />
    public string? FindNumber
    {
        get { lock (_structuralLock) { return _findNumber; } }
    }

    /// <inheritdoc />
    public string? ItemNumber
    {
        get { lock (_structuralLock) { return _itemNumber; } }
    }

    /// <inheritdoc />
    public string? ReferenceDesignator
    {
        get { lock (_structuralLock) { return _referenceDesignator; } }
    }

    /// <inheritdoc />
    public async Task SetBomLineAsync(
        decimal quantity, string? unitOfMeasure = null, string? findNumber = null,
        string? itemNumber = null, string? referenceDesignator = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, $"Quantity must be positive ({StructuralValidationRules.QuantityMustBePositive}).");

        var committed = await MutateAndPersistAsync(
            current => current with
            {
                BomLine = new EngineeringObjectBomLineState(quantity, unitOfMeasure, findNumber, itemNumber, referenceDesignator),
            },
            EngineeringAuditActions.BomLineSet,
            $"Quantity {quantity}{(unitOfMeasure is null ? string.Empty : " " + unitOfMeasure)}.",
            cancellationToken).ConfigureAwait(false);

        ApplyCommittedState(committed);
    }
}
