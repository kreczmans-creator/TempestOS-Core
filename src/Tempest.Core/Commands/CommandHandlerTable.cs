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
/// <b>Trust-ordered registration (ADR-0111, WP 13.2A).</b> A second,
/// lock-guarded dictionary tracks each registered command type's own owning
/// component principal (<see langword="null"/> = first-party), maintained in
/// lockstep with <c>_handlersByCommandType</c>. <see cref="Register{TCommand}"/>:
/// if the registrant is non-null, it must hold <c>plugin.commands.register</c>
/// (<see cref="Identity.IPermissionEvaluator.RequirePermission"/> — throws
/// <see cref="Identity.PermissionDeniedException"/> if not, propagating
/// uncaught). If the type is new, it is simply added, exactly as today. If
/// the type already has a handler: a <i>higher</i>-trust-tier registrant
/// (<see cref="Plugins.PluginTrustPermission.Rank"/>) evicts and replaces
/// the existing entry, logged loudly at <see cref="Logging.LogLevel.Warning"/>
/// — never silent; an <i>equal-or-lower</i>-tier registrant is rejected
/// exactly as today (<see cref="DuplicateCommandHandlerException"/>). No
/// <c>Unregister</c> exists here (ADR-0037's own "no Unregister/Deregister
/// is defined" — this Work Package does not add one). This is a real,
/// acknowledged, additive revision of <c>ADR-0037</c>'s own unconditional
/// duplicate-rejection behaviour — see <c>ADR-0111</c>'s own Decision
/// section for the full acknowledgement.
/// </para>
/// <para>
/// <b>Component-scope push around dispatch (ADR-0111, WP 13.2B correction).</b>
/// <see cref="DispatchAsync"/> pushes the handler's own recorded owner
/// (<see langword="null"/> = first-party) as current via
/// <see cref="Identity.CurrentComponentAccessor.BeginScope"/> for the duration
/// of the handler invocation, popping on return — mirroring
/// <see cref="Events.EventBus.PublishAsync{TEvent}"/>'s own per-subscriber
/// scope push exactly. Without this, a plugin-owned handler's own internal,
/// capability-gated calls (e.g. registering a Navigation item from inside
/// <c>HandleAsync</c>) would run under whichever component happened to be
/// ambient *before* dispatch rather than the handler's own principal,
/// silently skipping enforcement for that entire re-entry path.
/// </para>
/// </remarks>
public sealed class CommandHandlerTable
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, Func<ICommand, CancellationToken, Task<CommandResult>>> _handlersByCommandType = new();
    private readonly Dictionary<Type, Identity.IPrincipal?> _ownerByCommandType = new();
    private readonly Logging.ILogger? _logger;
    private readonly Identity.CurrentComponentAccessor? _currentComponentAccessor;
    private readonly Identity.IPermissionEvaluator? _permissionEvaluator;

    /// <summary>
    /// Initialises a new instance of the <see cref="CommandHandlerTable"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record ownership-override events via the
    /// logging abstraction. May be <see langword="null"/> if logging is not
    /// required.
    /// </param>
    /// <param name="currentComponentAccessor">
    /// An optional accessor resolving which loaded component's own code is
    /// currently registering a handler, and used to push each handler's own
    /// recorded owner as current around its invocation in
    /// <see cref="DispatchAsync"/> (ADR-0111). The concrete type, not the
    /// read-only interface, exactly as <see cref="Events.EventBus"/> already
    /// requires — <see cref="Identity.CurrentComponentAccessor.BeginScope"/>
    /// is deliberately not part of <see cref="Identity.ICurrentComponentAccessor"/>.
    /// <see langword="null"/> — the default — reproduces today's exact
    /// unconditional behaviour.
    /// </param>
    /// <param name="permissionEvaluator">
    /// An optional evaluator used to enforce <c>plugin.commands.register</c>
    /// (ADR-0111). <see langword="null"/> — the default — no-ops the check.
    /// </param>
    public CommandHandlerTable(
        Logging.ILogger? logger = null,
        Identity.CurrentComponentAccessor? currentComponentAccessor = null,
        Identity.IPermissionEvaluator? permissionEvaluator = null)
    {
        _logger = logger;
        _currentComponentAccessor = currentComponentAccessor;
        _permissionEvaluator = permissionEvaluator;
    }

    /// <summary>
    /// Registers <paramref name="handler"/> as the one handler for
    /// <typeparamref name="TCommand"/>.
    /// </summary>
    public void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
    {
        var registrant = _currentComponentAccessor?.Current;

        if (!Plugins.PluginTrustPermission.IsFirstParty(registrant))
            _permissionEvaluator?.RequirePermission(registrant!, new Identity.Permission(Plugins.PluginCapability.Commands));

        lock (_gate)
        {
            if (!_handlersByCommandType.ContainsKey(typeof(TCommand)))
            {
                _handlersByCommandType.Add(typeof(TCommand), (command, cancellationToken) => handler.HandleAsync((TCommand)command, cancellationToken));
                _ownerByCommandType.Add(typeof(TCommand), registrant);
            }
            else
            {
                var existingOwner = _ownerByCommandType[typeof(TCommand)];

                if (Plugins.PluginTrustPermission.Rank(registrant) <= Plugins.PluginTrustPermission.Rank(existingOwner))
                    throw new DuplicateCommandHandlerException(typeof(TCommand));

                _logger?.Warning(
                    $"Command handler for '{typeof(TCommand).Name}' ownership override: " +
                    $"'{existingOwner?.Identity.Id ?? "(first-party)"}' -> '{registrant?.Identity.Id ?? "(first-party)"}'.");

                _handlersByCommandType[typeof(TCommand)] = (command, cancellationToken) => handler.HandleAsync((TCommand)command, cancellationToken);
                _ownerByCommandType[typeof(TCommand)] = registrant;
            }
        }
    }

    /// <summary>
    /// Dispatches <paramref name="command"/> to its one registered handler,
    /// looked up by <see cref="object.GetType"/>.
    /// </summary>
    public async Task<CommandResult> DispatchAsync(ICommand command, CancellationToken cancellationToken)
    {
        Func<ICommand, CancellationToken, Task<CommandResult>>? handler;
        Identity.IPrincipal? owner;

        lock (_gate)
        {
            _handlersByCommandType.TryGetValue(command.GetType(), out handler);
            _ownerByCommandType.TryGetValue(command.GetType(), out owner);
        }

        if (handler is null)
            throw new CommandHandlerNotRegisteredException(command.GetType());

        using var scope = owner is not null ? _currentComponentAccessor?.BeginScope(owner) : null;

        return await handler(command, cancellationToken).ConfigureAwait(false);
    }
}
