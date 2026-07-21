namespace Tempest.Core.Modules;

/// <summary>
/// The base exception thrown when an operation against an
/// <see cref="IRuntimeModuleManager"/> fails.
/// </summary>
public class ModuleRegistrationException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleRegistrationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public ModuleRegistrationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleRegistrationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public ModuleRegistrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
