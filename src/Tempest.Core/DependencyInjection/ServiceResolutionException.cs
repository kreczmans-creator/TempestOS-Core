namespace Tempest.Core.DependencyInjection;

/// <summary>
/// The base exception thrown when an <see cref="ITempestServiceProvider"/> fails to
/// resolve a service.
/// </summary>
public class ServiceResolutionException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ServiceResolutionException"/> class.
    /// </summary>
    /// <param name="message">A message describing the resolution failure.</param>
    public ServiceResolutionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ServiceResolutionException"/> class.
    /// </summary>
    /// <param name="message">A message describing the resolution failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public ServiceResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
