namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin's manifest <c>RequestedCapabilities</c> includes a
/// key outside its assigned trust tier's ceiling, or a plugin module's
/// constructor requires a service type neither in the fixed always-allowed
/// baseline nor covered by an eligible, granted
/// <c>plugin.services.resolve:*</c> declaration.
/// </summary>
/// <remarks>
/// ADR-0025/ADR-0111/ADR-0112, category 17 — isolated to the one candidate
/// plugin, logged at <see cref="Logging.LogLevel.Warning"/>. Checked
/// entirely within Plugin Loading (Phase 3.2) — the plugin never reaches
/// Module Discovery (ADR-0111). Recorded in the Plugin Registry as
/// <see cref="PluginRegistryState.TrustDenied"/> — the sixth registry state
/// reserved for this decision.
/// </remarks>
public sealed class PluginTrustDeniedException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginTrustDeniedException"/>
    /// class.
    /// </summary>
    /// <param name="pluginId">The isolated plugin's declared identifier.</param>
    /// <param name="reason">A short description of why the plugin was denied.</param>
    public PluginTrustDeniedException(string pluginId, string reason)
        : base($"Plugin '{pluginId}' was denied: {reason}.")
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Gets the isolated plugin's declared identifier.
    /// </summary>
    public string PluginId { get; }
}
