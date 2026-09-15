using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// The durable attachment-content boundary (`TD-31`), against the real
/// <see cref="SqlitePersistenceStore"/> on a real file system (`WP 18.1A`
/// re-points this suite from the deleted file-per-key store; the claims
/// below are unchanged).
/// </summary>
/// <remarks>
/// The round-trip cases prove content survives; the rest prove the store
/// tells the truth when it does not. A content store that cannot
/// distinguish "never held" from "held and damaged" is one that will
/// eventually present damage as absence, and an engineer will believe it.
/// </remarks>
public class AttachmentContentStoreTests
{
    private static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    /// <summary>
    /// Owns one <see cref="SqlitePersistenceStore"/> instance over a test's
    /// root, so the store's own exclusive instance lock (`ADR-0144`) is
    /// released deterministically at the end of every test rather than left
    /// to finalization.
    /// </summary>
    private sealed class Scope : IDisposable
    {
        public Scope(string rootPath)
        {
            Backing = new SqlitePersistenceStore(BuildConfiguration(rootPath));
            Store = new AttachmentContentStore(Backing);
        }

        public AttachmentContentStore Store { get; }

        public SqlitePersistenceStore Backing { get; }

        public void Dispose() => Backing.Dispose();
    }

    private static Scope Build(string rootPath) => new(rootPath);

    public static TheoryData<string> RealFileNames()
    {
        var data = new TheoryData<string>();
        foreach (var (fileName, _, _) in AttachmentContentSamples.All())
            data.Add(fileName);

        return data;
    }

    private static byte[] BytesFor(string fileName) =>
        AttachmentContentSamples.All().First(s => s.FileName == fileName).Bytes;

    [Theory]
    [MemberData(nameof(RealFileNames))]
    public async Task EveryDocumentWorkflowFileType_RoundTripsAndVerifies(string fileName)
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var expected = BytesFor(fileName);

        var hash = await scope.Store.SaveAsync(attachmentId, expected);
        var result = await scope.Store.ReadAsync(attachmentId, hash, expected.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(expected, result.Bytes);
    }

    [Fact]
    public async Task TheHashRecordedOnSave_IsTheHashOfTheBytesStored()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var content = AttachmentContentSamples.Pdf();

        var hash = await scope.Store.SaveAsync(Guid.NewGuid(), content);

        Assert.Equal(AttachmentContentStore.ComputeHash(content), hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public async Task ContentThatWasNeverStored_ReadsAsMissing_NotAsAnError()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);

        var result = await scope.Store.ReadAsync(Guid.NewGuid(), expectedHash: null, expectedSizeInBytes: 0);

        Assert.Equal(AttachmentContentStatus.Missing, result.Status);
        Assert.Empty(result.Bytes);
    }

    [Fact]
    public async Task ContentTamperedWithOnDisk_ReadsAsCorrupt_AndTheBytesAreWithheld()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var original = AttachmentContentSamples.Png();
        var hash = await scope.Store.SaveAsync(attachmentId, original);

        // Same length, different bytes: only the hash can catch this, which
        // is the point of recording one. Tampered at the hash key: content
        // is content-addressed (`TD-95`), so that is where Save actually
        // put the bytes, not at the attachment's own Id.
        var tampered = (byte[])original.Clone();
        tampered[^5] ^= 0xFF;
        await scope.Backing.WriteBytesAsync(AttachmentContentStore.ContentCollectionName, hash, tampered);

        var result = await scope.Store.ReadAsync(attachmentId, hash, original.LongLength);

        Assert.Equal(AttachmentContentStatus.Corrupt, result.Status);
        Assert.Empty(result.Bytes);
    }

    [Fact]
    public async Task ContentTruncatedOnDisk_ReadsAsCorrupt()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var original = AttachmentContentSamples.Pdf();
        var hash = await scope.Store.SaveAsync(attachmentId, original);

        // Truncated at the hash key — content-addressed storage (`TD-95`)
        // means that is where the bytes actually live.
        await scope.Backing.WriteBytesAsync(
            AttachmentContentStore.ContentCollectionName, hash, original.AsMemory(0, original.Length / 2));

        var result = await scope.Store.ReadAsync(attachmentId, hash, original.LongLength);

        Assert.Equal(AttachmentContentStatus.Corrupt, result.Status);
        Assert.Empty(result.Bytes);
    }

    [Fact]
    public async Task ContentWhoseMetadataClaimsTheWrongSize_ReadsAsCorrupt()
    {
        // The metadata is as capable of being wrong as the content is. A
        // size that disagrees with the stored bytes means one of the two
        // is not what it claims, and the store cannot tell which — so it
        // returns neither.
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Jpeg();
        var hash = await scope.Store.SaveAsync(attachmentId, content);

        var result = await scope.Store.ReadAsync(attachmentId, hash, content.LongLength + 1);

        Assert.Equal(AttachmentContentStatus.Corrupt, result.Status);
    }

    [Fact]
    public async Task ContentWithNoRecordedHash_IsReturnedWhenTheSizeAgrees_AndSaysSoHonestly()
    {
        // A pre-`TD-31` attachment: nothing recorded what the content
        // should hash to, so the size is the whole of the verification.
        // Returned rather than refused - refusing would make every
        // attachment written before this work package permanently
        // unreadable - but with no pretence that it was verified.
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Csv();
        await scope.Store.SaveAsync(attachmentId, content);

        var result = await scope.Store.ReadAsync(attachmentId, expectedHash: null, content.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(content, result.Bytes);
    }

    [Fact]
    public async Task ContentWithNoRecordedHash_IsStillCaughtByTheSizeCheck()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        await scope.Store.SaveAsync(attachmentId, AttachmentContentSamples.Csv());

        var result = await scope.Store.ReadAsync(attachmentId, expectedHash: null, expectedSizeInBytes: 999_999);

        Assert.Equal(AttachmentContentStatus.Corrupt, result.Status);
    }

    [Fact]
    public async Task AHashComparison_IsCaseInsensitive_SoAHexCasingChangeIsNotCorruption()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Png();
        var hash = await scope.Store.SaveAsync(attachmentId, content);

        var result = await scope.Store.ReadAsync(attachmentId, hash.ToUpperInvariant(), content.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
    }

    [Fact]
    public async Task SavingTwiceForTheSameAttachment_ReplacesTheContent()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        await scope.Store.SaveAsync(attachmentId, AttachmentContentSamples.LargeDeterministicBlob(50_000));

        var replacement = AttachmentContentSamples.Png();
        var hash = await scope.Store.SaveAsync(attachmentId, replacement);

        var result = await scope.Store.ReadAsync(attachmentId, hash, replacement.LongLength);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(replacement, result.Bytes);
    }

    [Fact]
    public async Task DeletedContent_ReadsAsMissing()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Pdf();
        var hash = await scope.Store.SaveAsync(attachmentId, content);

        await scope.Store.DeleteAsync(attachmentId);

        Assert.Equal(AttachmentContentStatus.Missing, (await scope.Store.ReadAsync(attachmentId, hash, content.LongLength)).Status);
    }

    [Fact]
    public async Task DeletingContentThatWasNeverStored_IsNotAnError()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);

        await scope.Store.DeleteAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task TwoAttachments_DoNotShareOrOverwriteEachOthersContent()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var pdf = AttachmentContentSamples.Pdf();
        var png = AttachmentContentSamples.Png();

        var firstHash = await scope.Store.SaveAsync(first, pdf);
        var secondHash = await scope.Store.SaveAsync(second, png);

        Assert.Equal(pdf, (await scope.Store.ReadAsync(first, firstHash, pdf.LongLength)).Bytes);
        Assert.Equal(png, (await scope.Store.ReadAsync(second, secondHash, png.LongLength)).Bytes);
    }

    // ----------------------------------------------------------------
    // Content-addressed storage (`TD-95`): dedupe, reference counting, and
    // migrating a legacy, attachment-Id-keyed row on first read.
    // ----------------------------------------------------------------

    [Fact]
    public async Task TwoAttachmentsOfTheSameBytes_ShareOneStoredBlob()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var content = AttachmentContentSamples.Pdf();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var firstHash = await scope.Store.SaveAsync(first, content);
        var secondHash = await scope.Store.SaveAsync(second, content);

        Assert.Equal(firstHash, secondHash);

        // One BLOB: exactly one key under the content collection, whatever
        // else may be there for other hashes never saved in this test.
        var keys = await scope.Backing.ListKeysAsync(AttachmentContentStore.ContentCollectionName, firstHash);
        Assert.Equal([firstHash], keys);

        Assert.Equal(content, (await scope.Store.ReadAsync(first, firstHash, content.LongLength)).Bytes);
        Assert.Equal(content, (await scope.Store.ReadAsync(second, secondHash, content.LongLength)).Bytes);
    }

    [Fact]
    public async Task DeletingOneOfTwoAttachmentsSharingContent_LeavesTheOtherReadable()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var content = AttachmentContentSamples.Png();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var hash = await scope.Store.SaveAsync(first, content);
        await scope.Store.SaveAsync(second, content);

        await scope.Store.DeleteAsync(first);

        Assert.Equal(AttachmentContentStatus.Missing, (await scope.Store.ReadAsync(first, hash, content.LongLength)).Status);

        var secondResult = await scope.Store.ReadAsync(second, hash, content.LongLength);
        Assert.Equal(AttachmentContentStatus.Available, secondResult.Status);
        Assert.Equal(content, secondResult.Bytes);

        // The bytes themselves are still there — the second attachment's
        // reference kept them, exactly what `TD-95` exists to make happen.
        Assert.Equal([hash], await scope.Backing.ListKeysAsync(AttachmentContentStore.ContentCollectionName, hash));
    }

    [Fact]
    public async Task DeletingBothAttachmentsSharingContent_RemovesTheBytes()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var content = AttachmentContentSamples.Jpeg();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var hash = await scope.Store.SaveAsync(first, content);
        await scope.Store.SaveAsync(second, content);

        await scope.Store.DeleteAsync(first);
        await scope.Store.DeleteAsync(second);

        Assert.Equal(AttachmentContentStatus.Missing, (await scope.Store.ReadAsync(first, hash, content.LongLength)).Status);
        Assert.Equal(AttachmentContentStatus.Missing, (await scope.Store.ReadAsync(second, hash, content.LongLength)).Status);

        // Gone, not merely unreferenced: no key remains for that hash, and
        // its reference count record is cleaned up with it.
        Assert.Empty(await scope.Backing.ListKeysAsync(AttachmentContentStore.ContentCollectionName, hash));
        Assert.Empty(await scope.Backing.ListKeysAsync(AttachmentContentStore.ReferenceCountCollectionName, hash));
    }

    [Fact]
    public async Task ReplacingOneAttachmentsContent_ReleasesTheOldHash_WithoutDisturbingASiblingThatStillUsesIt()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var shared = AttachmentContentSamples.Csv();
        var replacement = AttachmentContentSamples.Png();
        var attachmentToReplace = Guid.NewGuid();
        var sibling = Guid.NewGuid();

        var sharedHash = await scope.Store.SaveAsync(attachmentToReplace, shared);
        await scope.Store.SaveAsync(sibling, shared);

        var newHash = await scope.Store.SaveAsync(attachmentToReplace, replacement);

        Assert.NotEqual(sharedHash, newHash);

        var replaced = await scope.Store.ReadAsync(attachmentToReplace, newHash, replacement.LongLength);
        Assert.Equal(AttachmentContentStatus.Available, replaced.Status);
        Assert.Equal(replacement, replaced.Bytes);

        // The sibling never asked for anything to change, and its content
        // is still there under the hash it always referenced.
        var siblingResult = await scope.Store.ReadAsync(sibling, sharedHash, shared.LongLength);
        Assert.Equal(AttachmentContentStatus.Available, siblingResult.Status);
        Assert.Equal(shared, siblingResult.Bytes);
    }

    [Fact]
    public async Task ALegacyAttachmentIdKeyedRow_MigratesToContentAddressedStorage_OnFirstRead()
    {
        // Simulates a row written before `TD-95` existed: bytes stored
        // directly under the attachment's own Id, with no hash mapping —
        // exactly what every pre-`WP 20.1C1` `SaveAsync` call produced.
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.OfficeDocumentContainer();
        var hash = AttachmentContentStore.ComputeHash(content);

        await scope.Backing.WriteBytesAsync(AttachmentContentStore.ContentCollectionName, attachmentId.ToString("N"), content);

        var result = await scope.Store.ReadAsync(attachmentId, hash, content.LongLength);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(content, result.Bytes);

        // Adopted into the content-addressed layout: the legacy key is
        // gone, the bytes now live under the hash, and a second attachment
        // that happens to share the content dedupes against it too.
        Assert.Null(await scope.Backing.ReadBytesAsync(AttachmentContentStore.ContentCollectionName, attachmentId.ToString("N")));
        Assert.Equal(content, await scope.Backing.ReadBytesAsync(AttachmentContentStore.ContentCollectionName, hash));

        var another = Guid.NewGuid();
        var anotherHash = await scope.Store.SaveAsync(another, content);
        Assert.Equal(hash, anotherHash);
        Assert.Equal([hash], await scope.Backing.ListKeysAsync(AttachmentContentStore.ContentCollectionName, hash));
    }

    [Fact]
    public async Task OpenReadAsync_ForALegacyAttachmentIdKeyedRow_ReadsItWithoutMigratingIt()
    {
        // `TD-96`'s own boundary: the streamed path must not adopt a
        // legacy row into content-addressed storage, because doing so
        // would mean holding a possibly-large file whole in memory purely
        // to relocate it — the material cost `OpenReadAsync` exists to
        // avoid. It reads the row exactly where it finds it instead.
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Pdf();
        var hash = AttachmentContentStore.ComputeHash(content);

        await scope.Backing.WriteBytesAsync(AttachmentContentStore.ContentCollectionName, attachmentId.ToString("N"), content);

        using var result = await scope.Store.OpenReadAsync(attachmentId, hash, content.LongLength);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);

        using var buffer = new MemoryStream();
        await result.Stream!.CopyToAsync(buffer);
        Assert.Equal(content, buffer.ToArray());

        // Untouched: still at the legacy key, no mapping was written.
        Assert.NotNull(await scope.Backing.ReadBytesAsync(AttachmentContentStore.ContentCollectionName, attachmentId.ToString("N")));
        Assert.Null(await scope.Backing.ReadBytesAsync(AttachmentContentStore.HashByAttachmentCollectionName, attachmentId.ToString("N")));
    }

    // ----------------------------------------------------------------
    // Streamed reads (`TD-96`): OpenReadAsync applies the identical
    // verification ReadAsync does, over a stream rather than an array.
    // ----------------------------------------------------------------

    [Fact]
    public async Task OpenReadAsync_ContentThatWasNeverStored_ReadsAsMissing()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);

        using var result = await scope.Store.OpenReadAsync(Guid.NewGuid(), expectedHash: null, expectedSizeInBytes: 0);

        Assert.Equal(AttachmentContentStatus.Missing, result.Status);
        Assert.Null(result.Stream);
    }

    [Fact]
    public async Task OpenReadAsync_AvailableContent_RoundTripsByteForByte()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Jpeg();
        var hash = await scope.Store.SaveAsync(attachmentId, content);

        using var result = await scope.Store.OpenReadAsync(attachmentId, hash, content.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.NotNull(result.Stream);

        using var buffer = new MemoryStream();
        await result.Stream!.CopyToAsync(buffer);
        Assert.Equal(content, buffer.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_TamperedContent_ReadsAsCorrupt_AndReturnsNoStream()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var original = AttachmentContentSamples.Png();
        var hash = await scope.Store.SaveAsync(attachmentId, original);

        var tampered = (byte[])original.Clone();
        tampered[^3] ^= 0xFF;
        await scope.Backing.WriteBytesAsync(AttachmentContentStore.ContentCollectionName, hash, tampered);

        using var result = await scope.Store.OpenReadAsync(attachmentId, hash, original.LongLength);

        Assert.Equal(AttachmentContentStatus.Corrupt, result.Status);
        Assert.Null(result.Stream);
    }

    [Fact]
    public async Task OpenReadAsync_WrongSize_ReadsAsCorrupt()
    {
        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Csv();
        var hash = await scope.Store.SaveAsync(attachmentId, content);

        using var result = await scope.Store.OpenReadAsync(attachmentId, hash, content.LongLength + 1);

        Assert.Equal(AttachmentContentStatus.Corrupt, result.Status);
    }

    /// <summary>
    /// The `TD-96` claim through the attachment-facing surface: a 20 MB
    /// attachment opens and reads through <see cref="IAttachmentContentStore.OpenReadAsync"/>
    /// in bounded chunks, including the verification pass that runs before
    /// the caller ever sees a byte — the whole point being that neither
    /// half of this call materialises the record whole.
    /// </summary>
    [Fact]
    public async Task OpenReadAsync_A20MegabyteAttachment_VerifiesAndReadsWithoutOneBigArray()
    {
        const int TotalSize = 20 * 1024 * 1024;
        const int ChunkSize = 64 * 1024;

        using var temp = new TempDirectory();
        using var scope = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.LargeDeterministicBlob(TotalSize);
        var hash = await scope.Store.SaveAsync(attachmentId, content);

        using var result = await scope.Store.OpenReadAsync(attachmentId, hash, content.LongLength);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);

        var buffer = new byte[ChunkSize];
        var actual = new byte[TotalSize];
        var totalRead = 0;
        var readCalls = 0;

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        int read;
        while ((read = result.Stream!.Read(buffer, 0, buffer.Length)) > 0)
        {
            Buffer.BlockCopy(buffer, 0, actual, totalRead, read);
            totalRead += read;
            readCalls++;
        }

        var allocatedDuringRead = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(TotalSize, totalRead);
        Assert.Equal(content, actual);
        Assert.True(readCalls > 100, $"Expected well over 100 chunked reads for a 20 MB attachment; got {readCalls}.");
        Assert.True(
            allocatedDuringRead < 5 * 1024 * 1024,
            $"Expected reading a 20 MB attachment to allocate well under its own size; allocated {allocatedDuringRead:N0} bytes.");
    }

    [Fact]
    public async Task ContentSurvivesANewStoreInstanceOverTheSameRoot()
    {
        // The durability claim at its smallest: nothing about the content
        // lives in the object that wrote it. The writer's store is disposed
        // before the reader's is opened — SqlitePersistenceStore (`ADR-0144`)
        // holds its root's instance lock exclusively, unlike the deleted
        // file-per-key store this suite once ran on, so two live instances
        // over one root is the very thing being refused, not tested.
        using var temp = new TempDirectory();
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.OfficeDocumentContainer();

        string hash;
        using (var writer = Build(temp.Path))
            hash = await writer.Store.SaveAsync(attachmentId, content);

        using var reader = Build(temp.Path);
        var result = await reader.Store.ReadAsync(attachmentId, hash, content.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(content, result.Bytes);
    }

    [Fact]
    public void ANullBinaryStore_IsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => new AttachmentContentStore(null!));
    }
}
