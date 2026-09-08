using Tempest.Core.EngineeringData;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The write half of <see cref="IEngineeringDocumentStore"/>, expressed
/// against one in-flight <see cref="IPersistenceTransaction"/> (`ADR-0145`).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a second, internal contract rather than more members on
/// <see cref="IEngineeringDocumentStore"/>: that interface is the
/// platform's public read surface for documents and revisions, and a
/// transaction handle has no meaning to a caller reading one. The public
/// methods on <see cref="EngineeringDocumentStore"/> remain, and remain
/// the only way anything outside this assembly reads a document; every
/// durable <em>write</em> to a document now happens through this contract,
/// inside the one transaction that also carries the object state the
/// document belongs to.
/// </para>
/// <para>
/// The writer never mints or advances a revision outside the transaction,
/// so the `TD-67` ordering note this replaces — "a crash between the two
/// writes leaves an orphaned revision" — no longer describes a reachable
/// state: there is one write, and it either lands whole or not at all.
/// </para>
/// </remarks>
internal sealed record DocumentCreation(IEngineeringDocument Document, IDocumentRevision Revision);

internal interface ITransactionalDocumentWriter
{
    /// <summary>Writes a new document and its revision 1 inside <paramref name="transaction"/>.</summary>
    /// <remarks>
    /// Returns the revision as well as the document because the caller is
    /// inside the transaction and cannot read it back through the public
    /// store: the SQLite backend's transaction holds the database's write
    /// lock, so a read issued against the outer store from inside the body
    /// contends with the transaction waiting for it.
    /// </remarks>
    Task<DocumentCreation> CreateAsync(
        IPersistenceTransaction transaction, Guid documentId, string kind, string initialContent, CancellationToken cancellationToken);

    /// <summary>Writes the next revision of an existing document, and the document record naming it, inside <paramref name="transaction"/>.</summary>
    Task<IDocumentRevision> ReviseAsync(
        IPersistenceTransaction transaction, Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken);

    /// <summary>Writes one outgoing reference inside <paramref name="transaction"/>.</summary>
    Task LinkAsync(
        IPersistenceTransaction transaction, Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken);

    /// <summary>Reads a document's own record through <paramref name="transaction"/>, seeing its uncommitted writes.</summary>
    Task<bool> ExistsAsync(IPersistenceTransaction transaction, Guid documentId, CancellationToken cancellationToken);
}

/// <summary>
/// The write half of <see cref="IAttachmentContentStore"/>, expressed
/// against one in-flight <see cref="IPersistenceTransaction"/> (`ADR-0145`).
/// </summary>
/// <remarks>
/// Attachment bytes are a BLOB in the same database, written in the same
/// transaction as the object state that references them (`WP 17.1B`,
/// binary payload consistency). A committed attachment reference can
/// therefore never name bytes that are not there, and a transaction that
/// does not commit leaves no bytes behind — which is why
/// <c>AttachmentWriteIntentStore</c> and the reconciliation sweep that
/// consumed it were deleted rather than kept.
/// </remarks>
internal interface ITransactionalAttachmentWriter
{
    /// <summary>Writes <paramref name="content"/> inside <paramref name="transaction"/> and returns its SHA-256.</summary>
    Task<string> SaveAsync(
        IPersistenceTransaction transaction, Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Removes an attachment's bytes inside <paramref name="transaction"/>. Idempotent.</summary>
    Task DeleteAsync(IPersistenceTransaction transaction, Guid attachmentId, CancellationToken cancellationToken);
}

/// <summary>
/// The write half of <see cref="IEngineeringObjectStateStore"/>, expressed
/// against one in-flight <see cref="IPersistenceTransaction"/> (`ADR-0145`).
/// </summary>
internal interface ITransactionalStateWriter
{
    /// <summary>Writes one object-state record inside <paramref name="transaction"/>.</summary>
    Task SaveAsync(IPersistenceTransaction transaction, EngineeringObjectState state, CancellationToken cancellationToken);
}

/// <summary>
/// The largest attachment payload this build will store (`WP 17.1B`).
/// </summary>
/// <remarks>
/// A calc-sheet PDF is under 5 MB and a scanned drawing under 50 MB; a
/// payload above this is refused with a clear message rather than held
/// whole in memory on both sides of a SQLite BLOB write.
/// </remarks>
public static class AttachmentContentLimits
{
    /// <summary>256 MB, in bytes.</summary>
    public const long MaximumSizeInBytes = 256L * 1024 * 1024;
}

/// <summary>
/// Raised when an attachment payload exceeds
/// <see cref="AttachmentContentLimits.MaximumSizeInBytes"/>.
/// </summary>
public sealed class AttachmentContentTooLargeException : EngineeringDomainException
{
    /// <summary>Initialises a new instance of the <see cref="AttachmentContentTooLargeException"/> class.</summary>
    public AttachmentContentTooLargeException(string fileName, long sizeInBytes)
        : base($"Attachment '{fileName}' is {sizeInBytes:N0} bytes, above the {AttachmentContentLimits.MaximumSizeInBytes:N0}-byte " +
               "limit this build stores. Attach a smaller file, or link to it rather than embedding it.")
    {
        FileName = fileName;
        SizeInBytes = sizeInBytes;
    }

    /// <summary>The offending attachment's file name.</summary>
    public string FileName { get; }

    /// <summary>The offending payload's size.</summary>
    public long SizeInBytes { get; }
}
