namespace Tempest.Core.Modules;

/// <summary>
/// The base exception thrown when framework module discovery fails.
/// </summary>
public class ModuleDiscoveryException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">A message describing the discovery failure.</param>
    public ModuleDiscoveryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">A message describing the discovery failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public ModuleDiscoveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
