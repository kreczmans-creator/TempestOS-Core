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
}
