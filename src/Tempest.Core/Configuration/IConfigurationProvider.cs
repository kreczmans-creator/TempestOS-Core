namespace Tempest.Core.Configuration;

/// <summary>
/// Read-only access to the runtime's configuration values.
/// </summary>
/// <remarks>
/// Configuration is data, not behaviour: it is loaded once, before the rest of the
/// runtime starts, and is immutable thereafter. This interface exposes no mutation
/// method of any kind — consumers read configuration; they never modify it. Keys are
/// case-insensitive throughout.
/// </remarks>
public interface IConfigurationProvider
{
    /// <summary>
    /// Gets the configured value for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The configuration key to look up.</param>
    /// <returns>The configured value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConfigurationKeyNotFoundException">No value is configured for <paramref name="key"/>.</exception>
    string Get(string key);

    /// <summary>
    /// Attempts to get the configured value for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The configuration key to look up.</param>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, the configured value;
    /// otherwise, <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="key"/> has a configured value; otherwise, <see langword="false"/>.</returns>
    bool TryGetValue(string key, out string? value);

    /// <summary>
    /// Determines whether a value is configured for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The configuration key to check.</param>
    /// <returns><see langword="true"/> if <paramref name="key"/> has a configured value; otherwise, <see langword="false"/>.</returns>
    bool ContainsKey(string key);

    /// <summary>
    /// Gets every configured key/value pair.
    /// </summary>
    /// <returns>Every configured key/value pair.</returns>
    IEnumerable<KeyValuePair<string, string>> GetAll();
}
