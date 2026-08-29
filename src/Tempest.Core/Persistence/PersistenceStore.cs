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
/// file within it. Both are percent-encoded (<see cref="Uri.EscapeDataString(string)"/>,
/// strengthened by <see cref="EncodeSegment"/> — see below)
/// so an arbitrary caller-supplied collection or key name can never
/// produce an invalid or unintended file-system path.
/// </para>
/// <para>
/// <b>Reserved-name-safe encoding (`TD-59` closure):</b>
/// <see cref="Uri.EscapeDataString(string)"/> alone leaves three classes
/// of name unrepresentable as a cross-platform file name, all of which
/// previously collapsed into silently-missing records on Windows:
/// reserved device stems (<c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>,
/// <c>COM0</c>–<c>COM9</c>, <c>LPT0</c>–<c>LPT9</c>, in any casing, with
/// or without an extension — Win32 routes these to devices, and
/// <c>File.Exists</c> then reports them absent), names ending in a dot
/// (Win32 strips trailing dots, aliasing <c>"Rev1."</c> onto
/// <c>"Rev1"</c>), and the pure directory-navigation names <c>"."</c>/
/// <c>".."</c>. <see cref="EncodeSegment"/> percent-encodes the first
/// character of a reserved device stem and any terminal dot, so every
/// key is unambiguously representable on every platform, and
/// <see cref="Uri.UnescapeDataString(string)"/> remains the exact
/// decoder. Keys that were already safe encode identically to before,
/// so existing stores keep working unchanged; a record persisted under
/// the old encoding of a now-specially-encoded key (possible only on
/// POSIX file systems, where such names were representable) is still
/// found by a legacy-path fallback on read and migrated forward on the
/// next write.
/// </para>
/// <para>
/// <b>Case-insensitive file systems:</b> distinct keys differing only in
/// case (<c>"Foo"</c>/<c>"foo"</c>) map to one file on Windows/macOS
/// default volumes. Rather than silently overwriting one key's record
/// with the other's (data loss), <see cref="WriteAsync"/> refuses the
/// colliding write with <see cref="PersistenceStoreUnavailableException"/>,
/// and <see cref="ReadAsync"/>/<see cref="DeleteAsync"/> match the
/// stored file name exactly, never a case-variant, so a lookup can
/// never return another key's record.
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
/// serialising access to two different keys against each other. The
/// lock key is derived from the encoded, case-folded file identity (not
/// the raw strings), so two keys that target the same physical file on
/// a case-insensitive file system always contend on the same lock.
/// Writes are additionally crash-safe: the value is written to a
/// temporary file in the store root and atomically renamed over the
/// target, so an interrupted write can never leave a torn file where a
/// previous good value used to be.
/// </para>
/// </remarks>
public sealed class PersistenceStore : IPersistenceStore, IBinaryPersistenceStore
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

    /// <summary>
    /// The three-letter reserved Win32 device stems. <c>COM0</c>–<c>COM9</c>
    /// and <c>LPT0</c>–<c>LPT9</c> are matched structurally in
    /// <see cref="IsReservedDeviceStem"/>.
    /// </summary>
    private static readonly string[] ReservedDeviceStems = ["CON", "PRN", "AUX", "NUL"];

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
                var readablePath = ResolveReadablePath(collection, key, path);
                if (readablePath is null)
                    return null;

                return await File.ReadAllTextAsync(readablePath, cancellationToken).ConfigureAwait(false);
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
                if (File.Exists(path) && !ExistsWithExactName(path))
                    throw new PersistenceStoreUnavailableException(
                        $"Cannot write collection '{collection}', key '{key}': a different key already occupies the " +
                        "same file name on this case-insensitive file system, and overwriting it would silently " +
                        "discard that key's record.");

                Directory.CreateDirectory(GetCollectionDirectory(collection));
                await WriteAtomicallyAsync(path, value, cancellationToken).ConfigureAwait(false);

                // Migrate forward: a record persisted under the plain
                // Uri.EscapeDataString encoding of a key that EncodeSegment
                // now encodes differently would otherwise shadow this
                // write on the legacy-fallback read path.
                var legacyPath = GetLegacyFilePath(collection, key);
                if (!string.Equals(legacyPath, path, StringComparison.Ordinal) && ExistsWithExactName(legacyPath))
                    File.Delete(legacyPath);
            }
            catch (PersistenceStoreUnavailableException)
            {
                throw;
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
                if (ExistsWithExactName(path))
                    File.Delete(path);

                var legacyPath = GetLegacyFilePath(collection, key);
                if (!string.Equals(legacyPath, path, StringComparison.Ordinal) && ExistsWithExactName(legacyPath))
                    File.Delete(legacyPath);
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

            // Distinct: a legacy-encoded file and its migrated successor
            // decode to the same key; a listing must never report a key
            // twice.
            IReadOnlyList<string> keys = Directory.GetFiles(directory)
                .Select(filePath => Uri.UnescapeDataString(Path.GetFileName(filePath)))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return Task.FromResult(keys);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"Persistence list failed for collection '{collection}'.", ex);
            throw new PersistenceStoreUnavailableException($"Failed to list collection '{collection}'.", ex);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The byte twin of <see cref="ReadAsync"/>, sharing its exact-name
    /// resolution and legacy-encoding fallback (`TD-59`) and its per-key
    /// lock, so a record's name and concurrency behaviour do not depend on
    /// whether its value happens to be text.
    /// </remarks>
    public async Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetFilePath(collection, key);

        using (await _keyLock.AcquireAsync(LockKey(collection, key), cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var readablePath = ResolveReadablePath(collection, key, path);
                if (readablePath is null)
                    return null;

                return await File.ReadAllBytesAsync(readablePath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.Warning($"Persistence byte read failed for collection '{collection}', key '{key}'.", ex);
                throw new PersistenceStoreUnavailableException(
                    $"Failed to read collection '{collection}', key '{key}'.", ex);
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The byte twin of <see cref="WriteAsync"/>, including the
    /// case-variant collision guard and the forward migration of a
    /// legacy-encoded record, so the two shapes cannot disagree about
    /// which file a key names.
    /// </remarks>
    public async Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetFilePath(collection, key);

        using (await _keyLock.AcquireAsync(LockKey(collection, key), cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (File.Exists(path) && !ExistsWithExactName(path))
                    throw new PersistenceStoreUnavailableException(
                        $"Cannot write collection '{collection}', key '{key}': a different key already occupies the " +
                        "same file name on this case-insensitive file system, and overwriting it would silently " +
                        "discard that key's record.");

                Directory.CreateDirectory(GetCollectionDirectory(collection));
                await WriteAtomicallyAsync(path, value, cancellationToken).ConfigureAwait(false);

                var legacyPath = GetLegacyFilePath(collection, key);
                if (!string.Equals(legacyPath, path, StringComparison.Ordinal) && ExistsWithExactName(legacyPath))
                    File.Delete(legacyPath);
            }
            catch (PersistenceStoreUnavailableException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.Warning($"Persistence byte write failed for collection '{collection}', key '{key}'.", ex);
                throw new PersistenceStoreUnavailableException(
                    $"Failed to write collection '{collection}', key '{key}'.", ex);
            }
        }
    }

    /// <summary>
    /// The byte overload of <see cref="WriteAtomicallyAsync(string, string, CancellationToken)"/>,
    /// with the identical temporary-file-then-rename guarantee.
    /// </summary>
    private async Task WriteAtomicallyAsync(string path, ReadOnlyMemory<byte> value, CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(_rootPath, $"write-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, value, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Writes <paramref name="value"/> to a temporary file in the store
    /// root and atomically renames it over <paramref name="path"/> — an
    /// interrupted write leaves either the previous value or the new
    /// one, never a torn file.
    /// </summary>
    private async Task WriteAtomicallyAsync(string path, string value, CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(_rootPath, $"write-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, value, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Resolves the on-disk path a read of <paramref name="key"/> should
    /// use: the current-encoding <paramref name="path"/> when its file
    /// exists (matched by exact name — a case-variant of a different key
    /// is never read), else the legacy plain-escaped path when that
    /// differs and exists, else <see langword="null"/> (no record).
    /// </summary>
    private string? ResolveReadablePath(string collection, string key, string path)
    {
        if (ExistsWithExactName(path))
            return path;

        var legacyPath = GetLegacyFilePath(collection, key);
        if (!string.Equals(legacyPath, path, StringComparison.Ordinal) && ExistsWithExactName(legacyPath))
            return legacyPath;

        return null;
    }

    /// <summary>
    /// Whether a file exists at <paramref name="path"/> under its exact
    /// (case-sensitive) name — on a case-insensitive file system,
    /// <see cref="File.Exists(string)"/> alone would also match another
    /// key's case-variant file.
    /// </summary>
    private static bool ExistsWithExactName(string path)
    {
        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return false;

        // AttributesToSkip: EnumerationOptions defaults to skipping
        // Hidden/System entries, and on Unix a dot-prefixed file name
        // (e.g. the encodings of "..", or of any key starting with a
        // dot) counts as Hidden — which would misreport a real record
        // as missing.
        return Directory
            .EnumerateFiles(directory, fileName, new EnumerationOptions
            {
                MatchCasing = MatchCasing.CaseSensitive,
                AttributesToSkip = FileAttributes.None,
            })
            .Any();
    }

    /// <summary>
    /// Encodes one caller-supplied name into a file-system-safe path
    /// segment: <see cref="Uri.EscapeDataString(string)"/>, then a
    /// percent-escape of the first character when the name's stem (the
    /// part before the first dot) is a reserved Win32 device name, and
    /// of a terminal dot (Win32 strips trailing dots, aliasing distinct
    /// keys). <see cref="Uri.UnescapeDataString(string)"/> exactly
    /// inverts every case; names needing no special handling encode
    /// identically to the plain escape, keeping existing stores valid.
    /// </summary>
    private static string EncodeSegment(string value)
    {
        var escaped = Uri.EscapeDataString(value);

        var dotIndex = escaped.IndexOf('.');
        var stem = dotIndex < 0 ? escaped : escaped[..dotIndex];
        if (IsReservedDeviceStem(stem))
            escaped = $"%{(int)escaped[0]:X2}{escaped[1..]}";

        if (escaped.EndsWith('.'))
            escaped = $"{escaped[..^1]}%2E";

        return escaped;
    }

    private static bool IsReservedDeviceStem(string stem)
    {
        if (stem.Length == 4
            && char.IsAsciiDigit(stem[3])
            && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
            return true;

        return stem.Length == 3 && ReservedDeviceStems.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }

    private string GetCollectionDirectory(string collection) =>
        Path.Combine(_rootPath, EncodeSegment(collection));

    private string GetFilePath(string collection, string key) =>
        Path.Combine(GetCollectionDirectory(collection), EncodeSegment(key));

    /// <summary>The pre-`TD-59` file path for <paramref name="key"/> — plain <see cref="Uri.EscapeDataString(string)"/>, no reserved-name handling.</summary>
    private string GetLegacyFilePath(string collection, string key) =>
        Path.Combine(GetCollectionDirectory(collection), Uri.EscapeDataString(key));

    /// <summary>
    /// The per-target lock identity: encoded (not raw) segments, joined
    /// with a separator no encoded segment can contain, case-folded so
    /// keys that share one physical file on a case-insensitive file
    /// system always contend on the same lock.
    /// </summary>
    private static string LockKey(string collection, string key) =>
        $"{EncodeSegment(collection)}\n{EncodeSegment(key)}".ToUpperInvariant();
}
