namespace Tempest.Core.Commands;

/// <summary>
/// Thrown when <see cref="ICommandDispatcher.DispatchAsync{TCommand}"/> is
/// called for a command type with no registered handler.
/// </summary>
public sealed class CommandHandlerNotRegisteredException : CommandException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CommandHandlerNotRegisteredException"/> class.
    /// </summary>
    /// <param name="commandType">The command type with no registered handler.</param>
    public CommandHandlerNotRegisteredException(Type commandType)
        : base($"No handler is registered for command type '{commandType.Name}'.")
    {
        CommandType = commandType;
    }

    /// <summary>
    /// Gets the command type with no registered handler.
    /// </summary>
    public Type CommandType { get; }
}
