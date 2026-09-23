namespace Tempest.Core.Persistence;

/// <summary>
/// The byte-valued shape of the platform's single durable store — the same
/// store, the same root, the same records, for values that are not text
/// (`TD-31`).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a sibling contract rather than four more members on
/// <see cref="IPersistenceStore"/>, and deliberately implemented by the
/// same <c>PersistenceStore</c> class rather than by a second store.
/// </para>
/// <para>
/// <b>Why not extend <see cref="IPersistenceStore"/>.</b> Twenty test
/// doubles across this repository implement that interface. Adding
/// byte members to it would break every one of them to serve callers
/// that will never store bytes, and would push a concern most
/// implementers do not have into all of them. Splitting the shape leaves
/// each implementer free to carry only what it actually stores.
/// </para>
/// <para>
/// <b>Why not a second store.</b> Everything that makes the text store
/// trustworthy is value-agnostic and was expensive to get right: the
/// reserved-device-name-safe file naming and its legacy migration
/// (`TD-59`), the per-key lock that keys case-variants onto one lock, the
/// exact-name resolution that never returns a case-variant's record, and
/// the write-to-temporary-then-rename that makes an interrupted write
/// leave either the old value or the new one. A separate byte store would
/// have to reproduce all of it, and would then be a second persistence
/// architecture with its own root, its own encoding, and its own bugs.
/// The implementation therefore shares those paths outright, and bytes
/// differ from text at exactly one point: the file is read and written
/// without an encoding.
/// </para>
/// <para>
/// A record written through one shape is not readable through the other,
/// and that is intentional rather than incidental: bytes that happen to
/// be valid UTF-8 are still bytes, and a caller that stored an image must
/// never receive a string that silently lost half of it. Collections are
/// owned by exactly one calling service, so no record is ever addressed
/// through both.
/// </para>
/// </remarks>
public interface IBinaryPersistenceStore
{
    /// <summary>
    /// Reads the bytes stored under <paramref name="key"/> within
    /// <paramref name="collection"/>, or <see langword="null"/> if no
    /// record exists.
    /// </summary>
    /// <param name="collection">A logical grouping, owned by exactly one calling service.</param>
    /// <param name="key">The record's key, unique within <paramref name="collection"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="ArgumentException"><paramref name="collection"/> or <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="PersistenceStoreUnavailableException">The record exists but could not be read.</exception>
    Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable, seekable stream over the bytes stored under
    /// <paramref name="key"/> within <paramref name="collection"/>, or
    /// <see langword="null"/> if no record exists — the same "missing"
    /// answer as <see cref="ReadBytesAsync"/>, without ever materialising
    /// the whole value in memory (`TD-96`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Backed by SQLite's own incremental BLOB I/O
    /// (<c>Microsoft.Data.Sqlite.SqliteBlob</c>, <c>sqlite3_blob_read</c>
    /// under it): a read against this stream costs the bytes actually
    /// requested, not the record's whole length, and the stream supports
    /// <see cref="Stream.Seek(long, SeekOrigin)"/> because the native API
    /// does. A caller that only needs a handful of bytes, or that will
    /// hold the whole value in memory anyway, still wants
    /// <see cref="ReadBytesAsync"/> — this exists for the caller that will
    /// not: a page renderer or a "save this attachment to disk" copy that
    /// would otherwise pull a 200 MB drawing into one array just to move
    /// it somewhere else one buffer at a time.
    /// </para>
    /// <para>
    /// The returned stream owns a dedicated connection to the store; it
    /// must be disposed once read, and disposing it never affects the
    /// stored value, only this read's own resources. A record whose value
    /// is text rather than bytes (<c>blob_value IS NULL</c>) is reported
    /// exactly as an absent record — the same rule <see cref="ReadBytesAsync"/>
    /// already applies.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="collection"/> or <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="PersistenceStoreUnavailableException">The record exists but could not be opened.</exception>
    Task<Stream?> OpenReadAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="value"/> under <paramref name="key"/> within
    /// <paramref name="collection"/>, creating or overwriting as needed.
    /// </summary>
    /// <param name="collection">A logical grouping, owned by exactly one calling service.</param>
    /// <param name="key">The record's key, unique within <paramref name="collection"/>.</param>
    /// <param name="value">The bytes to store. Empty is a legal value and is not the same as no record.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentException"><paramref name="collection"/> or <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="PersistenceStoreUnavailableException">The record could not be written.</exception>
    Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the record stored under <paramref name="key"/> within
    /// <paramref name="collection"/>, if any. Never throws for a missing
    /// key — deletion is idempotent.
    /// </summary>
    /// <remarks>
    /// Declared here as well as on <see cref="IPersistenceStore"/>, with
    /// the identical signature, because removing a record does not depend
    /// on the shape of the value in it: a caller holding only this
    /// contract still has to be able to delete what it wrote, without
    /// casting to a contract it was not given. One method on
    /// <c>PersistenceStore</c> satisfies both.
    /// </remarks>
    Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default);
}
