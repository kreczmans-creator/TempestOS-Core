using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Requirements;

/// <summary>
/// A hand-written <see cref="IPersistenceStore"/>/<see cref="IQueryablePersistenceStore"/>
/// test double that always fails, used to prove
/// <see cref="Tempest.Core.Requirements.RequirementsService"/> propagates a
/// <see cref="PersistenceStoreUnavailableException"/> unchanged rather than
/// masking it. Implements the queryable surface too (`TD-67`) — since
/// <see cref="Tempest.Core.Requirements.RequirementsService.CreateAsync"/>
/// now opens its one transaction through it — every member failing exactly
/// the same way.
/// </summary>
internal sealed class FailingPersistenceStore : IPersistenceStore, IQueryablePersistenceStore
{
    private static PersistenceStoreUnavailableException MakeException() =>
        new("Simulated persistence failure.", new IOException("Simulated."));

    public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public long CurrentSequence => throw MakeException();

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task ExecuteInTransactionAsync(Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<T> ExecuteInReadTransactionAsync<T>(Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) =>
        throw MakeException();
}
