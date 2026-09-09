using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// Hostile names, against a real <see cref="SqlitePersistenceStore"/> on
/// real storage — reserved Win32 device names, their case and extension
/// variants, trailing dots, dot-names, path-traversal shapes and arbitrary
/// Unicode must all be unambiguously representable, round-trip through
/// <see cref="IPersistenceStore.ListKeysAsync"/>, and never collapse into
/// a missing or aliased record (`ADR-0144`, `WP 17.1A`).
/// </summary>
/// <remarks>
/// These began as `TD-59` closure tests against the file-per-key store,
/// where every name in them was a file name and each was a real hazard.
/// On the SQLite backend a key is a value in a column, so none of these
/// names is dangerous any more — which is precisely why the tests still
/// run: the claim was never "the encoding is correct", it was "the
/// caller's key comes back", and that claim outlives the encoding. What
/// the two backends genuinely disagreed about — the legacy-encoding
/// fallback, and what happened to two keys differing only in case on a
/// case-insensitive file system — was about a file system rather than
/// about a store, and is deleted with that store (`WP 18.1A`).
/// </remarks>
public sealed class PersistenceStoreHostileNameTests : SqlitePersistenceStoreFixture
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
