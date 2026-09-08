using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;
using Xunit.Abstractions;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The claims that are <see cref="SqlitePersistenceStore"/>'s own
/// (`ADR-0144`, `WP 17.1A`): the schema it creates, the durability its
/// transactions give, the cross-process lock it holds, the file handles it
/// releases on disposal, and the query costs the file-per-key store could
/// not pay.
/// </summary>
/// <remarks>
/// Everything this store shares with the file store is asserted once, in
/// the backend-agnostic contract classes
/// (<see cref="PersistenceStoreTests{TBackend}"/>,
/// <see cref="BinaryPersistenceStoreTests{TBackend}"/>,
/// <see cref="PersistenceStoreHostileNameTests{TBackend}"/>,
/// <see cref="QueryablePersistenceStoreTests{TBackend}"/>), each of which
/// runs against both backends. Nothing here repeats them.
/// </remarks>
public sealed class SqlitePersistenceStoreTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TempDirectory _temporaryRoot = new();
    private readonly List<SqlitePersistenceStore> _stores = [];

    public SqlitePersistenceStoreTests(ITestOutputHelper output) => _output = output;

    private string RootPath => _temporaryRoot.Path;

    private static IConfigurationProvider ConfigurationFor(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    private SqlitePersistenceStore NewStore(string? rootPath = null)
    {
        var store = new SqlitePersistenceStore(ConfigurationFor(rootPath ?? RootPath));
        _stores.Add(store);
        return store;
    }

    private void ReleaseStores()
    {
        for (var i = _stores.Count - 1; i >= 0; i--)
            _stores[i].Dispose();

        _stores.Clear();
    }

    public void Dispose()
    {
        ReleaseStores();
        _temporaryRoot.Dispose();
    }

    // ----------------------------------------------------------------
    // Construction, root resolution and schema
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new SqlitePersistenceStore(null!));

    [Fact]
    public void Constructor_NoRootPathConfigured_ResolvesTheSameDefaultTheFileStoreDid()
    {
        // Asserted without constructing a store, deliberately: building
        // one would create the real, cwd-relative `persistence-data`
        // folder the shipped application keeps a user's data in, which is
        // the rule `WP 17.0A` set for this suite and `ADR-0144` did not
        // relax.
        Assert.Equal("persistence-data", PersistenceStore.DefaultRootPath);
    }

    [Fact]
    public void Constructor_CreatesTheRootDirectoryAndTheDatabaseFile()
    {
        var root = Path.Combine(RootPath, "not", "yet", "created");

        var store = NewStore(root);

        Assert.Equal(root, store.RootPath);
        Assert.True(Directory.Exists(root), "The store must create its own root.");
        Assert.True(File.Exists(Path.Combine(root, SqlitePersistenceStore.DatabaseFileName)));
        Assert.True(File.Exists(Path.Combine(root, SqlitePersistenceStore.LockFileName)));
    }

    [Fact]
    public async Task TheDatabase_CarriesTheDeclaredSchemaAtVersionOne()
    {
        var store = NewStore();
        await store.WriteAsync("collection", "key", "value");

        var connectionString = new SqliteConnectionStringBuilder { DataSource = store.DatabasePath }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connection, "SELECT version FROM schema_info;")));
        Assert.Equal(1, SqlitePersistenceStore.SchemaVersion);

        // The exact column set the ADR names, read back from the database
        // rather than from the source that created it.
        var columns = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM pragma_table_info('records') ORDER BY cid;";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));
        }

        Assert.Equal(new[] { "collection", "key", "text_value", "blob_value", "updated_utc" }, columns);

        Assert.Equal("wal", Convert.ToString(await ScalarAsync(connection, "PRAGMA journal_mode;")));
    }

    [Fact]
    public async Task EveryRecord_CarriesTheInstantItWasLastWritten()
    {
        var store = NewStore();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await store.WriteAsync("collection", "key", "value");

        var connectionString = new SqliteConnectionStringBuilder { DataSource = store.DatabasePath }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var stamp = Convert.ToString(await ScalarAsync(connection, "SELECT updated_utc FROM records;"));

        Assert.NotNull(stamp);
        Assert.True(DateTimeOffset.TryParse(stamp, out var parsed), $"'{stamp}' must be a round-trippable instant.");
        Assert.InRange(parsed, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    // ----------------------------------------------------------------
    // Names, now that they are not file names
    // ----------------------------------------------------------------

    [Fact]
    public async Task CaseVariantKeys_AreSimplyTwoDistinctRecords()
    {
        // The file store had to refuse the second of these on a
        // case-insensitive file system, because one physical file backed
        // both keys and overwriting would have silently discarded the
        // first key's record (`TD-59`,
        // PersistenceStoreHostileNameFileSystemTests). There is no file,
        // so there is no collision, so there is nothing to refuse: the
        // column collates as BINARY and these are two rows.
        var store = NewStore();

        await store.WriteAsync("collection", "Steel", "capitalised");
        await store.WriteAsync("collection", "steel", "lowercase");
        await store.WriteAsync("collection", "CON", "upper");
        await store.WriteAsync("collection", "Con", "mixed");

        Assert.Equal("capitalised", await store.ReadAsync("collection", "Steel"));
        Assert.Equal("lowercase", await store.ReadAsync("collection", "steel"));
        Assert.Equal("upper", await store.ReadAsync("collection", "CON"));
        Assert.Equal("mixed", await store.ReadAsync("collection", "Con"));
        Assert.Equal(4, (await store.ListKeysAsync("collection")).Count);

        await store.DeleteAsync("collection", "steel");
        Assert.Equal("capitalised", await store.ReadAsync("collection", "Steel"));
        Assert.Null(await store.ReadAsync("collection", "steel"));
    }

    [Fact]
    public async Task CaseVariantCollections_AreSimplyTwoDistinctCollections()
    {
        var store = NewStore();

        await store.WriteAsync("Materials", "key", "capitalised");
        await store.WriteAsync("materials", "key", "lowercase");

        Assert.Equal("capitalised", await store.ReadAsync("Materials", "key"));
        Assert.Equal("lowercase", await store.ReadAsync("materials", "key"));
    }

    [Fact]
    public async Task AKeyLongerThanAnyFileName_RoundTrips()
    {
        // 4,000 characters. Percent-encoded onto a file system this would
        // exceed every per-segment name limit there is; it is an ordinary
        // value in a column here.
        var store = NewStore();
        var key = string.Concat(Enumerable.Repeat("long-key-segment/", 250));

        await store.WriteAsync("collection", key, "value");

        Assert.Equal("value", await store.ReadAsync("collection", key));
        Assert.Contains(key, await store.ListKeysAsync("collection"));
    }

    // ----------------------------------------------------------------
    // Text and bytes share one row and differ at the column
    // ----------------------------------------------------------------

    [Fact]
    public async Task ARecordWrittenAsText_ReadsAsNoBytes_AndTheReverse()
    {
        // `IBinaryPersistenceStore` always intended this ("bytes that
        // happen to be valid UTF-8 are still bytes, and a caller that
        // stored an image must never receive a string that lost half of
        // it"), but the file store could only rely on collections being
        // owned by one service: an untagged file read through the other
        // shape simply hands back whatever that decoding produces. This
        // backend knows which column a value went into, so the intent is
        // now enforced rather than conventional.
        var store = NewStore();

        await store.WriteAsync("shared", "as-text", "not bytes");
        await store.WriteBytesAsync("shared", "as-bytes", new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        Assert.Null(await store.ReadBytesAsync("shared", "as-text"));
        Assert.Null(await store.ReadAsync("shared", "as-bytes"));

        // Both are still records, and both are still listed: the shape
        // decides how a value reads, not whether the key exists.
        Assert.Equal(2, (await store.ListKeysAsync("shared")).Count);

        // A rewrite through the other shape replaces the record wholly,
        // rather than leaving both readable.
        await store.WriteBytesAsync("shared", "as-text", new byte[] { 1, 2 });
        Assert.Null(await store.ReadAsync("shared", "as-text"));
        Assert.Equal(new byte[] { 1, 2 }, await store.ReadBytesAsync("shared", "as-text"));
    }

    [Fact]
    public async Task ReadAllAsync_SkipsRecordsWrittenAsBytes()
    {
        var store = NewStore();

        await store.WriteAsync("mixed", "text", "a string");
        await store.WriteBytesAsync("mixed", "bytes", new byte[] { 1, 2, 3 });

        var all = await store.ReadAllAsync("mixed");

        Assert.Equal(new[] { new KeyValuePair<string, string>("text", "a string") }, all);

        var read = await store.ReadManyAsync("mixed", new[] { "text", "bytes" });
        Assert.Equal("a string", read["text"]);
        Assert.Null(read["bytes"]);
    }

    // ----------------------------------------------------------------
    // Transactions
    // ----------------------------------------------------------------

    [Fact]
    public async Task AThrowInsideATransaction_RollsBackEveryWriteItHadMade()
    {
        var store = NewStore();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ExecuteInTransactionAsync(async (transaction, token) =>
            {
                await transaction.WriteAsync("collection", "first", "a", token);
                await transaction.WriteAsync("collection", "second", "b", token);
                await transaction.WriteBytesAsync("content", "blob", new byte[] { 1, 2, 3 }, token);
                throw new InvalidOperationException("the unit of work failed");
            }));

        Assert.Equal("the unit of work failed", thrown.Message);

        // Not "some of them are gone": none of them ever existed.
        Assert.Empty(await store.ListKeysAsync("collection"));
        Assert.Empty(await store.ListKeysAsync("content"));
        Assert.Null(await store.ReadAsync("collection", "first"));
        Assert.Null(await store.ReadAsync("collection", "second"));
        Assert.Null(await store.ReadBytesAsync("content", "blob"));
    }

    [Fact]
    public async Task AThrowInsideATransaction_LeavesTheStateBeforeItExactlyAsItWas()
    {
        var store = NewStore();
        await store.WriteAsync("collection", "existing", "original");
        await store.WriteAsync("collection", "doomed", "still here");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ExecuteInTransactionAsync(async (transaction, token) =>
            {
                await transaction.WriteAsync("collection", "existing", "overwritten", token);
                await transaction.DeleteAsync("collection", "doomed", token);
                await transaction.WriteAsync("collection", "added", "new", token);
                throw new InvalidOperationException("no");
            }));

        Assert.Equal("original", await store.ReadAsync("collection", "existing"));
        Assert.Equal("still here", await store.ReadAsync("collection", "doomed"));
        Assert.Null(await store.ReadAsync("collection", "added"));
        Assert.Equal(2, (await store.ListKeysAsync("collection")).Count);
    }

    [Fact]
    public async Task ACancelledTransaction_RollsBack()
    {
        var store = NewStore();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.ExecuteInTransactionAsync(
                async (transaction, token) =>
                {
                    await transaction.WriteAsync("collection", "key", "value", token);
                    await cancellation.CancelAsync();
                    token.ThrowIfCancellationRequested();
                },
                cancellation.Token));

        Assert.Empty(await store.ListKeysAsync("collection"));
    }

    [Fact]
    public async Task ReadAllAsync_ReturnsEverythingOneTransactionWrote_OrNothingOfIt()
    {
        // The all-or-nothing claim stated the way a reader experiences it:
        // a collection is never half-written. Asserted from OUTSIDE the
        // transaction, at the one moment a partial read would be possible
        // - 500 records written, none of them committed - rather than by
        // polling and hoping a poll lands there.
        var store = NewStore();
        var midAbandonedTransaction = -1;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ExecuteInTransactionAsync(async (transaction, token) =>
            {
                for (var i = 0; i < 500; i++)
                    await transaction.WriteAsync("collection", $"key-{i:D4}", $"value-{i}", token);

                // A separate task, so a separate connection: this is a
                // genuine outside observer, not the transaction reading
                // itself. It is a READ, which WAL never blocks - a write
                // from here would contend with the write lock this
                // transaction holds and fail on the busy timeout, which is
                // exactly what `IQueryablePersistenceStore` documents.
                midAbandonedTransaction = (await Task.Run(() => store.ReadAllAsync("collection"))).Count;

                throw new InvalidOperationException("abandoned after 500 writes");
            }));

        Assert.Equal(0, midAbandonedTransaction);
        Assert.Empty(await store.ReadAllAsync("collection"));

        // And the other half of "or nothing": the same 500 writes, allowed
        // to commit, arrive together.
        var midCommittingTransaction = -1;

        await store.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            for (var i = 0; i < 500; i++)
                await transaction.WriteAsync("collection", $"key-{i:D4}", $"value-{i}", token);

            midCommittingTransaction = (await Task.Run(() => store.ReadAllAsync("collection"))).Count;
        });

        Assert.Equal(0, midCommittingTransaction);
        Assert.Equal(500, (await store.ReadAllAsync("collection")).Count);
    }

    [Fact]
    public async Task ATransactionHandle_IsUnusableAfterItsUnitOfWorkReturns()
    {
        var store = NewStore();
        IPersistenceTransaction? escaped = null;

        await store.ExecuteInTransactionAsync((transaction, _) =>
        {
            escaped = transaction;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => escaped!.WriteAsync("collection", "key", "value"));
    }

    [Fact]
    public async Task ATransactionsWrites_AreVisibleToEveryLaterReaderOnceItCommits()
    {
        var store = NewStore();

        await store.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            await transaction.WriteAsync("collection", "alpha", "1", token);
            await transaction.WriteAsync("collection", "beta", "2", token);
        });

        ReleaseStores();
        var reopened = NewStore();

        Assert.Equal(2, (await reopened.ReadAllAsync("collection")).Count);
    }

    // ----------------------------------------------------------------
    // Instance lock and disposal
    // ----------------------------------------------------------------

    [Fact]
    public void ASecondStoreOverTheSameRoot_IsRefusedByNameRatherThanAllowedToShare()
    {
        var first = NewStore();

        var refusal = Assert.Throws<PersistenceStoreUnavailableException>(() => NewStore());

        Assert.Contains(first.RootPath, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("another TempestOS instance", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(SqlitePersistenceStore.LockFileName, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStoreOverADifferentRoot_IsNotAffectedByAnotherStoresLock()
    {
        var first = NewStore();
        var second = NewStore(Path.Combine(RootPath, "second"));

        await first.WriteAsync("collection", "key", "first");
        await second.WriteAsync("collection", "key", "second");

        Assert.Equal("first", await first.ReadAsync("collection", "key"));
        Assert.Equal("second", await second.ReadAsync("collection", "key"));
    }

    [Fact]
    public async Task AfterDisposal_TheRootDirectoryCanBeDeleted()
    {
        // The property every temporary root in this repository depends on,
        // and the reason `Dispose` calls `SqliteConnection.ClearAllPools`:
        // a pooled connection sitting idle still holds `tempest.db` open,
        // and on Windows an open handle blocks the delete.
        var root = Path.Combine(RootPath, "deletable");
        var store = NewStore(root);

        await store.WriteAsync("collection", "key", "value");
        await store.ReadAsync("collection", "key");
        await store.ExecuteInTransactionAsync((transaction, token) =>
            transaction.WriteAsync("collection", "another", "value", token));

        await store.DisposeAsync();

        Directory.Delete(root, recursive: true);

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task AfterDisposal_TheRootCanBeOpenedByAFreshStore_AndStillHoldsItsRecords()
    {
        var store = NewStore();
        await store.WriteAsync("collection", "key", "value");

        await store.DisposeAsync();

        var reopened = NewStore();
        Assert.Equal("value", await reopened.ReadAsync("collection", "key"));
    }

    [Fact]
    public async Task Disposal_IsIdempotent_AndEveryOperationAfterItIsRefused()
    {
        var store = NewStore();

        await store.DisposeAsync();
        await store.DisposeAsync();
        store.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.ReadAsync("collection", "key"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.WriteAsync("collection", "key", "value"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.ReadAllAsync("collection"));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => store.ExecuteInTransactionAsync((_, _) => Task.CompletedTask));
    }

    // ----------------------------------------------------------------
    // The cost the file-per-key store could not pay (`TD-12`)
    // ----------------------------------------------------------------

    [Fact]
    public async Task TenThousandKeysInOneCollection_ListAndReadAllWellInsideTheBudget()
    {
        // A budget, not a benchmark. The measured figure is reported so a
        // regression is visible in the log; the assertion is loose enough
        // that a slow or contended CI machine does not fail the build over
        // a number that is not the claim. The claim is that a whole
        // collection is one indexed query rather than 10,000 directory
        // entries and 10,000 file opens.
        var store = NewStore();

        var seeding = Stopwatch.StartNew();
        await store.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            for (var i = 0; i < 10_000; i++)
                await transaction.WriteAsync("bulk", $"key-{i:D6}", $"value-{i}", token);
        });
        seeding.Stop();

        var listing = Stopwatch.StartNew();
        var keys = await store.ListKeysAsync("bulk");
        listing.Stop();

        var reading = Stopwatch.StartNew();
        var all = await store.ReadAllAsync("bulk");
        reading.Stop();

        var prefixed = Stopwatch.StartNew();
        var prefixMatches = await store.ListKeysAsync("bulk", "key-0000");
        prefixed.Stop();

        _output.WriteLine(
            $"10,000 keys: seed (one transaction) {seeding.ElapsedMilliseconds} ms, " +
            $"ListKeysAsync {listing.ElapsedMilliseconds} ms, " +
            $"ReadAllAsync {reading.ElapsedMilliseconds} ms, " +
            $"prefix listing {prefixed.ElapsedMilliseconds} ms.");

        Assert.Equal(10_000, keys.Count);
        Assert.Equal(10_000, all.Count);
        Assert.Equal("value-0", all[0].Value);
        Assert.Equal(100, prefixMatches.Count);

        Assert.True(
            listing.ElapsedMilliseconds + reading.ElapsedMilliseconds < 10_000,
            $"Listing and reading 10,000 keys took {listing.ElapsedMilliseconds + reading.ElapsedMilliseconds} ms.");
    }

    // ----------------------------------------------------------------
    // The file backend's own answer, stated rather than implied
    // ----------------------------------------------------------------

    [Fact]
    public async Task TheFileBackend_DoesNotRollBackAUnitOfWork_AndThisIsDocumentedNotAccidental()
    {
        // `ADR-0144` retains `Persistence:Backend=files` for one release
        // and gives it `IQueryablePersistenceStore` so that a consumer
        // written against the interface still runs there. What it cannot
        // give it is atomicity. That is stated in capitals on
        // `PersistenceStore.ExecuteInTransactionAsync`, and asserted here,
        // so that no reader can acquire the opposite belief from a green
        // suite: the difference between the two backends is a fact about
        // the release, not a bug to be found later.
        using var temporary = new TempDirectory();
        var store = new PersistenceStore(ConfigurationFor(temporary.Path));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((IQueryablePersistenceStore)store).ExecuteInTransactionAsync(async (transaction, token) =>
            {
                await transaction.WriteAsync("collection", "first", "a", token);
                throw new InvalidOperationException("no");
            }));

        Assert.Equal("a", await store.ReadAsync("collection", "first"));
    }
}
