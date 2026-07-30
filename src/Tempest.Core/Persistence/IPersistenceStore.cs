namespace Tempest.Core.Persistence;

/// <summary>
/// A minimal, internal, platform-owned durable store. Not a general-
/// purpose database abstraction — scoped narrowly to what platform
/// services (Settings, Audit) need to remember between process runs.
/// </summary>
public interface IPersistenceStore
{
    /// <summary>
    /// Reads the value stored under <paramref name="key"/> within
    /// <paramref name="collection"/>, or <see langword="null"/> if none
    /// exists.
    /// </summary>
    /// <param name="collection">A logical grouping, owned by exactly one calling service.</param>
    /// <param name="key">The item's key, unique within <paramref name="collection"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="PersistenceException">The underlying store could not be read.</exception>
    Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="value"/> under <paramref name="key"/>
    /// within <paramref name="collection"/>, creating or overwriting as
    /// needed.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/>, <paramref name="key"/>, or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="PersistenceException">The underlying store could not be written.</exception>
    Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the item stored under <paramref name="key"/> within
    /// <paramref name="collection"/>, if any. Never throws for a
    /// missing key — deletion is idempotent.
    /// </summary>
    Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates every key currently stored within
    /// <paramref name="collection"/>. Never <see langword="null"/>;
    /// empty if the collection has no entries.
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default);
}
