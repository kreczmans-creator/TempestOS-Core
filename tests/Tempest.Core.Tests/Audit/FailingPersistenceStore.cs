using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Audit;

/// <summary>
/// A hand-written <see cref="IPersistenceStore"/> test double that always
/// fails, used to prove <see cref="Tempest.Core.Audit.AuditRecorder"/>/
/// <see cref="Tempest.Core.Audit.AuditQuery"/> propagate a
/// <see cref="PersistenceStoreUnavailableException"/> unchanged rather
/// than masking it.
/// </summary>
internal sealed class FailingPersistenceStore : IPersistenceStore
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
}
