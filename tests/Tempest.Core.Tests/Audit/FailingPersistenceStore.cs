using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Audit;

/// <summary>
/// A hand-written test double that always fails, used to prove
/// <see cref="Tempest.Core.Audit.AuditRecorder"/>/
/// <see cref="Tempest.Core.Audit.AuditQuery"/> propagate a
/// <see cref="PersistenceStoreUnavailableException"/> unchanged rather
/// than masking it.
/// </summary>
/// <remarks>
/// It implements <see cref="IQueryablePersistenceStore"/> as well as
/// <see cref="IPersistenceStore"/> because `WP 17.1B` moved
/// <see cref="Tempest.Core.Audit.AuditQuery"/> onto the query shape (a
/// by-object query is now one key-prefix listing rather than a scan), so
/// the query's failure path is now reached through
/// <see cref="ReadAllAsync"/> and <see cref="ListKeysAsync(string, string, CancellationToken)"/>.
/// The recorder still writes through <see cref="IPersistenceStore"/>, and
/// both halves fail identically here — the point of the double is that
/// <em>every</em> way in throws the store's own unavailability exception.
/// </remarks>
internal sealed class FailingPersistenceStore : IPersistenceStore, IQueryablePersistenceStore
{
    private static PersistenceStoreUnavailableException MakeException() =>
        new("Simulated persistence failure.", new IOException("Simulated."));

    public long CurrentSequence => throw MakeException();

    public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(
        string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        throw MakeException();

    public Task ExecuteInTransactionAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        throw MakeException();
}
