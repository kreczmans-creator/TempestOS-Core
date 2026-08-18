using System.Text.Json;

namespace Tempest.Core.Tests.Plugins;

/// <summary>
/// Builds raw plugin manifest JSON text for tests, covering both the v1
/// required fields and the v2 fields WP 13.1A adds (<c>Dependencies</c>,
/// <c>RequestedCapabilities</c>, <c>Publisher</c>, <c>Signature</c>) —
/// via <see cref="JsonSerializer"/> rather than hand-rolled string
/// interpolation, so array/nested-object shapes (dependency entries) are
/// always well-formed, and any field can be omitted, left blank, or set to
/// an arbitrary malformed value without fragile string surgery.
/// </summary>
internal static class PluginManifestJsonBuilder
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// Builds one raw, unvalidated dependency declaration for inclusion in a
    /// manifest's <c>Dependencies</c> array. Any field may be
    /// <see langword="null"/> to test a missing/malformed declaration.
    /// </summary>
    public sealed record DependencyFragment(string? Id, string? MinimumVersion, string? MaximumVersion = null)
    {
        public static DependencyFragment On(string id, string minimumVersion, string? maximumVersion = null) =>
            new(id, minimumVersion, maximumVersion);
    }

    public static string Build(
        string? id = "test.plugin",
        string? name = "Test Plugin",
        string? version = "1.0.0",
        string? minimumPlatformVersion = "0.1.0",
        string? assemblyFileName = "Plugin.dll",
        IReadOnlyList<DependencyFragment>? dependencies = null,
        IReadOnlyList<string>? requestedCapabilities = null,
        string? publisher = null,
        string? signature = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["Id"] = id,
            ["Name"] = name,
            ["Version"] = version,
            ["MinimumPlatformVersion"] = minimumPlatformVersion,
            ["AssemblyFileName"] = assemblyFileName,
        };

        if (dependencies is not null)
        {
            payload["Dependencies"] = dependencies
                .Select(d => new Dictionary<string, object?>
                {
                    ["Id"] = d.Id,
                    ["MinimumVersion"] = d.MinimumVersion,
                    ["MaximumVersion"] = d.MaximumVersion,
                })
                .ToList();
        }

        if (requestedCapabilities is not null)
            payload["RequestedCapabilities"] = requestedCapabilities;

        if (publisher is not null)
            payload["Publisher"] = publisher;

        if (signature is not null)
            payload["Signature"] = signature;

        return JsonSerializer.Serialize(payload, Options);
    }
}
