using System.Text.Json;

namespace Tempest.Companion.Contracts;

/// <summary>
/// The one <see cref="JsonSerializerOptions"/> configuration both sides of
/// the Companion HTTP boundary serialize with — web defaults (camelCase
/// property names, case-insensitive reads), declared once so the server's
/// projection and the client's deserialization can never disagree about
/// casing or enum handling.
/// </summary>
public static class CompanionJson
{
    /// <summary>Gets the shared serializer options. The instance is read-only after first use; never mutate it.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
