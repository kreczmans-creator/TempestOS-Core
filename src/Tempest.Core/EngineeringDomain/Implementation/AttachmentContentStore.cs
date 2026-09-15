using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
/// shape (<see cref="IBinaryPersistenceStore"/>) — the same substrate
/// <see cref="EngineeringObjectStateStore"/> already uses for object state
/// and <c>EngineeringDocumentStore</c> for documents. This introduces no
/// new storage mechanism and no second authority.
/// </para>
/// <para>
/// <b>Content-addressed, with a reference count (`TD-95`).</b> Bytes are
/// keyed by their own SHA-256, not by the attachment that first stored
/// them: attaching the same file to two objects stores it once, and
/// deleting one of the two attachments leaves the other's copy intact.
/// Three collections in the one generic <c>records</c> table carry this —
/// no change to the SQL schema (`ADR-0144`) was needed, because that
/// table was already collection/key-agnostic:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="ContentCollectionName"/> — the bytes themselves, keyed by content hash (64 lowercase hex characters).</description></item>
/// <item><description><see cref="ReferenceCountCollectionName"/> — how many attachments currently reference one hash, keyed by that hash.</description></item>
/// <item><description><see cref="HashByAttachmentCollectionName"/> — which hash one attachment currently references, keyed by the attachment's own Id (32 hex characters — never 64, so it can never collide with a hash key even though it shares <see cref="ContentCollectionName"/>'s legacy use, below).</description></item>
/// </list>
/// <para>
/// <b>Existing stores migrate on first read.</b> Before this Work
/// Package, content was keyed directly by attachment Id in
/// <see cref="ContentCollectionName"/> — a 32-hex-character key, never
/// colliding with a 64-hex-character hash key, so both shapes can live in
/// that one collection at once. A read that finds no
/// <see cref="HashByAttachmentCollectionName"/> mapping falls back to that
/// legacy, attachment-Id-keyed row; if it finds bytes there,
/// <see cref="ReadAsync"/> adopts them into the content-addressed layout
/// (retained under their hash, mapped, and the legacy row removed) before
/// returning them, so the row is deduplicated with anything else that
/// happens to share its content from that point on. <see cref="OpenReadAsync"/>
/// reads a legacy row exactly where it finds it but does not migrate it —
/// doing so would mean holding a large, not-yet-addressed legacy file
/// whole in memory purely to move it, which is the material `TD-96`
/// exists to avoid. Such a row keeps costing disk until something reads
/// it through <see cref="ReadAsync"/> instead; nothing is lost, and
/// nothing is at risk in the meantime.
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
/// <see cref="OpenReadAsync"/> applies the identical check over a
/// dedicated, bounded-memory pass through the stream before handing back
/// a second stream for the caller to actually consume (`TD-96`) — the
/// same discipline, paid for in one extra sequential pass rather than in
/// memory.
/// </para>
/// </remarks>
public sealed class AttachmentContentStore : IAttachmentContentStore, ITransactionalAttachmentWriter
{
    /// <summary>The persistence-store collection attachment content lives in.</summary>
    public const string ContentCollectionName = "EngineeringDomain.AttachmentContent";

    /// <summary>
    /// The persistence-store collection recording, per content hash, how
    /// many attachments currently reference it (`TD-95`). Values are a
    /// plain decimal integer, UTF-8 encoded into the byte store — the
    /// platform's one generic substrate, not a second storage shape.
    /// </summary>
    public const string ReferenceCountCollectionName = "EngineeringDomain.AttachmentContentRefCount";

    /// <summary>
    /// The persistence-store collection mapping one attachment's Id to the
    /// content hash it currently references (`TD-95`) — the only record
    /// that lets <see cref="DeleteAsync(Guid, CancellationToken)"/> and
    /// <see cref="SaveAsync"/>'s replace case find what to release,
    /// without the caller (which addresses everything else by attachment
    /// Id, never by hash) having to supply one.
    /// </summary>
    public const string HashByAttachmentCollectionName = "EngineeringDomain.AttachmentContentHashByAttachment";

    /// <summary>The buffer size <see cref="OpenReadAsync"/>'s verification pass reads in, and <see cref="Read(Span{byte})"/> discipline generally.</summary>
    private const int StreamCopyBufferSize = 81920;

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
    public async Task<string> SaveAsync(Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default) =>
        await SaveContentAsync(new BinaryStoreBackend(_binaryStore), attachmentId, content, cancellationToken).ConfigureAwait(false);

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
            bytes = await ReadStoredBytesAsync(attachmentId, expectedHash, cancellationToken).ConfigureAwait(false);
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
    public async Task<AttachmentContentStreamResult> OpenReadAsync(
        Guid attachmentId,
        string? expectedHash,
        long expectedSizeInBytes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mappedHash = await ReadMappingAsync(new BinaryStoreBackend(_binaryStore), attachmentId, cancellationToken).ConfigureAwait(false);

            // No mapping means either nothing was ever saved, or this is a
            // legacy row that predates content-addressing and still sits
            // at the attachment-Id key — the same fallback ReadAsync uses,
            // but without adopting it into the new layout: doing that here
            // would mean holding a possibly-large legacy file whole in
            // memory purely to relocate it (see the class remarks).
            var key = mappedHash ?? KeyOf(attachmentId);

            var status = await VerifyAsync(key, expectedHash, expectedSizeInBytes, cancellationToken).ConfigureAwait(false);
            if (status == AttachmentContentStatus.Missing)
                return AttachmentContentStreamResult.Missing();
            if (status == AttachmentContentStatus.Corrupt)
                return AttachmentContentStreamResult.Corrupt();

            // A second, independent stream: the caller is free to seek
            // within it — a PDF's own cross-reference lookup does exactly
            // that — without disturbing the verification pass that just
            // finished with its own, already-closed stream.
            var stream = await _binaryStore.OpenReadAsync(ContentCollectionName, key, cancellationToken).ConfigureAwait(false);

            // Vanished between the verification pass and this open — a
            // concurrent delete, not corruption; that pass already proved
            // the bytes were intact the instant before.
            return stream is null ? AttachmentContentStreamResult.Missing() : AttachmentContentStreamResult.Available(stream);
        }
        catch (PersistenceStoreUnavailableException ex)
        {
            _logger?.Warning($"Attachment content '{attachmentId}' could not be streamed and is reported as corrupt.", ex);
            return AttachmentContentStreamResult.Corrupt();
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
        DeleteContentAsync(new BinaryStoreBackend(_binaryStore), attachmentId, cancellationToken);

    /// <summary>
    /// Reads a chunk of <paramref name="key"/>'s stream, verifying its
    /// total length and (when <paramref name="expectedHash"/> is given)
    /// its SHA-256, in <see cref="StreamCopyBufferSize"/>-sized chunks —
    /// never holding the whole value in memory at once (`TD-96`).
    /// </summary>
    private async Task<AttachmentContentStatus> VerifyAsync(
        string key, string? expectedHash, long expectedSizeInBytes, CancellationToken cancellationToken)
    {
        var verify = await _binaryStore.OpenReadAsync(ContentCollectionName, key, cancellationToken).ConfigureAwait(false);
        if (verify is null)
            return AttachmentContentStatus.Missing;

        await using (verify.ConfigureAwait(false))
        {
            var buffer = ArrayPool<byte>.Shared.Rent(StreamCopyBufferSize);
            try
            {
                var total = 0L;
                using var hash = expectedHash is null ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                int read;
                while ((read = await verify.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    hash?.AppendData(buffer.AsSpan(0, read));
                }

                if (total != expectedSizeInBytes)
                {
                    _logger?.Warning($"Attachment content is {total} bytes but its metadata records {expectedSizeInBytes}.");
                    return AttachmentContentStatus.Corrupt;
                }

                if (hash is not null)
                {
                    var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
                    if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.Warning("Attachment content does not match the hash recorded for it.");
                        return AttachmentContentStatus.Corrupt;
                    }
                }

                return AttachmentContentStatus.Available;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Resolves and reads one attachment's stored bytes for
    /// <see cref="ReadAsync"/>: the content-addressed location if a
    /// mapping exists, otherwise the legacy attachment-Id-keyed location —
    /// migrating it into the content-addressed layout, best-effort, if
    /// found there.
    /// </summary>
    private async Task<byte[]?> ReadStoredBytesAsync(Guid attachmentId, string? expectedHash, CancellationToken cancellationToken)
    {
        var backend = new BinaryStoreBackend(_binaryStore);
        var mappedHash = await ReadMappingAsync(backend, attachmentId, cancellationToken).ConfigureAwait(false);
        if (mappedHash is not null)
            return await _binaryStore.ReadBytesAsync(ContentCollectionName, mappedHash, cancellationToken).ConfigureAwait(false);

        var legacyBytes = await _binaryStore.ReadBytesAsync(ContentCollectionName, KeyOf(attachmentId), cancellationToken).ConfigureAwait(false);
        if (legacyBytes is not null)
        {
            // The hash to adopt: the caller's, when it has one, so a
            // pre-`TD-31` row that has since gained a recorded hash (the
            // metadata was rehydrated another way) adopts that hash rather
            // than a freshly computed one that would otherwise agree with
            // it anyway.
            await TryMigrateAsync(attachmentId, expectedHash ?? ComputeHash(legacyBytes), legacyBytes, cancellationToken).ConfigureAwait(false);
        }

        return legacyBytes;
    }

    /// <summary>
    /// Adopts a legacy, attachment-Id-keyed row into the content-addressed
    /// layout: retained under its hash (deduplicating it with anything
    /// else already there), mapped, and the legacy row removed — in that
    /// order, so a failure partway through leaves the row exactly where it
    /// was rather than losing it. Best-effort: a read that already has the
    /// bytes in hand must not fail over a housekeeping step that did not.
    /// </summary>
    private async Task TryMigrateAsync(Guid attachmentId, string hash, byte[] content, CancellationToken cancellationToken)
    {
        try
        {
            var backend = new BinaryStoreBackend(_binaryStore);
            await RetainAsync(backend, hash, content, cancellationToken).ConfigureAwait(false);
            await WriteMappingAsync(backend, attachmentId, hash, cancellationToken).ConfigureAwait(false);
            await _binaryStore.DeleteAsync(ContentCollectionName, KeyOf(attachmentId), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Warning(
                $"Attachment content '{attachmentId}' could not be migrated to content-addressed storage; " +
                "it remains readable at its legacy location.", ex);
        }
    }

    // ----------------------------------------------------------------
    // ITransactionalAttachmentWriter (`ADR-0145`)
    //
    // The bytes go into the same database, in the same transaction as the
    // object-state record that names them (`WP 17.1B`, binary payload
    // consistency). A committed attachment reference can therefore never
    // point at bytes that are not there, and a transaction that does not
    // commit leaves none behind — which is the whole reason the
    // write-intent marker and the reconciliation sweep were deleted
    // rather than kept. The content-addressing bookkeeping (`TD-95`) rides
    // in the identical transaction, through the identical
    // IPersistenceTransaction the object-state write uses: a reference
    // count is never left inconsistent with the mapping that earned it.
    // ----------------------------------------------------------------

    /// <inheritdoc />
    async Task<string> ITransactionalAttachmentWriter.SaveAsync(
        IPersistenceTransaction transaction, Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return await SaveContentAsync(new TransactionBackend(transaction), attachmentId, content, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    Task ITransactionalAttachmentWriter.DeleteAsync(IPersistenceTransaction transaction, Guid attachmentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return DeleteContentAsync(new TransactionBackend(transaction), attachmentId, cancellationToken);
    }

    // ----------------------------------------------------------------
    // Shared save/delete logic (`TD-95`), against the minimal backend
    // shape both a plain IBinaryPersistenceStore and an in-flight
    // IPersistenceTransaction already satisfy — one algorithm, run either
    // directly or inside a domain write transaction.
    // ----------------------------------------------------------------

    private static async Task<string> SaveContentAsync(
        IContentBackend backend, Guid attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var hash = ComputeHash(content.Span);
        var previousHash = await ReadMappingAsync(backend, attachmentId, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(previousHash, hash, StringComparison.Ordinal))
        {
            // Replacing this attachment's content (or saving it for the
            // first time): drop the old reference before adding the new
            // one, so a hash momentarily referenced by nobody is exactly
            // as reachable as "referenced by nobody forever" — there is no
            // window where both the old and the new content are retained
            // for this one attachment.
            if (previousHash is not null)
                await ReleaseAsync(backend, previousHash, cancellationToken).ConfigureAwait(false);

            await RetainAsync(backend, hash, content, cancellationToken).ConfigureAwait(false);
            await WriteMappingAsync(backend, attachmentId, hash, cancellationToken).ConfigureAwait(false);
        }

        // Identical content re-saved under the same attachment needs
        // neither a release nor a retain: the mapping and the reference
        // count already correctly describe one reference to it.
        return hash;
    }

    private static async Task DeleteContentAsync(IContentBackend backend, Guid attachmentId, CancellationToken cancellationToken)
    {
        var hash = await ReadMappingAsync(backend, attachmentId, cancellationToken).ConfigureAwait(false);
        if (hash is not null)
        {
            await ReleaseAsync(backend, hash, cancellationToken).ConfigureAwait(false);
            await backend.DeleteAsync(HashByAttachmentCollectionName, KeyOf(attachmentId), cancellationToken).ConfigureAwait(false);
            return;
        }

        // No content-addressed mapping: either nothing was ever stored for
        // this attachment, or it is a legacy row still sitting at the
        // attachment-Id key. Deleting that key is correct and idempotent
        // either way.
        await backend.DeleteAsync(ContentCollectionName, KeyOf(attachmentId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records one more reference to <paramref name="hash"/>, storing
    /// <paramref name="content"/> under it only the first time anything
    /// references it — the dedupe itself (`TD-95`).
    /// </summary>
    private static async Task RetainAsync(IContentBackend backend, string hash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var existing = await ReadRefCountAsync(backend, hash, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            await backend.WriteBytesAsync(ContentCollectionName, hash, content, cancellationToken).ConfigureAwait(false);
            await WriteRefCountAsync(backend, hash, 1, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteRefCountAsync(backend, hash, existing.Value + 1, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Drops one reference to <paramref name="hash"/>, deleting its bytes
    /// once nothing references it any longer.
    /// </summary>
    private static async Task ReleaseAsync(IContentBackend backend, string hash, CancellationToken cancellationToken)
    {
        var existing = await ReadRefCountAsync(backend, hash, cancellationToken).ConfigureAwait(false) ?? 0;
        if (existing <= 1)
        {
            await backend.DeleteAsync(ReferenceCountCollectionName, hash, cancellationToken).ConfigureAwait(false);
            await backend.DeleteAsync(ContentCollectionName, hash, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteRefCountAsync(backend, hash, existing - 1, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<int?> ReadRefCountAsync(IContentBackend backend, string hash, CancellationToken cancellationToken)
    {
        var bytes = await backend.ReadBytesAsync(ReferenceCountCollectionName, hash, cancellationToken).ConfigureAwait(false);
        return bytes is null ? null : int.Parse(Encoding.ASCII.GetString(bytes), CultureInfo.InvariantCulture);
    }

    private static Task WriteRefCountAsync(IContentBackend backend, string hash, int count, CancellationToken cancellationToken) =>
        backend.WriteBytesAsync(
            ReferenceCountCollectionName, hash, Encoding.ASCII.GetBytes(count.ToString(CultureInfo.InvariantCulture)), cancellationToken);

    private static async Task<string?> ReadMappingAsync(IContentBackend backend, Guid attachmentId, CancellationToken cancellationToken)
    {
        var bytes = await backend.ReadBytesAsync(HashByAttachmentCollectionName, KeyOf(attachmentId), cancellationToken).ConfigureAwait(false);
        return bytes is null ? null : Encoding.ASCII.GetString(bytes);
    }

    private static Task WriteMappingAsync(IContentBackend backend, Guid attachmentId, string hash, CancellationToken cancellationToken) =>
        backend.WriteBytesAsync(HashByAttachmentCollectionName, KeyOf(attachmentId), Encoding.ASCII.GetBytes(hash), cancellationToken);

    private static string KeyOf(Guid attachmentId) => attachmentId.ToString("N");

    /// <summary>
    /// The minimal byte-record shape <see cref="SaveContentAsync"/>,
    /// <see cref="DeleteContentAsync"/> and their helpers run against — the
    /// three members <see cref="IBinaryPersistenceStore"/> and an in-flight
    /// <see cref="IPersistenceTransaction"/> already share, so one
    /// dedupe/refcount algorithm serves both the plain store and a domain
    /// write transaction without either backend knowing about the other.
    /// </summary>
    private interface IContentBackend
    {
        Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken);

        Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken);

        Task DeleteAsync(string collection, string key, CancellationToken cancellationToken);
    }

    private sealed class BinaryStoreBackend(IBinaryPersistenceStore store) : IContentBackend
    {
        public Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken) =>
            store.ReadBytesAsync(collection, key, cancellationToken);

        public Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken) =>
            store.WriteBytesAsync(collection, key, value, cancellationToken);

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken) =>
            store.DeleteAsync(collection, key, cancellationToken);
    }

    private sealed class TransactionBackend(IPersistenceTransaction transaction) : IContentBackend
    {
        public Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken) =>
            transaction.ReadBytesAsync(collection, key, cancellationToken);

        public Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken) =>
            transaction.WriteBytesAsync(collection, key, value, cancellationToken);

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken) =>
            transaction.DeleteAsync(collection, key, cancellationToken);
    }
}
