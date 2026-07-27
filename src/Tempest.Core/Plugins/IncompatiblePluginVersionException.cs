namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a well-formed plugin manifest declares a
/// <c>MinimumPlatformVersion</c> that exceeds the running platform's own version.
/// </summary>
/// <remarks>
/// ADR-0025, category 4 — isolated to the one candidate plugin, logged at
/// <see cref="Logging.LogLevel.Information"/>: an old plugin correctly declining
/// to run on a newer platform is an expected outcome, not a mistake.
/// </remarks>
public sealed class IncompatiblePluginVersionException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="IncompatiblePluginVersionException"/> class.
    /// </summary>
    /// <param name="pluginId">The incompatible plugin's declared identifier.</param>
    /// <param name="declaredMinimumVersion">The plugin's declared minimum platform version.</param>
    /// <param name="runningPlatformVersion">The running platform's own version.</param>
    public IncompatiblePluginVersionException(string pluginId, Version declaredMinimumVersion, Version runningPlatformVersion)
        : base(
            $"Plugin '{pluginId}' requires platform version {declaredMinimumVersion} or later; " +
            $"the running platform is version {runningPlatformVersion}.")
    {
        PluginId = pluginId;
        DeclaredMinimumVersion = declaredMinimumVersion;
        RunningPlatformVersion = runningPlatformVersion;
    }

    /// <summary>
    /// Gets the incompatible plugin's declared identifier.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets the plugin's declared minimum platform version.
    /// </summary>
    public Version DeclaredMinimumVersion { get; }

    /// <summary>
    /// Gets the running platform's own version.
    /// </summary>
    public Version RunningPlatformVersion { get; }
}
