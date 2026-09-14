using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// Wraps an <see cref="IQueryablePersistenceStore"/> and can park one
/// transaction inside its body, so a second writer's behaviour while a
/// first holds the write path is observable without timing (`ADR-0145`).
/// </summary>
/// <remarks>
/// <para>
/// The gate closes at the <em>end</em> of the transaction body — after
/// everything the mutation wanted to write has been staged, before the
/// commit. That is the interleaving worth forcing: the first writer has
/// done all its work and is holding the domain write lock and the
/// database write lock; a second writer arriving now must wait, and must
/// then read the first writer's committed state rather than the state it
/// saw before the first began.
/// </para>
/// <para>
/// This replaces the per-object state-store gates the suite used to carry
/// (<c>GatedObjectStateStore</c> and its in-memory twins). Those parked a
/// single writer among four; there is one writer now, and one place to
/// park it.
/// </para>
/// </remarks>
public sealed class GatedPersistenceStore(IQueryablePersistenceStore inner) : IQueryablePersistenceStore
{
    private TaskCompletionSource? _release;
    private TaskCompletionSource? _parked;
    private int _armed;

    /// <summary>The store being wrapped.</summary>
    public IQueryablePersistenceStore Inner { get; } = inner;

    /// <summary>
    /// Arms the gate: the next transaction parks at the end of its body
    /// until <see cref="Release"/> is called.
    /// </summary>
    /// <returns>A task that completes when that transaction has parked.</returns>
    public Task ArmNextTransaction()
    {
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _release, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        Volatile.Write(ref _parked, parked);
        Volatile.Write(ref _armed, 1);
        return parked.Task;
    }

    /// <summary>Releases a parked transaction so it can commit.</summary>
    /// <remarks>
    /// Reads the completion source rather than taking it, because the
    /// parked transaction is still awaiting it. Only the <c>_armed</c>
    /// flag is claimed, and only by the transaction that parks — so a
    /// second transaction is not gated, and <see cref="Release"/> can be
    /// called before or after the park without racing.
    /// </remarks>
    public void Release() => Volatile.Read(ref _release)?.TrySetResult();

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
        Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        return Inner.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await work(transaction, token).ConfigureAwait(false);

                // Claim the arming, so only the next transaction parks.
                if (Interlocked.Exchange(ref _armed, 0) == 0)
                    return;

                var release = Volatile.Read(ref _release)!;
                Volatile.Read(ref _parked)?.TrySetResult();
                await release.Task.ConfigureAwait(false);
            },
            cancellationToken);
    }

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
