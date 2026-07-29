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
    public async Task DeleteAsync_FileLockedByAnotherHandle_ThrowsPersistenceStoreUnavailableException()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        await store.WriteAsync("collection", "key", "value");
        var filePath = Path.Combine(temp.Path, Uri.EscapeDataString("collection"), Uri.EscapeDataString("key"));

        using var lockingHandle = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => store.DeleteAsync("collection", "key"));
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
