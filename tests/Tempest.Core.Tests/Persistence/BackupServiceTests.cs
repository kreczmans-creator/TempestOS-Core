using Microsoft.Data.Sqlite;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// <see cref="BackupService"/>'s own claims (`WP 21.5A`, `WP RC.0A` scope
/// items 3 and 4): a backup taken through the online backup API is a real,
/// independently-openable copy with the source's own tables; restoring
/// moves the replaced database aside rather than deleting it; and
/// <see cref="SqlitePersistenceStore"/>'s own constructor backs up a
/// database whose recorded schema version is behind this build's before
/// any migration DDL runs, naming the backup file it created in its log.
/// </summary>
public sealed class BackupServiceTests : IDisposable
{
    private readonly TempDirectory _temporaryRoot = new();

    private string RootPath => _temporaryRoot.Path;

    public void Dispose() => _temporaryRoot.Dispose();

    [Fact]
    public void CreateBackup_copies_the_source_and_verifies_by_counting_tables()
    {
        var sourcePath = Path.Combine(RootPath, "source.db");
        CreateRawSqliteDatabase(sourcePath, schemaVersion: 1, withTable: true);

        var backupPath = Path.Combine(RootPath, "backups", "source-backup.db");

        var outcome = BackupService.CreateBackup(sourcePath, backupPath);

        Assert.True(outcome.Verified);
        Assert.Equal(1, outcome.TableCount);
        Assert.True(File.Exists(backupPath));

        // The backup is a genuinely independent, openable database — not
        // merely a byte-identical file the source connection still has
        // some claim on.
        using var reopened = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        reopened.Open();
        using var command = reopened.CreateCommand();
        command.CommandText = "SELECT version FROM schema_info LIMIT 1;";
        Assert.Equal(1L, (long)command.ExecuteScalar()!);
    }

    [Fact]
    public void CreateBackup_throws_when_the_source_has_no_tables()
    {
        var sourcePath = Path.Combine(RootPath, "empty.db");
        CreateRawSqliteDatabase(sourcePath, schemaVersion: 1, withTable: false);

        var backupPath = Path.Combine(RootPath, "backups", "empty-backup.db");

        Assert.Throws<BackupVerificationException>(() => BackupService.CreateBackup(sourcePath, backupPath));
    }

    [Fact]
    public void BuildPreMigrationBackupFileName_names_the_version_being_replaced()
    {
        var name = BackupService.BuildPreMigrationBackupFileName(0, new DateTimeOffset(2026, 9, 15, 8, 30, 0, TimeSpan.Zero));

        Assert.Equal("tempest-0-20260915-083000.db", name);
    }

    [Fact]
    public void RestoreFromBackup_moves_the_replaced_database_aside_rather_than_deleting_it()
    {
        var currentPath = Path.Combine(RootPath, "tempest.db");
        CreateRawSqliteDatabase(currentPath, schemaVersion: 1, withTable: true);

        var backupPath = Path.Combine(RootPath, "backups", "restore-source.db");
        BackupService.CreateBackup(currentPath, backupPath);

        // Mutate the "current" database after taking the backup, so the
        // restored content is distinguishable from what is left behind.
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = currentPath }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE schema_info;";
            command.ExecuteNonQuery();
        }
        SqliteConnection.ClearPool(new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = currentPath }.ToString()));

        var replacedPath = Path.Combine(RootPath, "tempest-replaced-20260915-090000.db");

        BackupService.RestoreFromBackup(currentPath, backupPath, replacedPath);

        Assert.True(File.Exists(currentPath));
        Assert.True(File.Exists(replacedPath));

        using var restored = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = currentPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        restored.Open();
        using var restoredCommand = restored.CreateCommand();
        restoredCommand.CommandText = "SELECT version FROM schema_info LIMIT 1;";
        Assert.Equal(1L, (long)restoredCommand.ExecuteScalar()!);

        using var replaced = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = replacedPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        replaced.Open();
        using var replacedCommand = replaced.CreateCommand();
        replacedCommand.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_info';";
        Assert.Equal(0L, (long)replacedCommand.ExecuteScalar()!);
    }

    [Fact]
    public async Task SqlitePersistenceStore_backs_up_before_migrating_a_database_behind_this_builds_schema_version()
    {
        // A database this build has never opened, fabricated at a schema
        // version behind SqlitePersistenceStore.SchemaVersion (1) — the
        // seam `WP 21.5A`'s own brief asks for: a real "found a database
        // behind the current version" launch, reproduced without needing a
        // second, live schema version to exist anywhere in this build.
        var databasePath = Path.Combine(RootPath, SqlitePersistenceStore.DatabaseFileName);
        CreateRawSqliteDatabase(databasePath, schemaVersion: 0, withTable: true);

        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, RootPath),
        ])).Build();

        using (var store = new SqlitePersistenceStore(configuration))
        {
            // The store having opened successfully at all confirms
            // InitialiseSchema ran the backup-then-migrate path to
            // completion rather than faulting on it.
            await store.WriteAsync("probe", "key", "value");
        }

        var backupsFolder = Path.Combine(RootPath, BackupService.BackupsFolderName);
        var backupFiles = Directory.Exists(backupsFolder) ? Directory.GetFiles(backupsFolder, "tempest-0-*.db") : [];

        Assert.Single(backupFiles);

        using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupFiles[0], Mode = SqliteOpenMode.ReadOnly }.ToString());
        backup.Open();
        using var command = backup.CreateCommand();
        command.CommandText = "SELECT version FROM schema_info LIMIT 1;";

        // The backup is a snapshot of the database exactly as it stood
        // before migration — still recording the old version, 0, never the
        // new one the live database was upgraded to in place.
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
    }

    /// <summary>
    /// Builds a minimal, standalone SQLite database carrying only the one
    /// table <see cref="SqlitePersistenceStore"/>'s own schema reads its
    /// version from (<c>schema_info</c>) — deliberately not built through
    /// <see cref="SqlitePersistenceStore"/> itself, so a test can fabricate
    /// a schema version this build's own <see cref="SqlitePersistenceStore.SchemaVersion"/>
    /// constant could never itself produce.
    /// </summary>
    private static void CreateRawSqliteDatabase(string path, int schemaVersion, bool withTable)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        connection.Open();

        if (withTable)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE schema_info (version INTEGER NOT NULL); INSERT INTO schema_info (version) VALUES ($version);";
            command.Parameters.AddWithValue("$version", schemaVersion);
            command.ExecuteNonQuery();
        }
    }
}
