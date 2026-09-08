namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin manifest declares a <c>Signature</c> that fails to
/// verify — the thumbprint is not in the trust store, the signature does
/// not verify against the recomputed payload, or the certificate is outside
/// its validity window.
/// </summary>
/// <remarks>
/// ADR-0025/ADR-0112, category 15 — isolated to the one candidate plugin,
/// logged at <see cref="Logging.LogLevel.Error"/>. Never falls back to
/// Unsigned-Local — a broken signature is treated as tampering, not
/// absence, mirroring how ADR-0025 already distinguishes a malformed
/// manifest (category 2) from a well-formed-but-incompatible one
/// (category 4). Checked at Plugin Discovery, before Plugin Loading, so the
/// plugin's assembly is never <c>Assembly.LoadFrom</c>'d.
/// </remarks>
public sealed class PluginSignatureVerificationFailedException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the
    /// <see cref="PluginSignatureVerificationFailedException"/> class.
    /// </summary>
    /// <param name="pluginId">The isolated plugin's declared identifier.</param>
    /// <param name="reason">A short description of why verification failed.</param>
    public PluginSignatureVerificationFailedException(string pluginId, string reason)
        : base($"Plugin '{pluginId}' declares a Signature that failed to verify: {reason}.")
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Gets the isolated plugin's declared identifier.
    /// </summary>
    public string PluginId { get; }
}
