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
/// file. The platform's only store since <c>v0.18.0</c>: the file-per-key
/// store this once stood beside, which satisfied the first two shapes
/// from one directory tree, is deleted (`WP 18.1A`, `ADR-0144`).
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
/// refusal that the deleted file-per-key store needed all existed to make
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
/// clears its own connection pool (<see cref="SqliteConnection.ClearPool(SqliteConnection)"/>), so no pooled
/// connection is left holding <c>tempest.db</c> and the root directory can
/// be deleted — which is what a test's temporary root, and a user's
/// "delete <c>persistence-data</c> to reset", both need.
/// </para>
/// </remarks>
public sealed class SqlitePersistenceStore
    : IPersistenceStore, IBinaryPersistenceStore, IQueryablePersistenceStore, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The configuration key the storage root path is read from.
    /// Relocated here from the now-deleted file-per-key <c>PersistenceStore</c>
    /// (`WP 18.1A`, `ADR-0144`); the key string and its default are
    /// unchanged.
    /// </summary>
    public const string RootPathConfigurationKey = "Persistence:RootPath";

    /// <summary>The root path used when <see cref="RootPathConfigurationKey"/> is not configured.</summary>
    public const string DefaultRootPath = "persistence-data";

    /// <summary>
    /// The configuration key selecting which persistence backend the Host
    /// registers.
    /// </summary>
    public const string BackendConfigurationKey = "Persistence:Backend";

    /// <summary>
    /// The <see cref="BackendConfigurationKey"/> value selecting this
    /// store — the only recognised value since <c>v0.18.0</c>: the
    /// file-per-key store this once named alongside (<c>files</c>) is
    /// deleted (`WP 18.1A`, `ADR-0144`), and that value is now unknown
    /// configuration rather than a second backend.
    /// </summary>
    public const string SqliteBackendValue = "sqlite";

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
    private long _currentSequence;

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

        _rootPath = configuration.TryGetValue(RootPathConfigurationKey, out var configuredPath)
            && !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : DefaultRootPath;

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
    public long CurrentSequence => Volatile.Read(ref _currentSequence);

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

                // The sequence advances inside the same BEGIN IMMEDIATE …
                // COMMIT as everything `work` wrote (`WP 18.1A`): a
                // transaction that throws after this point still rolls the
                // increment back with everything else, and one that
                // commits reports a sequence a concurrent reader can never
                // observe ahead of the data that earned it.
                var sequence = await IncrementSequenceAsync(connection, cancellationToken).ConfigureAwait(false);
                await ExecuteNonQueryAsync(connection, "COMMIT;", cancellationToken).ConfigureAwait(false);

                // SQLite's own write lock (taken by BEGIN IMMEDIATE, above)
                // serialises every transaction on this store end to end, so
                // no later commit's sequence can reach `_currentSequence`
                // before this one's — a plain write is enough, and Volatile
                // rather than Interlocked.Exchange because nothing here
                // races the same slot for supremacy, only for visibility.
                Volatile.Write(ref _currentSequence, sequence);
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

    /// <inheritdoc />
    public async Task<T> ExecuteInReadTransactionAsync<T>(
        Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        ThrowIfDisposed();

        SqliteConnection connection;
        try
        {
            connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not PersistenceStoreUnavailableException)
        {
            throw new PersistenceStoreUnavailableException(
                $"Failed to open '{_databasePath}' to begin a read transaction.", ex);
        }

        await using (connection.ConfigureAwait(false))
        {
            // The default deferred BEGIN, not BEGIN IMMEDIATE: a read
            // transaction takes no write lock and never contends with one,
            // which is the whole point of a store that never blocks the UI
            // thread on persistence. WAL mode (`ADR-0144`) gives it its own
            // consistent snapshot as of its first statement, regardless of
            // any commit that lands after that statement runs.
            await ExecuteNonQueryAsync(connection, "BEGIN;", cancellationToken).ConfigureAwait(false);

            var transaction = new SqliteReadTransactionScope(
                connection, await ReadSequenceAsync(connection, cancellationToken).ConfigureAwait(false));
            try
            {
                var result = await read(transaction, cancellationToken).ConfigureAwait(false);
                transaction.Invalidate();

                // COMMIT rather than ROLLBACK on a read-only transaction:
                // either ends it correctly on SQLite, and COMMIT is the one
                // that never logs a warning about an active statement.
                await ExecuteNonQueryAsync(connection, "COMMIT;", cancellationToken).ConfigureAwait(false);
                return result;
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "The search limit must be at least 1.");
        ThrowIfDisposed();

        var matchExpression = BuildMatchExpression(query);
        if (matchExpression is null)
            return [];

        return await ExecuteAsync(
            $"search for '{query}'",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT object_id, kind, project_id, bm25(search_index) AS match_rank, " +
                    "snippet(search_index, -1, '[', ']', '…', 8) AS match_snippet " +
                    "FROM search_index WHERE search_index MATCH $match ORDER BY match_rank LIMIT $limit;";
                command.Parameters.AddWithValue("$match", matchExpression);
                command.Parameters.AddWithValue("$limit", limit);

                var hits = new List<SearchHit>();
                await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    var objectId = Guid.ParseExact(reader.GetString(0), "N");
                    var kind = reader.GetString(1);
                    Guid? projectId = await reader.IsDBNullAsync(2, token).ConfigureAwait(false)
                        ? null
                        : Guid.ParseExact(reader.GetString(2), "N");
                    var rank = reader.GetDouble(3);
                    var snippet = reader.GetString(4);

                    hits.Add(new SearchHit(objectId, kind, projectId, rank, snippet));
                }

                return (IReadOnlyList<SearchHit>)hits;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return await ExecuteAsync(
            "check whether the search index is empty",
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT NOT EXISTS (SELECT 1 FROM search_index);";
                var value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns a caller's free-text <paramref name="query"/> into an FTS5
    /// <c>MATCH</c> expression: every alphanumeric run becomes its own
    /// double-quoted prefix token (<c>"bra"*</c>), space-joined, so tokens
    /// implicitly AND — narrowing, not widening, as more is typed — and
    /// each one matches as a prefix, so a partial word finds a whole one.
    /// </summary>
    /// <returns><see langword="null"/> if <paramref name="query"/> has no searchable token (blank, or punctuation only).</returns>
    private static string? BuildMatchExpression(string query)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        foreach (var ch in query)
        {
            if (char.IsLetterOrDigit(ch))
                current.Append(ch);
            else
                Flush();
        }

        Flush();

        if (tokens.Count == 0)
            return null;

        return string.Join(" ", tokens.Select(t => $"\"{t.Replace("\"", "\"\"", StringComparison.Ordinal)}\"*"));
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
        // root directory to be deletable. THIS store's pool only: the
        // process-wide ClearAllPools this used to call disposed the native
        // handle under other, still-live stores in the same process — seen
        // as `ObjectDisposedException: SQLitePCL.sqlite3` in roughly one of
        // every four parallel test runs, and reachable in the product by any
        // two hosts in one process.
        using (var pooled = new SqliteConnection(_connectionString))
            SqliteConnection.ClearPool(pooled);

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
                $"instance, or point this one at a different root with '{RootPathConfigurationKey}'.",
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

                -- `WP 18.1A`: the store's own monotonic commit counter
                -- (IQueryablePersistenceStore.CurrentSequence). A single
                -- row rather than a bare PRAGMA user_version, because it
                -- must be advanced inside the very transaction it counts
                -- (a PRAGMA cannot be) and read back through the same
                -- table a coherent snapshot read reads its data from.
                CREATE TABLE IF NOT EXISTS store_sequence (
                    id    INTEGER PRIMARY KEY CHECK (id = 1),
                    value INTEGER NOT NULL
                );

                INSERT INTO store_sequence (id, value)
                SELECT 1, 0 WHERE NOT EXISTS (SELECT 1 FROM store_sequence WHERE id = 1);

                -- `WP 18.1B`: the platform's one full-text search index.
                -- `object_id`/`kind`/`project_id` are UNINDEXED — carried
                -- alongside a match, never tokenised or matched against —
                -- while `title`/`identifier`/`refs` are the searchable
                -- columns. A separate virtual table rather than columns on
                -- `records`, because FTS5's own tokeniser and ranking apply
                -- to a table, not to a subset of another table's columns.
                CREATE VIRTUAL TABLE IF NOT EXISTS search_index USING fts5(
                    object_id UNINDEXED,
                    kind UNINDEXED,
                    project_id UNINDEXED,
                    title,
                    identifier,
                    refs
                );
                """;
            command.Parameters.AddWithValue("$version", SchemaVersion);
            command.ExecuteNonQuery();

            using var readSequence = connection.CreateCommand();
            readSequence.CommandText = "SELECT value FROM store_sequence WHERE id = 1;";
            _currentSequence = Convert.ToInt64(readSequence.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new PersistenceStoreUnavailableException(
                $"Failed to open or initialise the persistence database '{_databasePath}'.", ex);
        }
    }

    /// <summary>
    /// Advances <c>store_sequence</c> by one and returns its new value,
    /// inside the caller's already-open transaction.
    /// </summary>
    private static async Task<long> IncrementSequenceAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, "UPDATE store_sequence SET value = value + 1 WHERE id = 1;", cancellationToken)
            .ConfigureAwait(false);

        return await ReadSequenceAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads <c>store_sequence</c>'s current value inside the caller's already-open transaction.</summary>
    private static async Task<long> ReadSequenceAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM store_sequence WHERE id = 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
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

        public async Task IndexTextAsync(
            Guid objectId, string kind, Guid? projectId, string title, string? identifier, string? refs,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kind);
            ArgumentNullException.ThrowIfNull(title);
            ThrowIfFinished();

            // Delete-then-insert, not an upsert: a plain FTS5 table carries
            // no unique constraint over its own UNINDEXED columns to
            // conflict on, so this is the only way to replace a row rather
            // than accumulate a second one for the same object every time
            // its state is written.
            await using (var delete = _connection.CreateCommand())
            {
                delete.CommandText = "DELETE FROM search_index WHERE object_id = $objectId;";
                delete.Parameters.AddWithValue("$objectId", objectId.ToString("N"));
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var insert = _connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO search_index (object_id, kind, project_id, title, identifier, refs) " +
                "VALUES ($objectId, $kind, $projectId, $title, $identifier, $refs);";
            insert.Parameters.AddWithValue("$objectId", objectId.ToString("N"));
            insert.Parameters.AddWithValue("$kind", kind);
            insert.Parameters.AddWithValue("$projectId", (object?)projectId?.ToString("N") ?? DBNull.Value);
            insert.Parameters.AddWithValue("$title", title);
            insert.Parameters.AddWithValue("$identifier", (object?)identifier ?? DBNull.Value);
            insert.Parameters.AddWithValue("$refs", (object?)refs ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveFromIndexAsync(Guid objectId, CancellationToken cancellationToken = default)
        {
            ThrowIfFinished();

            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM search_index WHERE object_id = $objectId;";
            command.Parameters.AddWithValue("$objectId", objectId.ToString("N"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private void ThrowIfFinished()
        {
            if (_finished)
                throw new InvalidOperationException(
                    "This IPersistenceTransaction has already committed or rolled back. A transaction handle is " +
                    "valid only for the duration of the ExecuteInTransactionAsync call that produced it.");
        }
    }

    /// <summary>
    /// The <see cref="IPersistenceReadTransaction"/> handed to a caller's
    /// read (`WP 18.1A`): every statement on the one connection that holds
    /// the open, write-lock-free <c>BEGIN</c>, so every read this hands out
    /// sees the same WAL snapshot as <see cref="Sequence"/> was read from.
    /// </summary>
    private sealed class SqliteReadTransactionScope : IPersistenceReadTransaction
    {
        private readonly SqliteConnection _connection;
        private bool _finished;

        internal SqliteReadTransactionScope(SqliteConnection connection, long sequence)
        {
            _connection = connection;
            Sequence = sequence;
        }

        public long Sequence { get; }

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

        public async Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(collection);
            ThrowIfFinished();

            await using var command = _connection.CreateCommand();
            command.CommandText =
                "SELECT key, text_value FROM records " +
                "WHERE collection = $collection AND text_value IS NOT NULL ORDER BY key;";
            command.Parameters.AddWithValue("$collection", collection);

            var results = new List<KeyValuePair<string, string>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(new KeyValuePair<string, string>(reader.GetString(0), reader.GetString(1)));

            return results;
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
                    "This IPersistenceReadTransaction has already ended. A read transaction handle is valid only " +
                    "for the duration of the ExecuteInReadTransactionAsync call that produced it.");
        }
    }
}
