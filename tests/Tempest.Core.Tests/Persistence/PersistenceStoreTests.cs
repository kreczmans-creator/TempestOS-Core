using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

public class PersistenceStoreTests
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
    // Round-trip correctness
    // ----------------------------------------------------------------

    [Fact]
    public async Task WriteThenRead_ReturnsTheWrittenValue()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "key", "value");
        var result = await store.ReadAsync("collection", "key");

        Assert.Equal("value", result);
    }

    [Fact]
    public async Task WriteTwice_Overwrites_ReadReturnsLatestValue()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "key", "first");
        await store.WriteAsync("collection", "key", "second");
        var result = await store.ReadAsync("collection", "key");

        Assert.Equal("second", result);
    }

    [Fact]
    public async Task ReadAsync_KeyNeverWritten_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        var result = await store.ReadAsync("collection", "nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ExistingKey_RemovesIt()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        await store.WriteAsync("collection", "key", "value");

        await store.DeleteAsync("collection", "key");
        var result = await store.ReadAsync("collection", "key");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_KeyNeverWritten_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        var exception = await Record.ExceptionAsync(() => store.DeleteAsync("collection", "nonexistent"));

        Assert.Null(exception);
    }

    // ----------------------------------------------------------------
    // ListKeysAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task ListKeysAsync_CollectionNeverWritten_ReturnsEmpty()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        var keys = await store.ListKeysAsync("nonexistent-collection");

        Assert.Empty(keys);
    }

    [Fact]
    public async Task ListKeysAsync_ReturnsEveryWrittenKey()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        await store.WriteAsync("collection", "alpha", "1");
        await store.WriteAsync("collection", "beta", "2");

        var keys = await store.ListKeysAsync("collection");

        Assert.Equal(2, keys.Count);
        Assert.Contains("alpha", keys);
        Assert.Contains("beta", keys);
    }

    [Fact]
    public async Task ListKeysAsync_AfterDelete_NoLongerIncludesTheDeletedKey()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        await store.WriteAsync("collection", "alpha", "1");
        await store.WriteAsync("collection", "beta", "2");

        await store.DeleteAsync("collection", "alpha");
        var keys = await store.ListKeysAsync("collection");

        Assert.DoesNotContain("alpha", keys);
        Assert.Contains("beta", keys);
    }

    // ----------------------------------------------------------------
    // Collection-scoping isolation
    // ----------------------------------------------------------------

    [Fact]
    public async Task SameKeyInDifferentCollections_AreIndependent()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection-a", "key", "value-a");
        await store.WriteAsync("collection-b", "key", "value-b");

        Assert.Equal("value-a", await store.ReadAsync("collection-a", "key"));
        Assert.Equal("value-b", await store.ReadAsync("collection-b", "key"));
    }

    [Fact]
    public async Task ListKeysAsync_NeverIncludesAKeyFromAnotherCollection()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection-a", "shared-key", "value-a");
        await store.WriteAsync("collection-b", "other-key", "value-b");

        var keysInA = await store.ListKeysAsync("collection-a");

        Assert.DoesNotContain("other-key", keysInA);
    }

    // ----------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------

    [Fact]
    public async Task Constructor_NoRootPathConfigured_UsesDefaultRootPath()
    {
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build();
        var store = new PersistenceStore(configuration);
        var defaultDirectory = Path.Combine(PersistenceStore.DefaultRootPath, Uri.EscapeDataString("wp64-config-default-test"));

        try
        {
            await store.WriteAsync("wp64-config-default-test", "key", "value");

            Assert.True(Directory.Exists(defaultDirectory));
        }
        finally
        {
            if (Directory.Exists(PersistenceStore.DefaultRootPath))
                Directory.Delete(PersistenceStore.DefaultRootPath, recursive: true);
        }
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

    // ----------------------------------------------------------------
    // Thread safety / concurrency
    // ----------------------------------------------------------------

    [Fact]
    public async Task ConcurrentWritesToDifferentKeys_InTheSameCollection_DoNotCorruptEachOther()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        var keys = Enumerable.Range(0, 20).Select(i => $"key-{i}").ToList();

        await Task.WhenAll(keys.Select(key => store.WriteAsync("collection", key, $"value-{key}")));

        foreach (var key in keys)
            Assert.Equal($"value-{key}", await store.ReadAsync("collection", key));
    }

    [Fact]
    public async Task ConcurrentWritesToTheSameKey_NeverThrowsAndReadReturnsOneOfTheWrittenValues()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        var values = Enumerable.Range(0, 20).Select(i => $"value-{i}").ToList();

        await Task.WhenAll(values.Select(value => store.WriteAsync("collection", "key", value)));
        var result = await store.ReadAsync("collection", "key");

        Assert.Contains(result, values);
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
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.ReadAsync(collection!, "key"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_NullEmptyOrWhitespaceKey_ThrowsArgumentException(string? key)
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.ReadAsync("collection", key!));
    }

    [Fact]
    public async Task WriteAsync_NullValue_ThrowsArgumentNullException()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.WriteAsync("collection", "key", null!));
    }
}
