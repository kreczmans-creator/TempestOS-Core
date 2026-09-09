using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The query shape of the store (`ADR-0144`, `WP 17.1A`) — prefix listing,
/// whole-collection read and multi-key read — run once per backend.
/// </summary>
/// <remarks>
/// Both backends answer these; only one answers them without scanning.
/// The <em>answers</em> are the contract and are asserted here; the cost
/// is not a testable property and is not asserted, except as the timing
/// figure <see cref="SqlitePersistenceStoreTests"/> reports.
/// <see cref="IQueryablePersistenceStore.ExecuteInTransactionAsync"/> is
/// deliberately absent from this class: the two backends genuinely differ
/// on it, and each states its own behaviour in its own file rather than
/// pretending to share one.
/// </remarks>
public abstract class QueryablePersistenceStoreTests<TBackend> : PersistenceStoreBackendFixture<TBackend>
    where TBackend : IPersistenceStoreBackend, new()
{
    private async Task SeedAsync()
    {
        await Store.WriteAsync("collection", "part:001", "one");
        await Store.WriteAsync("collection", "part:002", "two");
        await Store.WriteAsync("collection", "requirement:001", "three");
        await Store.WriteAsync("other", "part:999", "elsewhere");
    }

    [Fact]
    public async Task ListKeysAsync_WithAPrefix_ReturnsOnlyThatPrefixesKeys_InOrder()
    {
        await SeedAsync();

        var keys = await QueryableStore.ListKeysAsync("collection", "part:");

        Assert.Equal(new[] { "part:001", "part:002" }, keys);
    }

    [Fact]
    public async Task ListKeysAsync_WithAnEmptyPrefix_ReturnsEveryKey()
    {
        await SeedAsync();

        var keys = await QueryableStore.ListKeysAsync("collection", string.Empty);

        Assert.Equal(3, keys.Count);
    }

    [Fact]
    public async Task ListKeysAsync_PrefixIsExactAndCaseSensitive()
    {
        await SeedAsync();

        Assert.Empty(await QueryableStore.ListKeysAsync("collection", "PART:"));
        Assert.Empty(await QueryableStore.ListKeysAsync("collection", "art:"));
    }

    [Fact]
    public async Task ListKeysAsync_PrefixWildcardCharacters_AreNotWildcards()
    {
        // A `LIKE`-based implementation would treat these as patterns and
        // return every key. They are literal characters in a key.
        await Store.WriteAsync("collection", "100% done", "a");
        await Store.WriteAsync("collection", "1_0", "b");
        await Store.WriteAsync("collection", "other", "c");

        Assert.Equal(new[] { "100% done" }, await QueryableStore.ListKeysAsync("collection", "100%"));
        Assert.Equal(new[] { "1_0" }, await QueryableStore.ListKeysAsync("collection", "1_"));
    }

    [Fact]
    public async Task ListKeysAsync_NeverCrossesACollectionBoundary()
    {
        await SeedAsync();

        var keys = await QueryableStore.ListKeysAsync("collection", "part:");

        Assert.DoesNotContain("part:999", keys);
    }

    [Fact]
    public async Task ReadAllAsync_ReturnsEveryTextRecord_OrderedByKey()
    {
        await SeedAsync();

        var all = await QueryableStore.ReadAllAsync("collection");

        Assert.Equal(
            new[]
            {
                new KeyValuePair<string, string>("part:001", "one"),
                new KeyValuePair<string, string>("part:002", "two"),
                new KeyValuePair<string, string>("requirement:001", "three"),
            },
            all);
    }

    [Fact]
    public async Task ReadAllAsync_EmptyCollection_ReturnsEmpty()
    {
        Assert.Empty(await QueryableStore.ReadAllAsync("never-written"));
    }

    // What ReadAllAsync does with a record written as BYTES is NOT in this
    // contract, and cannot be: the SQLite backend knows which column a
    // record was written into and skips it, while the file backend stores
    // an untagged file and hands back whatever decoding those bytes as
    // text produces. That is a real difference between the two, and it is
    // asserted where it belongs -
    // SqlitePersistenceStoreTests.ARecordWrittenAsText_ReadsAsNoBytes_AndTheReverse
    // for the backend that has the guarantee, and the same class's file
    // backend note for the one that does not.

    [Fact]
    public async Task ReadManyAsync_ReturnsAnEntryForEveryRequestedKey_NullWhereAbsent()
    {
        await SeedAsync();

        var read = await QueryableStore.ReadManyAsync("collection", ["part:001", "requirement:001", "absent"]);

        Assert.Equal(3, read.Count);
        Assert.Equal("one", read["part:001"]);
        Assert.Equal("three", read["requirement:001"]);
        Assert.Null(read["absent"]);
    }

    [Fact]
    public async Task ReadManyAsync_NoKeys_ReturnsEmpty()
    {
        await SeedAsync();

        Assert.Empty(await QueryableStore.ReadManyAsync("collection", []));
    }

    [Fact]
    public async Task ReadManyAsync_ARepeatedKey_AppearsOnce()
    {
        await SeedAsync();

        var read = await QueryableStore.ReadManyAsync("collection", ["part:001", "part:001"]);

        Assert.Single(read);
        Assert.Equal("one", read["part:001"]);
    }

    [Fact]
    public async Task ReadManyAsync_BeyondOneBatch_ReadsEveryKey()
    {
        // The SQLite backend binds keys into an `IN (...)` clause in
        // batches of 500, because SQLite's own parameter limit is 999.
        // 1,200 keys therefore crosses that boundary twice.
        var keys = Enumerable.Range(0, 1200).Select(i => $"key-{i:D5}").ToList();

        // Seeded through the unit-of-work shape rather than 1,200
        // individual writes: on SQLite that is one commit and one fsync
        // instead of 1,200, which is the difference between a test that
        // runs in well under a second and one that does not.
        await QueryableStore.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            foreach (var key in keys)
                await transaction.WriteAsync("bulk", key, $"value-{key}", token);
        });

        var read = await QueryableStore.ReadManyAsync("bulk", keys);

        Assert.Equal(1200, read.Count);
        Assert.All(keys, key => Assert.Equal($"value-{key}", read[key]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMissingCollection_IsRejectedByEveryQuery(string? blank)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => QueryableStore.ListKeysAsync(blank!, string.Empty));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => QueryableStore.ReadAllAsync(blank!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => QueryableStore.ReadManyAsync(blank!, ["key"]));
    }

    [Fact]
    public async Task ANullPrefixKeysOrUnitOfWork_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => QueryableStore.ListKeysAsync("collection", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => QueryableStore.ReadManyAsync("collection", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => QueryableStore.ExecuteInTransactionAsync(null!));
    }

    [Fact]
    public async Task AUnitOfWork_SeesItsOwnWritesThroughItsOwnHandle()
    {
        // True of both backends, for different reasons: on SQLite because
        // the transaction reads its own uncommitted state, on the file
        // store because there is no transaction and the write has simply
        // already landed. The claim a caller writes against is the same.
        await QueryableStore.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            await transaction.WriteAsync("collection", "key", "written", token);
            Assert.Equal("written", await transaction.ReadAsync("collection", "key", token));

            await transaction.WriteBytesAsync("bytes", "key", new byte[] { 4, 5 }, token);
            Assert.Equal(new byte[] { 4, 5 }, await transaction.ReadBytesAsync("bytes", "key", token));

            Assert.Equal(new[] { "key" }, await transaction.ListKeysAsync("collection", string.Empty, token));

            await transaction.DeleteAsync("collection", "key", token);
            Assert.Null(await transaction.ReadAsync("collection", "key", token));
        });

        Assert.Null(await Store.ReadAsync("collection", "key"));
        Assert.Equal(new byte[] { 4, 5 }, await BinaryStore.ReadBytesAsync("bytes", "key"));
    }

    [Fact]
    public async Task AUnitOfWorkThatCompletes_CommitsEveryWrite()
    {
        await QueryableStore.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            for (var i = 0; i < 25; i++)
                await transaction.WriteAsync("collection", $"key-{i:D2}", $"value-{i}", token);
        });

        var all = await QueryableStore.ReadAllAsync("collection");

        Assert.Equal(25, all.Count);
        Assert.Equal("value-0", all[0].Value);
    }
}

/// <summary>The query shape against the file-per-key backend.</summary>
public sealed class FileBackedQueryablePersistenceStoreTests : QueryablePersistenceStoreTests<FileStoreBackend>;

/// <summary>The query shape against the SQLite backend (`ADR-0144`).</summary>
public sealed class SqliteBackedQueryablePersistenceStoreTests : QueryablePersistenceStoreTests<SqliteStoreBackend>;
