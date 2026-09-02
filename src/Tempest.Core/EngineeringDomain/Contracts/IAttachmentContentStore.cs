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
    /// Removes the content of <paramref name="attachmentId"/>, if any.
    /// Idempotent: removing content that was never stored is not an error.
    /// </summary>
    Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
