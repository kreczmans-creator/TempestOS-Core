namespace Tempest.Core.Configuration;

/// <summary>
/// Thrown when <see cref="ConfigurationBuilder.Build"/> finds the same key more than
/// once within a single <see cref="IConfigurationSource"/>.
/// </summary>
/// <remarks>
/// A key appearing in more than one <em>different</em> source is not an error — later
/// sources are expected to override earlier ones. This exception is specifically for a
/// single source producing the same key twice, which is always a defect in that
/// source, never legitimate override behaviour.
/// </remarks>
public sealed class DuplicateConfigurationKeyException : ConfigurationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateConfigurationKeyException"/> class.
    /// </summary>
    /// <param name="key">The key that appeared more than once.</param>
    /// <param name="sourceType">The concrete type of the offending <see cref="IConfigurationSource"/>.</param>
    public DuplicateConfigurationKeyException(string key, Type sourceType)
        : base($"Duplicate configuration key '{key}' detected within source '{sourceType.Name}'.")
    {
        Key = key;
        SourceType = sourceType;
    }

    /// <summary>
    /// Gets the key that appeared more than once.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the concrete type of the offending <see cref="IConfigurationSource"/>.
    /// </summary>
    public Type SourceType { get; }
}
