namespace Tempest.Core.Persistence;

/// <summary>
/// The query and transaction shape of the platform's single durable store
/// (`ADR-0144`) — the four things every consumer of
/// <see cref="IPersistenceStore"/> has been simulating in application code
/// since `ADR-0041`: list a subset of a collection's keys, read a whole
/// collection at once, read a named set of keys at once, and write more
/// than one key so that either all of them land or none of them do.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a third interface rather than more members on the first two.</b>
/// The same reason <see cref="IBinaryPersistenceStore"/> is a sibling
/// rather than four more members on <see cref="IPersistenceStore"/>: a
/// large number of test doubles across this repository implement those
/// two, and none of them is asked to grow a query engine. The two
/// shipped implementations both carry this shape —
/// <c>SqlitePersistenceStore</c> natively, <see cref="PersistenceStore"/>
/// by scanning — so a consumer may depend on it without knowing which
/// backend is configured.
/// </para>
/// <para>
/// <b>The one thing the two backends do not agree on is atomicity.</b>
/// <see cref="ExecuteInTransactionAsync"/> is a real transaction on
/// SQLite and a bare sequence of writes on the file-per-key store, which
/// has no mechanism capable of being one. The file store's own
/// implementation says so at the method, loudly; `ADR-0144` retires that
/// backend in `v0.18.0` for exactly this reason, and `WP 17.1B`'s
/// transactional object store is built on the SQLite backend alone.
/// </para>
/// <para>
/// Keys and collections are exact and case-sensitive here, as everywhere
/// else on this store under `ADR-0144`.
/// </para>
/// </remarks>
public interface IQueryablePersistenceStore
{
    /// <summary>
    /// Lists every key in <paramref name="collection"/> that begins with
    /// <paramref name="keyPrefix"/>, in ascending ordinal key order.
    /// </summary>
    /// <param name="collection">A logical grouping, owned by exactly one calling service.</param>
    /// <param name="keyPrefix">The exact, case-sensitive prefix to match. The empty string matches every key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Every matching key; empty if the collection has none, never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="collection"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="keyPrefix"/> is <see langword="null"/>.</exception>
    /// <exception cref="PersistenceStoreUnavailableException">The store could not be queried.</exception>
    Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads every text record in <paramref name="collection"/> in one
    /// query, in ascending ordinal key order.
    /// </summary>
    /// <remarks>
    /// Records written through <see cref="IBinaryPersistenceStore"/> are
    /// not text records and do not appear here — the same rule
    /// <see cref="IPersistenceStore.ReadAsync"/> follows for a single key.
    /// </remarks>
    /// <param name="collection">A logical grouping, owned by exactly one calling service.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <exception cref="ArgumentException"><paramref name="collection"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="PersistenceStoreUnavailableException">The store could not be queried.</exception>
    Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the named <paramref name="keys"/> from
    /// <paramref name="collection"/> in one query.
    /// </summary>
    /// <remarks>
    /// The result holds one entry for <b>every</b> requested key, whose
    /// value is <see langword="null"/> when that key has no record or
    /// holds bytes rather than text — so a caller can distinguish "asked
    /// for and absent" from "never asked for" without comparing key sets.
    /// A key repeated in <paramref name="keys"/> appears once.
    /// </remarks>
    /// <param name="collection">A logical grouping, owned by exactly one calling service.</param>
    /// <param name="keys">The keys to read. Empty is legal and yields an empty result.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <exception cref="ArgumentException"><paramref name="collection"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> is <see langword="null"/>.</exception>
    /// <exception cref="PersistenceStoreUnavailableException">The store could not be queried.</exception>
    Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> against a single transaction, which
    /// commits when <paramref name="work"/> returns and rolls back if it
    /// throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="IPersistenceTransaction"/> handed to
    /// <paramref name="work"/> is valid only for the duration of that
    /// call; using it afterwards is an error. Every read and write it
    /// performs sees the transaction's own uncommitted state, so a write
    /// followed by a read of the same key returns what was just written.
    /// </para>
    /// <para>
    /// <b>Use the handle, not the store, inside the body.</b> The SQLite
    /// backend runs the transaction on its own connection under
    /// <c>BEGIN IMMEDIATE</c>, which holds the database's single write
    /// lock; a call made to the outer store from inside
    /// <paramref name="work"/> therefore contends with the transaction
    /// that is waiting for it and fails on the busy timeout rather than
    /// waiting forever. That is a bounded error, not a deadlock, and it
    /// is the reason the transaction carries a full read/write surface of
    /// its own instead of a write-only one.
    /// </para>
    /// </remarks>
    /// <param name="work">The unit of work to run inside the transaction.</param>
    /// <param name="cancellationToken">Cancels the transaction; a cancelled transaction rolls back.</param>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    /// <exception cref="PersistenceStoreUnavailableException">The transaction could not be begun or committed.</exception>
    Task ExecuteInTransactionAsync(Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default);
}

/// <summary>
/// The read/write surface of one in-flight transaction opened by
/// <see cref="IQueryablePersistenceStore.ExecuteInTransactionAsync"/>.
/// </summary>
/// <remarks>
/// Deliberately mirrors <see cref="IPersistenceStore"/> and
/// <see cref="IBinaryPersistenceStore"/> member for member rather than
/// inheriting from them: those two are the store, this is a scope on it,
/// and a transaction handed out as an <see cref="IPersistenceStore"/>
/// could be stored past the transaction's own lifetime by a caller that
/// had no way to know it was holding one.
/// </remarks>
public interface IPersistenceTransaction
{
    /// <summary>Reads the text value under <paramref name="key"/>, including this transaction's own uncommitted writes.</summary>
    Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>Writes <paramref name="value"/> under <paramref name="key"/> within this transaction.</summary>
    Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes the record under <paramref name="key"/> within this transaction. Idempotent.</summary>
    Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>Reads the bytes under <paramref name="key"/>, including this transaction's own uncommitted writes.</summary>
    Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>Writes <paramref name="value"/> under <paramref name="key"/> within this transaction.</summary>
    Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every key in <paramref name="collection"/> beginning with
    /// <paramref name="keyPrefix"/> (the empty string matches every key),
    /// in ascending ordinal key order, including this transaction's own
    /// uncommitted writes.
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default);
}
