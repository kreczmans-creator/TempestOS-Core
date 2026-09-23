using Tempest.Core.Secrets;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// A plain in-memory <see cref="ISecretStore"/> for a connector contract
/// test that is not itself testing <see cref="OAuthAuthoriser"/>'s own
/// storage discipline (`OAuthAuthoriserTests` and `SecretStoreTests` cover
/// that against the real, disk-backed stores) — lets a test seed a
/// connector as already-authorised without a real file system or DPAPI.
/// </summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        _values[key] = value;

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _values.Remove(key);

        return Task.CompletedTask;
    }
}
