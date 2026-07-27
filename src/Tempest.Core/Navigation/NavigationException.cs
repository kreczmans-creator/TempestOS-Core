namespace Tempest.Core.Navigation;

/// <summary>
/// The base exception thrown when an operation against an
/// <see cref="INavigationProvider"/> fails.
/// </summary>
public class NavigationException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="NavigationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public NavigationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="NavigationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public NavigationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
