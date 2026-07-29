namespace Tempest.Core.Commands;

/// <summary>
/// Thrown when <see cref="ICommandRegistry.RegisterDescriptor"/> is called
/// for an Id that already has a registered <see cref="CommandDescriptor"/>.
/// </summary>
/// <remarks>
/// First registration wins; a colliding, later registration is rejected —
/// never a silent override. See ADR-0037 and RD-0041. This rule alone does
/// not establish that the first registrant was the intended owner of a
/// well-known Id — see <c>Command Framework Architecture.md</c>'s Security
/// Review, Finding CMD-1 (<c>TD-11</c>).
/// </remarks>
public sealed class DuplicateCommandIdException : CommandException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateCommandIdException"/> class.
    /// </summary>
    /// <param name="id">The command Id that is already registered.</param>
    public DuplicateCommandIdException(string id)
        : base($"A command descriptor is already registered under Id '{id}'.")
    {
        Id = id;
    }

    /// <summary>
    /// Gets the command Id that is already registered.
    /// </summary>
    public string Id { get; }
}
