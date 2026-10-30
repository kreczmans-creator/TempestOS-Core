namespace Tempest.Calc.Library;

/// <summary>
/// Which optional calculator features this installation has switched on (ADR-0173). Every flag is off unless the machine's administrator has
/// switched it on, in a source only an administrator can write (<see cref="ICalcFeatureSource"/>: the HKLM policy key on Windows, a root-owned file
/// elsewhere). The application installs per user, so neither its install folder, the current directory, the environment nor the command line is a
/// source: an engineer cannot self-enable an unreviewed calculator. The set is fixed when the catalogue is built: nothing in the running application
/// can switch a flag on, so there is no setter, no command and no per-user setting that could be used to bypass the gate.
/// </summary>
public sealed class CalcFeatureFlags
{
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

    /// <summary>The flags a trusted source switches on; a source that throws is no source: every feature stays off and <paramref name="problem"/> says why.</summary>
    public static CalcFeatureFlags FromSource(ICalcFeatureSource source, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            return source.Read(out problem);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            problem = $"The feature source could not be read ({ex.GetType().Name}); every feature stays off.";
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
