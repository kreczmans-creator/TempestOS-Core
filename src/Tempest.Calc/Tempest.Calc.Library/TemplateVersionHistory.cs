using System.Text.Json;

namespace Tempest.Calc.Library;

/// <summary>
/// Every template version this product has recorded as existing, by <c>id@version</c>, with the content hash it had (ADR-0177, DC-14). The record is
/// <c>tests/Tempest.Calc.Tests.Library/TemplateVersionManifest.json</c>, which this assembly embeds (the csproj links it): <c>TemplateVersionManifestTests</c>
/// keeps it complete (a new template version fails until its line is added) and add-only (a line is never changed or removed). The catalogue reads it so
/// that a version whose formulas are no longer the installed ones can still be named, resolved and explained: a calculation made on it opens with a
/// sentence about what became of that version, not as a template that "is not registered".
/// </summary>
public sealed class TemplateVersionHistory
{
    /// <summary>The embedded manifest's resource name.</summary>
    public const string ManifestResourceName = "Tempest.Calc.Library.TemplateVersionManifest.json";

    /// <summary>The most bytes of manifest read: the shipped one is about 12 KB; anything larger is not this file.</summary>
    private const int MaxManifestBytes = 4 * 1024 * 1024;

    private static readonly Lazy<TemplateVersionHistory> ShippedHistory = new(LoadShipped);

    private readonly Dictionary<string, string> _hashes;

    public TemplateVersionHistory(IEnumerable<KeyValuePair<string, string>> hashes)
    {
        ArgumentNullException.ThrowIfNull(hashes);
        _hashes = new Dictionary<string, string>(hashes, StringComparer.Ordinal);
    }

    /// <summary>A history that records nothing: every older version is "not recorded".</summary>
    public static TemplateVersionHistory Empty { get; } = new([]);

    /// <summary>The history this build ships: the embedded manifest.</summary>
    public static TemplateVersionHistory Shipped => ShippedHistory.Value;

    /// <summary>The number of recorded <c>id@version</c> references.</summary>
    public int Count => _hashes.Count;

    /// <summary>Every recorded reference, in no particular order.</summary>
    public IReadOnlyCollection<string> References => _hashes.Keys;

    /// <summary>Whether <paramref name="reference"/> (<c>id@version</c>) is recorded.</summary>
    public bool Knows(string? reference) => reference is not null && _hashes.ContainsKey(reference);

    /// <summary>The full content hash recorded for <paramref name="reference"/>, or <see langword="null"/>.</summary>
    public string? HashOf(string? reference) => reference is not null && _hashes.TryGetValue(reference, out var hash) ? hash : null;

    /// <summary>The recorded versions of template <paramref name="id"/>, oldest first.</summary>
    public IReadOnlyList<string> VersionsOf(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var prefix = id + "@";
        var versions = _hashes.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Select(k => k[prefix.Length..]).ToList();
        versions.Sort(CalcCatalogue.CompareVersions);
        return versions;
    }

    /// <summary>Reads a manifest: a JSON object of <c>"id@version": "64 hex characters"</c>. A line that is neither is refused.</summary>
    public static TemplateVersionHistory Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length > MaxManifestBytes)
            throw new InvalidDataException("The template version manifest is larger than expected.");

        var lines = JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? [];

        foreach (var (reference, hash) in lines)
        {
            var at = reference.LastIndexOf('@');

            if (at <= 0 || at >= reference.Length - 1 || hash is not { Length: 64 } || hash.Any(c => !char.IsAsciiHexDigitLower(c)))
                throw new InvalidDataException($"The template version manifest line '{reference}' is not an id@version with a 64-character hash.");
        }

        return new TemplateVersionHistory(lines.Select(l => new KeyValuePair<string, string>(l.Key, l.Value!)));
    }

    private static TemplateVersionHistory LoadShipped()
    {
        using var stream = typeof(TemplateVersionHistory).Assembly.GetManifestResourceStream(ManifestResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ManifestResourceName}' is missing from {typeof(TemplateVersionHistory).Assembly.GetName().Name}.");

        if (stream.Length > MaxManifestBytes)
            throw new InvalidDataException("The template version manifest is larger than expected.");

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
