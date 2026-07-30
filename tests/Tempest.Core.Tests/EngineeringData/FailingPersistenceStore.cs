using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringData;

/// <summary>
/// A hand-written <see cref="IPersistenceStore"/> test double that always
/// fails, used to prove
/// <see cref="Tempest.Core.EngineeringData.EngineeringDocumentStore"/>
/// propagates a <see cref="PersistenceStoreUnavailableException"/>
/// unchanged rather than masking it.
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
