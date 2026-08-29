using System.Security.Cryptography;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Persists and reads attachment bytes — the durable half of an attached
/// file (`TD-31`).
/// </summary>
/// <remarks>
/// <para>
/// Writes through the platform's single persistence store in its byte
/// shape (<see cref="IBinaryPersistenceStore"/>), one record per
/// attachment, keyed by the attachment's own Id — the same substrate and
/// the same record shape <see cref="EngineeringObjectStateStore"/> already
/// uses for object state and <c>EngineeringDocumentStore</c> for
/// documents. This introduces no new storage mechanism and no second
/// authority.
/// </para>
/// <para>
/// <b>Bytes, not base64 in the text store.</b> The text store would have
/// held this content too, at a third again the size and by turning every
/// read and write into an encode/decode of the whole file. The byte shape
/// exists so that a 40 MB drawing costs 40 MB on disk and one read, and so
/// that content is never at the mercy of an encoding round-trip it did not
/// ask for.
/// </para>
/// <para>
/// <b>Integrity is checked on the way out, not assumed.</b> Every save
/// records a SHA-256 of what was stored; every read recomputes it and
/// compares, along with the size the metadata claims. A record that
/// disagrees with its own metadata is reported
/// <see cref="AttachmentContentStatus.Corrupt"/> and its bytes are not
/// returned. This is what makes "the file survived the restart" a checked
/// claim rather than a hope: a truncated write, a half-copied store
/// directory or a corrupted disk block all surface as a damaged
/// attachment instead of as silently wrong content handed to an engineer.
/// </para>
/// </remarks>
public sealed class AttachmentContentStore : IAttachmentContentStore
{
    /// <summary>The persistence-store collection attachment content lives in.</summary>
    public const string ContentCollectionName = "EngineeringDomain.AttachmentContent";

    private readonly IBinaryPersistenceStore _binaryStore;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="AttachmentContentStore"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="binaryStore"/> is <see langword="null"/>.</exception>
    public AttachmentContentStore(IBinaryPersistenceStore binaryStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(binaryStore);

        _binaryStore = binaryStore;
        _logger = logger;
    }

    /// <summary>
    /// The hash this store records for <paramref name="content"/>, as
    /// lowercase hex.
    /// </summary>
    /// <remarks>
    /// Exposed so the metadata that describes content and the store that
    /// holds it derive the hash the same way, from one definition. A
    /// second, privately duplicated hash function is exactly how a
    /// verification check quietly becomes a check of nothing.
    /// </remarks>
    public static string ComputeHash(ReadOnlySpan<byte> content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    /// <inheritdoc />
    public async Task<string> SaveAsync(Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        await _binaryStore.WriteBytesAsync(ContentCollectionName, KeyOf(attachmentId), content, cancellationToken).ConfigureAwait(false);

        return ComputeHash(content.Span);
    }

    /// <inheritdoc />
    public async Task<AttachmentContentResult> ReadAsync(
        Guid attachmentId,
        string? expectedHash,
        long expectedSizeInBytes,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes;
        try
        {
            bytes = await _binaryStore.ReadBytesAsync(ContentCollectionName, KeyOf(attachmentId), cancellationToken).ConfigureAwait(false);
        }
        catch (PersistenceStoreUnavailableException ex)
        {
            // A record that exists and cannot be read is damaged from the
            // caller's point of view, not absent — reported as such rather
            // than thrown, so one unreadable attachment does not fail the
            // object that owns it (`TD-60`).
            _logger?.Warning($"Attachment content '{attachmentId}' could not be read and is reported as corrupt.", ex);
            return AttachmentContentResult.Corrupt();
        }

        if (bytes is null)
            return AttachmentContentResult.Missing();

        if (bytes.LongLength != expectedSizeInBytes)
        {
            _logger?.Warning(
                $"Attachment content '{attachmentId}' is {bytes.LongLength} bytes but its metadata records {expectedSizeInBytes}.");
            return AttachmentContentResult.Corrupt();
        }

        // A null hash is an attachment written before this store existed:
        // there is nothing to compare against, so the size check above is
        // the whole of the verification. Honest about what it can check
        // rather than passing an unverifiable record off as verified.
        if (expectedHash is not null && !string.Equals(ComputeHash(bytes), expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.Warning($"Attachment content '{attachmentId}' does not match the hash recorded for it.");
            return AttachmentContentResult.Corrupt();
        }

        return AttachmentContentResult.Available(bytes);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
        _binaryStore.DeleteAsync(ContentCollectionName, KeyOf(attachmentId), cancellationToken);

    private static string KeyOf(Guid attachmentId) => attachmentId.ToString("N");
}
