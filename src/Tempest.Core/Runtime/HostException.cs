namespace Tempest.Core.Runtime;

/// <summary>
/// The base exception thrown when a <see cref="ITempestHost"/> operation fails.
/// </summary>
public class HostException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="HostException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public HostException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="HostException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public HostException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
