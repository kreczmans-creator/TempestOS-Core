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
                    _attachments.Select(a => new EngineeringObjectAttachmentState(a.Id, a.FileName, a.ContentType, a.SizeInBytes, a.ContentHash)).ToList(),
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
    protected Task PersistStateAsync(CancellationToken cancellationToken = default) =>
        _context.ObjectStateStore is { } store
            ? store.SaveAsync(CaptureState(), cancellationToken)
            : Task.CompletedTask;

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
        revised.RestoreState(CaptureState());
        _context.Repository.Register(revised);

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

        var attachmentId = Guid.NewGuid();

        // Content first: a crash between the two writes leaves unreferenced
        // bytes, not an attachment promising content nobody stored.
        var contentHash = await contentStore.SaveAsync(attachmentId, content, cancellationToken).ConfigureAwait(false);

        var attachment = new Attachment(attachmentId, fileName, contentType, content.Length, contentHash);

        lock (_attachments) { _attachments.Add(attachment); }
        await PersistStateAsync(cancellationToken).ConfigureAwait(false);

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
