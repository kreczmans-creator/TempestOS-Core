using Tempest.Core.ExportImport;

namespace Tempest.Core.Tests.ExportImport;

// Proves JsonExportPayloadSerializer - the optional "Serialization
// abstraction" - round-trips a key/value data set exactly, and rejects
// malformed bytes as CorruptedExportArtifactException. Entirely separate
// from ExportService/ImportService's own IExportFormat framing - this
// abstraction never sees more than one source's own data at a time.
public class JsonExportPayloadSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_RoundTripsExactly()
    {
        var serializer = new JsonExportPayloadSerializer();
        var data = new Dictionary<string, string> { ["Greeting"] = "hello", ["Count"] = "3" };

        var payload = serializer.Serialize(data);
        var result = serializer.Deserialize(payload);

        Assert.Equal(data, result);
    }

    [Fact]
    public void SerializeThenDeserialize_EmptyDictionary_RoundTripsAsEmpty()
    {
        var serializer = new JsonExportPayloadSerializer();

        var payload = serializer.Serialize(new Dictionary<string, string>());
        var result = serializer.Deserialize(payload);

        Assert.Empty(result);
    }

    [Fact]
    public void Deserialize_MalformedBytes_ThrowsCorruptedExportArtifactException()
    {
        var serializer = new JsonExportPayloadSerializer();

        Assert.Throws<CorruptedExportArtifactException>(() =>
            serializer.Deserialize("not json"u8.ToArray()));
    }

    [Fact]
    public void Serialize_NullData_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new JsonExportPayloadSerializer().Serialize(null!));

    [Fact]
    public void Deserialize_NullPayload_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new JsonExportPayloadSerializer().Deserialize(null!));
}
