namespace Tempest.Core.EngineeringData;

/// <summary>
/// A shared store for engineering-domain documents — revisioned, typed,
/// and linkable, but opaque in content. Not a general-purpose document
/// database; scoped to what Engineering Foundation and future
/// Engineering Module consumers need.
/// </summary>
public interface IEngineeringDocumentStore
{
    /// <summary>Creates a new document of the given <paramref name="kind"/> with an initial revision.</summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="initialContent"/> is <see langword="null"/>.</exception>
    Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default);

    /// <summary>Returns the document, or <see langword="null"/> if none exists.</summary>
    Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Records a new revision, incrementing <see cref="IEngineeringDocument.CurrentRevisionNumber"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="newContent"/> is <see langword="null"/>.</exception>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="documentId"/> does not exist.</exception>
    Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default);

    /// <summary>Every revision of the document, oldest first. Never <see langword="null"/>.</summary>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="documentId"/> does not exist.</exception>
    Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Records a typed, directed relationship between two existing documents.</summary>
    /// <exception cref="ArgumentException"><paramref name="relationshipKind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="sourceDocumentId"/> or <paramref name="targetDocumentId"/> does not exist.</exception>
    Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default);

    /// <summary>Every reference where <paramref name="documentId"/> is the source. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default);
}
