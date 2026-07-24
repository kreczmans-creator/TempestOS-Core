namespace Tempest.Core.Plugins;

/// <summary>
/// The base exception thrown when Plugin Discovery or Plugin Loading fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Modules.ModuleDiscoveryException"/>'s own base-plus-subtype
/// pattern. Per ADR-0025, every subtype except the Host's own orchestration
/// defect is isolated to the one candidate plugin — a single
/// <c>catch (PluginException)</c> at the Plugin Discovery/Loading call site is
/// sufficient to implement that isolation uniformly, without needing to catch
/// each subtype separately.
/// </remarks>
public class PluginException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public PluginException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public PluginException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
