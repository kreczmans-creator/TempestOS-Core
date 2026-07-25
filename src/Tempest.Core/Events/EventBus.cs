using Tempest.Core.Logging;

namespace Tempest.Core.Events;

/// <summary>
/// The concrete <see cref="IEventBus"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Subscribers are held in a single, lock-guarded dictionary keyed by exact
/// event type, mirroring <see cref="Modules.RuntimeModuleManager"/>'s own
/// <c>_gate</c> pattern. <see cref="PublishAsync{TEvent}"/> takes an
/// immutable snapshot of the current subscriber list for
/// <typeparamref name="TEvent"/> under the lock, then dispatches outside
/// it — so a handler that subscribes or unsubscribes during its own
/// <c>HandleAsync</c> call (including for the same event type) does not
/// mutate the list this dispatch is currently iterating, and a nested,
/// re-entrant <see cref="PublishAsync{TEvent}"/> call from within a handler
/// is free to take its own, independent snapshot without deadlocking.
/// </para>
/// <para>
/// Dispatch is sequential and awaited, one subscriber at a time, in
/// subscription order — the same established sequential-batch shape
/// <see cref="Modules.ModuleLifecycleManager"/>'s own <c>RunBatchAsync</c>
/// already uses. Cancellation is checked between subscribers, never
/// mid-<c>HandleAsync</c>, and <see cref="OperationCanceledException"/> is
/// never isolated — it propagates directly to the publisher. Every other
/// subscriber exception is caught, logged at <see cref="LogLevel.Error"/>,
/// and never rethrown — see ADR-0028 for the complete reasoning.
/// </para>
/// </remarks>
public sealed class EventBus : IEventBus
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<object>> _subscribersByEventType = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="EventBus"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record subscription and dispatch activity
    /// via the logging abstraction. May be <see langword="null"/> if
    /// logging is not required.
    /// </param>
    public EventBus(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            GetOrCreateSubscriberList(typeof(TEvent)).Add(handler);
        }

        _logger?.Information(
            $"Handler '{handler.GetType().Name}' subscribed to '{typeof(TEvent).Name}'.");
    }

    /// <inheritdoc />
    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            if (_subscribersByEventType.TryGetValue(typeof(TEvent), out var subscribers))
                subscribers.Remove(handler);
        }

        _logger?.Information(
            $"Handler '{handler.GetType().Name}' unsubscribed from '{typeof(TEvent).Name}'.");
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        IReadOnlyList<IEventHandler<TEvent>> snapshot;

        lock (_gate)
        {
            snapshot = _subscribersByEventType.TryGetValue(typeof(TEvent), out var subscribers)
                ? subscribers.Cast<IEventHandler<TEvent>>().ToList()
                : Array.Empty<IEventHandler<TEvent>>();
        }

        _logger?.Information(
            $"Publishing '{typeof(TEvent).Name}' to {snapshot.Count} subscriber(s).");

        foreach (var handler in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Error(
                    $"Subscriber '{handler.GetType().Name}' threw while handling '{typeof(TEvent).Name}'.",
                    ex);
            }
        }

        _logger?.Information($"Publish completed for '{typeof(TEvent).Name}'.");
    }

    private List<object> GetOrCreateSubscriberList(Type eventType)
    {
        if (!_subscribersByEventType.TryGetValue(eventType, out var subscribers))
        {
            subscribers = new List<object>();
            _subscribersByEventType[eventType] = subscribers;
        }

        return subscribers;
    }
}
