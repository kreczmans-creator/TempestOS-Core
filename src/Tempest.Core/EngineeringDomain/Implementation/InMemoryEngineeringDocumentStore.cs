using System.Collections.Concurrent;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// A fully in-memory <see cref="IEngineeringDocumentStore"/> — same contract, same behaviour as the
/// production, <see cref="Persistence.IPersistenceStore"/>-backed <see cref="EngineeringDocumentStore"/>,
/// but backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/> instead. Not a competing storage
/// hierarchy (ADR-0072 forbids that) — a second implementation of the same, already-shipped interface,
/// used where this Work Package's own "no persistence, no database, no file storage" constraint applies
/// (tests, the reference sample module). A consumer running through the real <see cref="Runtime.TempestHost"/>
/// continues to receive the shared, persistence-backed <see cref="EngineeringDocumentStore"/> — this type is
/// never registered there.
/// </summary>
public sealed class InMemoryEngineeringDocumentStore : IEngineeringDocumentStore
{
    public const string UnknownAuthorPrincipalId = "unknown";

    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly ConcurrentDictionary<Guid, DocumentState> _documents = new();

    public InMemoryEngineeringDocumentStore(ICurrentPrincipalAccessor currentPrincipalAccessor)
    {
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        _currentPrincipalAccessor = currentPrincipalAccessor;
    }

    public Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(initialContent);

        var documentId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var authorPrincipalId = ResolveAuthorPrincipalId();

        var state = new DocumentState(kind, createdAt);
        state.Revisions.Add(new DocumentRevision(documentId, 1, initialContent, changeSummary: null, authorPrincipalId, createdAt));
        _documents[documentId] = state;

        return Task.FromResult<IEngineeringDocument>(new EngineeringDocument(documentId, kind, currentRevisionNumber: 1, createdAt));
    }

    public Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var state))
            return Task.FromResult<IEngineeringDocument?>(null);

        lock (state)
        {
            return Task.FromResult<IEngineeringDocument?>(
                new EngineeringDocument(documentId, state.Kind, state.Revisions.Count, state.CreatedAt));
        }
    }

    public Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newContent);

        if (!_documents.TryGetValue(documentId, out var state))
            throw new EngineeringDocumentNotFoundException(documentId);

        var authorPrincipalId = ResolveAuthorPrincipalId();

        lock (state)
        {
            var revisionNumber = state.Revisions.Count + 1;
            var revision = new DocumentRevision(documentId, revisionNumber, newContent, changeSummary, authorPrincipalId, DateTimeOffset.UtcNow);
            state.Revisions.Add(revision);
            return Task.FromResult<IDocumentRevision>(revision);
        }
    }

    public Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var state))
            throw new EngineeringDocumentNotFoundException(documentId);

        lock (state)
        {
            IReadOnlyList<IDocumentRevision> revisions = state.Revisions.ToList();
            return Task.FromResult(revisions);
        }
    }

    public Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipKind);

        if (!_documents.TryGetValue(sourceDocumentId, out var sourceState))
            throw new EngineeringDocumentNotFoundException(sourceDocumentId);

        if (!_documents.ContainsKey(targetDocumentId))
            throw new EngineeringDocumentNotFoundException(targetDocumentId);

        lock (sourceState)
        {
            sourceState.References.Add(new DocumentReference(sourceDocumentId, targetDocumentId, relationshipKind, ResolveAuthorPrincipalId(), DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var state))
            return Task.FromResult<IReadOnlyList<DocumentReference>>(Array.Empty<DocumentReference>());

        lock (state)
        {
            IReadOnlyList<DocumentReference> references = state.References.ToList();
            return Task.FromResult(references);
        }
    }

    private string ResolveAuthorPrincipalId() =>
        _currentPrincipalAccessor.Current?.Identity.Id ?? UnknownAuthorPrincipalId;

    private sealed class DocumentState
    {
        public string Kind { get; }
        public DateTimeOffset CreatedAt { get; }
        public List<IDocumentRevision> Revisions { get; } = new();
        public List<DocumentReference> References { get; } = new();

        public DocumentState(string kind, DateTimeOffset createdAt)
        {
            Kind = kind;
            CreatedAt = createdAt;
        }
    }
}
