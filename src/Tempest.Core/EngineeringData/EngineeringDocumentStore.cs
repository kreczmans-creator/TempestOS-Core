using System.Text.Json;
using Tempest.Core.Concurrency;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringData;

/// <summary>
/// The concrete <see cref="IEngineeringDocumentStore"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Storage substrate (`ADR-0053`):</b> built directly on
/// <see cref="IPersistenceStore"/>, serializing document, revision, and
/// reference structure into that store's own key/value shape — no new
/// storage abstraction was introduced. Three <see cref="IPersistenceStore"/>
/// collections are used, each owned exclusively by this store, mirroring
/// <see cref="Settings.SettingsProvider.SettingsCollectionName"/>'s and
/// <see cref="Audit.AuditRecorder.AuditCollectionName"/>'s own
/// collection-ownership convention:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="DocumentsCollectionName"/> — one entry per document, keyed by the document's own Id, holding <see cref="EngineeringDocumentDto"/> (kind, created-at, current revision number).</description></item>
/// <item><description><see cref="RevisionsCollectionName"/> — one entry per revision, keyed by <c>"{documentId:N}_{revisionNumber:D10}"</c>, holding <see cref="DocumentRevisionDto"/>. Revision numbers are sequential and known from the document's own <see cref="IEngineeringDocument.CurrentRevisionNumber"/>, so <see cref="GetRevisionHistoryAsync"/> reads exactly the keys it needs directly — it never enumerates the whole collection, unlike <see cref="Audit.AuditQuery"/>'s own disclosed linear-scan limitation (`TD-12`).</description></item>
/// <item><description>A reference collection per source document (<see cref="GetReferencesCollectionName"/>), holding <see cref="DocumentReferenceDto"/> keyed by a random Id — this avoids a whole-collection scan for <see cref="GetReferencesAsync"/> too, since each document's own outgoing references live in their own, separately-enumerable collection.</description></item>
/// </list>
/// <para>
/// <b>Revision-number atomicity:</b> a per-document <see cref="AsyncKeyedLock"/>
/// serialises the read-current-then-write-next sequence in
/// <see cref="ReviseAsync"/>, mirroring
/// <see cref="Settings.SettingsProvider"/>'s own per-key locking
/// rationale — two concurrent <see cref="ReviseAsync"/> calls against the
/// same document can never produce two revisions claiming the same
/// <see cref="IDocumentRevision.RevisionNumber"/>.
/// </para>
/// <para>
/// <b>Author attribution</b> mirrors <see cref="Audit.AuditRecorder"/>'s
/// own pattern exactly: resolved from <see cref="ICurrentPrincipalAccessor"/>,
/// falling back to <see cref="UnknownAuthorPrincipalId"/> rather than
/// failing the write when no principal is currently established.
/// </para>
/// </remarks>
public sealed class EngineeringDocumentStore : IEngineeringDocumentStore
{
    /// <summary>
    /// The <see cref="IPersistenceStore"/> collection every document's
    /// own identity record is stored under.
    /// </summary>
    public const string DocumentsCollectionName = "EngineeringData.Documents";

    /// <summary>
    /// The <see cref="IPersistenceStore"/> collection every revision is
    /// stored under.
    /// </summary>
    public const string RevisionsCollectionName = "EngineeringData.Revisions";

    /// <summary>
    /// The <see cref="IDocumentRevision.AuthorPrincipalId"/> recorded when
    /// no principal is currently established.
    /// </summary>
    public const string UnknownAuthorPrincipalId = "unknown";

    private readonly IPersistenceStore _persistenceStore;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly ILogger? _logger;
    private readonly AsyncKeyedLock _documentLock = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="EngineeringDocumentStore"/> class.
    /// </summary>
    /// <param name="persistenceStore">The store this instance persists through.</param>
    /// <param name="currentPrincipalAccessor">The service this instance resolves the acting principal from.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="persistenceStore"/> or <paramref name="currentPrincipalAccessor"/> is <see langword="null"/>.
    /// </exception>
    public EngineeringDocumentStore(IPersistenceStore persistenceStore, ICurrentPrincipalAccessor currentPrincipalAccessor, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        _persistenceStore = persistenceStore;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _logger = logger;
    }

    /// <summary>
    /// The <see cref="IPersistenceStore"/> collection <paramref name="sourceDocumentId"/>'s
    /// own outgoing references are stored under.
    /// </summary>
    public static string GetReferencesCollectionName(Guid sourceDocumentId) =>
        $"EngineeringData.References.{sourceDocumentId:N}";

    /// <inheritdoc />
    public async Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(initialContent);

        var documentId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var authorPrincipalId = ResolveAuthorPrincipalId();

        await WriteDocumentAsync(documentId, new EngineeringDocumentDto(kind, createdAt, CurrentRevisionNumber: 1), cancellationToken)
            .ConfigureAwait(false);
        await WriteRevisionAsync(
            documentId,
            revisionNumber: 1,
            new DocumentRevisionDto(initialContent, ChangeSummary: null, authorPrincipalId, createdAt),
            cancellationToken)
            .ConfigureAwait(false);

        _logger?.Information($"Engineering document created: '{documentId}' (kind '{kind}').");

        return new EngineeringDocument(documentId, kind, currentRevisionNumber: 1, createdAt);
    }

    /// <inheritdoc />
    public async Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var dto = await ReadDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);

        return dto is null ? null : new EngineeringDocument(documentId, dto.Kind, dto.CurrentRevisionNumber, dto.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newContent);

        var authorPrincipalId = ResolveAuthorPrincipalId();

        using (await _documentLock.AcquireAsync(documentId.ToString("N"), cancellationToken).ConfigureAwait(false))
        {
            var dto = await ReadDocumentAsync(documentId, cancellationToken).ConfigureAwait(false)
                ?? throw new EngineeringDocumentNotFoundException(documentId);

            var newRevisionNumber = dto.CurrentRevisionNumber + 1;
            var revisedAt = DateTimeOffset.UtcNow;

            await WriteRevisionAsync(
                documentId,
                newRevisionNumber,
                new DocumentRevisionDto(newContent, changeSummary, authorPrincipalId, revisedAt),
                cancellationToken)
                .ConfigureAwait(false);
            await WriteDocumentAsync(documentId, dto with { CurrentRevisionNumber = newRevisionNumber }, cancellationToken)
                .ConfigureAwait(false);

            _logger?.Information($"Engineering document revised: '{documentId}' (revision {newRevisionNumber}).");

            return new DocumentRevision(documentId, newRevisionNumber, newContent, changeSummary, authorPrincipalId, revisedAt);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var dto = await ReadDocumentAsync(documentId, cancellationToken).ConfigureAwait(false)
            ?? throw new EngineeringDocumentNotFoundException(documentId);

        var revisions = new List<IDocumentRevision>(dto.CurrentRevisionNumber);

        for (var revisionNumber = 1; revisionNumber <= dto.CurrentRevisionNumber; revisionNumber++)
        {
            var revisionDto = await ReadRevisionAsync(documentId, revisionNumber, cancellationToken).ConfigureAwait(false)
                ?? throw new EngineeringDataException(
                    $"Engineering document '{documentId}' is missing its own revision {revisionNumber} — the store is internally inconsistent.");

            revisions.Add(new DocumentRevision(
                documentId, revisionNumber, revisionDto.Content, revisionDto.ChangeSummary, revisionDto.AuthorPrincipalId, revisionDto.CreatedAt));
        }

        return revisions;
    }

    /// <inheritdoc />
    public async Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipKind);

        if (await ReadDocumentAsync(sourceDocumentId, cancellationToken).ConfigureAwait(false) is null)
            throw new EngineeringDocumentNotFoundException(sourceDocumentId);

        if (await ReadDocumentAsync(targetDocumentId, cancellationToken).ConfigureAwait(false) is null)
            throw new EngineeringDocumentNotFoundException(targetDocumentId);

        var dto = new DocumentReferenceDto(targetDocumentId, relationshipKind, ResolveAuthorPrincipalId(), DateTimeOffset.UtcNow);
        var key = Guid.NewGuid().ToString("N");

        await _persistenceStore.WriteAsync(
            GetReferencesCollectionName(sourceDocumentId), key, JsonSerializer.Serialize(dto), cancellationToken)
            .ConfigureAwait(false);

        _logger?.Information(
            $"Engineering document link recorded: '{sourceDocumentId}' --[{relationshipKind}]--> '{targetDocumentId}'.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var collection = GetReferencesCollectionName(documentId);
        var keys = await _persistenceStore.ListKeysAsync(collection, cancellationToken).ConfigureAwait(false);

        var references = new List<DocumentReference>(keys.Count);

        foreach (var key in keys)
        {
            var json = await _persistenceStore.ReadAsync(collection, key, cancellationToken).ConfigureAwait(false);

            // A benign race with... nothing, in practice - references are
            // never deleted by this store - but ReadAsync's own contract
            // permits null for "no longer present," mirroring
            // AuditQuery's own identical defensive skip.
            if (json is null)
                continue;

            DocumentReferenceDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<DocumentReferenceDto>(json)
                    ?? throw new EngineeringDataException($"Reference '{key}' for document '{documentId}' could not be deserialised.");
            }
            catch (JsonException ex)
            {
                // Malformed stored content surfaces as this store's own
                // controlled exception type, never a raw JsonException
                // from a passive read (`TD-60`).
                throw new EngineeringDataException($"Reference '{key}' for document '{documentId}' could not be deserialised.", ex);
            }

            references.Add(new DocumentReference(documentId, dto.TargetDocumentId, dto.RelationshipKind, dto.CreatedByPrincipalId, dto.CreatedAt));
        }

        return references;
    }

    private string ResolveAuthorPrincipalId() =>
        _currentPrincipalAccessor.Current?.Identity.Id ?? UnknownAuthorPrincipalId;

    private async Task<EngineeringDocumentDto?> ReadDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var json = await _persistenceStore.ReadAsync(DocumentsCollectionName, documentId.ToString("N"), cancellationToken)
            .ConfigureAwait(false);

        if (json is null)
            return null;

        // Corrupted stored content is corruption, not absence — a null
        // deserialisation result (the literal `null` document) or a
        // JsonException must never be misreported as "no such document"
        // or escape as a raw BCL exception (`TD-60`).
        try
        {
            return JsonSerializer.Deserialize<EngineeringDocumentDto>(json)
                ?? throw new EngineeringDataException($"Document '{documentId}' could not be deserialised.");
        }
        catch (JsonException ex)
        {
            throw new EngineeringDataException($"Document '{documentId}' could not be deserialised.", ex);
        }
    }

    private Task WriteDocumentAsync(Guid documentId, EngineeringDocumentDto dto, CancellationToken cancellationToken) =>
        _persistenceStore.WriteAsync(DocumentsCollectionName, documentId.ToString("N"), JsonSerializer.Serialize(dto), cancellationToken);

    private async Task<DocumentRevisionDto?> ReadRevisionAsync(Guid documentId, int revisionNumber, CancellationToken cancellationToken)
    {
        var json = await _persistenceStore.ReadAsync(RevisionsCollectionName, RevisionKey(documentId, revisionNumber), cancellationToken)
            .ConfigureAwait(false);

        if (json is null)
            return null;

        // Same corruption-is-not-absence guard as ReadDocumentAsync (`TD-60`).
        try
        {
            return JsonSerializer.Deserialize<DocumentRevisionDto>(json)
                ?? throw new EngineeringDataException($"Revision {revisionNumber} of document '{documentId}' could not be deserialised.");
        }
        catch (JsonException ex)
        {
            throw new EngineeringDataException($"Revision {revisionNumber} of document '{documentId}' could not be deserialised.", ex);
        }
    }

    private Task WriteRevisionAsync(Guid documentId, int revisionNumber, DocumentRevisionDto dto, CancellationToken cancellationToken) =>
        _persistenceStore.WriteAsync(RevisionsCollectionName, RevisionKey(documentId, revisionNumber), JsonSerializer.Serialize(dto), cancellationToken);

    private static string RevisionKey(Guid documentId, int revisionNumber) =>
        $"{documentId:N}_{revisionNumber:D10}";
}
