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
    /// <summary>Explicit rather than implicit (`WP 21.5F` Offensive Security Audit, OSA-05) — see <see cref="JsonExportFormat"/>'s own identical remark.</summary>
    private static readonly JsonSerializerOptions Options = new() { MaxDepth = 64 };

    /// <inheritdoc />
    public byte[] Serialize(IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return JsonSerializer.SerializeToUtf8Bytes(data, Options);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Deserialize(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(payload, Options);

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
