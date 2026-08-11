using System.Collections.Concurrent;
using Tempest.Core.Persistence;

namespace Tempest.Desktop.Tests;

/// <summary>
/// A hand-written, in-memory <see cref="IPersistenceStore"/> test double —
/// this test assembly's own copy of <c>Tempest.Core.Tests.Settings.InMemoryPersistenceStore</c>'s
/// identical fake (test doubles are not shared across test assemblies in
/// this codebase; each keeps its own). Used to construct a real,
/// standalone <see cref="Tempest.Core.Settings.SettingsProvider"/> for
/// <c>DesktopPanelUiStateTests</c> without needing a full
/// <see cref="WorkspaceHost"/>.
/// </summary>
internal sealed class InMemoryPersistenceStore : IPersistenceStore
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    private static string MakeKey(string collection, string key) => $"{collection}{key}";

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
        var prefix = $"{collection}";
        IReadOnlyList<string> keys = _values.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k[prefix.Length..])
            .ToList();

        return Task.FromResult(keys);
    }
}
