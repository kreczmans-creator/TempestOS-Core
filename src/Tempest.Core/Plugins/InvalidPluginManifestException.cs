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
    /// <param name="pluginId">
    /// The candidate's own declared identifier, if it had already been read
    /// and validated by the point this exception was thrown (e.g. a later
    /// field, not <c>Id</c> itself, is what failed validation). <see langword="null"/>
    /// if no reliable identifier was available yet — the manifest failed to
    /// parse at all, or the <c>Id</c> field itself is what's missing/invalid
    /// (WP 13.3A fault-injection finding: every other category carries a
    /// reliable identifier at the point it throws; this is the one category
    /// where that isn't always true, so it's the one exception in this
    /// namespace where the identifier is optional rather than required).
    /// </param>
    public InvalidPluginManifestException(string message, string? pluginId = null)
        : base(message)
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="InvalidPluginManifestException"/> class.
    /// </summary>
    /// <param name="message">A message describing why the manifest is invalid.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    /// <param name="pluginId">
    /// The candidate's own declared identifier, if already known — see the
    /// other constructor's own remarks.
    /// </param>
    public InvalidPluginManifestException(string message, Exception innerException, string? pluginId = null)
        : base(message, innerException)
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Gets the candidate's own declared identifier, if it was already known
    /// at the point this exception was thrown. <see langword="null"/> if not.
    /// </summary>
    public string? PluginId { get; }
}
