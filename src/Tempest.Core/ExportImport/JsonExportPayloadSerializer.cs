using System.Text.Json;

namespace Tempest.Core.ExportImport;

/// <summary>
/// A general-purpose <see cref="IExportPayloadSerializer"/> that serializes
/// a key/value data set as UTF-8 JSON — the platform's own ready-to-use
/// serializer for any <see cref="IExportable"/>/<see cref="IImportable"/>
/// pair that does not need a more specific payload shape of its own.
/// </summary>
public sealed class JsonExportPayloadSerializer : IExportPayloadSerializer
{
    /// <inheritdoc />
    public byte[] Serialize(IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return JsonSerializer.SerializeToUtf8Bytes(data);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Deserialize(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(payload);

            if (data is null)
                throw new CorruptedExportArtifactException("the payload deserialized to no content.");

            return data;
        }
        catch (JsonException ex)
        {
            throw new CorruptedExportArtifactException(ex.Message);
        }
    }
}
