namespace Tempest.Core.Commands;

/// <summary>
/// Thrown when <see cref="ICommandRegistry.InvokeAsync"/> is called with an
/// Id that has not been registered.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Navigation.NavigationItemNotFoundException"/>'s own
/// "unknown Id" precedent — this is application logic's own error to
/// handle (a caller invoking a stale or mistyped Id), not a Host-level
/// concern.
/// </remarks>
public sealed class CommandNotFoundException : CommandException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CommandNotFoundException"/> class.
    /// </summary>
    /// <param name="id">The command Id that was not found.</param>
    public CommandNotFoundException(string id)
        : base($"No command descriptor is registered under Id '{id}'.")
    {
        Id = id;
    }

    /// <summary>
    /// Gets the command Id that was not found.
    /// </summary>
    public string Id { get; }
}
