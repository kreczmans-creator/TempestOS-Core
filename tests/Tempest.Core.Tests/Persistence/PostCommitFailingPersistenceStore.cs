using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// Wraps a real <see cref="SqlitePersistenceStore"/> and makes every
/// transaction's connection-close step throw <em>after</em> its
/// <c>COMMIT;</c> has already returned, so a test can drive the domain
/// through a write that lands durably and a close that does not, and see
/// it behave exactly as a fully clean run (`TD-150`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this cannot be a plain decorator over
/// <see cref="IQueryablePersistenceStore"/>, the way
/// <see cref="CommitFailingPersistenceStore"/> is.</b> That double fails a
/// transaction by making its own wrapped <paramref name="work"/> delegate
/// throw, which runs <em>before</em> <c>COMMIT;</c> — exactly the window
/// `TD-147` closed, and the opposite of the window this one proves. The
/// window `TD-150` closes opens only <em>after</em> a real
/// <c>COMMIT;</c> has returned, inside
/// <see cref="SqlitePersistenceStore.ExecuteInTransactionAsync"/>'s own
/// connection-close step — a detail no interface exposes and no decorator
/// over it can reach. This double therefore wraps the concrete store
/// directly and drives its internal
/// <see cref="SqlitePersistenceStore.TestOnlyConnectionCloser"/> seam
/// (reachable only from this assembly, <c>InternalsVisibleTo</c>), rather
/// than adding a second, production-facing hook: the real store already
/// had no other seam a test could use to make its own dispose step fail
/// without weakening something a real caller depends on.
/// </para>
/// <para>
/// Every non-transactional member delegates unchanged, so a test can read
/// the wrapped store directly (it is <see cref="Inner"/>) and see what
/// really committed — same shape as <see cref="CommitFailingPersistenceStore"/>.
/// </para>
/// </remarks>
public sealed class PostCommitFailingPersistenceStore : IQueryablePersistenceStore
{
    /// <summary>The real store being wrapped, for reading what actually landed.</summary>
    public SqlitePersistenceStore Inner { get; }

    public PostCommitFailingPersistenceStore(SqlitePersistenceStore inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        Inner = inner;
        Inner.TestOnlyConnectionCloser = async connection =>
        {
            // Real cleanup first, so this double never leaks a pooled
            // connection or a native handle across tests — only then does
            // it throw, simulating a close that reports failure after
            // actually completing.
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new IOException(
                "Injected post-commit close failure: the connection closed, but reporting that failed.");
        };
    }

    /// <inheritdoc />
    public long CurrentSequence => Inner.CurrentSequence;

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
        Inner.ListKeysAsync(collection, keyPrefix, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
        Inner.ReadAllAsync(collection, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(
        string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        Inner.ReadManyAsync(collection, keys, cancellationToken);

    /// <inheritdoc />
    public Task ExecuteInTransactionAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        Inner.ExecuteInTransactionAsync(work, cancellationToken);

    /// <inheritdoc />
    public Task<T> ExecuteInReadTransactionAsync<T>(
        Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default) =>
        Inner.ExecuteInReadTransactionAsync(read, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
        Inner.SearchAsync(query, limit, cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) =>
        Inner.IsSearchIndexEmptyAsync(cancellationToken);
}
