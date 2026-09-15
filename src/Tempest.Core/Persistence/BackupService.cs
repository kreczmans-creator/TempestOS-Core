using System.Globalization;
using Microsoft.Data.Sqlite;
using Tempest.Core.Logging;

namespace Tempest.Core.Persistence;

/// <summary>
/// The outcome of a backup created by <see cref="BackupService"/>: where it
/// landed, and what verifying it (opening it read-only and counting its
/// tables — `WP 21.5A`) found.
/// </summary>
/// <param name="BackupPath">The full path of the backup file created.</param>
/// <param name="TableCount">
/// The number of ordinary tables (<c>sqlite_master.type = 'table'</c>,
/// excluding SQLite's own internal <c>sqlite_%</c> tables) the backup file
/// was found to contain when opened read-only immediately after creation.
/// </param>
public readonly record struct BackupOutcome(string BackupPath, int TableCount)
{
    /// <summary>
    /// <see langword="true"/> when the backup file, opened read-only, was
    /// found to contain at least one table — the verification `WP 21.5A`'s
    /// brief asks for ("the resulting file verified by opening it read-only
    /// and counting its tables"). A backup with zero tables signals a
    /// corrupt or truncated copy, never treated as a good backup by any
    /// caller of <see cref="BackupService"/>.
    /// </summary>
    public bool Verified => TableCount > 0;
}

/// <summary>
/// Thrown when a backup was created but failed verification (zero tables
/// found on read-back), or when the online backup itself failed.
/// </summary>
public sealed class BackupVerificationException : PersistenceException
{
    /// <summary>Initialises a new instance of the <see cref="BackupVerificationException"/> class.</summary>
    public BackupVerificationException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="BackupVerificationException"/> class.</summary>
    public BackupVerificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Backs up and restores <c>tempest.db</c> through SQLite's own online
/// backup API (`Microsoft.Data.Sqlite`'s <see cref="SqliteConnection.BackupDatabase(SqliteConnection)"/>,
/// itself a thin wrapper over <c>sqlite3_backup_init</c>/<c>_step</c>/<c>_finish</c>)
/// — never a plain file copy of an open WAL database, which can copy a
/// database file mid-write and land a corrupt or torn snapshot (`WP 21.5A`,
/// `WP RC.0A`).
/// </summary>
/// <remarks>
/// <para>
/// Two callers: <see cref="SqlitePersistenceStore"/>'s own constructor,
/// which calls the <see langword="static"/> members of this class directly
/// (synchronously, before any container exists to resolve a service from —
/// see its own <c>InitialiseSchema</c>) to back up a database whose schema
/// version is behind the running build's before any migration runs; and
/// Settings → Data ("Back up now…"/"Restore from backup…"), which resolves
/// this class as an ordinary Platform Service. Both paths go through the
/// identical <see cref="CreateBackup"/>/<see cref="RestoreFromBackup"/>
/// mechanics, so a manual backup and a pre-migration backup are provably
/// the same operation, not two implementations that could drift.
/// </para>
/// <para>
/// <b>Restore precondition.</b> <see cref="RestoreFromBackup"/> assumes the
/// live database is already closed — no open <see cref="SqlitePersistenceStore"/>
/// holding it — since it moves and copies files SQLite itself may otherwise
/// still have open. The Desktop's own Settings → Data handler enforces this
/// by refusing to restore while a project is open and by restarting the
/// application immediately after a successful restore (`WP 21.5A` scope
/// item 4); this class enforces nothing about that itself, because it has
/// no notion of "a project is open" to enforce.
/// </para>
/// </remarks>
public sealed class BackupService
{
    /// <summary>The name of the folder backups are written into, directly beside <c>tempest.db</c>.</summary>
    public const string BackupsFolderName = "backups";

    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="BackupService"/> class.</summary>
    public BackupService(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>Builds the pre-migration backup's own file name: <c>tempest-&lt;schemaVersionBeingReplaced&gt;-&lt;yyyyMMdd-HHmmss&gt;.db</c> — the schema version named is the one the database carried *before* migration, so the file reads as "this is what version N looked like right before it became version N+1".</summary>
    public static string BuildPreMigrationBackupFileName(int schemaVersionBeingReplaced, DateTimeOffset timestampUtc) =>
        $"tempest-{schemaVersionBeingReplaced.ToString(CultureInfo.InvariantCulture)}-{timestampUtc:yyyyMMdd-HHmmss}.db";

    /// <summary>Builds a manual backup's own suggested file name: <c>tempest-backup-&lt;yyyyMMdd-HHmmss&gt;.db</c> — offered as the Save picker's default; the operator may rename it.</summary>
    public static string BuildManualBackupFileName(DateTimeOffset timestampUtc) =>
        $"tempest-backup-{timestampUtc:yyyyMMdd-HHmmss}.db";

    /// <summary>Builds the file name the current database is moved aside to during a restore: <c>tempest-replaced-&lt;yyyyMMdd-HHmmss&gt;.db</c>.</summary>
    public static string BuildReplacedDatabaseFileName(DateTimeOffset timestampUtc) =>
        $"tempest-replaced-{timestampUtc:yyyyMMdd-HHmmss}.db";

    /// <summary>
    /// Backs up <paramref name="sourceDatabasePath"/> to
    /// <paramref name="destinationBackupPath"/> via SQLite's online backup
    /// API, then verifies the result by reopening it read-only and counting
    /// its tables.
    /// </summary>
    /// <param name="sourceDatabasePath">The live database file to back up. Opened read-only — a concurrent reader never blocks a concurrent writer under WAL, and this call never needs write access to the source.</param>
    /// <param name="destinationBackupPath">Where the backup file is written. Its parent directory is created if it does not already exist.</param>
    /// <exception cref="BackupVerificationException">The online backup failed, or the resulting file has no tables.</exception>
    public static BackupOutcome CreateBackup(string sourceDatabasePath, string destinationBackupPath, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationBackupPath);

        var destinationDirectory = Path.GetDirectoryName(destinationBackupPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        // A stale file from a previous, failed attempt at this exact path
        // (the same second, in practice only ever a test) must not make
        // SQLite open what looks like an existing, unrelated database.
        if (File.Exists(destinationBackupPath))
            File.Delete(destinationBackupPath);

        try
        {
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = sourceDatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString());
            source.Open();

            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destinationBackupPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString());
            destination.Open();

            // The online backup API (`sqlite3_backup_*`): a consistent,
            // page-by-page copy of the source at a single point in time,
            // safe against a concurrent WAL writer — the property a plain
            // `File.Copy` of `tempest.db` does not have.
            source.BackupDatabase(destination);
        }
        catch (Exception ex) when (ex is not BackupVerificationException)
        {
            throw new BackupVerificationException(
                $"Failed to back up '{sourceDatabasePath}' to '{destinationBackupPath}'.", ex);
        }
        finally
        {
            SqliteConnection.ClearPool(new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destinationBackupPath }.ToString()));
        }

        var tableCount = CountTables(destinationBackupPath);
        var outcome = new BackupOutcome(destinationBackupPath, tableCount);

        if (!outcome.Verified)
        {
            throw new BackupVerificationException(
                $"Backup '{destinationBackupPath}' was created but verification found no tables — treating it as untrustworthy rather than a usable backup.");
        }

        logger?.Information($"Backup created and verified at '{destinationBackupPath}' ({tableCount} table(s)).");

        return outcome;
    }

    /// <summary>
    /// Restores <paramref name="currentDatabasePath"/> from
    /// <paramref name="backupFilePath"/>: moves the current database (and
    /// its <c>-wal</c>/<c>-shm</c> siblings, if present) aside to
    /// <paramref name="replacedDatabasePath"/>, then copies the backup file
    /// in as the new current database.
    /// </summary>
    /// <remarks>
    /// Assumes no <see cref="SqlitePersistenceStore"/> currently holds
    /// <paramref name="currentDatabasePath"/> open — see this class's own
    /// remarks on the restore precondition.
    /// </remarks>
    public static void RestoreFromBackup(string currentDatabasePath, string backupFilePath, string replacedDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacedDatabasePath);

        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"Backup file '{backupFilePath}' does not exist.", backupFilePath);

        // Defensive, not merely decorative: `Microsoft.Data.Sqlite` pools
        // connections per exact connection string, so any earlier,
        // already-`Dispose`d connection this process opened against these
        // paths (a `CreateBackup` read of what is about to be moved, in
        // particular) can otherwise still be holding the underlying file
        // open at the OS level, and `File.Move` below would fail with
        // "the process cannot access the file". The live persistence
        // store's own connection pool is expected to already be clear by
        // the time a restore runs (`SqlitePersistenceStore.DisposeAsync`
        // clears its own pool on shutdown, and the Desktop restarts itself
        // after a restore — see this class's own restore-precondition
        // remarks) — this call is the belt to that braces.
        SqliteConnection.ClearAllPools();

        var replacedDirectory = Path.GetDirectoryName(replacedDatabasePath);
        if (!string.IsNullOrEmpty(replacedDirectory))
            Directory.CreateDirectory(replacedDirectory);

        if (File.Exists(currentDatabasePath))
            File.Move(currentDatabasePath, replacedDatabasePath, overwrite: true);

        // The WAL/SHM siblings are part of the database, not a cache
        // (PHYSICAL_REVIEW.md §4) — moved aside alongside the main file so
        // no stale write-ahead log from the replaced database is ever read
        // against the restored one. Best-effort: their absence (a database
        // that was cleanly closed, with its WAL already checkpointed) is
        // the ordinary case, not a failure.
        MoveSiblingIfExists(currentDatabasePath, replacedDatabasePath, "-wal");
        MoveSiblingIfExists(currentDatabasePath, replacedDatabasePath, "-shm");

        var currentDirectory = Path.GetDirectoryName(currentDatabasePath);
        if (!string.IsNullOrEmpty(currentDirectory))
            Directory.CreateDirectory(currentDirectory);

        File.Copy(backupFilePath, currentDatabasePath, overwrite: true);
    }

    private static void MoveSiblingIfExists(string currentDatabasePath, string replacedDatabasePath, string suffix)
    {
        var siblingSource = currentDatabasePath + suffix;
        if (!File.Exists(siblingSource))
            return;

        try
        {
            File.Move(siblingSource, replacedDatabasePath + suffix, overwrite: true);
        }
        catch (IOException)
        {
            // Best-effort, as above — a lingering WAL/SHM file that cannot
            // be moved does not prevent the restore of the main database
            // file, which is the operation that actually matters.
        }
    }

    private static int CountTables(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Instance wrapper over <see cref="CreateBackup"/>, for Settings →
    /// Data's own "Back up now…" — asynchronous (the online backup and the
    /// read-back verification are both blocking file I/O, so this runs them
    /// on a background thread, never the UI thread — `NoBlockingPersistenceCallsTests`).
    /// </summary>
    public Task<BackupOutcome> BackUpNowAsync(string sourceDatabasePath, string destinationBackupPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => CreateBackup(sourceDatabasePath, destinationBackupPath, _logger), cancellationToken);

    /// <summary>
    /// Instance wrapper over <see cref="RestoreFromBackup"/>, for Settings →
    /// Data's own "Restore from backup…" — asynchronous, for the same
    /// reason as <see cref="BackUpNowAsync"/>.
    /// </summary>
    public Task RestoreAsync(string currentDatabasePath, string backupFilePath, string replacedDatabasePath, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            RestoreFromBackup(currentDatabasePath, backupFilePath, replacedDatabasePath);
            _logger?.Information($"Restored '{currentDatabasePath}' from backup '{backupFilePath}'; the replaced database was moved to '{replacedDatabasePath}'.");
        }, cancellationToken);
}
