namespace Tempest.Core.EngineeringData;

/// <summary>
/// An immutable, append-only snapshot of a document's content at one
/// point in its own revision history.
/// </summary>
/// <remarks>
/// Once written, a revision is never modified or deleted —
/// <see cref="IEngineeringDocumentStore.ReviseAsync"/> always creates a
/// new revision, mirroring <see cref="Audit.IAuditRecord"/>'s own
/// append-only shape.
/// </remarks>
public interface IDocumentRevision
{
    /// <summary>Gets the identity of the document this revision belongs to.</summary>
    Guid DocumentId { get; }

    /// <summary>Gets this revision's own sequence number, starting at 1 for a document's initial revision.</summary>
    int RevisionNumber { get; }

    /// <summary>
    /// Gets this revision's content — opaque to this namespace, defined
    /// entirely by the calling consumer.
    /// </summary>
    string Content { get; }

    /// <summary>Gets a caller-supplied summary of what changed in this revision, if any was given.</summary>
    string? ChangeSummary { get; }

    /// <summary>Gets the identity of the principal who authored this revision.</summary>
    string AuthorPrincipalId { get; }

    /// <summary>Gets the instant this revision was recorded.</summary>
    DateTimeOffset CreatedAt { get; }
}
