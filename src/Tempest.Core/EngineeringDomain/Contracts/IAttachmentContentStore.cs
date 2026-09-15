namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The durable store of attachment <b>bytes</b> — what makes an attached
/// file a file this platform holds rather than a description of one
/// (`TD-31`).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately separate from <see cref="IAttachment"/>, which carries the
/// metadata: file name, content type, size, and the hash of the content
/// this store holds. That split is the point of the boundary. The
/// engineering object owns the fact that a file is attached and what it
/// is; this store owns the bytes. An object's state can be read,
/// rehydrated, listed and rendered without ever loading a megabyte of PDF,
/// and the bytes can be verified without reopening the object.
/// </para>
/// <para>
/// The mirror of <see cref="IEngineeringObjectStateStore"/> one level down:
/// the same single <c>IPersistenceStore</c> substrate, its own collection,
/// one record per attachment keyed by the attachment's own Id. No second
/// storage mechanism, no second root, and no path to a file outside the
/// store — a stored path would make the record a promise about someone
/// else's disk, which is precisely the limitation `TD-31` exists to
/// remove.
/// </para>
/// </remarks>
public interface IAttachmentContentStore
{
    /// <summary>
    /// Stores <paramref name="content"/> as the bytes of
    /// <paramref name="attachmentId"/>, replacing any previous record.
    /// </summary>
    /// <returns>The hash of what was stored, for the metadata that describes it.</returns>
    /// <exception cref="Persistence.PersistenceStoreUnavailableException">The content could not be written.</exception>
    Task<string> SaveAsync(Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the bytes of <paramref name="attachmentId"/> and checks them
    /// against the metadata that describes them.
    /// </summary>
    /// <param name="attachmentId">The attachment whose content to read.</param>
    /// <param name="expectedHash">
    /// The hash recorded when the content was stored, or <see langword="null"/>
    /// for an attachment that carries no hash — an attachment created
    /// before `TD-31`, whose content cannot be verified because nothing
    /// ever recorded what it should be.
    /// </param>
    /// <param name="expectedSizeInBytes">The size recorded in the attachment's metadata.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <remarks>
    /// Never throws for missing or damaged content: both are ordinary
    /// answers about a passive read, reported through
    /// <see cref="AttachmentContentResult"/> rather than as failures
    /// (`TD-60`'s discipline — one unreadable record must not cost the
    /// caller every other one).
    /// </remarks>
    Task<AttachmentContentResult> ReadAsync(
        Guid attachmentId,
        string? expectedHash,
        long expectedSizeInBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a verified, seekable stream over one attachment's bytes, for
    /// a caller that will consume them without ever holding the whole
    /// file in memory (`TD-96`) — a large drawing opened in the Document
    /// Viewer, chiefly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verification runs first, over a dedicated pass through the stored
    /// bytes in bounded-size chunks — the same "checked on the way out"
    /// discipline <see cref="ReadAsync"/> applies to a whole array in one
    /// go, applied one chunk at a time so confirming a 200 MB drawing does
    /// not cost 200 MB of memory. Once that pass has passed, a second,
    /// independent stream over the same content is opened and handed
    /// back: the caller is free to seek within it — a PDF's own
    /// cross-reference lookup does exactly that — without disturbing the
    /// check that already ran.
    /// </para>
    /// <para>
    /// Never throws for missing or damaged content, exactly as
    /// <see cref="ReadAsync"/> does not: both are ordinary answers about a
    /// passive read, reported through <see cref="AttachmentContentStreamResult.Status"/>.
    /// </para>
    /// </remarks>
    /// <param name="attachmentId">The attachment whose content to open.</param>
    /// <param name="expectedHash">The hash recorded when the content was stored, or <see langword="null"/> for an attachment that carries none (`TD-31`).</param>
    /// <param name="expectedSizeInBytes">The size recorded in the attachment's metadata.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// A result the caller must dispose: disposing it disposes the stream,
    /// if one was returned.
    /// </returns>
    Task<AttachmentContentStreamResult> OpenReadAsync(
        Guid attachmentId,
        string? expectedHash,
        long expectedSizeInBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the content of <paramref name="attachmentId"/>, if any.
    /// Idempotent: removing content that was never stored is not an error.
    /// </summary>
    Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of opening one attachment's stored bytes as a stream
/// (`TD-96`) — the streaming counterpart of <see cref="AttachmentContentResult"/>.
/// </summary>
/// <remarks>
/// A three-valued result rather than a nullable <see cref="Stream"/>, for
/// the identical reason <see cref="AttachmentContentResult"/> is not a
/// nullable <c>byte[]</c>: "we never held this file" and "we held it and
/// what came back is not it" are different facts a caller must be able to
/// tell apart. <see cref="Stream"/> is non-null only for
/// <see cref="AttachmentContentStatus.Available"/>.
/// </remarks>
public sealed class AttachmentContentStreamResult : IDisposable
{
    private AttachmentContentStreamResult(AttachmentContentStatus status, Stream? stream)
    {
        Status = status;
        Stream = stream;
    }

    /// <summary>Gets what happened.</summary>
    public AttachmentContentStatus Status { get; }

    /// <summary>Gets the opened stream — <see langword="null"/> unless <see cref="Status"/> is <see cref="AttachmentContentStatus.Available"/>.</summary>
    public Stream? Stream { get; }

    /// <summary>Gets whether the content was found intact and a stream was opened.</summary>
    public bool IsAvailable => Status is AttachmentContentStatus.Available;

    /// <summary>The content was found and verified; <paramref name="stream"/> is open and positioned at the start.</summary>
    public static AttachmentContentStreamResult Available(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new AttachmentContentStreamResult(AttachmentContentStatus.Available, stream);
    }

    /// <summary>No content is stored for this attachment.</summary>
    public static AttachmentContentStreamResult Missing() => new(AttachmentContentStatus.Missing, null);

    /// <summary>Content is stored but does not match the metadata describing it.</summary>
    public static AttachmentContentStreamResult Corrupt() => new(AttachmentContentStatus.Corrupt, null);

    /// <summary>Disposes <see cref="Stream"/>, if one was returned. Idempotent.</summary>
    public void Dispose() => Stream?.Dispose();
}
