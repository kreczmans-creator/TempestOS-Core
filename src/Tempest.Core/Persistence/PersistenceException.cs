namespace Tempest.Core.Persistence;

/// <summary>
/// The base exception thrown when a Persistence operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Identity.IdentityException"/>'s and
/// <see cref="Commands.CommandException"/>'s own base-plus-subtype
/// pattern.
/// </remarks>
public class PersistenceException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PersistenceException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public PersistenceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="PersistenceException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public PersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
