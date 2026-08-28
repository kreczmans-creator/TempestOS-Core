using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// `TD-59` closure tests against the REAL <see cref="PersistenceStore"/>
/// on a real file system — reserved Win32 device names, their case and
/// extension variants, trailing dots, dot-names, and path-traversal
/// shapes must all be unambiguously representable, round-trip through
/// <see cref="IPersistenceStore.ListKeysAsync"/>, and never escape the
/// store root or collapse into a missing or aliased record.
/// </summary>
public class PersistenceStoreHostileNameTests
{
    private static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    // ----------------------------------------------------------------
    // Reserved device names (`TD-59`'s original failure shape)
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("NUL")]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    [InlineData("con")]
    [InlineData("Con")]
    [InlineData("CON.txt")]
    [InlineData("con.json")]
    [InlineData("NUL.tar.gz")]
    public async Task ReservedDeviceNameKey_RoundTripsThroughWriteReadListDelete(string key)
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", key, "value-" + key);

        Assert.Equal("value-" + key, await store.ReadAsync("collection", key));
        var keys = await store.ListKeysAsync("collection");
        Assert.Equal(1, keys.Count(k => k == key));

        await store.DeleteAsync("collection", key);
        Assert.Null(await store.ReadAsync("collection", key));
        Assert.DoesNotContain(key, await store.ListKeysAsync("collection"));
    }

    [Theory]
    [InlineData("NUL")]
    [InlineData("CON")]
    [InlineData("con.json")]
    [InlineData("COM1")]
    public async Task ReservedDeviceNameKey_NeverProducesAReservedFileName(string key)
    {
        // The Windows failure (writes routed to a device, File.Exists
        // false, record silently missing) cannot reproduce on this test
        // host — so this asserts the cross-platform invariant that makes
        // it impossible: no file the store creates has a reserved stem.
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", key, "value");

        var files = Directory.GetFiles(Path.Combine(temp.Path, "collection")).Select(Path.GetFileName).ToList();
        Assert.Single(files);
        var stem = files[0]!.Split('.')[0];
        Assert.DoesNotMatch("^(?i)(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", stem);
    }

    [Fact]
    public async Task ReservedDeviceNameCaseVariants_AreDistinctRecords()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "CON", "upper");
        await store.WriteAsync("collection", "con", "lower");
        await store.WriteAsync("collection", "Con", "mixed");

        Assert.Equal("upper", await store.ReadAsync("collection", "CON"));
        Assert.Equal("lower", await store.ReadAsync("collection", "con"));
        Assert.Equal("mixed", await store.ReadAsync("collection", "Con"));
        Assert.Equal(3, (await store.ListKeysAsync("collection")).Count);
    }

    [Fact]
    public async Task ValidIdentifiersAdjacentToReservedNames_KeepTheirPlainEncoding()
    {
        // "CONX", "XCON", "COM10", "LPT" are NOT reserved — they must
        // keep encoding exactly as before the `TD-59` fix, so existing
        // stores stay readable.
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        foreach (var key in new[] { "CONX", "XCON", "COM10", "LPT", "CONsole" })
            await store.WriteAsync("collection", key, "v-" + key);

        var files = Directory.GetFiles(Path.Combine(temp.Path, "collection")).Select(Path.GetFileName).ToList();
        foreach (var key in new[] { "CONX", "XCON", "COM10", "LPT", "CONsole" })
            Assert.Contains(key, files);

        foreach (var key in new[] { "CONX", "XCON", "COM10", "LPT", "CONsole" })
            Assert.Equal("v-" + key, await store.ReadAsync("collection", key));
    }

    // ----------------------------------------------------------------
    // Trailing dots and dot-names (Win32 strips trailing dots; "." and
    // ".." are directory navigation, not file names)
    // ----------------------------------------------------------------

    [Fact]
    public async Task TrailingDotKey_IsDistinctFromItsDotlessSibling()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "Rev1", "plain");
        await store.WriteAsync("collection", "Rev1.", "dotted");

        Assert.Equal("plain", await store.ReadAsync("collection", "Rev1"));
        Assert.Equal("dotted", await store.ReadAsync("collection", "Rev1."));
        Assert.Equal(2, (await store.ListKeysAsync("collection")).Count);

        // The dotted key's file must not literally end in a dot (Win32
        // would strip it, silently aliasing the two records).
        var files = Directory.GetFiles(Path.Combine(temp.Path, "collection")).Select(Path.GetFileName).ToList();
        Assert.All(files, f => Assert.False(f!.EndsWith('.'), $"'{f}' ends with a dot"));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    public async Task DotOnlyKeys_RoundTrip(string key)
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", key, "value-" + key.Length);

        Assert.Equal("value-" + key.Length, await store.ReadAsync("collection", key));
        Assert.Contains(key, await store.ListKeysAsync("collection"));
    }

    // ----------------------------------------------------------------
    // Path traversal / separator injection (the escaping guard itself —
    // previously entirely unpinned by any test)
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("/etc/passwd")]
    [InlineData("%2e%2e%2fescape")]
    public async Task TraversalShapedKey_StaysInsideItsCollectionDirectory_AndRoundTrips(string key)
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", key, "contained");

        Assert.Equal("contained", await store.ReadAsync("collection", key));
        Assert.Contains(key, await store.ListKeysAsync("collection"));

        // Exactly one file, inside the collection directory; nothing
        // anywhere else under (or beside) the root.
        var collectionDirectory = Path.Combine(temp.Path, "collection");
        Assert.Single(Directory.GetFiles(collectionDirectory));
        Assert.Single(Directory.GetDirectories(temp.Path));
        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("..")]
    public async Task ReservedCollectionName_RoundTrips(string collection)
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync(collection, "key", "value");

        Assert.Equal("value", await store.ReadAsync(collection, "key"));
        Assert.Contains("key", await store.ListKeysAsync(collection));
    }

    // ----------------------------------------------------------------
    // Legacy-encoding migration: a record persisted before the `TD-59`
    // encoding change (possible only on POSIX file systems) must stay
    // readable, and migrate forward on the next write.
    // ----------------------------------------------------------------

    [Fact]
    public async Task LegacyPlainEscapedReservedNameFile_IsStillReadable_AndMigratesOnWrite()
    {
        using var temp = new TempDirectory();
        var collectionDirectory = Path.Combine(temp.Path, "collection");
        Directory.CreateDirectory(collectionDirectory);

        // Simulate a pre-fix store: key "CON" persisted under the plain
        // Uri.EscapeDataString encoding (the literal file name "CON").
        await File.WriteAllTextAsync(Path.Combine(collectionDirectory, "CON"), "legacy-value");

        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        Assert.Equal("legacy-value", await store.ReadAsync("collection", "CON"));
        Assert.Contains("CON", await store.ListKeysAsync("collection"));

        await store.WriteAsync("collection", "CON", "new-value");

        Assert.Equal("new-value", await store.ReadAsync("collection", "CON"));
        Assert.False(File.Exists(Path.Combine(collectionDirectory, "CON")), "legacy file should be migrated away");
        Assert.Equal(1, (await store.ListKeysAsync("collection")).Count(k => k == "CON"));

        await store.DeleteAsync("collection", "CON");
        Assert.Null(await store.ReadAsync("collection", "CON"));
    }

    [Fact]
    public async Task LegacyFileAndMigratedFileCoexisting_ListReportsTheKeyOnce()
    {
        using var temp = new TempDirectory();
        var collectionDirectory = Path.Combine(temp.Path, "collection");
        Directory.CreateDirectory(collectionDirectory);
        await File.WriteAllTextAsync(Path.Combine(collectionDirectory, "CON"), "legacy");
        await File.WriteAllTextAsync(Path.Combine(collectionDirectory, "%43ON"), "migrated");

        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        Assert.Equal(1, (await store.ListKeysAsync("collection")).Count(k => k == "CON"));

        // The migrated (current-encoding) file wins on read.
        Assert.Equal("migrated", await store.ReadAsync("collection", "CON"));
    }

    // ----------------------------------------------------------------
    // Atomic-write hygiene
    // ----------------------------------------------------------------

    [Fact]
    public async Task Writes_LeaveNoTemporaryFilesBehind()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        for (var i = 0; i < 10; i++)
            await store.WriteAsync("collection", $"key-{i}", $"value-{i}");
        await store.WriteAsync("collection", "key-0", "overwritten");

        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
        Assert.Empty(Directory.GetFiles(temp.Path));
        Assert.Equal("overwritten", await store.ReadAsync("collection", "key-0"));
    }

    // ----------------------------------------------------------------
    // Case-exactness: a lookup never returns another key's record
    // ----------------------------------------------------------------

    [Fact]
    public async Task CaseVariantKeys_AreIndependentRecords()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "Steel", "capitalised");
        await store.WriteAsync("collection", "steel", "lowercase");

        Assert.Equal("capitalised", await store.ReadAsync("collection", "Steel"));
        Assert.Equal("lowercase", await store.ReadAsync("collection", "steel"));

        await store.DeleteAsync("collection", "steel");
        Assert.Equal("capitalised", await store.ReadAsync("collection", "Steel"));
        Assert.Null(await store.ReadAsync("collection", "steel"));
    }

    [Fact]
    public async Task ReadAsync_NeverReturnsACaseVariantsRecord()
    {
        // On this (case-sensitive) file system the OS already keeps the
        // records apart; this pins the exact-name matching that makes
        // the same lookup correct on case-insensitive file systems too.
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "Steel", "capitalised");

        Assert.Null(await store.ReadAsync("collection", "steel"));
        Assert.Null(await store.ReadAsync("collection", "STEEL"));
    }
}
