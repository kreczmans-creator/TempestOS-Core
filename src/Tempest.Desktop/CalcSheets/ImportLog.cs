using System.Globalization;
using Tempest.Core.Logging;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The one Information line per CAD or scan import step that support has to go on when "my scan file won't import" (Grey GR-B-02):
/// the kind of import, the file name (never its path), its size, the first 12 hex digits of its SHA-256 and the counts and refusal
/// codes of the step. Never a value read from the file, a parameter name, a path or a free-text refusal (those can quote the file).
/// A null logger logs nothing.
/// </summary>
internal static class ImportLog
{
    /// <summary>The number of SHA-256 hex digits logged: enough to tell two files apart, not enough to be the file.</summary>
    public const int HashDigits = 12;

    /// <summary>The first <see cref="HashDigits"/> hex digits of a SHA-256.</summary>
    public static string HashPrefix(string? sha256) => string.IsNullOrEmpty(sha256) ? "none" : sha256[..Math.Min(HashDigits, sha256.Length)].ToLowerInvariant();

    /// <summary>The file's own name, whatever the picker handed over (a path is cut to its last segment).</summary>
    public static string FileName(string? name) => string.IsNullOrWhiteSpace(name) ? "unnamed" : Path.GetFileName(name.Replace('\\', '/'));

    /// <summary>Writes "<paramref name="what"/> (key=value, …)" as an Information line, with the same pairs as structured properties.</summary>
    public static void Info(ILogger? logger, string what, params (string Key, object? Value)[] fields)
    {
        if (logger is null)
            return;

        var properties = fields.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
        var text = string.Join(", ", fields.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Key}={f.Value}")));
        logger.Information(fields.Length == 0 ? what : $"{what} ({text})", null, properties);
    }
}
