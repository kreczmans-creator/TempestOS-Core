using Tempest.Core.Commands;
using Tempest.Core.Logging;

namespace Tempest.Core.Input;

/// <summary>The concrete <see cref="IInputBindingRegistry"/> implementation.</summary>
/// <remarks>
/// Isolates a throwing provider or a failed invocation — logged, never
/// propagated back into the provider that raised it — mirroring
/// <see cref="Events.IEventBus"/>'s own established subscriber-isolation
/// precedent (`ADR-0028`): one misbehaving controller must not be able to
/// crash every other bound input source.
/// </remarks>
public sealed class InputBindingRouter : IInputBindingRegistry
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly ILogger? _logger;
    private readonly object _gate = new();
    private readonly List<IInputBindingProvider> _providers = [];
    private readonly Dictionary<IInputBindingProvider, Action<string>> _handlersByProvider = new();

    /// <summary>Initialises a new instance of the <see cref="InputBindingRouter"/> class.</summary>
    public InputBindingRouter(ICommandRegistry commandRegistry, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _commandRegistry = commandRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<IInputBindingProvider> Providers
    {
        get
        {
            lock (_gate)
                return _providers.ToList();
        }
    }

    /// <inheritdoc />
    public void Register(IInputBindingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        void Handler(string commandId) => RouteAsync(provider, commandId);

        lock (_gate)
        {
            if (_handlersByProvider.ContainsKey(provider))
                return;

            _handlersByProvider[provider] = Handler;
            _providers.Add(provider);
            provider.CommandRequested += Handler;
        }

        _logger?.Information($"Input binding provider registered: '{provider.SourceName}'.");
    }

    /// <inheritdoc />
    public void Unregister(IInputBindingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (_gate)
        {
            if (!_handlersByProvider.Remove(provider, out var handler))
                return;

            provider.CommandRequested -= handler;
            _providers.Remove(provider);
        }

        _logger?.Information($"Input binding provider unregistered: '{provider.SourceName}'.");
    }

    /// <summary>
    /// Invokes <paramref name="commandId"/> via <see cref="ICommandRegistry.InvokeAsync"/>,
    /// fire-and-forget from <paramref name="provider"/>'s own synchronous
    /// event — any exception (a bad Id, a handler that throws) is caught
    /// and logged here, never left to propagate back into
    /// <paramref name="provider"/>'s own event-raising code.
    /// </summary>
    private async void RouteAsync(IInputBindingProvider provider, string commandId)
    {
        try
        {
            await _commandRegistry.InvokeAsync(commandId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.Error($"Input binding provider '{provider.SourceName}' requested command '{commandId}', which failed.", ex);
        }
    }
}
