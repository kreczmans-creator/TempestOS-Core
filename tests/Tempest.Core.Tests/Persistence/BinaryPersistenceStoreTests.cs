using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.EngineeringDomain;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The byte shape of the platform's single store (`TD-31`), against a real
/// store on real storage — run once per backend (`ADR-0144`,
/// `WP 17.1A`).
/// </summary>
/// <remarks>
/// These prove the two claims the shape exists to make: that bytes survive
/// unchanged, and that they inherit — rather than re-implement — every
/// property the text shape already had. On the file backend that meant
/// reserved-name-safe naming, exact-name resolution and atomic
/// replacement; on the SQLite backend it means the same row, the same
/// exact key, and the same single-statement write. The tests do not name
/// either mechanism, which is why they run against both.
/// </remarks>
public abstract class BinaryPersistenceStoreTests<TBackend> : PersistenceStoreBackendFixture<TBackend>
    where TBackend : IPersistenceStoreBackend, new()
{
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
        var expected = BytesFor(fileName);

        await BinaryStore.WriteBytesAsync("content", fileName, expected);
        var actual = await BinaryStore.ReadBytesAsync("content", fileName);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task EveryByteValue_SurvivesUnchanged()
    {
        // The one assertion that covers the whole alphabet: if any value is
        // dropped, translated or truncated on the way through, it is in here.
        var expected = AttachmentContentSamples.EveryByteValue();

        await BinaryStore.WriteBytesAsync("content", "all-bytes", expected);

        Assert.Equal(expected, await BinaryStore.ReadBytesAsync("content", "all-bytes"));
    }

    [Fact]
    public async Task AMultiMegabyteRecord_RoundTripsIntact()
    {
        var expected = AttachmentContentSamples.LargeDeterministicBlob(4 * 1024 * 1024);

        await BinaryStore.WriteBytesAsync("content", "large", expected);
        var actual = await BinaryStore.ReadBytesAsync("content", "large");

        Assert.NotNull(actual);
        Assert.Equal(expected.LongLength, actual.LongLength);
        Assert.True(expected.AsSpan().SequenceEqual(actual), "A 4 MB record must round-trip byte for byte.");
    }

    [Fact]
    public async Task AnEmptyRecord_IsStoredAndIsNotTheSameAsNoRecord()
    {
        await BinaryStore.WriteBytesAsync("content", "empty", ReadOnlyMemory<byte>.Empty);

        var stored = await BinaryStore.ReadBytesAsync("content", "empty");
        Assert.NotNull(stored);
        Assert.Empty(stored);

        // The distinction the attachment layer depends on: a zero-byte
        // record is a record, and is not the absence of one.
        Assert.Null(await BinaryStore.ReadBytesAsync("content", "never-written"));
    }

    [Fact]
    public async Task ReadingAKeyThatWasNeverWritten_ReturnsNull_RatherThanThrowing()
    {
        Assert.Null(await BinaryStore.ReadBytesAsync("content", "absent"));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("NUL")]
    [InlineData("com1")]
    [InlineData("PRN.pdf")]
    public async Task AReservedDeviceNameKey_IsSafeForBytesToo(string key)
    {
        // Inherited from the text shape rather than re-implemented: both
        // backends resolve a key the same way whatever its value holds -
        // the file store through `TD-59`'s encoding, SQLite by not having
        // a file name in the first place.
        var expected = AttachmentContentSamples.Png();

        await BinaryStore.WriteBytesAsync("content", key, expected);

        Assert.Equal(expected, await BinaryStore.ReadBytesAsync("content", key));
        Assert.Contains(key, await Store.ListKeysAsync("content"));
    }

    [Fact]
    public async Task OverwritingARecord_ReplacesItEntirely_LeavingNoTailOfTheOldOne()
    {
        // The failure this guards against is a shorter write leaving the
        // tail of a longer previous value in place, which a naive
        // open-and-write would do and neither an atomic rename nor a row
        // update can.
        await BinaryStore.WriteBytesAsync("content", "key", AttachmentContentSamples.LargeDeterministicBlob(64 * 1024));
        var replacement = AttachmentContentSamples.Png();
        await BinaryStore.WriteBytesAsync("content", "key", replacement);

        Assert.Equal(replacement, await BinaryStore.ReadBytesAsync("content", "key"));
    }

    [Fact]
    public async Task DeletingARecord_RemovesIt_AndIsIdempotent()
    {
        await BinaryStore.WriteBytesAsync("content", "key", AttachmentContentSamples.Jpeg());

        await BinaryStore.DeleteAsync("content", "key");
        Assert.Null(await BinaryStore.ReadBytesAsync("content", "key"));

        await BinaryStore.DeleteAsync("content", "key");
    }

    [Fact]
    public async Task TheSameStoreInstance_SatisfiesEveryShape_WithoutASecondStore()
    {
        // The architectural claim, asserted rather than described: one
        // object, one root, every contract. If this ever needs two
        // instances, a second persistence mechanism has appeared.
        Assert.IsAssignableFrom<IPersistenceStore>(Store);
        Assert.IsAssignableFrom<IBinaryPersistenceStore>(Store);
        Assert.IsAssignableFrom<IQueryablePersistenceStore>(Store);

        await Store.WriteAsync("text", "key", "a string");
        await BinaryStore.WriteBytesAsync("bytes", "key", AttachmentContentSamples.Png());

        Assert.Equal("a string", await Store.ReadAsync("text", "key"));
        Assert.Equal(AttachmentContentSamples.Png(), await BinaryStore.ReadBytesAsync("bytes", "key"));
    }

    // Whether a record written through ONE shape reads as absent through
    // the OTHER is NOT in this contract, because the two backends
    // genuinely differ and only one of them can make the promise. The
    // file store writes an untagged file, so bytes read back as text are
    // whatever decoding them produces; SQLite knows which column the value
    // went into and returns null from the other. `IBinaryPersistenceStore`
    // always intended the separation ("bytes that happen to be valid UTF-8
    // are still bytes"), and `ADR-0144` is the first backend able to
    // enforce it rather than rely on collections being owned by one
    // service. Asserted in SqlitePersistenceStoreTests, where it holds.

    [Fact]
    public async Task WritingBytesToOneCollection_DoesNotDisturbAnother()
    {
        await BinaryStore.WriteBytesAsync("first", "key", AttachmentContentSamples.Pdf());
        await BinaryStore.WriteBytesAsync("second", "key", AttachmentContentSamples.Png());

        Assert.Equal(AttachmentContentSamples.Pdf(), await BinaryStore.ReadBytesAsync("first", "key"));
        Assert.Equal(AttachmentContentSamples.Png(), await BinaryStore.ReadBytesAsync("second", "key"));
    }

    [Fact]
    public async Task ConcurrentWritesToDifferentKeys_AllArriveIntact()
    {
        var payloads = Enumerable.Range(0, 16)
            .ToDictionary(i => $"key-{i}", i => AttachmentContentSamples.LargeDeterministicBlob(4096 + i));

        await Task.WhenAll(payloads.Select(p => BinaryStore.WriteBytesAsync("content", p.Key, p.Value)));

        foreach (var (key, expected) in payloads)
            Assert.Equal(expected, await BinaryStore.ReadBytesAsync("content", key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMissingCollectionOrKey_IsRejected(string? blank)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => BinaryStore.WriteBytesAsync(blank!, "key", new byte[] { 1 }));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => BinaryStore.WriteBytesAsync("content", blank!, new byte[] { 1 }));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => BinaryStore.ReadBytesAsync(blank!, "key"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => BinaryStore.ReadBytesAsync("content", blank!));
    }
}

/// <summary>The byte shape against the file-per-key backend.</summary>
public sealed class FileBackedBinaryPersistenceStoreTests : BinaryPersistenceStoreTests<FileStoreBackend>;

/// <summary>The byte shape against the SQLite backend (`ADR-0144`).</summary>
public sealed class SqliteBackedBinaryPersistenceStoreTests : BinaryPersistenceStoreTests<SqliteStoreBackend>;

/// <summary>
/// The one byte-shape claim that is about the file backend's own medium:
/// the temporary file its atomic write stages a value in must not survive
/// the write.
/// </summary>
/// <remarks>
/// There is no SQLite counterpart, and inventing one would be a different
/// test wearing this one's name — the equivalent property there is that a
/// rolled-back transaction leaves nothing behind, which
/// <see cref="SqlitePersistenceStoreTests"/> asserts directly.
/// </remarks>
public class BinaryPersistenceStoreFileSystemTests
{
    [Fact]
    public async Task NoTemporaryFile_IsLeftBehindByAByteWrite()
    {
        using var temp = new TempDirectory();
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
        ])).Build();
        var store = new PersistenceStore(configuration);

        await store.WriteBytesAsync("content", "key", AttachmentContentSamples.Pdf());

        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }
}
