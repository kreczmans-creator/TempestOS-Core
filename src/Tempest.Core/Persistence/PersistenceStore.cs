using Tempest.Core.Concurrency;
using Tempest.Core.Configuration;
using Tempest.Core.Logging;

namespace Tempest.Core.Persistence;

/// <summary>
/// The concrete <see cref="IPersistenceStore"/> implementation — a
/// simple, file-backed key/value store, one file per
/// <c>collection</c>/<c>key</c> pair.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately minimal, per this namespace's own scope: no schema, no
/// querying beyond key lookup and full-collection key enumeration, no
/// transactions across multiple keys. A <c>collection</c> maps to a
/// subdirectory of the configured root path; a <c>key</c> maps to one
/// file within it. Both are percent-encoded (<see cref="Uri.EscapeDataString(string)"/>)
/// so an arbitrary caller-supplied collection or key name can never
/// produce an invalid or unintended file-system path.
/// </para>
/// <para>
/// The root path is read once from <see cref="IConfigurationProvider"/>
/// at construction (key <see cref="RootPathConfigurationKey"/>),
/// defaulting to <see cref="DefaultRootPath"/> if unconfigured — the
/// same "read once, from Configuration, with a sensible default"
/// convention <c>LoggerFactory</c> already established for
/// <c>Runtime:Logging:MinimumLevel</c>.
/// </para>
/// <para>
/// Every operation acquires a per-<c>collection</c>/<c>key</c>
/// <see cref="AsyncKeyedLock"/> before touching the file system — this is
/// what satisfies this namespace's own Thread Safety Expectations
/// (concurrent writes to the same key never corrupt or interleave;
/// concurrent reads never observe a partially-written file), without
/// serialising access to two different keys against each other.
/// </para>
/// </remarks>
public sealed class PersistenceStore : IPersistenceStore
{
    /// <summary>
    /// The configuration key the storage backend's root path is read
    /// from.
    /// </summary>
    public const string RootPathConfigurationKey = "Persistence:RootPath";

    /// <summary>
    /// The root path used when <see cref="RootPathConfigurationKey"/> is
    /// not configured.
    /// </summary>
    public const string DefaultRootPath = "persistence-data";

    private readonly string _rootPath;
    private readonly ILogger? _logger;
    private readonly AsyncKeyedLock _keyLock = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="PersistenceStore"/> class.
    /// </summary>
    /// <param name="configuration">The configuration the storage root path is read from.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    public PersistenceStore(IConfigurationProvider configuration, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _rootPath = configuration.TryGetValue(RootPathConfigurationKey, out var configuredPath)
            ? configuredPath!
            : DefaultRootPath;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetFilePath(collection, key);

        using (await _keyLock.AcquireAsync(LockKey(collection, key), cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.Warning($"Persistence read failed for collection '{collection}', key '{key}'.", ex);
                throw new PersistenceStoreUnavailableException(
                    $"Failed to read collection '{collection}', key '{key}'.", ex);
            }
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var path = GetFilePath(collection, key);

        using (await _keyLock.AcquireAsync(LockKey(collection, key), cancellationToken).ConfigureAwait(false))
        {
            try
            {
                Directory.CreateDirectory(GetCollectionDirectory(collection));
                await File.WriteAllTextAsync(path, value, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.Warning($"Persistence write failed for collection '{collection}', key '{key}'.", ex);
                throw new PersistenceStoreUnavailableException(
                    $"Failed to write collection '{collection}', key '{key}'.", ex);
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetFilePath(collection, key);

        using (await _keyLock.AcquireAsync(LockKey(collection, key), cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.Warning($"Persistence delete failed for collection '{collection}', key '{key}'.", ex);
                throw new PersistenceStoreUnavailableException(
                    $"Failed to delete collection '{collection}', key '{key}'.", ex);
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);

        cancellationToken.ThrowIfCancellationRequested();

        var directory = GetCollectionDirectory(collection);

        try
        {
            if (!Directory.Exists(directory))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            IReadOnlyList<string> keys = Directory.GetFiles(directory)
                .Select(filePath => Uri.UnescapeDataString(Path.GetFileName(filePath)))
                .ToList();

            return Task.FromResult(keys);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"Persistence list failed for collection '{collection}'.", ex);
            throw new PersistenceStoreUnavailableException($"Failed to list collection '{collection}'.", ex);
        }
    }

    private string GetCollectionDirectory(string collection) =>
        Path.Combine(_rootPath, Uri.EscapeDataString(collection));

    private string GetFilePath(string collection, string key) =>
        Path.Combine(GetCollectionDirectory(collection), Uri.EscapeDataString(key));

    private static string LockKey(string collection, string key) => $"{collection}{key}";
}
