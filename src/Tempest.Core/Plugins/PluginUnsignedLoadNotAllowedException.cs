namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin manifest declares no <c>Signature</c> field, and
/// <c>Plugins:AllowUnsignedLoad</c> is not enabled.
/// </summary>
/// <remarks>
/// ADR-0025/ADR-0112, category 16 — isolated to the one candidate plugin,
/// logged at <see cref="Logging.LogLevel.Warning"/>. Distinct from
/// <see cref="PluginSignatureVerificationFailedException"/> (category 15) —
/// an honest, unsigned plugin correctly declining to run under the
/// operator's current configuration, not a corrupted artefact. Checked at
/// Plugin Discovery, before Plugin Loading, so the plugin's assembly is
/// never <c>Assembly.LoadFrom</c>'d.
/// </remarks>
public sealed class PluginUnsignedLoadNotAllowedException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the
    /// <see cref="PluginUnsignedLoadNotAllowedException"/> class.
    /// </summary>
    /// <param name="pluginId">The isolated plugin's declared identifier.</param>
    public PluginUnsignedLoadNotAllowedException(string pluginId)
        : base($"Plugin '{pluginId}' declares no Signature, and Plugins:AllowUnsignedLoad is not enabled.")
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Gets the isolated plugin's declared identifier.
    /// </summary>
    public string PluginId { get; }
}
