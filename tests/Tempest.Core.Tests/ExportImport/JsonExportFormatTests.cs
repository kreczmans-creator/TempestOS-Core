using Tempest.Core.ExportImport;

namespace Tempest.Core.Tests.ExportImport;

// Proves JsonExportFormat's own framing round-trips every section's own
// Kind, SchemaVersion, and opaque Payload bytes exactly, and rejects a
// malformed or truncated artifact as CorruptedExportArtifactException
// rather than an unrelated, undocumented exception type.
public class JsonExportFormatTests
{
    [Fact]
    public async Task WriteThenRead_SingleSection_RoundTripsExactly()
    {
        var format = new JsonExportFormat();
        var section = new ExportSection("kind.a", 3, [1, 2, 3, 4, 5]);
        using var stream = new MemoryStream();

        await format.WriteAsync([section], stream);
        stream.Position = 0;
        var sections = await format.ReadAsync(stream);

        var read = Assert.Single(sections);
        Assert.Equal("kind.a", read.Kind);
        Assert.Equal(3, read.SchemaVersion);
        Assert.Equal(section.Payload, read.Payload);
    }

    [Fact]
    public async Task WriteThenRead_MultipleSections_PreservesOrder()
    {
        var format = new JsonExportFormat();
        using var stream = new MemoryStream();

        await format.WriteAsync(
            [new ExportSection("first", 1, [1]), new ExportSection("second", 2, [2]), new ExportSection("third", 3, [3])],
            stream);
        stream.Position = 0;
        var sections = await format.ReadAsync(stream);

        Assert.Equal(["first", "second", "third"], sections.Select(s => s.Kind));
    }

    [Fact]
    public async Task WriteThenRead_EmptyPayload_RoundTripsAsEmptyBytes()
    {
        var format = new JsonExportFormat();
        using var stream = new MemoryStream();

        await format.WriteAsync([new ExportSection("kind.a", 1, [])], stream);
        stream.Position = 0;
        var sections = await format.ReadAsync(stream);

        Assert.Empty(Assert.Single(sections).Payload);
    }

    [Fact]
    public async Task WriteThenRead_NoSections_RoundTripsAsEmptyList()
    {
        var format = new JsonExportFormat();
        using var stream = new MemoryStream();

        await format.WriteAsync([], stream);
        stream.Position = 0;
        var sections = await format.ReadAsync(stream);

        Assert.Empty(sections);
    }

    [Fact]
    public async Task ReadAsync_NotJson_ThrowsCorruptedExportArtifactException()
    {
        var format = new JsonExportFormat();
        using var stream = new MemoryStream("definitely not json"u8.ToArray());

        await Assert.ThrowsAsync<CorruptedExportArtifactException>(() => format.ReadAsync(stream));
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_ThrowsCorruptedExportArtifactException()
    {
        var format = new JsonExportFormat();
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<CorruptedExportArtifactException>(() => format.ReadAsync(stream));
    }

    [Fact]
    public async Task ReadAsync_PayloadIsNotValidBase64_ThrowsCorruptedExportArtifactException()
    {
        var format = new JsonExportFormat();
        var json = """[{"Kind":"kind.a","SchemaVersion":1,"Payload":"not base64!!!"}]""";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        await Assert.ThrowsAsync<CorruptedExportArtifactException>(() => format.ReadAsync(stream));
    }

    [Fact]
    public async Task WriteAsync_NullSections_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new JsonExportFormat().WriteAsync(null!, new MemoryStream()));

    [Fact]
    public async Task WriteAsync_NullDestination_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new JsonExportFormat().WriteAsync([], null!));

    [Fact]
    public async Task ReadAsync_NullSource_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new JsonExportFormat().ReadAsync(null!));

    // ========================================================================
    // WP 21.5F — Offensive Security Audit: OSA-05, unbounded import resource consumption.
    // ========================================================================

    [Fact]
    public async Task OSA05_AnArtifactOverTheSizeLimit_IsRefusedBeforeParsing()
    {
        // The exploit: before this fix, an artifact of any size was
        // deserialised in full with nothing to stop it - a hostile
        // "export" file with an enormous payload was read entirely into
        // memory. A seekable stream over the limit is now refused by its
        // own reported Length, before JsonSerializer ever runs - proven
        // with a stream that reports a huge Length without actually
        // backing it with that much real memory, so this test itself
        // stays cheap.
        var format = new JsonExportFormat();
        using var stream = new FakeLengthStream(600_000_001);

        var ex = await Assert.ThrowsAsync<CorruptedExportArtifactException>(() => format.ReadAsync(stream));
        Assert.Contains("more than this platform will import", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A stream that reports an arbitrary, larger-than-its-real-backing <see cref="Length"/> — proves a size guard without actually allocating that much memory.</summary>
    private sealed class FakeLengthStream(long length) : MemoryStream
    {
        public override long Length => length;
        public override bool CanSeek => true;
    }

    [Fact]
    public async Task OSA05_AnArtifactDeclaringMoreSectionsThanThePlatformWillImport_IsRefused()
    {
        var format = new JsonExportFormat();
        var sections = Enumerable.Range(0, 10_001)
            .Select(i => new ExportSection($"kind.{i}", 1, [1]))
            .ToList();
        using var stream = new MemoryStream();
        await format.WriteAsync(sections, stream);
        stream.Position = 0;

        var ex = await Assert.ThrowsAsync<CorruptedExportArtifactException>(() => format.ReadAsync(stream));
        Assert.Contains("more than this platform will import", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OSA05_AnOrdinaryArtifact_IsStillAccepted()
    {
        // The guard must not be so aggressive it refuses a real export -
        // this mirrors WriteThenRead_SingleSection_RoundTripsExactly above,
        // placed here so the "not a false positive" property sits next to
        // the refusal tests it protects against regressing.
        var format = new JsonExportFormat();
        var section = new ExportSection("kind.a", 3, [1, 2, 3, 4, 5]);
        using var stream = new MemoryStream();

        await format.WriteAsync([section], stream);
        stream.Position = 0;
        var sections = await format.ReadAsync(stream);

        Assert.Single(sections);
    }
}
