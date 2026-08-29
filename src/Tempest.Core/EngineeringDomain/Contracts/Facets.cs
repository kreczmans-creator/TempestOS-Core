namespace Tempest.Core.EngineeringDomain;

public interface IHasBusinessIdentifier
{
    string? Identifier { get; }
    string DisplayName { get; }
}

public interface IHasMetadata
{
    string? Category { get; }
    string? Discipline { get; }
    string? Owner { get; }
    IReadOnlyList<string> Tags { get; }
    string? Classification { get; }
    string? Notes { get; }
}

public interface IHasLifecycle
{
    LifecycleState Status { get; }
    IReadOnlyList<ILifecycleTransitionRecord> History { get; }
    Task TransitionAsync(LifecycleState target, CancellationToken cancellationToken = default);
}

/// <summary>A single, immutable content revision — a same-shape analogue of <see cref="EngineeringData.IDocumentRevision"/>, scoped to one object's own history. Not defined by <c>WP8.2B Interface Catalogue.md</c> despite being referenced by <see cref="IHasRevisions.GetRevisionHistoryAsync"/> — a disclosed, implementation-time contract gap closed here (WP 8.2C).</summary>
public interface IRevisionRecord
{
    int RevisionNumber { get; }
    string Content { get; }
    string? ChangeSummary { get; }
    string AuthorPrincipalId { get; }
    DateTimeOffset CreatedAt { get; }
}

public interface IHasRevisions
{
    string Content { get; }
    string AuthorPrincipalId { get; }
    Task<IHasRevisions> ReviseAsync(string newContent, string? changeSummary, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IRevisionRecord>> GetRevisionHistoryAsync(CancellationToken cancellationToken = default);
}

public interface IHasRelationships
{
    Task LinkAsync(Guid targetId, string relationshipKind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IEngineeringRelationship>> GetRelationshipsAsync(CancellationToken cancellationToken = default);
}

public interface ITraceable
{
    Task<IEvidence> GetEvidenceAsync(CancellationToken cancellationToken = default);
}

public interface IValidatable
{
    Task<IValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
}

public interface IHasAttachments
{
    Task AttachAsync(IAttachment attachment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IAttachment>> GetAttachmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a real file: stores <paramref name="content"/> durably and
    /// records the metadata describing it (`TD-31`).
    /// </summary>
    /// <param name="fileName">The file's own name.</param>
    /// <param name="contentType">The file's MIME type.</param>
    /// <param name="content">The file's bytes. Empty is legal; a zero-byte file is a file.</param>
    /// <param name="cancellationToken">Cancels the attach.</param>
    /// <returns>The attachment recorded, carrying the size and hash of what was actually stored.</returns>
    /// <remarks>
    /// <para>
    /// The size and hash are derived from the bytes rather than accepted
    /// from the caller, so metadata cannot describe a file the store does
    /// not hold. <see cref="AttachAsync"/> remains for the metadata-only
    /// case it has always served — an attachment that names a file this
    /// platform does not have.
    /// </para>
    /// <para>
    /// Content is written before metadata, deliberately. A crash between
    /// the two leaves bytes that nothing references, which is invisible
    /// and reclaimable; the other order would leave an attachment
    /// promising content that was never stored, which is a record that
    /// lies.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">No <see cref="IAttachmentContentStore"/> is configured, so content cannot be stored.</exception>
    Task<IAttachment> AttachContentAsync(
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the durable bytes of one of this object's attachments,
    /// verified against the metadata describing them (`TD-31`).
    /// </summary>
    /// <remarks>
    /// Never throws for an attachment this object does not have, for one
    /// whose content was never stored, or for content that fails its own
    /// integrity check: all three are ordinary answers, reported through
    /// <see cref="AttachmentContentResult"/>.
    /// </remarks>
    Task<AttachmentContentResult> ReadAttachmentContentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}

public interface ISearchable
{
    string SearchableText { get; }
}
