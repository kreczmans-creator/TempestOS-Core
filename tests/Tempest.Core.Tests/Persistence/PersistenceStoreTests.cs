using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The store contract: everything <see cref="IPersistenceStore"/> promises
/// that is true of the store rather than of any one backend's storage
/// medium. Run once per backend (`ADR-0144`, `WP 17.1A`) by the two sealed
/// classes at the bottom of this file.
/// </summary>
/// <remarks>
/// What is deliberately NOT here, and lives in
/// <see cref="PersistenceStoreFileSystemTests"/> instead: the injected
/// I/O failures, whose mechanism is a blocked directory or an exclusively
/// held file handle, and the constructor's own root-path resolution, which
/// each backend answers for itself. Everything else in this class was
/// passing against the file store before this Work Package and passes
/// unchanged against SQLite, which is the point of splitting it out.
/// </remarks>
public abstract class PersistenceStoreTests<TBackend> : PersistenceStoreBackendFixture<TBackend>
    where TBackend : IPersistenceStoreBackend, new()
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

/// <summary>The store contract against the file-per-key backend.</summary>
public sealed class FileBackedPersistenceStoreTests : PersistenceStoreTests<FileStoreBackend>;

/// <summary>The store contract against the SQLite backend (`ADR-0144`).</summary>
public sealed class SqliteBackedPersistenceStoreTests : PersistenceStoreTests<SqliteStoreBackend>;

/// <summary>
/// The claims about <see cref="PersistenceStore"/> that are genuinely
/// about a file system, and so are asserted against that backend only.
/// </summary>
/// <remarks>
/// Each of these injects a real, forced I/O failure through the medium
/// itself — a file where a directory must go, a handle held exclusively
/// over a record — and there is no SQLite equivalent that would be the
/// same test rather than a different one wearing its name.
/// <see cref="SqlitePersistenceStoreTests"/> injects the failures that
/// backend can actually have. The constructor tests below are here for a
/// duller reason: the root-path resolution they pin is
/// <see cref="PersistenceStore"/>'s own, and
/// <see cref="SqlitePersistenceStore"/> pins the identical rule in its own
/// file.
/// </remarks>
public class PersistenceStoreFileSystemTests
{
    private static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    /// <summary>
    /// Whether this platform prevents deleting a file that another handle
    /// holds open with <see cref="FileShare.None"/> — determined
    /// empirically, by holding one open and trying.
    /// </summary>
    /// <remarks>
    /// Win32 share modes are mandatory: the open handle blocks the unlink,
    /// and <see cref="PersistenceStore.DeleteAsync"/> surfaces that as
    /// <see cref="PersistenceStoreUnavailableException"/>. POSIX unlink
    /// removes the directory entry regardless of open handles, so the same
    /// delete simply succeeds and the record is gone. Both are correct;
    /// which one happens is the platform's decision, not the store's, so
    /// the test below asserts whichever applies here instead of asserting
    /// the Win32 one everywhere and reporting a false defect on Linux.
    /// Determined by probing rather than by OS name so the answer comes
    /// from the file system actually under the test's temp directory,
    /// which is the thing that decides.
    /// </remarks>
    private static bool DeleteIsBlockedByAnOpenExclusiveHandle(string directory)
    {
        var probe = Path.Combine(directory, "TempestDeleteProbe.tmp");
        File.WriteAllText(probe, "probe");
        try
        {
            using var handle = new FileStream(probe, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            try
            {
                File.Delete(probe);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
        finally
        {
            if (File.Exists(probe))
                File.Delete(probe);
        }
    }

    // ----------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------

    // `WP 17.0A`: this test used to write into, and then recursively delete,
    // PersistenceStore.DefaultRootPath — the real, cwd-relative folder the
    // shipped application keeps a user's data in. Run from the wrong
    // working directory it would have deleted that data. No test in this
    // suite touches the default root any more; the default is asserted as
    // the value the store resolves, against a root it is told to use.
    [Fact]
    public void Constructor_NoRootPathConfigured_ResolvesTheDefaultRootPath()
    {
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build();

        var store = new PersistenceStore(configuration);

        Assert.Equal("persistence-data", PersistenceStore.DefaultRootPath);
        Assert.Equal(PersistenceStore.DefaultRootPath, store.RootPath);
    }

    [Fact]
    public void Constructor_RootPathConfigured_ResolvesThatPath()
    {
        var configured = Path.Combine(Path.GetTempPath(), $"tempest-root-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, configured)]))
            .Build();

        var store = new PersistenceStore(configuration);

        Assert.Equal(configured, store.RootPath);
    }

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PersistenceStore(null!));
    }

    // ----------------------------------------------------------------
    // Failure injection: a real, forced I/O failure, not a fake
    // ----------------------------------------------------------------

    [Fact]
    public async Task WriteAsync_CollectionDirectoryPathIsBlockedByAFile_ThrowsPersistenceStoreUnavailableException()
    {
        using var temp = new TempDirectory();
        var blockedPath = Path.Combine(temp.Path, Uri.EscapeDataString("blocked"));
        File.WriteAllText(blockedPath, "a file where a directory should be");
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => store.WriteAsync("blocked", "key", "value"));
    }

    [Fact]
    public async Task ReadAsync_FileLockedByAnotherHandle_ThrowsPersistenceStoreUnavailableException()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        await store.WriteAsync("collection", "key", "value");
        var filePath = Path.Combine(temp.Path, Uri.EscapeDataString("collection"), Uri.EscapeDataString("key"));

        using var lockingHandle = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => store.ReadAsync("collection", "key"));
    }

    [Fact]
    public async Task DeleteAsync_FileLockedByAnotherHandle_ThrowsOrUnlinksAccordingToThePlatform()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        await store.WriteAsync("collection", "key", "value");
        var filePath = Path.Combine(temp.Path, Uri.EscapeDataString("collection"), Uri.EscapeDataString("key"));

        if (DeleteIsBlockedByAnOpenExclusiveHandle(temp.Path))
        {
            // Win32: the open handle blocks the unlink. The store must
            // report that as its own failure type rather than leaking the
            // IOException.
            using (new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
                    () => store.DeleteAsync("collection", "key"));
            }

            // Checked only after the handle is released, because
            // FileShare.None blocks the read as well — that is what
            // ReadAsync_FileLockedByAnotherHandle asserts two tests above,
            // and the first version of this branch asserted the surviving
            // record while still holding the lock, so it failed on Windows
            // for its own reasons rather than the store's. The claim that
            // matters is this one: a delete that did not happen must never
            // look like one that did.
            Assert.Equal("value", await store.ReadAsync("collection", "key"));
            return;
        }

        // POSIX: unlink removes the directory entry whatever handles are
        // open, so the delete genuinely succeeds. The assertion that
        // matters is that the store agrees the record is gone afterwards,
        // rather than reporting a stale one from a file that no longer has
        // a name.
        using (new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await store.DeleteAsync("collection", "key");
        }

        Assert.Null(await store.ReadAsync("collection", "key"));
        Assert.DoesNotContain("key", await store.ListKeysAsync("collection"));
    }
}
