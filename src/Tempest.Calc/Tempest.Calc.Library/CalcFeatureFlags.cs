using System.Text.Json;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// Which optional calculator features this installation has switched on (ADR-0173). Every flag is off unless the installation's own
/// <c>appsettings.json</c>, in the folder beside the executable, says <c>true</c> for it: <c>{ "Calc": { "Features": { "Lifting": true } } }</c>.
/// That file is in the install folder, which an ordinary user cannot write; the current directory, the environment and the command line are
/// never read for a feature flag, so an engineer cannot self-enable an unreviewed calculator by starting the application differently. The set is
/// fixed when the catalogue is built: nothing in the running application can switch a flag on, so there is no setter, no command and no
/// per-user setting that could be used to bypass the gate.
/// </summary>
public sealed class CalcFeatureFlags
{
    /// <summary>The configuration key prefix; a flag's key is the prefix plus its name with the first letter upper-cased.</summary>
    public const string ConfigurationPrefix = "Calc:Features:";

    /// <summary>The file the flags are read from, in the install folder.</summary>
    public const string InstallFileName = "appsettings.json";

    /// <summary>The largest file read (a flag file is a few lines; anything bigger is not read).</summary>
    public const int MaxFileBytes = 1_000_000;

    private readonly Dictionary<string, string> _enabled;

    /// <param name="enabled">The flags that are on.</param>
    /// <param name="source">Where they were switched on, for the log and the issued snapshot (for example the file path); null for a test harness.</param>
    public CalcFeatureFlags(IEnumerable<string>? enabled = null, string? source = null)
    {
        _enabled = (enabled ?? []).Distinct(StringComparer.Ordinal).ToDictionary(f => f, _ => source ?? "set in code (test or tool host)", StringComparer.Ordinal);
    }

    /// <summary>Every flag off: what the product composes in by default.</summary>
    public static CalcFeatureFlags None { get; } = new();

    /// <summary>The flags that are on, ordered.</summary>
    public IReadOnlyList<string> Enabled => [.. _enabled.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Whether <paramref name="flag"/> is on. A template with no flag (<see langword="null"/> or empty) is never gated.</summary>
    public bool IsEnabled(string? flag) => string.IsNullOrEmpty(flag) || _enabled.ContainsKey(flag);

    /// <summary>The source a flag that is on was read from, or <see langword="null"/> when it is off.</summary>
    public string? SourceOf(string? flag) => flag is not null && _enabled.TryGetValue(flag, out var source) ? source : null;

    /// <summary>The configuration key that switches <paramref name="flag"/> on.</summary>
    public static string ConfigurationKey(string flag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flag);
        return ConfigurationPrefix + char.ToUpperInvariant(flag[0]) + flag[1..];
    }

    /// <summary>
    /// The flags the installation switches on, read from <c>appsettings.json</c> in <paramref name="installDirectory"/> (the folder beside the
    /// executable) and from nowhere else. Only the JSON value <c>true</c> (or the string "true", any case) counts; a missing file, a missing key,
    /// <c>false</c>, <c>1</c>, <c>yes</c>, a typo or a file that does not parse leaves the flag off. <paramref name="problem"/> says why a file was
    /// ignored, for the log.
    /// </summary>
    public static CalcFeatureFlags FromInstallFile(string? installDirectory, out string? problem)
    {
        problem = null;

        if (string.IsNullOrWhiteSpace(installDirectory))
            return None;

        var path = Path.Combine(installDirectory, InstallFileName);

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
                return None;

            if (info.Length > MaxFileBytes)
            {
                problem = $"{path} is larger than {MaxFileBytes} bytes and was not read for feature flags.";
                return None;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 16 });

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("Calc", out var calc) || calc.ValueKind != JsonValueKind.Object
                || !calc.TryGetProperty("Features", out var features) || features.ValueKind != JsonValueKind.Object)
                return None;

            var on = new List<string>();

            foreach (var flag in CalcFeatures.All)
            {
                foreach (var property in features.EnumerateObject())
                {
                    if (!string.Equals(property.Name, char.ToUpperInvariant(flag[0]) + flag[1..], StringComparison.Ordinal))
                        continue;

                    var value = property.Value;

                    if (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && string.Equals(value.GetString()?.Trim(), "true", StringComparison.OrdinalIgnoreCase)))
                        on.Add(flag);
                }
            }

            return new CalcFeatureFlags(on, $"{InstallFileName} beside the executable ({path})");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            problem = $"{path} could not be read for feature flags ({ex.GetType().Name}); every feature stays off.";
            return None;
        }
    }
}

/// <summary>
/// Thrown when something asks for a calculator whose feature flag is off. A <see cref="KeyNotFoundException"/>, so every caller that
/// already treats an unregistered template as absent treats a switched-off one the same way; the message says why.
/// </summary>
public sealed class CalcFeatureDisabledException : KeyNotFoundException
{
    public CalcFeatureDisabledException(string message) : base(message) { }

    public CalcFeatureDisabledException() { }

    public CalcFeatureDisabledException(string message, Exception innerException) : base(message, innerException) { }
}
