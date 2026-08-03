namespace Tempest.Core.EngineeringData;

/// <summary>
/// The concrete <see cref="IDocumentRevision"/> implementation — an
/// immutable snapshot of one revision's own content and provenance.
/// </summary>
internal sealed class DocumentRevision : IDocumentRevision
{
    public DocumentRevision(
        Guid documentId,
        int revisionNumber,
        string content,
        string? changeSummary,
        string authorPrincipalId,
        DateTimeOffset createdAt)
    {
        DocumentId = documentId;
        RevisionNumber = revisionNumber;
        Content = content;
        ChangeSummary = changeSummary;
        AuthorPrincipalId = authorPrincipalId;
        CreatedAt = createdAt;
    }

    /// <inheritdoc />
    public Guid DocumentId { get; }

    /// <inheritdoc />
    public int RevisionNumber { get; }

    /// <inheritdoc />
    public string Content { get; }

    /// <inheritdoc />
    public string? ChangeSummary { get; }

    /// <inheritdoc />
    public string AuthorPrincipalId { get; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; }
}
