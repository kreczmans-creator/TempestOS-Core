using System.Text.Json;
using Tempest.Companion.Contracts;

namespace Tempest.Companion.Offline;

/// <summary>
/// The Companion's read-only snapshot cache (<c>ADR-0115</c>) — the last
/// successful response per endpoint, stored as one JSON file per key with
/// the moment it was fetched. Deliberately not a database and not a sync
/// queue: the cache only ever holds what the authoritative platform
/// already served, so there is nothing in it to conflict, merge, or push
/// back. A corrupt or unreadable file reads as "no snapshot" — the
/// offline path must never itself crash the app.
/// </summary>
public sealed class SnapshotCache
{
    private readonly string _cacheDirectory;

    /// <summary>
    /// Initialises a new instance of the <see cref="SnapshotCache"/> class.
    /// </summary>
    /// <param name="rootPath">The Companion's own data folder — the cache lives in a <c>cache/</c> subfolder.</param>
    public SnapshotCache(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _cacheDirectory = Path.Combine(rootPath, "cache");
    }

    /// <summary>Stores <paramref name="data"/> as the current snapshot for <paramref name="key"/>, stamped <paramref name="fetchedAtUtc"/>.</summary>
    public void Store<T>(string key, T data, DateTimeOffset fetchedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(data);

        Directory.CreateDirectory(_cacheDirectory);

        var envelope = new SnapshotEnvelope<T>(fetchedAtUtc, data);
        File.WriteAllText(PathFor(key), JsonSerializer.Serialize(envelope, CompanionJson.Options));
    }

    /// <summary>Loads the stored snapshot for <paramref name="key"/>, or <see langword="null"/> when none exists or it cannot be read.</summary>
    public (T Data, DateTimeOffset FetchedAtUtc)? Load<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var path = PathFor(key);
            if (!File.Exists(path))
                return null;

            var envelope = JsonSerializer.Deserialize<SnapshotEnvelope<T>>(File.ReadAllText(path), CompanionJson.Options);
            return envelope is null || envelope.Data is null ? null : (envelope.Data, envelope.FetchedAtUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Deletes every stored snapshot — the logout/device-hygiene path (`WP 14.0A` security review: cached engineering data is removed when the connection is cleared).</summary>
    public void Clear()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
                Directory.Delete(_cacheDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a locked file must not turn hygiene into a crash.
        }
    }

    private string PathFor(string key) => Path.Combine(_cacheDirectory, key + ".json");

    private sealed record SnapshotEnvelope<T>(DateTimeOffset FetchedAtUtc, T Data);
}
