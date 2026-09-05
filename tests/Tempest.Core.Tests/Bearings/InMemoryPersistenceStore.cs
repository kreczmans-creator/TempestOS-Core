using System.Collections.Concurrent;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Bearings;

/// <summary>
/// A hand-written, in-memory <see cref="IPersistenceStore"/> test double —
/// mirrors <c>Tempest.Core.Tests.Materials.InMemoryPersistenceStore</c>'s
/// own convention, duplicated here rather than shared, per this codebase's
/// own established precedent of small, test-local fakes.
/// </summary>
internal sealed class InMemoryPersistenceStore : IPersistenceStore
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    private static string MakeKey(string collection, string key) => $"{collection} {key}";

    public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.TryGetValue(MakeKey(collection, key), out var value) ? value : null);

    public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
    {
        _values[MakeKey(collection, key)] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        _values.TryRemove(MakeKey(collection, key), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
    {
        var prefix = $"{collection} ";
        IReadOnlyList<string> keys = _values.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k[prefix.Length..])
            .ToList();

        return Task.FromResult(keys);
    }
}
