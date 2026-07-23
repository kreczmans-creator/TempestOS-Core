namespace Tempest.Core.Configuration;

/// <summary>
/// Thrown when <see cref="IConfigurationProvider.Get"/> is called with a key that has
/// no configured value.
/// </summary>
public sealed class ConfigurationKeyNotFoundException : ConfigurationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ConfigurationKeyNotFoundException"/> class.
    /// </summary>
    /// <param name="key">The key that had no configured value.</param>
    public ConfigurationKeyNotFoundException(string key)
        : base($"No configuration value is registered for key '{key}'.")
    {
        Key = key;
    }

    /// <summary>
    /// Gets the key that had no configured value.
    /// </summary>
    public string Key { get; }
}
