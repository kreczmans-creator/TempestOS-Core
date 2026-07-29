namespace Tempest.Core.Commands;

/// <summary>
/// The base exception thrown when a Command Framework operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Navigation.NavigationException"/>'s own base-plus-subtype
/// pattern.
/// </remarks>
public class CommandException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CommandException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public CommandException(string message)
        : base(message)
    {
    }
}
