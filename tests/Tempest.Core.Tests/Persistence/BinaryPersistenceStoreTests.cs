using Tempest.Core.Persistence;
using Tempest.Core.Tests.EngineeringDomain;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The byte shape of the platform's single store (`TD-31`), against a real
/// <see cref="SqlitePersistenceStore"/> on real storage (`ADR-0144`,
/// `WP 17.1A`).
/// </summary>
/// <remarks>
/// Re-pointed from the deleted file-per-key store (`WP 18.1A`): this ran
/// once per backend while that store still shipped, to prove the two
/// claims the shape exists to make — that bytes survive unchanged, and
/// that they inherit every property the text shape already had, rather
/// than re-implementing them. The one claim that was genuinely about the
/// file backend's own medium (that its atomic write leaves no temporary
/// file behind) is deleted with it; the SQLite equivalent — that a rolled
/// back transaction leaves nothing behind — is asserted directly in
/// <see cref="SqlitePersistenceStoreTests"/>.
/// </remarks>
public sealed class BinaryPersistenceStoreTests : SqlitePersistenceStoreFixture
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

    // ----------------------------------------------------------------
    // OpenReadAsync (`TD-96`) — the streamed counterpart of ReadBytesAsync.
    // ----------------------------------------------------------------

    [Fact]
    public async Task OpenReadAsync_ForAKeyThatWasNeverWritten_ReturnsNull()
    {
        Assert.Null(await BinaryStore.OpenReadAsync("content", "absent"));
    }

    [Fact]
    public async Task OpenReadAsync_RoundTripsByteForByte()
    {
        var expected = AttachmentContentSamples.Png();
        await BinaryStore.WriteBytesAsync("content", "key", expected);

        await using var stream = await BinaryStore.OpenReadAsync("content", "key");
        Assert.NotNull(stream);

        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer);

        Assert.Equal(expected, buffer.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_AnEmptyRecord_IsAnAlreadyExhaustedStream_NotNull()
    {
        await BinaryStore.WriteBytesAsync("content", "empty", ReadOnlyMemory<byte>.Empty);

        await using var stream = await BinaryStore.OpenReadAsync("content", "empty");
        Assert.NotNull(stream);
        Assert.Equal(-1, stream!.ReadByte());
    }

    [Fact]
    public async Task OpenReadAsync_SupportsSeekingWithinTheRecord()
    {
        var expected = AttachmentContentSamples.LargeDeterministicBlob(64 * 1024);
        await BinaryStore.WriteBytesAsync("content", "key", expected);

        await using var stream = await BinaryStore.OpenReadAsync("content", "key");
        Assert.NotNull(stream);
        Assert.True(stream!.CanSeek);
        Assert.Equal(expected.LongLength, stream.Length);

        stream.Seek(40_000, SeekOrigin.Begin);
        var actual = new byte[100];
        var read = stream.Read(actual, 0, actual.Length);

        Assert.Equal(actual.Length, read);
        Assert.Equal(expected.AsSpan(40_000, 100).ToArray(), actual);
    }

    /// <summary>
    /// The `TD-96` claim itself: a large record is readable through
    /// <see cref="IBinaryPersistenceStore.OpenReadAsync"/> in bounded-size
    /// chunks a caller chooses, never as one array the size of the whole
    /// record.
    /// </summary>
    /// <remarks>
    /// The counting half is direct rather than a wrapping wrapper stream —
    /// this test is the consumer, and it is the one deciding how large a
    /// buffer to hand <see cref="Stream.Read(byte[], int, int)"/>, so the
    /// number of calls it takes to drain a 20 MB record is itself the
    /// proof nothing coalesced them into one big read. The allocation
    /// measurement stays synchronous end to end (no <c>await</c> inside
    /// the timed region) specifically so <see cref="GC.GetAllocatedBytesForCurrentThread"/>
    /// is measuring the one thread that did the work, not whichever
    /// thread pool thread happened to resume after a hop.
    /// </remarks>
    [Fact]
    public async Task OpenReadAsync_A20MegabyteRecord_IsReadInBoundedChunks_AllocatingNothingNearItsSize()
    {
        const int TotalSize = 20 * 1024 * 1024;
        const int ChunkSize = 64 * 1024;

        var expected = AttachmentContentSamples.LargeDeterministicBlob(TotalSize);
        await BinaryStore.WriteBytesAsync("content", "large", expected);

        await using var stream = await BinaryStore.OpenReadAsync("content", "large");
        Assert.NotNull(stream);

        var buffer = new byte[ChunkSize];
        var actual = new byte[TotalSize];
        var totalRead = 0;
        var readCalls = 0;

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        int read;
        while ((read = stream!.Read(buffer, 0, buffer.Length)) > 0)
        {
            Buffer.BlockCopy(buffer, 0, actual, totalRead, read);
            totalRead += read;
            readCalls++;
        }

        var allocatedDuringRead = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(TotalSize, totalRead);
        Assert.Equal(expected, actual);

        // A 20 MB record drained through a 64 KB buffer takes on the order
        // of 320 reads; a handful would mean something coalesced the reads
        // back into a small number of large ones.
        Assert.True(readCalls > 100, $"Expected well over 100 chunked reads for a 20 MB record; got {readCalls}.");

        // Generously below the 20 MB the old ReadBytesAsync-only path would
        // have allocated for this same record — proof of the claim, not a
        // tight tripwire that would fail on ordinary ADO.NET bookkeeping.
        Assert.True(
            allocatedDuringRead < 5 * 1024 * 1024,
            $"Expected reading a 20 MB record through OpenReadAsync to allocate well under its own size; allocated {allocatedDuringRead:N0} bytes.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OpenReadAsync_AMissingCollectionOrKey_IsRejected(string? blank)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => BinaryStore.OpenReadAsync(blank!, "key"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => BinaryStore.OpenReadAsync("content", blank!));
    }
}
