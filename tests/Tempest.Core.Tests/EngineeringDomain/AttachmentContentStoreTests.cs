using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// The durable attachment-content boundary (`TD-31`), against the real
/// <see cref="PersistenceStore"/> on a real file system.
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
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    private static (AttachmentContentStore Store, PersistenceStore Backing) Build(string rootPath)
    {
        var backing = new PersistenceStore(BuildConfiguration(rootPath));
        return (new AttachmentContentStore(backing), backing);
    }

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
        var (store, _) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var expected = BytesFor(fileName);

        var hash = await store.SaveAsync(attachmentId, expected);
        var result = await store.ReadAsync(attachmentId, hash, expected.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(expected, result.Bytes);
    }

    [Fact]
    public async Task TheHashRecordedOnSave_IsTheHashOfTheBytesStored()
    {
        using var temp = new TempDirectory();
        var (store, _) = Build(temp.Path);
        var content = AttachmentContentSamples.Pdf();

        var hash = await store.SaveAsync(Guid.NewGuid(), content);

        Assert.Equal(AttachmentContentStore.ComputeHash(content), hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public async Task ContentThatWasNeverStored_ReadsAsMissing_NotAsAnError()
    {
        using var temp = new TempDirectory();
        var (store, _) = Build(temp.Path);

        var result = await store.ReadAsync(Guid.NewGuid(), expectedHash: null, expectedSizeInBytes: 0);

        Assert.Equal(AttachmentContentStatus.Missing, result.Status);
        Assert.Empty(result.Bytes);
    }

    [Fact]
    public async Task ContentTamperedWithOnDisk_ReadsAsCorrupt_AndTheBytesAreWithheld()
    {
        using var temp = new TempDirectory();
        var (store, backing) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var original = AttachmentContentSamples.Png();
        var hash = await store.SaveAsync(attachmentId, original);

        // Same length, different bytes: only the hash can catch this, which
        // is the point of recording one.
        var tampered = (byte[])original.Clone();
        tampered[^5] ^= 0xFF;
        await backing.WriteBytesAsync(
            AttachmentContentStore.ContentCollectionName, attachmentId.ToString("N"), tampered);

        var result = await store.ReadAsync(attachmentId, hash, original.LongLength);

        Assert.Equal(AttachmentContentStatus.Corrupt, result.Status);
        Assert.Empty(result.Bytes);
    }

    [Fact]
    public async Task ContentTruncatedOnDisk_ReadsAsCorrupt()
    {
        using var temp = new TempDirectory();
        var (store, backing) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var original = AttachmentContentSamples.Pdf();
        var hash = await store.SaveAsync(attachmentId, original);

        await backing.WriteBytesAsync(
            AttachmentContentStore.ContentCollectionName, attachmentId.ToString("N"), original.AsMemory(0, original.Length / 2));

        var result = await store.ReadAsync(attachmentId, hash, original.LongLength);

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
        var (store, _) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Jpeg();
        var hash = await store.SaveAsync(attachmentId, content);

        var result = await store.ReadAsync(attachmentId, hash, content.LongLength + 1);

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
        var (store, _) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Csv();
        await store.SaveAsync(attachmentId, content);

        var result = await store.ReadAsync(attachmentId, expectedHash: null, content.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(content, result.Bytes);
    }

    [Fact]
    public async Task ContentWithNoRecordedHash_IsStillCaughtByTheSizeCheck()
    {
        using var temp = new TempDirectory();
        var (store, _) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        await store.SaveAsync(attachmentId, AttachmentContentSamples.Csv());

        var result = await store.ReadAsync(attachmentId, expectedHash: null, expectedSizeInBytes: 999_999);

        Assert.Equal(AttachmentContentStatus.Corrupt, result.Status);
    }

    [Fact]
    public async Task AHashComparison_IsCaseInsensitive_SoAHexCasingChangeIsNotCorruption()
    {
        using var temp = new TempDirectory();
        var (store, _) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Png();
        var hash = await store.SaveAsync(attachmentId, content);

        var result = await store.ReadAsync(attachmentId, hash.ToUpperInvariant(), content.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
    }

    [Fact]
    public async Task SavingTwiceForTheSameAttachment_ReplacesTheContent()
    {
        using var temp = new TempDirectory();
        var (store, _) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        await store.SaveAsync(attachmentId, AttachmentContentSamples.LargeDeterministicBlob(50_000));

        var replacement = AttachmentContentSamples.Png();
        var hash = await store.SaveAsync(attachmentId, replacement);

        var result = await store.ReadAsync(attachmentId, hash, replacement.LongLength);
        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(replacement, result.Bytes);
    }

    [Fact]
    public async Task DeletedContent_ReadsAsMissing()
    {
        using var temp = new TempDirectory();
        var (store, _) = Build(temp.Path);
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.Pdf();
        var hash = await store.SaveAsync(attachmentId, content);

        await store.DeleteAsync(attachmentId);

        Assert.Equal(AttachmentContentStatus.Missing, (await store.ReadAsync(attachmentId, hash, content.LongLength)).Status);
    }

    [Fact]
    public async Task DeletingContentThatWasNeverStored_IsNotAnError()
    {
        using var temp = new TempDirectory();
        var (store, _) = Build(temp.Path);

        await store.DeleteAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task TwoAttachments_DoNotShareOrOverwriteEachOthersContent()
    {
        using var temp = new TempDirectory();
        var (store, _) = Build(temp.Path);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var pdf = AttachmentContentSamples.Pdf();
        var png = AttachmentContentSamples.Png();

        var firstHash = await store.SaveAsync(first, pdf);
        var secondHash = await store.SaveAsync(second, png);

        Assert.Equal(pdf, (await store.ReadAsync(first, firstHash, pdf.LongLength)).Bytes);
        Assert.Equal(png, (await store.ReadAsync(second, secondHash, png.LongLength)).Bytes);
    }

    [Fact]
    public async Task ContentSurvivesANewStoreInstanceOverTheSameRoot()
    {
        // The durability claim at its smallest: nothing about the content
        // lives in the object that wrote it.
        using var temp = new TempDirectory();
        var attachmentId = Guid.NewGuid();
        var content = AttachmentContentSamples.OfficeDocumentContainer();

        var (writer, _) = Build(temp.Path);
        var hash = await writer.SaveAsync(attachmentId, content);

        var (reader, _) = Build(temp.Path);
        var result = await reader.ReadAsync(attachmentId, hash, content.LongLength);

        Assert.Equal(AttachmentContentStatus.Available, result.Status);
        Assert.Equal(content, result.Bytes);
    }

    [Fact]
    public void ANullBinaryStore_IsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => new AttachmentContentStore(null!));
    }
}
