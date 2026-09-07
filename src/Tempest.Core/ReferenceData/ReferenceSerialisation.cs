using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.Core.ReferenceData;

/// <summary>The single <see cref="JsonSerializerOptions"/> instance every Group A record is written and read with.</summary>
/// <remarks>
/// One shared, immutable instance rather than a fresh one per call:
/// <c>System.Text.Json</c> caches its own compiled metadata per options
/// instance, so constructing one per write would discard that cache every
/// time. Enum-as-string is the substantive choice here — a durable
/// engineering record must not change meaning because an enum member was
/// inserted above it in a later version.
/// </remarks>
public static class ReferenceSerialisation
{
    /// <summary>The options every Group A record is written and read with.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
