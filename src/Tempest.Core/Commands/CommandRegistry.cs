using Tempest.Core.Logging;

namespace Tempest.Core.Commands;

/// <summary>
/// The concrete <see cref="ICommandRegistry"/> implementation.
/// </summary>
/// <remarks>
/// Registered items are held in a single, lock-guarded dictionary keyed by
/// <see cref="CommandDescriptor.Id"/>, mirroring
/// <see cref="Navigation.NavigationService"/>'s own pattern exactly.
/// Delegates dispatch to the shared <see cref="CommandHandlerTable"/> — see
/// that type's own remarks for why this is necessary.
/// </remarks>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CommandDescriptor> _descriptorsById = new(StringComparer.Ordinal);
    private readonly CommandHandlerTable _table;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="CommandRegistry"/> class.
    /// </summary>
    /// <param name="table">The shared handler table this registry dispatches through.</param>
    /// <param name="logger">
    /// An optional logger used to record registration and invocation
    /// activity via the logging abstraction. May be <see langword="null"/>
    /// if logging is not required.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
    public CommandRegistry(CommandHandlerTable table, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(table);

        _table = table;
        _logger = logger;
    }

    /// <inheritdoc />
    public void RegisterDescriptor(CommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        lock (_gate)
        {
            if (_descriptorsById.ContainsKey(descriptor.Id))
                throw new DuplicateCommandIdException(descriptor.Id);

            _descriptorsById.Add(descriptor.Id, descriptor);
        }

        _logger?.Information($"Command descriptor registered: '{descriptor.Id}' ({descriptor.DisplayName}).");
    }

    /// <inheritdoc />
    public IReadOnlyList<CommandDescriptor> Items
    {
        get
        {
            lock (_gate)
            {
                return _descriptorsById.Values
                    .OrderBy(descriptor => descriptor.Category, StringComparer.Ordinal)
                    .ThenBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    /// <inheritdoc />
    public async Task<CommandResult> InvokeAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        CommandDescriptor descriptor;

        lock (_gate)
        {
            if (!_descriptorsById.TryGetValue(id, out var found))
                throw new CommandNotFoundException(id);

            descriptor = found;
        }

        if (descriptor.CreateDefault is null)
        {
            throw new CommandException(
                $"Command '{id}' has no default-instance factory and cannot be invoked by Id.");
        }

        _logger?.Information($"Invoking command '{id}'.");

        var command = descriptor.CreateDefault();

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
            _logger?.Error($"Command '{id}' handler threw.", ex);
            throw;
        }

        if (result.Succeeded)
            _logger?.Information($"Command '{id}' invoked: Succeeded.");
        else
            _logger?.Warning($"Command '{id}' invoked: Failed ({result.Message}).");

        return result;
    }
}
