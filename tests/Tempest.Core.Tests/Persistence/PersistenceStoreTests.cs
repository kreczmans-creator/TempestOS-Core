using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The store contract: everything <see cref="IPersistenceStore"/> promises
/// that is true of the store itself, against <see cref="SqlitePersistenceStore"/>
/// (`ADR-0144`, `WP 17.1A`).
/// </summary>
/// <remarks>
/// Re-pointed from the deleted file-per-key store (`WP 18.1A`): this ran
/// once per backend while that store still shipped. The forced-I/O-failure
/// and constructor root-path-resolution cases that were genuinely about a
/// file system (blocked directories, exclusively held handles) were split
/// into <c>PersistenceStoreFileSystemTests</c> and are deleted with that
/// store; <see cref="SqlitePersistenceStoreTests"/> already pins this
/// backend's own root-path resolution and its own failure modes.
/// Everything below is unchanged from what passed against the file store.
/// </remarks>
public sealed class PersistenceStoreTests : SqlitePersistenceStoreFixture
{
    // ----------------------------------------------------------------
    // Round-trip correctness
    // ----------------------------------------------------------------

    [Fact]
    public async Task WriteThenRead_ReturnsTheWrittenValue()
    {
        await Store.WriteAsync("collection", "key", "value");
        var result = await Store.ReadAsync("collection", "key");

        Assert.Equal("value", result);
    }

    [Fact]
    public async Task WriteTwice_Overwrites_ReadReturnsLatestValue()
    {
        await Store.WriteAsync("collection", "key", "first");
        await Store.WriteAsync("collection", "key", "second");
        var result = await Store.ReadAsync("collection", "key");

        Assert.Equal("second", result);
    }

    [Fact]
    public async Task ReadAsync_KeyNeverWritten_ReturnsNull()
    {
        var result = await Store.ReadAsync("collection", "nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ExistingKey_RemovesIt()
    {
        await Store.WriteAsync("collection", "key", "value");

        await Store.DeleteAsync("collection", "key");
        var result = await Store.ReadAsync("collection", "key");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_KeyNeverWritten_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => Store.DeleteAsync("collection", "nonexistent"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task AnEmptyStringValue_IsStoredAndIsNotTheSameAsNoRecord()
    {
        await Store.WriteAsync("collection", "empty", string.Empty);

        Assert.Equal(string.Empty, await Store.ReadAsync("collection", "empty"));
        Assert.Null(await Store.ReadAsync("collection", "never-written"));
        Assert.Contains("empty", await Store.ListKeysAsync("collection"));
    }

    // ----------------------------------------------------------------
    // ListKeysAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task ListKeysAsync_CollectionNeverWritten_ReturnsEmpty()
    {
        var keys = await Store.ListKeysAsync("nonexistent-collection");

        Assert.Empty(keys);
    }

    [Fact]
    public async Task ListKeysAsync_ReturnsEveryWrittenKey()
    {
        await Store.WriteAsync("collection", "alpha", "1");
        await Store.WriteAsync("collection", "beta", "2");

        var keys = await Store.ListKeysAsync("collection");

        Assert.Equal(2, keys.Count);
        Assert.Contains("alpha", keys);
        Assert.Contains("beta", keys);
    }

    [Fact]
    public async Task ListKeysAsync_AfterDelete_NoLongerIncludesTheDeletedKey()
    {
        await Store.WriteAsync("collection", "alpha", "1");
        await Store.WriteAsync("collection", "beta", "2");

        await Store.DeleteAsync("collection", "alpha");
        var keys = await Store.ListKeysAsync("collection");

        Assert.DoesNotContain("alpha", keys);
        Assert.Contains("beta", keys);
    }

    // ----------------------------------------------------------------
    // Collection-scoping isolation
    // ----------------------------------------------------------------

    [Fact]
    public async Task SameKeyInDifferentCollections_AreIndependent()
    {
        await Store.WriteAsync("collection-a", "key", "value-a");
        await Store.WriteAsync("collection-b", "key", "value-b");

        Assert.Equal("value-a", await Store.ReadAsync("collection-a", "key"));
        Assert.Equal("value-b", await Store.ReadAsync("collection-b", "key"));
    }

    [Fact]
    public async Task ListKeysAsync_NeverIncludesAKeyFromAnotherCollection()
    {
        await Store.WriteAsync("collection-a", "shared-key", "value-a");
        await Store.WriteAsync("collection-b", "other-key", "value-b");

        var keysInA = await Store.ListKeysAsync("collection-a");

        Assert.DoesNotContain("other-key", keysInA);
    }

    // ----------------------------------------------------------------
    // Durability across store instances: a value written by one store
    // over a root is readable by the next one over the same root. The
    // whole point of the thing, and previously asserted only indirectly,
    // through a Host restart.
    // ----------------------------------------------------------------

    [Fact]
    public async Task AValueWritten_IsReadableByAFreshStoreOverTheSameRoot()
    {
        await Store.WriteAsync("collection", "key", "durable");
        await BinaryStore.WriteBytesAsync("bytes", "key", new byte[] { 7, 8, 9 });

        // The first store must let go of the root before the second opens
        // it: SQLite holds an exclusive instance lock for its lifetime.
        ReleaseStores();

        var reopened = NewStore();

        Assert.Equal("durable", await reopened.ReadAsync("collection", "key"));
        Assert.Equal(new byte[] { 7, 8, 9 }, await ((IBinaryPersistenceStore)reopened).ReadBytesAsync("bytes", "key"));
    }

    // ----------------------------------------------------------------
    // Thread safety / concurrency
    // ----------------------------------------------------------------

    [Fact]
    public async Task ConcurrentWritesToDifferentKeys_InTheSameCollection_DoNotCorruptEachOther()
    {
        var keys = Enumerable.Range(0, 20).Select(i => $"key-{i}").ToList();

        await Task.WhenAll(keys.Select(key => Store.WriteAsync("collection", key, $"value-{key}")));

        foreach (var key in keys)
            Assert.Equal($"value-{key}", await Store.ReadAsync("collection", key));
    }

    [Fact]
    public async Task ConcurrentWritesToTheSameKey_NeverThrowsAndReadReturnsOneOfTheWrittenValues()
    {
        var values = Enumerable.Range(0, 20).Select(i => $"value-{i}").ToList();

        await Task.WhenAll(values.Select(value => Store.WriteAsync("collection", "key", value)));
        var result = await Store.ReadAsync("collection", "key");

        Assert.Contains(result, values);
    }

    [Fact]
    public async Task FiftyWritersToOneKeyAndFiftyToDistinctKeys_AllLandWholeAndNoneInterleave()
    {
        // `WP 17.1A`: the file store made this true with a per-key
        // AsyncKeyedLock; SQLite makes it true because a write is one
        // statement, serialised by the database's own single-writer lock
        // and waited on for up to `busy_timeout`. The claim is identical
        // and is asserted identically, which is the only way to know the
        // new backend did not quietly lose it.
        var contendedValues = Enumerable.Range(0, 50).Select(i => $"contended-{i}").ToList();
        var distinctKeys = Enumerable.Range(0, 50).Select(i => $"distinct-{i}").ToList();

        var writers = contendedValues
            .Select(value => Store.WriteAsync("collection", "one-key", value))
            .Concat(distinctKeys.Select(key => Store.WriteAsync("collection", key, $"value-{key}")))
            .ToList();

        await Task.WhenAll(writers);

        // The contended key holds exactly one of the fifty values, whole -
        // never a blend of two, and never a torn or empty record.
        var contended = await Store.ReadAsync("collection", "one-key");
        Assert.Contains(contended, contendedValues);

        // Every uncontended writer landed its own value.
        foreach (var key in distinctKeys)
            Assert.Equal($"value-{key}", await Store.ReadAsync("collection", key));

        Assert.Equal(51, (await Store.ListKeysAsync("collection")).Count);
    }

    [Fact]
    public async Task OneStoreInstance_ReadsAndWritesConcurrentlyFromManyThreads()
    {
        // Cross-thread safety of a single instance, asserted directly:
        // readers and writers interleaved on purpose, all through the one
        // object the Host registers.
        await Store.WriteAsync("collection", "seed", "seed-value");

        var work = Enumerable.Range(0, 40).Select(i => Task.Run(async () =>
        {
            if (i % 2 == 0)
            {
                await Store.WriteAsync("collection", $"key-{i}", $"value-{i}");
                return;
            }

            Assert.Equal("seed-value", await Store.ReadAsync("collection", "seed"));
            _ = await Store.ListKeysAsync("collection");
        }));

        await Task.WhenAll(work);

        for (var i = 0; i < 40; i += 2)
            Assert.Equal($"value-{i}", await Store.ReadAsync("collection", $"key-{i}"));
    }

    // ----------------------------------------------------------------
    // Argument validation
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_NullEmptyOrWhitespaceCollection_ThrowsArgumentException(string? collection)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Store.ReadAsync(collection!, "key"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_NullEmptyOrWhitespaceKey_ThrowsArgumentException(string? key)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Store.ReadAsync("collection", key!));
    }

    [Fact]
    public async Task WriteAsync_NullValue_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Store.WriteAsync("collection", "key", null!));
    }
}
