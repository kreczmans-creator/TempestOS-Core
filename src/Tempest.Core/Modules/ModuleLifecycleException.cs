namespace Tempest.Core.Modules;

/// <summary>
/// The base exception thrown when a module lifecycle operation fails.
/// </summary>
public class ModuleLifecycleException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleLifecycleException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public ModuleLifecycleException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleLifecycleException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public ModuleLifecycleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
