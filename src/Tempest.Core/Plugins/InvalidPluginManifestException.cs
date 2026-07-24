namespace Tempest.Core.Plugins;

/// <summary>
/// Thrown when a plugin manifest cannot be read or is malformed — invalid JSON,
/// or a required field that is missing, empty, or whitespace.
/// </summary>
/// <remarks>
/// ADR-0025, category 2 — isolated to the one candidate plugin, logged at
/// <see cref="Logging.LogLevel.Warning"/>.
/// </remarks>
public sealed class InvalidPluginManifestException : PluginException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="InvalidPluginManifestException"/> class.
    /// </summary>
    /// <param name="message">A message describing why the manifest is invalid.</param>
    public InvalidPluginManifestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="InvalidPluginManifestException"/> class.
    /// </summary>
    /// <param name="message">A message describing why the manifest is invalid.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public InvalidPluginManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
