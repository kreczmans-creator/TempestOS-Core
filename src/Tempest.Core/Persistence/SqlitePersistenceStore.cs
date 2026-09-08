using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Tempest.Core.Configuration;
using Tempest.Core.Logging;

namespace Tempest.Core.Persistence;

/// <summary>
/// The platform's durable store (`ADR-0144`): one SQLite database file,
/// <c>tempest.db</c>, under the configured persistence root, in WAL
/// journal mode with <c>synchronous=FULL</c>.
/// </summary>
/// <remarks>
/// <para>
/// Satisfies all three store shapes — <see cref="IPersistenceStore"/>,
/// <see cref="IBinaryPersistenceStore"/> and
/// <see cref="IQueryablePersistenceStore"/> — from one instance over one
/// file, exactly as <see cref="PersistenceStore"/> satisfied the first two
/// from one directory tree.
/// </para>
/// <para>
/// <b>Schema (version 1).</b>
/// </para>
/// <code>
/// records(collection TEXT NOT NULL,
///         key        TEXT NOT NULL,
///         text_value TEXT NULL,
///         blob_value BLOB NULL,
///         updated_utc TEXT NOT NULL,
///         PRIMARY KEY(collection, key))
/// schema_info(version INTEGER NOT NULL)
/// </code>
/// <para>
/// One table carries both value shapes, because a record's name, its
/// concurrency behaviour and its transaction membership do not depend on
/// whether its value happens to be text — the same reason
/// <see cref="IBinaryPersistenceStore"/> was implemented by the file store
/// rather than by a second store. The shapes stay separate at the
/// <em>value</em>: a write through one clears the other's column, so a key
/// written as text reads <see langword="null"/> as bytes and a key written
/// as bytes reads <see langword="null"/> as text, precisely as before. The
/// composite primary key is the collection index; a prefix listing or a
/// whole-collection read is one indexed seek, not the directory scan
/// `TD-12` recorded.
/// </para>
/// <para>
/// <b>Names are exact.</b> Collections and keys are stored verbatim as
/// SQLite <c>TEXT</c> under the default <c>BINARY</c> collation, so any
/// Unicode string is a legal name and two names differing only in case are
/// two records. The reserved-device-name encoding, the trailing-dot
/// encoding, the legacy-path fallback and the case-insensitive-collision
/// refusal that <see cref="PersistenceStore"/> needed all existed to make
/// a caller's key survive a file system; nothing here is a file name, so
/// none of them exists. <c>CON</c>, <c>..</c>, <c>Rev1.</c> and
/// <c>Steel</c>/<c>steel</c> are now simply four ordinary, distinct keys.
/// </para>
/// <para>
/// <b>Durability.</b> Every connection sets <c>journal_mode=WAL</c>,
/// <c>synchronous=FULL</c>, <c>foreign_keys=ON</c> and
/// <c>busy_timeout=5000</c>. <c>synchronous=FULL</c> in WAL mode means
/// each commit is fsynced before it is reported as committed, which is the
/// property `TD-137` found the file-per-key store lacked and the reason a
/// write that returns has landed.
/// </para>
/// <para>
/// <b>Concurrency.</b> One pooled connection per operation, so one store
/// instance is safe to use from any number of threads. Serialisation
/// between concurrent writers is SQLite's own single-writer lock, waited
/// on for up to the busy timeout — a write to a key is one statement and
/// therefore atomic, so concurrent writes to one key cannot interleave and
/// a reader can never observe half of one.
/// </para>
/// <para>
/// <b>Cross-process safety.</b> The store holds
/// <c>&lt;root&gt;/tempest.lock</c> open with
/// <see cref="FileShare.None"/> for its whole lifetime. A second TempestOS
/// instance pointed at the same root is refused at construction with
/// <see cref="PersistenceStoreUnavailableException"/> naming the root,
/// rather than being allowed to share a database whose in-memory caches it
/// cannot coordinate with.
/// </para>
/// <para>
/// <b>Disposal.</b> <see cref="DisposeAsync"/> releases the lock file and
/// calls <see cref="SqliteConnection.ClearAllPools"/>, so no pooled
/// connection is left holding <c>tempest.db</c> and the root directory can
/// be deleted — which is what a test's temporary root, and a user's
/// "delete <c>persistence-data</c> to reset", both need.
/// </para>
/// </remarks>
public sealed class SqlitePersistenceStore
    : IPersistenceStore, IBinaryPersistenceStore, IQueryablePersistenceStore, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The configuration key selecting which persistence backend the Host
    /// registers.
    /// </summary>
    public const string BackendConfigurationKey = "Persistence:Backend";

    /// <summary>The <see cref="BackendConfigurationKey"/> value selecting this store. The default.</summary>
    public const string SqliteBackendValue = "sqlite";

    /// <summary>
    /// The <see cref="BackendConfigurationKey"/> value selecting the
    /// file-per-key <see cref="PersistenceStore"/>. Retained for exactly
    /// one release; removed in <c>v0.18.0</c> (`ADR-0144`).
    /// </summary>
    public const string FileBackendValue = "files";

    /// <summary>The database file's name within the persistence root.</summary>
    public const string DatabaseFileName = "tempest.db";

    /// <summary>The advisory cross-process lock file's name within the persistence root.</summary>
    public const string LockFileName = "tempest.lock";

    /// <summary>The schema version this build reads and writes.</summary>
    public const int SchemaVersion = 1;

    /// <summary>
    /// The maximum number of keys bound into one <c>IN (...)</c> clause by
    /// <see cref="ReadManyAsync"/>. SQLite's own default parameter limit is
    /// 999; this stays well inside it and keeps a large request to a
    /// handful of round trips rather than one per key.
    /// </summary>
    private const int ReadManyBatchSize = 500;

    private readonly string _rootPath;
    private readonly string _databasePath;
    private readonly string _lockFilePath;
    private readonly string _connectionString;
    private readonly ILogger? _logger;
    private readonly FileStream _lockFile;

    private bool _disposed;

    /// <summary>
    /// Initialises a new instance of the <see cref="SqlitePersistenceStore"/>
    /// class: resolves the root, creates it, takes the cross-process lock,
    /// and opens (creating if absent) <c>tempest.db</c> at
    /// <see cref="SchemaVersion"/>.
    /// </summary>
    /// <param name="configuration">The configuration the storage root path is read from.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="PersistenceStoreUnavailableException">
    /// The root could not be created, the lock is already held by another
    /// instance, or the database could not be opened or initialised.
    /// </exception>
    /// <summary>
    /// Binds the native SQLite provider once, before any connection is
    /// opened. Microsoft.Data.Sqlite otherwise binds it lazily on first
    /// use, and two stores opening on two threads at the same instant — a
    /// parallel test run is exactly that — have been seen to race that
    /// first-use initialisation. <c>Batteries_V2.Init</c> is idempotent.
    /// </summary>
    static SqlitePersistenceStore()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public SqlitePersistenceStore(IConfigurationProvider configuration, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _rootPath = configuration.TryGetValue(PersistenceStore.RootPathConfigurationKey, out var configuredPath)
            && !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : PersistenceStore.DefaultRootPath;

        _databasePath = Path.Combine(_rootPath, DatabaseFileName);
        _lockFilePath = Path.Combine(_rootPath, LockFileName);
        _logger = logger;

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = true,
        }.ToString();

        try
        {
            Directory.CreateDirectory(_rootPath);
        }
        catch (Exception ex)
        {
            throw new PersistenceStoreUnavailableException(
                $"Failed to create the persistence root '{_rootPath}'.", ex);
        }

        _lockFile = AcquireInstanceLock();

        try
        {
            InitialiseSchema();
        }
        catch
        {
            _lockFile.Dispose();
            TryDeleteLockFile();
            throw;
        }
    }

    /// <summary>The root directory this store's database lives in, exactly as resolved at construction.</summary>
    public string RootPath => _rootPath;

    /// <summary>The full path of the database file this store reads and writes.</summary>
    public string DatabasePath => _databasePath;

    // ----------------------------------------------------------------
    // IPersistenceStore
    // ----------------------------------------------------------------

    /// <inheritdoc />
    public async Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfDisposed();

        return await ExecuteAsync(
            $"read collection '{collection}', key '{key}'",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT text_value FROM records WHERE collection = $collection AND key = $key;";
                command.Parameters.AddWithValue("$collection", collection);
                command.Parameters.AddWithValue("$key", key);

                var value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return value is null or DBNull ? null : (string)value;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ThrowIfDisposed();

        await ExecuteAsync(
            $"write collection '{collection}', key '{key}'",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                PrepareTextUpsert(command, collection, key, value);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return 0;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfDisposed();

        await ExecuteAsync(
            $"delete collection '{collection}', key '{key}'",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                PrepareDelete(command, collection, key);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return 0;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
        ListKeysAsync(collection, string.Empty, cancellationToken);

    // ----------------------------------------------------------------
    // IBinaryPersistenceStore
    // ----------------------------------------------------------------

    /// <inheritdoc />
    public async Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfDisposed();

        return await ExecuteAsync(
            $"read bytes for collection '{collection}', key '{key}'",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT blob_value FROM records WHERE collection = $collection AND key = $key;";
                command.Parameters.AddWithValue("$collection", collection);
                command.Parameters.AddWithValue("$key", key);

                await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token).ConfigureAwait(false);
                if (!await reader.ReadAsync(token).ConfigureAwait(false) || await reader.IsDBNullAsync(0, token).ConfigureAwait(false))
                    return null;

                // GetFieldValue<byte[]> rather than GetBytes: a zero-length
                // BLOB is a real, stored value distinct from no record, and
                // must come back as an empty array rather than as null.
                return reader.GetFieldValue<byte[]>(0);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfDisposed();

        var bytes = value.ToArray();

        await ExecuteAsync(
            $"write bytes for collection '{collection}', key '{key}'",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                PrepareBlobUpsert(command, collection, key, bytes);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return 0;
            },
            cancellationToken).ConfigureAwait(false);
    }

    // ----------------------------------------------------------------
    // IQueryablePersistenceStore
    // ----------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(keyPrefix);
        ThrowIfDisposed();

        return await ExecuteAsync(
            $"list collection '{collection}'",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                PrepareListKeys(command, collection, keyPrefix);
                return await ReadKeysAsync(command, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ThrowIfDisposed();

        return await ExecuteAsync(
            $"read all of collection '{collection}'",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT key, text_value FROM records " +
                    "WHERE collection = $collection AND text_value IS NOT NULL ORDER BY key;";
                command.Parameters.AddWithValue("$collection", collection);

                var results = new List<KeyValuePair<string, string>>();
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                    results.Add(new KeyValuePair<string, string>(reader.GetString(0), reader.GetString(1)));

                return (IReadOnlyList<KeyValuePair<string, string>>)results;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(
        string collection,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(keys);
        ThrowIfDisposed();

        var requested = keys.Distinct(StringComparer.Ordinal).ToList();
        var results = new Dictionary<string, string?>(requested.Count, StringComparer.Ordinal);
        foreach (var key in requested)
            results[key] = null;

        if (requested.Count == 0)
            return results;

        return await ExecuteAsync(
            $"read {requested.Count} key(s) of collection '{collection}'",
            async (connection, token) =>
            {
                for (var offset = 0; offset < requested.Count; offset += ReadManyBatchSize)
                {
                    var batch = requested.Skip(offset).Take(ReadManyBatchSize).ToList();

                    await using var command = connection.CreateCommand();
                    var parameterNames = new string[batch.Count];
                    for (var i = 0; i < batch.Count; i++)
                    {
                        parameterNames[i] = $"$k{i}";
                        command.Parameters.AddWithValue(parameterNames[i], batch[i]);
                    }

                    command.CommandText =
                        "SELECT key, text_value FROM records " +
                        $"WHERE collection = $collection AND key IN ({string.Join(", ", parameterNames)});";
                    command.Parameters.AddWithValue("$collection", collection);

                    await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        results[reader.GetString(0)] =
                            await reader.IsDBNullAsync(1, token).ConfigureAwait(false) ? null : reader.GetString(1);
                    }
                }

                return (IReadOnlyDictionary<string, string?>)results;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ThrowIfDisposed();

        SqliteConnection connection;
        try
        {
            connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not PersistenceStoreUnavailableException)
        {
            throw new PersistenceStoreUnavailableException(
                $"Failed to open '{_databasePath}' to begin a transaction.", ex);
        }

        await using (connection.ConfigureAwait(false))
        {
            // BEGIN IMMEDIATE, not the default deferred BEGIN: the write
            // lock is taken up front, so a transaction that is going to
            // contend fails at its start (bounded by busy_timeout) rather
            // than half way through, after its caller has already been
            // told several writes succeeded.
            await ExecuteNonQueryAsync(connection, "BEGIN IMMEDIATE;", cancellationToken).ConfigureAwait(false);

            var transaction = new SqliteTransactionScope(connection);
            try
            {
                await work(transaction, cancellationToken).ConfigureAwait(false);
                await ExecuteNonQueryAsync(connection, "COMMIT;", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                transaction.Invalidate();
                await RollBackQuietlyAsync(connection).ConfigureAwait(false);
                throw;
            }
            finally
            {
                transaction.Invalidate();
            }
        }
    }

    // ----------------------------------------------------------------
    // Lifetime
    // ----------------------------------------------------------------

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Pools first: an idle pooled connection still holds the database
        // file open, and every caller of this method is about to want the
        // root directory to be deletable.
        SqliteConnection.ClearAllPools();

        _lockFile.Dispose();
        TryDeleteLockFile();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // ----------------------------------------------------------------
    // Internals
    // ----------------------------------------------------------------

    private FileStream AcquireInstanceLock()
    {
        try
        {
            return new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PersistenceStoreUnavailableException(
                $"The persistence root '{_rootPath}' is already in use: another TempestOS instance holds its " +
                $"instance lock '{_lockFilePath}'. Two instances must not share one database file, because " +
                "each keeps in-memory indexes over it that the other cannot invalidate. Close the other " +
                $"instance, or point this one at a different root with '{PersistenceStore.RootPathConfigurationKey}'.",
                ex);
        }
    }

    private void TryDeleteLockFile()
    {
        try
        {
            if (File.Exists(_lockFilePath))
                File.Delete(_lockFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort. The lock is the open handle, not the directory
            // entry; leaving the entry behind costs a stale empty file and
            // nothing else, and failing disposal over disk hygiene would be
            // the worse trade.
            _logger?.Warning($"Persistence could not remove its instance lock file '{_lockFilePath}'.", ex);
        }
    }

    private void InitialiseSchema()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            ApplyPragmas(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS records (
                    collection  TEXT NOT NULL,
                    key         TEXT NOT NULL,
                    text_value  TEXT NULL,
                    blob_value  BLOB NULL,
                    updated_utc TEXT NOT NULL,
                    PRIMARY KEY (collection, key)
                );

                CREATE TABLE IF NOT EXISTS schema_info (
                    version INTEGER NOT NULL
                );

                INSERT INTO schema_info (version)
                SELECT $version WHERE NOT EXISTS (SELECT 1 FROM schema_info);
                """;
            command.Parameters.AddWithValue("$version", SchemaVersion);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            throw new PersistenceStoreUnavailableException(
                $"Failed to open or initialise the persistence database '{_databasePath}'.", ex);
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            ApplyPragmas(connection);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Applies this store's four pragmas to <paramref name="connection"/>.
    /// Run on every connection, not once per database: <c>busy_timeout</c>,
    /// <c>synchronous</c> and <c>foreign_keys</c> are per-connection
    /// settings, and a connection handed back by the pool may not be the
    /// one that last set them.
    /// </summary>
    /// <remarks>
    /// <b><c>busy_timeout</c> is set first, and the order is load-bearing.</b>
    /// SQLite's default busy timeout is zero, so any statement issued
    /// before it — including <c>PRAGMA journal_mode</c>, which touches the
    /// database header — fails outright with <c>SQLITE_BUSY</c> the
    /// instant another connection holds a lock, instead of waiting the
    /// five seconds this store is configured to wait. Setting the timeout
    /// after the other three left a race that surfaced under concurrent
    /// opens exactly once during this Work Package's own test runs, which
    /// is once more than a persistence layer gets.
    /// </remarks>
    private static void ApplyPragmas(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "PRAGMA busy_timeout = 5000; " +
            "PRAGMA journal_mode = WAL; " +
            "PRAGMA synchronous = FULL; " +
            "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
    }

    private async Task<T> ExecuteAsync<T>(
        string description,
        Func<SqliteConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (connection.ConfigureAwait(false))
                return await operation(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not PersistenceStoreUnavailableException)
        {
            _logger?.Warning($"Persistence could not {description}.", ex);
            throw new PersistenceStoreUnavailableException($"Failed to {description}.", ex);
        }
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RollBackQuietlyAsync(SqliteConnection connection)
    {
        try
        {
            // CancellationToken.None deliberately: a rollback triggered by a
            // cancelled unit of work must still run, or the connection goes
            // back to the pool inside an open transaction.
            await ExecuteNonQueryAsync(connection, "ROLLBACK;", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.Warning("Persistence could not roll back a failed transaction.", ex);
        }
    }

    private static string UtcNowStamp() =>
        DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    private static void PrepareTextUpsert(SqliteCommand command, string collection, string key, string value)
    {
        command.CommandText =
            """
            INSERT INTO records (collection, key, text_value, blob_value, updated_utc)
            VALUES ($collection, $key, $value, NULL, $updated)
            ON CONFLICT (collection, key) DO UPDATE SET
                text_value = excluded.text_value,
                blob_value = NULL,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$collection", collection);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updated", UtcNowStamp());
    }

    private static void PrepareBlobUpsert(SqliteCommand command, string collection, string key, byte[] value)
    {
        command.CommandText =
            """
            INSERT INTO records (collection, key, text_value, blob_value, updated_utc)
            VALUES ($collection, $key, NULL, $value, $updated)
            ON CONFLICT (collection, key) DO UPDATE SET
                text_value = NULL,
                blob_value = excluded.blob_value,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$collection", collection);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updated", UtcNowStamp());
    }

    private static void PrepareDelete(SqliteCommand command, string collection, string key)
    {
        command.CommandText = "DELETE FROM records WHERE collection = $collection AND key = $key;";
        command.Parameters.AddWithValue("$collection", collection);
        command.Parameters.AddWithValue("$key", key);
    }

    /// <summary>
    /// Builds the prefix listing. <c>substr</c>, not <c>LIKE</c>: SQLite's
    /// <c>LIKE</c> is case-insensitive for ASCII by default and treats
    /// <c>%</c> and <c>_</c> in the caller's prefix as wildcards, both of
    /// which would silently return another key's record.
    /// </summary>
    private static void PrepareListKeys(SqliteCommand command, string collection, string keyPrefix)
    {
        if (keyPrefix.Length == 0)
        {
            command.CommandText = "SELECT key FROM records WHERE collection = $collection ORDER BY key;";
            command.Parameters.AddWithValue("$collection", collection);
            return;
        }

        command.CommandText =
            "SELECT key FROM records " +
            "WHERE collection = $collection AND substr(key, 1, $length) = $prefix ORDER BY key;";
        command.Parameters.AddWithValue("$collection", collection);
        command.Parameters.AddWithValue("$length", keyPrefix.Length);
        command.Parameters.AddWithValue("$prefix", keyPrefix);
    }

    private static async Task<IReadOnlyList<string>> ReadKeysAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            keys.Add(reader.GetString(0));

        return keys;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// The <see cref="IPersistenceTransaction"/> handed to a caller's unit
    /// of work: every statement on the one connection that holds the open
    /// <c>BEGIN IMMEDIATE</c>, so a read inside the transaction sees the
    /// transaction's own writes and no second connection is ever waiting
    /// on a lock this one holds.
    /// </summary>
    private sealed class SqliteTransactionScope : IPersistenceTransaction
    {
        private readonly SqliteConnection _connection;
        private bool _finished;

        internal SqliteTransactionScope(SqliteConnection connection) => _connection = connection;

        internal void Invalidate() => _finished = true;

        public async Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(collection);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ThrowIfFinished();

            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT text_value FROM records WHERE collection = $collection AND key = $key;";
            command.Parameters.AddWithValue("$collection", collection);
            command.Parameters.AddWithValue("$key", key);

            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : (string)value;
        }

        public async Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(collection);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            ThrowIfFinished();

            await using var command = _connection.CreateCommand();
            PrepareTextUpsert(command, collection, key, value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(collection);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ThrowIfFinished();

            await using var command = _connection.CreateCommand();
            PrepareDelete(command, collection, key);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(collection);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ThrowIfFinished();

            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT blob_value FROM records WHERE collection = $collection AND key = $key;";
            command.Parameters.AddWithValue("$collection", collection);
            command.Parameters.AddWithValue("$key", key);

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                || await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
                return null;

            return reader.GetFieldValue<byte[]>(0);
        }

        public async Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(collection);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ThrowIfFinished();

            await using var command = _connection.CreateCommand();
            PrepareBlobUpsert(command, collection, key, value.ToArray());
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(collection);
            ArgumentNullException.ThrowIfNull(keyPrefix);
            ThrowIfFinished();

            await using var command = _connection.CreateCommand();
            PrepareListKeys(command, collection, keyPrefix);
            return await ReadKeysAsync(command, cancellationToken).ConfigureAwait(false);
        }

        private void ThrowIfFinished()
        {
            if (_finished)
                throw new InvalidOperationException(
                    "This IPersistenceTransaction has already committed or rolled back. A transaction handle is " +
                    "valid only for the duration of the ExecuteInTransactionAsync call that produced it.");
        }
    }
}
