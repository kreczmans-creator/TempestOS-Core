namespace Tempest.Core.Configuration;

/// <summary>
/// Thrown when <see cref="ConfigurationBuilder.Build"/> finds an entry with a null or
/// empty key, or a null value, in a <see cref="IConfigurationSource"/>.
/// </summary>
public sealed class InvalidConfigurationEntryException : ConfigurationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="InvalidConfigurationEntryException"/> class.
    /// </summary>
    /// <param name="reason">A message describing what was invalid about the entry.</param>
    /// <param name="sourceType">The concrete type of the offending <see cref="IConfigurationSource"/>.</param>
    public InvalidConfigurationEntryException(string reason, Type sourceType)
        : base($"{reason} (source: '{sourceType.Name}').")
    {
        SourceType = sourceType;
    }

    /// <summary>
    /// Gets the concrete type of the offending <see cref="IConfigurationSource"/>.
    /// </summary>
    public Type SourceType { get; }
}
