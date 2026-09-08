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

    /// <summary>The store being wrapped.</summary>
    public IQueryablePersistenceStore Inner { get; } = inner;

    /// <summary>
    /// Arms the gate: the next transaction parks at the end of its body
    /// until <see cref="Release"/> is called.
    /// </summary>
    /// <returns>A task that completes when that transaction has parked.</returns>
    public Task ArmNextTransaction()
    {
        _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _parked.Task;
    }

    /// <summary>Releases a parked transaction so it can commit.</summary>
    public void Release() => _release?.TrySetResult();

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

                if (Interlocked.Exchange(ref _release, null) is not { } release)
                    return;

                Interlocked.Exchange(ref _parked, null)?.TrySetResult();
                await release.Task.ConfigureAwait(false);
            },
            cancellationToken);
    }
}
