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
/// <para>
/// <b>All-or-nothing reporting (`TD-143`, `WP 16.4B-R7`).</b> A write
/// that throws has not changed the stored value, and a write that
/// returns has. That is a stronger statement than crash-safety and it
/// is what makes an in-memory rollback by a caller
/// (<c>EngineeringObjectBase</c>'s mutators) correct rather than a
/// guess: before `WP 16.4B-R7` two steps ran <em>after</em>
/// <c>File.Move</c> had already committed the new value — the
/// temporary-file cleanup and the forward migration of a legacy-encoded
/// record — and either could raise, so
/// <see cref="PersistenceStoreUnavailableException"/> was thrown on both
/// sides of the commit point with nothing to tell them apart. Both are
/// now best-effort and logged: they cannot turn a committed write into a
/// reported failure. Neither is load-bearing for correctness — the
/// stale temporary file is unreferenced by any key, and a surviving
/// legacy file is inert <b>for as long as the current-encoding record
/// exists</b>, because <see cref="ResolveReadablePath"/> prefers that
/// record whenever it is present and <see cref="ListKeysAsync"/>
/// de-duplicates the pair — and the next successful write of the same key
/// retries both. The qualifier is real and is spelled out on
/// <see cref="MigrateLegacyRecordAfterCommit"/>: <see cref="DeleteAsync"/>
/// removes the current record first, so a delete that then fails on the
/// legacy record makes the stale value live again. That is a defect of
/// this store's delete ordering, pre-existing and out of `TD-143`'s
/// scope, and it is recorded rather than quietly fixed here.
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

                // Everything above this line is pre-commit: it can throw and
                // the stored value is unchanged. `WriteAtomicallyAsync`
                // commits at its `File.Move`; nothing after it may fail the
                // write. See `MigrateLegacyRecordAfterCommit`.
                await WriteAtomicallyAsync(path, value, cancellationToken).ConfigureAwait(false);

                MigrateLegacyRecordAfterCommit(collection, key, path);
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
    /// case-variant collision guard, the forward migration of a
    /// legacy-encoded record and the same commit boundary (`TD-143`), so
    /// the two shapes cannot disagree about which file a key names or
    /// about when a write has landed.
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

                // Pre-commit above, commit inside, best-effort after — the
                // identical boundary as the text overload (`TD-143`).
                await WriteAtomicallyAsync(path, value, cancellationToken).ConfigureAwait(false);

                MigrateLegacyRecordAfterCommit(collection, key, path);
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

            // THE COMMIT POINT.
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            DiscardTemporaryFile(temporaryPath);
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

            // THE COMMIT POINT. `File.Move` with `overwrite: true` is a
            // rename within `_rootPath`, so it either replaces the target
            // wholly or leaves it wholly untouched. Every statement before
            // it can fail without changing the stored value; no statement
            // after it is allowed to fail the write (`TD-143`).
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            DiscardTemporaryFile(temporaryPath);
        }
    }

    /// <summary>
    /// Removes the temporary file <see cref="WriteAtomicallyAsync(string, string, CancellationToken)"/>
    /// staged the value in, without ever letting that removal decide the
    /// outcome of the write (`TD-143`, `WP 16.4B-R7`).
    /// </summary>
    /// <remarks>
    /// This runs in a <c>finally</c>, so on the success path it runs
    /// <em>after</em> the commit — where a throw would report a landed
    /// write as failed, which is the defect `TD-143` exists for — and on
    /// the failure path it would replace the real cause of the failure
    /// with a cleanup fault. Neither is wanted, and in both cases what is
    /// left behind is one unreferenced <c>write-*.tmp</c> file that no key
    /// resolves to. The exception is logged rather than silently
    /// discarded: nothing here is being hidden, it is being denied the
    /// power to fail somebody else's operation.
    /// </remarks>
    private void DiscardTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"Persistence could not remove its temporary file '{temporaryPath}'.", ex);
        }
    }

    /// <summary>
    /// Removes the superseded legacy-encoded record for
    /// <paramref name="key"/> after the current-encoding record has
    /// already been committed — best-effort, and never able to fail the
    /// write it follows (`TD-143`, `WP 16.4B-R7`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this may not throw.</b> It runs after
    /// <see cref="WriteAtomicallyAsync(string, string, CancellationToken)"/>
    /// has committed, so a caller told the write failed here would have
    /// been told a lie about durable state it cannot check — and
    /// <see cref="PersistenceStoreUnavailableException"/> is the same type
    /// this method's pre-commit siblings raise, so no caller could tell the
    /// two apart. <c>EngineeringObjectBase</c> rolls a failed mutation back
    /// in memory on the strength of "threw, therefore did not land"; this
    /// is one of the two places that used to make that false.
    /// </para>
    /// <para>
    /// <b>Why leaving the file is safe for this write.</b> The legacy file
    /// is only ever consulted when the current-encoding file is absent
    /// (<see cref="ResolveReadablePath"/>), which it no longer is, so it
    /// cannot shadow this write; <see cref="ListKeysAsync"/> already
    /// de-duplicates the pair because both names decode to the same key;
    /// nothing else in this platform enumerates a collection directory. It
    /// is removed by the next successful write of the same key.
    /// </para>
    /// <para>
    /// <b>QUALIFIER, and it is not a footnote (`WP 16.4B-R7`, round 2,
    /// `B-F3`). The surviving legacy file is inert only FOR AS LONG AS THE
    /// CURRENT-ENCODING RECORD EXISTS.</b> <see cref="DeleteAsync"/>
    /// removes the current-encoding file <em>first</em> and the legacy file
    /// second, so a delete that succeeds on the first removal and fails on
    /// the second leaves the stale legacy value as the <em>live</em> record
    /// for that key — a value the caller believed overwritten, readable
    /// again. That ordering is pre-existing, is not reached by any
    /// `TD-143` path (no mutator on <c>EngineeringObjectBase</c> calls
    /// <see cref="IEngineeringObjectStateStore.DeleteAsync"/>, and nothing
    /// in <c>src/</c> does), and is deliberately NOT changed here: it is a
    /// defect of <see cref="DeleteAsync"/>'s own removal order, it wants
    /// its own register row and its own board, and reordering a shipped
    /// store's delete semantics is not in `TD-143`'s scope.
    /// <b>What `WP 16.4B-R7` does change is the signal.</b> Before it, a
    /// legacy file that could not be removed made <em>every</em> write of
    /// that key fail loudly; now it produces the warning below and nothing
    /// else. That trade is deliberate — failing a committed write is the
    /// `TD-143` defect at this layer and could not be kept — but it means
    /// the condition under which the paragraph above stops holding no
    /// longer announces itself, which is why the warning names the
    /// consequence rather than merely reporting the failure.
    /// </para>
    /// </remarks>
    private void MigrateLegacyRecordAfterCommit(string collection, string key, string path)
    {
        try
        {
            var legacyPath = GetLegacyFilePath(collection, key);
            if (!string.Equals(legacyPath, path, StringComparison.Ordinal) && ExistsWithExactName(legacyPath))
                File.Delete(legacyPath);
        }
        catch (Exception ex)
        {
            _logger?.Warning(
                $"Persistence committed the write for collection '{collection}', key '{key}', but could not remove " +
                "the superseded legacy-encoded record. The write stands and reads of this key are unaffected while " +
                "the current-encoding record exists. It will be retried by the next successful write of this key. " +
                "Until it is removed, a delete of this key that fails part-way would leave the stale legacy value " +
                "readable as the live record.", ex);
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
