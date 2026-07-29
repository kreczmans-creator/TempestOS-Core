namespace Tempest.Core.Commands;

/// <summary>
/// Handles exactly one concrete <see cref="ICommand"/> type.
/// </summary>
/// <remarks>
/// Registered with <see cref="ICommandDispatcher.RegisterHandler{TCommand}"/>
/// as an already-constructed instance — the identical shape
/// <see cref="Events.IEventHandler{TEvent}"/> already establishes for the
/// Event Bus, applied here for exactly one handler per command type rather
/// than any number of subscribers. See <c>Command Framework
/// Architecture.md</c> for the complete design and ADR-0037 for why this
/// registration model was chosen over a DI-container-resolved,
/// reflection-discovered alternative.
/// </remarks>
/// <typeparam name="TCommand">The concrete command type this handler handles.</typeparam>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    /// <summary>
    /// Handles <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token observed while handling the command.</param>
    /// <returns>
    /// A <see cref="CommandResult"/> describing whether the command
    /// succeeded. A handler that encounters a genuine defect in its own
    /// execution, rather than an expected, nameable failure case, should
    /// throw instead — see ADR-0038.
    /// </returns>
    Task<CommandResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
