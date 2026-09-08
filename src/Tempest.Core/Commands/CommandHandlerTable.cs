namespace Tempest.Core.Commands;

/// <summary>
/// The type-keyed handler store shared by <see cref="CommandDispatcher"/>
/// and <see cref="CommandRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// An implementation-supporting collaborator, not a third front-door
/// contract — <see cref="ICommandDispatcher"/> and <see cref="ICommandRegistry"/>
/// remain the Command Framework's only two documented, architecturally
/// significant public contracts (ADR-0036/ADR-0037). This type is
/// <see langword="public"/> only because a type referenced by a
/// container-constructed service's public constructor must itself be at
/// least as accessible as that constructor (C# CS0051) — it is not, and is
/// not intended to be, a consumer-facing API. Exists because
/// <see cref="ICommandRegistry.InvokeAsync"/> must dispatch a command whose
/// concrete type is known only at runtime (from
/// <see cref="CommandDescriptor.CreateDefault"/>), while
/// <see cref="ICommandDispatcher"/>'s own public contract is deliberately
/// generic-only (<see cref="ICommandDispatcher.DispatchAsync{TCommand}"/>) —
/// see <c>Command Framework Architecture.md</c>'s Architecture Verification
/// section for the full reasoning. Registered as an ordinary,
/// container-constructed singleton (mirroring every other stateful platform
/// service in this codebase) so that whichever of
/// <see cref="ICommandDispatcher"/>/<see cref="ICommandRegistry"/> a module
/// resolves, both operate against the identical, shared handler set. A
/// caller that resolves this type directly, bypassing
/// <see cref="ICommandDispatcher"/>, gains no capability beyond what
/// <see cref="ICommandDispatcher"/> already grants identically — only
/// <see cref="CommandDispatcher"/>'s own logging wrapper is bypassed.
/// </para>
/// <para>
/// Handlers are stored as type-erased delegates, closing over the
/// originally-registered, strongly-typed <see cref="ICommandHandler{TCommand}"/>
/// instance — this avoids reflection entirely: the closure created in
/// <see cref="Register{TCommand}"/> already knows <c>TCommand</c> at the
/// point it is created, so invoking it later never needs to construct a
/// generic method at runtime.
/// </para>
/// <para>
/// <b>Frozen by ADR-0146 (<c>WP 17.2A</c>).</b> This class used to carry a
/// second dictionary of owning component principals, an optional
/// component-scope accessor and an optional permission evaluator, so that a
/// higher-trust-tier plugin could evict a lower one's handler and so that a
/// plugin's handler ran inside its own pushed component scope. Nothing
/// third-party ever loaded, so registration is unconditional duplicate
/// rejection again — exactly what <c>ADR-0037</c> originally specified —
/// and the trust platform is frozen at
/// <c>src/Frozen/Tempest.Core.Plugins</c>.
/// </para>
/// </remarks>
public sealed class CommandHandlerTable
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, Func<ICommand, CancellationToken, Task<CommandResult>>> _handlersByCommandType = new();

    /// <summary>
    /// Registers <paramref name="handler"/> as the one handler for
    /// <typeparamref name="TCommand"/>.
    /// </summary>
    public void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
    {
        lock (_gate)
        {
            if (_handlersByCommandType.ContainsKey(typeof(TCommand)))
                throw new DuplicateCommandHandlerException(typeof(TCommand));

            _handlersByCommandType.Add(typeof(TCommand), (command, cancellationToken) => handler.HandleAsync((TCommand)command, cancellationToken));
        }
    }

    /// <summary>
    /// Dispatches <paramref name="command"/> to its one registered handler,
    /// looked up by <see cref="object.GetType"/>.
    /// </summary>
    public async Task<CommandResult> DispatchAsync(ICommand command, CancellationToken cancellationToken)
    {
        Func<ICommand, CancellationToken, Task<CommandResult>>? handler;

        lock (_gate)
            _handlersByCommandType.TryGetValue(command.GetType(), out handler);

        if (handler is null)
            throw new CommandHandlerNotRegisteredException(command.GetType());

        return await handler(command, cancellationToken).ConfigureAwait(false);
    }
}
