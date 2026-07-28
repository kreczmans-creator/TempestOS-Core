using Tempest.Core.Logging;

namespace Tempest.Core.Commands;

/// <summary>
/// The concrete <see cref="ICommandDispatcher"/> implementation.
/// </summary>
/// <remarks>
/// Delegates its actual handler storage and lookup to a shared
/// <see cref="CommandHandlerTable"/> — see that type's own remarks for why.
/// A handler's own exception is logged at <see cref="LogLevel.Error"/> for
/// diagnostic visibility, then rethrown uncaught — never isolated. See
/// ADR-0038.
/// </remarks>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly CommandHandlerTable _table;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="CommandDispatcher"/> class.
    /// </summary>
    /// <param name="table">The shared handler table this dispatcher operates against.</param>
    /// <param name="logger">
    /// An optional logger used to record registration, dispatch, and failure
    /// activity via the logging abstraction. May be <see langword="null"/>
    /// if logging is not required.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
    public CommandDispatcher(CommandHandlerTable table, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(table);

        _table = table;
        _logger = logger;
    }

    /// <inheritdoc />
    public void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(handler);

        _table.Register(handler);

        _logger?.Information($"Command handler registered for '{typeof(TCommand).Name}'.");
    }

    /// <inheritdoc />
    public async Task<CommandResult> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger?.Information($"Dispatching command '{typeof(TCommand).Name}'.");

        CommandResult result;

        try
        {
            result = await _table.DispatchAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Error($"Command '{typeof(TCommand).Name}' handler threw.", ex);
            throw;
        }

        if (result.Succeeded)
            _logger?.Information($"Command '{typeof(TCommand).Name}' dispatched: Succeeded.");
        else
            _logger?.Warning($"Command '{typeof(TCommand).Name}' dispatched: Failed ({result.Message}).");

        return result;
    }
}
