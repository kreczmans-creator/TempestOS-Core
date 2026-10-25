using Tempest.Calc.Sheets;
using Tempest.Core.Configuration;

namespace Tempest.Calc.Library;

/// <summary>
/// Which optional calculator features this installation has switched on (ADR-0173). Every flag is off unless the installation's
/// configuration says <c>true</c> for it, spelled exactly: <c>Calc:Features:Lifting = true</c> for <see cref="CalcFeatures.Lifting"/>.
/// The set is fixed when the catalogue is built: nothing in the running application can switch a flag on, so there is no setter, no
/// command and no per-user setting that could be used to bypass the gate.
/// </summary>
public sealed class CalcFeatureFlags
{
    /// <summary>The configuration key prefix; a flag's key is the prefix plus its name with the first letter upper-cased.</summary>
    public const string ConfigurationPrefix = "Calc:Features:";

    private readonly HashSet<string> _enabled;

    public CalcFeatureFlags(IEnumerable<string>? enabled = null)
    {
        _enabled = new HashSet<string>(enabled ?? [], StringComparer.Ordinal);
    }

    /// <summary>Every flag off: what the product composes in by default.</summary>
    public static CalcFeatureFlags None { get; } = new();

    /// <summary>The flags that are on, ordered.</summary>
    public IReadOnlyList<string> Enabled => [.. _enabled.Order(StringComparer.Ordinal)];

    /// <summary>Whether <paramref name="flag"/> is on. A template with no flag (<see langword="null"/> or empty) is never gated.</summary>
    public bool IsEnabled(string? flag) => string.IsNullOrEmpty(flag) || _enabled.Contains(flag);

    /// <summary>The configuration key that switches <paramref name="flag"/> on.</summary>
    public static string ConfigurationKey(string flag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flag);
        return ConfigurationPrefix + char.ToUpperInvariant(flag[0]) + flag[1..];
    }

    /// <summary>
    /// The flags the installation's configuration switches on: only the exact value <c>true</c> (any case, trimmed) counts; a missing key,
    /// <c>false</c>, <c>1</c>, <c>yes</c> or anything else leaves the flag off.
    /// </summary>
    public static CalcFeatureFlags FromConfiguration(IConfigurationProvider? configuration)
    {
        if (configuration is null)
            return None;

        var on = new List<string>();

        foreach (var flag in CalcFeatures.All)
        {
            if (configuration.TryGetValue(ConfigurationKey(flag), out var value) && string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                on.Add(flag);
        }

        return new CalcFeatureFlags(on);
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
