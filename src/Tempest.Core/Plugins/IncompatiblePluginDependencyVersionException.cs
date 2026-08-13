namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin's declared dependency is present among the surviving
/// candidate set, but its version falls outside the declared
/// <c>[MinimumVersion, MaximumVersion]</c> range.
/// </summary>
/// <remarks>
/// ADR-0107, category 13 — isolated to the dependent plugin, logged at
/// <see cref="Logging.LogLevel.Warning"/>. Never Host-fatal. Distinguished
/// from <see cref="MissingPluginDependencyException"/> (category 12) for
/// diagnostic clarity — "the dependency doesn't exist" and "the dependency
/// exists but is the wrong version" are different, actionable facts for a
/// plugin author.
/// </remarks>
public sealed class IncompatiblePluginDependencyVersionException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="IncompatiblePluginDependencyVersionException"/> class.
    /// </summary>
    /// <param name="pluginId">The dependent plugin's declared identifier.</param>
    /// <param name="dependencyId">The dependency's declared identifier.</param>
    /// <param name="minimumVersion">The declared minimum version required.</param>
    /// <param name="maximumVersion">The declared maximum version permitted, or <see langword="null"/> if unbounded above.</param>
    /// <param name="actualVersion">
    /// The dependency's actual, raw declared version string, or
    /// <see langword="null"/> if it could not be parsed as a <see cref="System.Version"/>
    /// for comparison.
    /// </param>
    public IncompatiblePluginDependencyVersionException(
        string pluginId, string dependencyId, Version minimumVersion, Version? maximumVersion, string? actualVersion)
        : base(BuildMessage(pluginId, dependencyId, minimumVersion, maximumVersion, actualVersion))
    {
        PluginId = pluginId;
        DependencyId = dependencyId;
        MinimumVersion = minimumVersion;
        MaximumVersion = maximumVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>
    /// Gets the dependent plugin's declared identifier.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets the dependency's declared identifier.
    /// </summary>
    public string DependencyId { get; }

    /// <summary>
    /// Gets the declared minimum version required.
    /// </summary>
    public Version MinimumVersion { get; }

    /// <summary>
    /// Gets the declared maximum version permitted, or <see langword="null"/> if unbounded above.
    /// </summary>
    public Version? MaximumVersion { get; }

    /// <summary>
    /// Gets the dependency's actual, raw declared version string, or
    /// <see langword="null"/> if it could not be parsed as a <see cref="System.Version"/>
    /// for comparison.
    /// </summary>
    public string? ActualVersion { get; }

    private static string BuildMessage(
        string pluginId, string dependencyId, Version minimumVersion, Version? maximumVersion, string? actualVersion)
    {
        var range = maximumVersion is null
            ? $"[{minimumVersion}, unbounded)"
            : $"[{minimumVersion}, {maximumVersion}]";

        var actual = actualVersion ?? "unparseable";

        return $"Plugin '{pluginId}' depends on '{dependencyId}' with required version range {range}, " +
               $"but the present version is '{actual}'.";
    }
}
