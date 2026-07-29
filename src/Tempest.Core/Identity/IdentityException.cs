namespace Tempest.Core.Identity;

/// <summary>
/// The base exception thrown when an Identity &amp; Permissions operation
/// fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Commands.CommandException"/>'s own base-plus-subtype
/// pattern.
/// </remarks>
public class IdentityException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="IdentityException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public IdentityException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="IdentityException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public IdentityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
