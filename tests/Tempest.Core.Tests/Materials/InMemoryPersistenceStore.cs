using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.Materials;

/// <summary>
/// A hand-written, in-memory <see cref="IPersistenceStore"/> test double —
/// mirrors <c>Tempest.Core.Tests.EngineeringData.InMemoryPersistenceStore</c>'s
/// own convention, duplicated here rather than shared, per this codebase's
/// own established precedent of small, test-local fakes.
/// </summary>
/// <remarks>
/// `TD-158`: <c>ReferenceDataCatalog&lt;TDefinition&gt;</c> now refuses a
/// store that is not also an <see cref="IQueryablePersistenceStore"/> (a
/// clear refusal, never a silent non-transactional fallback), so this
/// double must be one too. Rather than reimplementing real transaction
/// semantics a third time, it composes the suite's own hardened,
/// already-shared double (<see cref="InMemoryQueryablePersistenceStore"/>)
/// for storage and <see cref="CommitFailingPersistenceStore"/> for fault
/// injection, and exposes <see cref="FailNextCommit"/> straight through to
/// it — the same mechanism `TransactionalWriteFaultInjectionTests` already
/// uses for the engineering-object write path (`WP 17.1B`).
/// </remarks>
internal sealed class InMemoryPersistenceStore : IPersistenceStore, IQueryablePersistenceStore
{
    private readonly InMemoryQueryablePersistenceStore _inner = new();
    private readonly CommitFailingPersistenceStore _transactional;

    public InMemoryPersistenceStore()
    {
        _transactional = new CommitFailingPersistenceStore(_inner);
    }

    /// <summary>When set, the next transaction's commit fails after its body completes — `TD-158` fault injection.</summary>
    public bool FailNextCommit
    {
        get => _transactional.FailNextCommit;
        set => _transactional.FailNextCommit = value;
    }

    // ----------------------------------------------------------------
    // IPersistenceStore — straight through to the shared backing store.
    // ----------------------------------------------------------------

    public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(collection, key, cancellationToken);

    public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(collection, key, value, cancellationToken);

    public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(collection, key, cancellationToken);

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
        _inner.ListKeysAsync(collection, cancellationToken);

    // ----------------------------------------------------------------
    // IQueryablePersistenceStore — through the fault-injecting wrapper,
    // so a test can arm FailNextCommit and see the whole transaction body
    // (document write, index write, secondary index write) fail to land.
    // ----------------------------------------------------------------

    public long CurrentSequence => _inner.CurrentSequence;

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
        _transactional.ListKeysAsync(collection, keyPrefix, cancellationToken);

    public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
        _transactional.ReadAllAsync(collection, cancellationToken);

    public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(
        string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        _transactional.ReadManyAsync(collection, keys, cancellationToken);

    public Task ExecuteInTransactionAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        _transactional.ExecuteInTransactionAsync(work, cancellationToken);

    public Task<T> ExecuteInReadTransactionAsync<T>(
        Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default) =>
        _transactional.ExecuteInReadTransactionAsync(read, cancellationToken);

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
        _transactional.SearchAsync(query, limit, cancellationToken);

    public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) =>
        _transactional.IsSearchIndexEmptyAsync(cancellationToken);
}
