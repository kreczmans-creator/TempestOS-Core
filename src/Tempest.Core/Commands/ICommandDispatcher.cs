namespace Tempest.Core.Commands;

/// <summary>
/// Registers command handlers and dispatches an already-constructed,
/// concretely-typed command to its one registered handler.
/// </summary>
/// <remarks>
/// <para>
/// A Platform Service (ADR-0036), DI-public like <see cref="Events.IEventBus"/>
/// and <see cref="Navigation.INavigationProvider"/> — resolved via ordinary
/// constructor injection, never a Host-owned collaborator, since it carries
/// no orchestration authority over the module pipeline.
/// </para>
/// <para>
/// Serves a caller that already has a concrete, typed command instance with
/// real data. A caller with only a string Id (a menu, a keyboard shortcut,
/// automation, or a future AI service) uses
/// <see cref="ICommandRegistry.InvokeAsync"/> instead — see
/// <c>Command Framework Architecture.md</c> for why these are two separate
/// contracts, not one.
/// </para>
/// </remarks>
public interface ICommandDispatcher
{
    /// <summary>
    /// Registers <paramref name="handler"/> as the one handler for
    /// <typeparamref name="TCommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command type <paramref name="handler"/> handles.</typeparam>
    /// <param name="handler">The handler instance to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateCommandHandlerException">
    /// A handler is already registered for <typeparamref name="TCommand"/>.
    /// </exception>
    void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;

    /// <summary>
    /// Dispatches <paramref name="command"/> to its one registered handler.
    /// </summary>
    /// <typeparam name="TCommand">The command's concrete type.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">A token observed while the handler runs.</param>
    /// <returns>The <see cref="CommandResult"/> the handler returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="CommandHandlerNotRegisteredException">
    /// No handler is registered for <typeparamref name="TCommand"/>.
    /// </exception>
    /// <remarks>
    /// A handler's own exception propagates directly out of this method —
    /// it is never caught, logged, and isolated the way
    /// <see cref="Events.IEventBus.PublishAsync{TEvent}"/> isolates a
    /// subscriber's exception. See ADR-0038.
    /// </remarks>
    Task<CommandResult> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : ICommand;
}
