namespace Tempest.Core.Commands;

/// <summary>
/// Thrown when <see cref="ICommandDispatcher.RegisterHandler{TCommand}"/> is
/// called for a command type that already has a registered handler.
/// </summary>
/// <remarks>
/// First registration wins; a colliding, later registration is rejected —
/// never a silent override. See ADR-0037 and RD-0041.
/// </remarks>
public sealed class DuplicateCommandHandlerException : CommandException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateCommandHandlerException"/> class.
    /// </summary>
    /// <param name="commandType">The command type that already has a registered handler.</param>
    public DuplicateCommandHandlerException(Type commandType)
        : base($"A handler is already registered for command type '{commandType.Name}'.")
    {
        CommandType = commandType;
    }

    /// <summary>
    /// Gets the command type that already has a registered handler.
    /// </summary>
    public Type CommandType { get; }
}
