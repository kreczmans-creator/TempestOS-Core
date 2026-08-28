namespace Tempest.Companion.Offline;

/// <summary>
/// What one refresh produced: the data (live or cached), how fresh it is,
/// when it was fetched, and — when the platform was unreachable or
/// refused the request — the user-presentable reason. Every Companion
/// screen renders from exactly this shape, so loading/offline/stale/error
/// presentation is uniform by construction.
/// </summary>
/// <typeparam name="T">The wire DTO this result carries.</typeparam>
/// <param name="Data">The data, or <see langword="null"/> when <see cref="Freshness"/> is <see cref="DataFreshness.Unavailable"/>.</param>
/// <param name="Freshness">How current <see cref="Data"/> is.</param>
/// <param name="FetchedAtUtc">When <see cref="Data"/> was fetched from the platform, or <see langword="null"/> when unavailable.</param>
/// <param name="Error">The failure that prevented a live fetch, or <see langword="null"/> when <see cref="Freshness"/> is <see cref="DataFreshness.Live"/>.</param>
public sealed record SnapshotResult<T>(T? Data, DataFreshness Freshness, DateTimeOffset? FetchedAtUtc, string? Error)
    where T : class
{
    /// <summary>Creates a live result.</summary>
    public static SnapshotResult<T> Live(T data, DateTimeOffset fetchedAtUtc) => new(data, DataFreshness.Live, fetchedAtUtc, null);

    /// <summary>Creates a cached/stale fallback result.</summary>
    public static SnapshotResult<T> FromCache(T data, DateTimeOffset fetchedAtUtc, bool stale, string error) =>
        new(data, stale ? DataFreshness.Stale : DataFreshness.Cached, fetchedAtUtc, error);

    /// <summary>Creates an unavailable result — nothing live, nothing cached.</summary>
    public static SnapshotResult<T> Unavailable(string error) => new(null, DataFreshness.Unavailable, null, error);
}
