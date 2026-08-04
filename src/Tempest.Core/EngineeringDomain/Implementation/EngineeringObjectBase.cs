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
    IHasRelationships, ITraceable, IValidatable, IHasAttachments, ISearchable
{
    private readonly EngineeringDomainContext _context;
    private readonly List<ILifecycleTransitionRecord> _history = new();
    private readonly List<IAttachment> _attachments = new();
    private readonly object _lifecycleLock = new();
    private Func<IEngineeringDocument, IDocumentRevision, EngineeringObjectBase>? _selfFactory;
    private LifecycleState _status = LifecycleState.Draft;

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
        DisplayName = displayName;
        Metadata = metadata;
    }

    protected IEngineeringDocument Document { get; }
    protected IDocumentRevision CurrentRevision { get; }
    protected EngineeringObjectMetadata Metadata { get; }
    protected EngineeringDomainContext Context => _context;

    /// <summary>Called once by the factory that constructed this instance, so <see cref="ReviseAsync"/> can produce a correctly-typed successor.</summary>
    internal void AttachSelfFactory(Func<IEngineeringDocument, IDocumentRevision, EngineeringObjectBase> selfFactory) =>
        _selfFactory = selfFactory;

    // IEngineeringObject
    public Guid Id => Document.Id;
    public string Kind => Document.Kind;
    public int CurrentRevisionNumber => CurrentRevision.RevisionNumber;
    public DateTimeOffset CreatedAt => Document.CreatedAt;

    // IHasBusinessIdentifier
    public string? Identifier { get; }
    public string DisplayName { get; }

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

        return Task.CompletedTask;
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
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IAttachment>> GetAttachmentsAsync(CancellationToken cancellationToken = default)
    {
        lock (_attachments)
        {
            IReadOnlyList<IAttachment> snapshot = _attachments.ToList();
            return Task.FromResult(snapshot);
        }
    }

    // ISearchable
    public virtual string SearchableText =>
        string.Join(' ', new[] { DisplayName, Identifier, Category, Content }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
