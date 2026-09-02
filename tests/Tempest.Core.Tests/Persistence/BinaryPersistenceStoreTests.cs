using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.EngineeringDomain;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The byte shape of the platform's single store (`TD-31`), against the
/// real <see cref="PersistenceStore"/> on a real file system.
/// </summary>
/// <remarks>
/// These prove the two claims the shape exists to make: that bytes survive
/// unchanged, and that they inherit — rather than re-implement — every
/// property the text shape already had (reserved-name-safe naming,
/// exact-name resolution, atomic replacement).
/// </remarks>
public class BinaryPersistenceStoreTests
{
    private static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    public static TheoryData<string, string> RealFiles()
    {
        var data = new TheoryData<string, string>();
        foreach (var (fileName, contentType, _) in AttachmentContentSamples.All())
            data.Add(fileName, contentType);

        return data;
    }

    private static byte[] BytesFor(string fileName) =>
        AttachmentContentSamples.All().First(s => s.FileName == fileName).Bytes;

    [Theory]
    [MemberData(nameof(RealFiles))]
    public async Task RealFileContent_RoundTripsByteForByte(string fileName, string contentType)
    {
        _ = contentType;
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        var expected = BytesFor(fileName);

        await store.WriteBytesAsync("content", fileName, expected);
        var actual = await store.ReadBytesAsync("content", fileName);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task EveryByteValue_SurvivesUnchanged()
    {
        // The one assertion that covers the whole alphabet: if any value is
        // dropped, translated or truncated on the way through, it is in here.
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        var expected = AttachmentContentSamples.EveryByteValue();

        await store.WriteBytesAsync("content", "all-bytes", expected);

        Assert.Equal(expected, await store.ReadBytesAsync("content", "all-bytes"));
    }

    [Fact]
    public async Task AMultiMegabyteRecord_RoundTripsIntact()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        var expected = AttachmentContentSamples.LargeDeterministicBlob(4 * 1024 * 1024);

        await store.WriteBytesAsync("content", "large", expected);
        var actual = await store.ReadBytesAsync("content", "large");

        Assert.NotNull(actual);
        Assert.Equal(expected.LongLength, actual.LongLength);
        Assert.True(expected.AsSpan().SequenceEqual(actual), "A 4 MB record must round-trip byte for byte.");
    }

    [Fact]
    public async Task AnEmptyRecord_IsStoredAndIsNotTheSameAsNoRecord()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteBytesAsync("content", "empty", ReadOnlyMemory<byte>.Empty);

        var stored = await store.ReadBytesAsync("content", "empty");
        Assert.NotNull(stored);
        Assert.Empty(stored);

        // The distinction the attachment layer depends on: a zero-byte file
        // is a file, and is not the absence of one.
        Assert.Null(await store.ReadBytesAsync("content", "never-written"));
    }

    [Fact]
    public async Task ReadingAKeyThatWasNeverWritten_ReturnsNull_RatherThanThrowing()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        Assert.Null(await store.ReadBytesAsync("content", "absent"));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("NUL")]
    [InlineData("com1")]
    [InlineData("PRN.pdf")]
    public async Task AReservedDeviceNameKey_IsSafeForBytesToo(string key)
    {
        // Inherited from the text shape rather than re-implemented: the
        // byte path shares GetFilePath, so `TD-59`'s encoding covers it.
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        var expected = AttachmentContentSamples.Png();

        await store.WriteBytesAsync("content", key, expected);

        Assert.Equal(expected, await store.ReadBytesAsync("content", key));
        Assert.Contains(key, await store.ListKeysAsync("content"));
    }

    [Fact]
    public async Task OverwritingARecord_ReplacesItEntirely_LeavingNoTailOfTheOldOne()
    {
        // The failure this guards against is a shorter write leaving the
        // tail of a longer previous value in place, which a naive
        // open-and-write would do and an atomic rename cannot.
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteBytesAsync("content", "key", AttachmentContentSamples.LargeDeterministicBlob(64 * 1024));
        var replacement = AttachmentContentSamples.Png();
        await store.WriteBytesAsync("content", "key", replacement);

        Assert.Equal(replacement, await store.ReadBytesAsync("content", "key"));
    }

    [Fact]
    public async Task DeletingARecord_RemovesIt_AndIsIdempotent()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        await store.WriteBytesAsync("content", "key", AttachmentContentSamples.Jpeg());

        await ((IBinaryPersistenceStore)store).DeleteAsync("content", "key");
        Assert.Null(await store.ReadBytesAsync("content", "key"));

        await ((IBinaryPersistenceStore)store).DeleteAsync("content", "key");
    }

    [Fact]
    public async Task TheSameStoreInstance_SatisfiesBothShapes_WithoutASecondStore()
    {
        // The architectural claim, asserted rather than described: one
        // object, one root, both contracts. If this ever needs two
        // instances, a second persistence mechanism has appeared.
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        Assert.IsAssignableFrom<IPersistenceStore>(store);
        Assert.IsAssignableFrom<IBinaryPersistenceStore>(store);

        await store.WriteAsync("text", "key", "a string");
        await store.WriteBytesAsync("bytes", "key", AttachmentContentSamples.Png());

        Assert.Equal("a string", await store.ReadAsync("text", "key"));
        Assert.Equal(AttachmentContentSamples.Png(), await store.ReadBytesAsync("bytes", "key"));
    }

    [Fact]
    public async Task WritingBytesToOneCollection_DoesNotDisturbAnother()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteBytesAsync("first", "key", AttachmentContentSamples.Pdf());
        await store.WriteBytesAsync("second", "key", AttachmentContentSamples.Png());

        Assert.Equal(AttachmentContentSamples.Pdf(), await store.ReadBytesAsync("first", "key"));
        Assert.Equal(AttachmentContentSamples.Png(), await store.ReadBytesAsync("second", "key"));
    }

    [Fact]
    public async Task ConcurrentWritesToDifferentKeys_AllArriveIntact()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));
        var payloads = Enumerable.Range(0, 16)
            .ToDictionary(i => $"key-{i}", i => AttachmentContentSamples.LargeDeterministicBlob(4096 + i));

        await Task.WhenAll(payloads.Select(p => store.WriteBytesAsync("content", p.Key, p.Value)));

        foreach (var (key, expected) in payloads)
            Assert.Equal(expected, await store.ReadBytesAsync("content", key));
    }

    [Fact]
    public async Task NoTemporaryFile_IsLeftBehindByAByteWrite()
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await store.WriteBytesAsync("content", "key", AttachmentContentSamples.Pdf());

        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMissingCollectionOrKey_IsRejected(string? blank)
    {
        using var temp = new TempDirectory();
        var store = new PersistenceStore(BuildConfiguration(temp.Path));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.WriteBytesAsync(blank!, "key", new byte[] { 1 }));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.WriteBytesAsync("content", blank!, new byte[] { 1 }));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.ReadBytesAsync(blank!, "key"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.ReadBytesAsync("content", blank!));
    }
}
