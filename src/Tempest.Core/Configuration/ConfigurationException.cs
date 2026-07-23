namespace Tempest.Core.Configuration;

/// <summary>
/// The base exception thrown when a configuration operation fails.
/// </summary>
public class ConfigurationException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public ConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public ConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
