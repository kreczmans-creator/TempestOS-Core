using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// Hostile names, against a real store on real storage — reserved Win32
/// device names, their case and extension variants, trailing dots,
/// dot-names, path-traversal shapes and arbitrary Unicode must all be
/// unambiguously representable, round-trip through
/// <see cref="IPersistenceStore.ListKeysAsync"/>, and never collapse into
/// a missing or aliased record. Run once per backend (`ADR-0144`,
/// `WP 17.1A`).
/// </summary>
/// <remarks>
/// <para>
/// These began as `TD-59` closure tests against the file-per-key store,
/// where every name in them was a file name and each was a real hazard.
/// On the SQLite backend a key is a value in a column, so none of these
/// names is dangerous any more — which is precisely why the tests still
/// run there: the claim was never "the encoding is correct", it was "the
/// caller's key comes back", and that claim outlives the encoding.
/// </para>
/// <para>
/// What the two backends genuinely disagree about — the legacy-encoding
/// fallback, and what happens to two keys differing only in case on a
/// case-insensitive file system — is in
/// <see cref="PersistenceStoreHostileNameFileSystemTests"/> below, and is
/// about a file system rather than about a store.
/// </para>
/// </remarks>
public abstract class PersistenceStoreHostileNameTests<TBackend> : PersistenceStoreBackendFixture<TBackend>
    where TBackend : IPersistenceStoreBackend, new()
{
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
        await Store.WriteAsync("collection", key, "value-" + key);

        Assert.Equal("value-" + key, await Store.ReadAsync("collection", key));
        var keys = await Store.ListKeysAsync("collection");
        Assert.Equal(1, keys.Count(k => k == key));

        await Store.DeleteAsync("collection", key);
        Assert.Null(await Store.ReadAsync("collection", key));
        Assert.DoesNotContain(key, await Store.ListKeysAsync("collection"));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("..")]
    public async Task ReservedCollectionName_RoundTrips(string collection)
    {
        await Store.WriteAsync(collection, "key", "value");

        Assert.Equal("value", await Store.ReadAsync(collection, "key"));
        Assert.Contains("key", await Store.ListKeysAsync(collection));
    }

    // ----------------------------------------------------------------
    // Trailing dots and dot-names (Win32 strips trailing dots; "." and
    // ".." are directory navigation, not file names)
    // ----------------------------------------------------------------

    [Fact]
    public async Task TrailingDotKey_IsDistinctFromItsDotlessSibling()
    {
        await Store.WriteAsync("collection", "Rev1", "plain");
        await Store.WriteAsync("collection", "Rev1.", "dotted");

        Assert.Equal("plain", await Store.ReadAsync("collection", "Rev1"));
        Assert.Equal("dotted", await Store.ReadAsync("collection", "Rev1."));
        Assert.Equal(2, (await Store.ListKeysAsync("collection")).Count);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    public async Task DotOnlyKeys_RoundTrip(string key)
    {
        await Store.WriteAsync("collection", key, "value-" + key.Length);

        Assert.Equal("value-" + key.Length, await Store.ReadAsync("collection", key));
        Assert.Contains(key, await Store.ListKeysAsync("collection"));
    }

    // ----------------------------------------------------------------
    // Path traversal / separator injection
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("/etc/passwd")]
    [InlineData("%2e%2e%2fescape")]
    public async Task TraversalShapedKey_RoundTripsAsAnOrdinaryKey(string key)
    {
        await Store.WriteAsync("collection", key, "contained");

        Assert.Equal("contained", await Store.ReadAsync("collection", key));
        Assert.Contains(key, await Store.ListKeysAsync("collection"));
        Assert.Single(await Store.ListKeysAsync("collection"));
    }

    // ----------------------------------------------------------------
    // Names no encoding scheme had to care about, and one now does not
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("Fußplatte")]
    [InlineData("支持板")]
    [InlineData("Ø-42×3 CHS")]
    [InlineData("key with spaces")]
    [InlineData("key\twith\ttabs")]
    [InlineData("key\nwith\nnewlines")]
    [InlineData("100% pinned")]
    [InlineData("a+b=c&d?e#f")]
    [InlineData("'; DROP TABLE records; --")]
    public async Task AnArbitraryUnicodeOrPunctuatedKey_RoundTrips(string key)
    {
        // The SQL-injection-shaped entry is not decoration: every statement
        // in `SqlitePersistenceStore` binds its collection and key as
        // parameters, and a test that only ever passed identifier-shaped
        // keys would not notice the day one of them stopped.
        await Store.WriteAsync("collection", key, "value");

        Assert.Equal("value", await Store.ReadAsync("collection", key));
        Assert.Contains(key, await Store.ListKeysAsync("collection"));

        await Store.DeleteAsync("collection", key);
        Assert.Empty(await Store.ListKeysAsync("collection"));
    }

    // ----------------------------------------------------------------
    // Case-exactness: a lookup never returns another key's record
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReadAsync_NeverReturnsACaseVariantsRecord()
    {
        await Store.WriteAsync("collection", "Steel", "capitalised");

        Assert.Null(await Store.ReadAsync("collection", "steel"));
        Assert.Null(await Store.ReadAsync("collection", "STEEL"));
    }
}

/// <summary>Hostile names against the file-per-key backend.</summary>
public sealed class FileBackedPersistenceStoreHostileNameTests : PersistenceStoreHostileNameTests<FileStoreBackend>;

/// <summary>Hostile names against the SQLite backend (`ADR-0144`).</summary>
public sealed class SqliteBackedPersistenceStoreHostileNameTests : PersistenceStoreHostileNameTests<SqliteStoreBackend>;

/// <summary>
/// The hostile-name claims that are about a FILE SYSTEM rather than about
/// a store, and so are asserted against <see cref="PersistenceStore"/>
/// only.
/// </summary>
/// <remarks>
/// <para>
/// Three groups, and each is file-store-only for the same reason: the
/// behaviour it pins exists because a key had to become a file name.
/// </para>
/// <list type="bullet">
/// <item><description><b>The encoding itself</b> — that no file the store creates has a reserved Win32 stem or a terminal dot, that a name adjacent to a reserved one still encodes plainly, and that a traversal-shaped key never leaves its collection directory. `ADR-0144` deletes the whole mechanism: a SQLite key is a column value and produces no path at all.</description></item>
/// <item><description><b>The legacy-path fallback</b> — a record written under the pre-`TD-59` encoding stays readable and migrates forward. There has never been a legacy SQLite encoding to fall back from; the database is created at schema version 1 by this Work Package.</description></item>
/// <item><description><b>The case-insensitive collision refusal</b> — where the file system cannot hold <c>Steel</c> and <c>steel</c> apart, the store refuses the second write rather than silently discarding the first key's record. On SQLite they are two rows under the BINARY collation, so there is no collision to refuse; the SQLite counterpart is asserted in <see cref="SqlitePersistenceStoreTests"/> as the plain statement that they are two distinct records.</description></item>
/// </list>
/// </remarks>
public class PersistenceStoreHostileNameFileSystemTests
{
    private static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    /// <summary>
    /// Whether the file system under <paramref name="path"/> is
    /// case-insensitive — determined empirically, by creating a file and
    /// asking for it back under a different case.
    /// </summary>
    /// <remarks>
    /// Never inferred from the OS name: macOS's default volume is
    /// case-insensitive, and a Windows directory can be marked
    /// case-sensitive (`fsutil file setCaseSensitiveInfo`, Windows 10
    /// 1803 onward), so "is this Windows" answers a different question
    /// from the one that matters. `TD-59` exists precisely because this
    /// property differs between the file systems this platform runs on,
    /// and <see cref="PersistenceStore.WriteAsync"/> has two documented,
    /// deliberately different behaviours across it: where the file system
    /// keeps two case-variant keys apart, so does the store; where it
    /// cannot, the store refuses the second write rather than silently
    /// discarding the first key's record. The tests below assert whichever
    /// of the two applies here, so each is a real assertion on both
    /// platforms instead of a POSIX-only claim that reports a false defect
    /// on Windows.
    /// </remarks>
    private static bool IsCaseInsensitiveFileSystem(string path)
    {
        var probe = Path.Combine(path, "TempestCaseProbe.tmp");
        File.WriteAllText(probe, "probe");
        try
        {
            return File.Exists(Path.Combine(path, "tempestcaseprobe.tmp"));
        }
        finally
        {
            File.Delete(probe);
        }
    }

    /// <summary>
    /// Whether a file with the literal name <paramref name="name"/> can
    /// actually be created in <paramref name="directory"/> and read back.
    /// </summary>
    /// <remarks>
    /// On Win32 a path whose stem is a reserved device name is routed to
    /// the device rather than the file system, so the write appears to
    /// succeed and no file exists afterwards — the very failure `TD-59`
    /// was raised for. A test that needs such a file as its *fixture*
    /// therefore cannot run there, and must say so rather than fail.
    /// </remarks>
    private static bool CanCreateFileNamed(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        try
        {
            File.WriteAllText(candidate, "probe");
            return File.Exists(candidate) && File.ReadAllText(candidate) == "probe";
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(candidate))
                    File.Delete(candidate);
            }
            catch (IOException)
            {
                // Best effort: the probe lives in a per-test temp
                // directory that is deleted wholesale either way.
            }
        }
    }

    // ----------------------------------------------------------------
    // The encoding itself
    // ----------------------------------------------------------------

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

    [Fact]
    public async Task TrailingDotKey_ProducesNoFileNameEndingInADot()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "Rev1", "plain");
        await store.WriteAsync("collection", "Rev1.", "dotted");

        // The dotted key's file must not literally end in a dot (Win32
        // would strip it, silently aliasing the two records).
        var files = Directory.GetFiles(Path.Combine(temp.Path, "collection")).Select(Path.GetFileName).ToList();
        Assert.All(files, f => Assert.False(f!.EndsWith('.'), $"'{f}' ends with a dot"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("/etc/passwd")]
    [InlineData("%2e%2e%2fescape")]
    public async Task TraversalShapedKey_StaysInsideItsCollectionDirectory(string key)
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", key, "contained");

        // Exactly one file, inside the collection directory; nothing
        // anywhere else under (or beside) the root.
        var collectionDirectory = Path.Combine(temp.Path, "collection");
        Assert.Single(Directory.GetFiles(collectionDirectory));
        Assert.Single(Directory.GetDirectories(temp.Path));
        Assert.Empty(Directory.GetFiles(temp.Path));
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

        if (!CanCreateFileNamed(collectionDirectory, "CON"))
        {
            // This fixture is a pre-`TD-59` store, and a pre-`TD-59` store
            // containing a literal "CON" file can only ever have been
            // written on a file system that permits the name. Where it
            // does not — Win32 routes the path to the console device —
            // the legacy record this test migrates cannot exist, so the
            // path under test is unreachable rather than broken. Assert
            // that reason, which is `TD-59`'s own premise, instead of
            // asserting a fixture the platform refused to create.
            Assert.False(File.Exists(Path.Combine(collectionDirectory, "CON")));
            return;
        }

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

        if (!CanCreateFileNamed(collectionDirectory, "CON"))
        {
            // Same unreachable fixture as the migration test above, and
            // the reason this one needs the guard even though it was
            // passing: without a literal "CON" file the two records never
            // coexist, so the test was asserting "reported once" over a
            // directory holding exactly one file — green, and about
            // nothing. Found while fixing its neighbours; a test that
            // cannot fail is worse than one that does.
            Assert.False(File.Exists(Path.Combine(collectionDirectory, "CON")));
            return;
        }

        await File.WriteAllTextAsync(Path.Combine(collectionDirectory, "CON"), "legacy");
        await File.WriteAllTextAsync(Path.Combine(collectionDirectory, "%43ON"), "migrated");

        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        Assert.Equal(1, (await store.ListKeysAsync("collection")).Count(k => k == "CON"));

        // The migrated (current-encoding) file wins on read.
        Assert.Equal("migrated", await store.ReadAsync("collection", "CON"));
    }

    // ----------------------------------------------------------------
    // Case-insensitive collision refusal
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReservedDeviceNameCaseVariants_AreDistinctRecords_OrTheCollidingWriteIsRefused()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "CON", "upper");

        if (IsCaseInsensitiveFileSystem(temp.Path))
        {
            // "CON" and "Con" encode to file names differing only in
            // case, so one physical file backs both keys and the store
            // cannot keep them apart. What it must never do is overwrite:
            // "CON"'s record survives intact, and "Con" reads as the
            // absent record it is, rather than silently returning
            // another key's value.
            await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
                () => store.WriteAsync("collection", "Con", "mixed"));

            Assert.Equal("upper", await store.ReadAsync("collection", "CON"));
            Assert.Null(await store.ReadAsync("collection", "Con"));
            Assert.Single(await store.ListKeysAsync("collection"));
            return;
        }

        await store.WriteAsync("collection", "con", "lower");
        await store.WriteAsync("collection", "Con", "mixed");

        Assert.Equal("upper", await store.ReadAsync("collection", "CON"));
        Assert.Equal("lower", await store.ReadAsync("collection", "con"));
        Assert.Equal("mixed", await store.ReadAsync("collection", "Con"));
        Assert.Equal(3, (await store.ListKeysAsync("collection")).Count);
    }

    [Fact]
    public async Task CaseVariantKeys_AreIndependentRecords_OrTheCollidingWriteIsRefused()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteAsync("collection", "Steel", "capitalised");

        if (IsCaseInsensitiveFileSystem(temp.Path))
        {
            // The file system itself cannot hold "Steel" and "steel"
            // apart, so neither can the store. The guarantee that still
            // holds, and the one that matters, is that the record already
            // there is never silently discarded.
            await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
                () => store.WriteAsync("collection", "steel", "lowercase"));

            Assert.Equal("capitalised", await store.ReadAsync("collection", "Steel"));
            Assert.Null(await store.ReadAsync("collection", "steel"));
            return;
        }

        await store.WriteAsync("collection", "steel", "lowercase");

        Assert.Equal("capitalised", await store.ReadAsync("collection", "Steel"));
        Assert.Equal("lowercase", await store.ReadAsync("collection", "steel"));

        await store.DeleteAsync("collection", "steel");
        Assert.Equal("capitalised", await store.ReadAsync("collection", "Steel"));
        Assert.Null(await store.ReadAsync("collection", "steel"));
    }
}
