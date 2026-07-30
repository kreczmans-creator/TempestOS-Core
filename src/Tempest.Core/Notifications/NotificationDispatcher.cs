using Tempest.Core.Logging;

namespace Tempest.Core.Notifications;

/// <summary>
/// The concrete <see cref="INotificationDispatcher"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Subscribers are held in a single, lock-guarded dictionary keyed by
/// exact notification type — the identical shape
/// <see cref="Events.EventBus"/> already uses, reused deliberately
/// rather than reinvented, per `ADR-0046`'s own "never a parallel
/// implementation of subscription/dispatch machinery" requirement.
/// <see cref="PublishAsync{TNotification}"/> takes an immutable snapshot
/// of the current subscriber list under the lock, then dispatches
/// outside it, exactly mirroring <see cref="Events.EventBus.PublishAsync{TEvent}"/>'s
/// own re-entrancy-safe design.
/// </para>
/// <para>
/// Dispatch is sequential and awaited, one subscriber at a time, in
/// subscription order. Cancellation is checked between subscribers,
/// never mid-<c>HandleAsync</c>, and <see cref="OperationCanceledException"/>
/// is never isolated — it propagates directly to the publisher. Every
/// other subscriber exception is caught and logged at
/// <see cref="LogLevel.Warning"/> (not <see cref="LogLevel.Error"/>, the
/// level <see cref="Events.EventBus"/> itself uses) — `Platform Service
/// Contracts.md`'s own Logging Requirements state explicitly "Logs a
/// warning for each isolated handler failure," a deliberate, disclosed
/// severity distinction from the Event Bus: a notification is
/// presentation-oriented and lower-stakes than a platform event, so a
/// failed notification handler is judged a warning-level operational
/// concern, not an error-level one — never rethrown either way.
/// </para>
/// </remarks>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<object>> _subscribersByNotificationType = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="NotificationDispatcher"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record subscription and dispatch
    /// activity via the logging abstraction. May be <see langword="null"/>
    /// if logging is not required.
    /// </param>
    public NotificationDispatcher(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void Subscribe<TNotification>(INotificationHandler<TNotification> handler) where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            GetOrCreateSubscriberList(typeof(TNotification)).Add(handler);
        }

        _logger?.Information(
            $"Handler '{handler.GetType().Name}' subscribed to '{typeof(TNotification).Name}'.");
    }

    /// <inheritdoc />
    public void Unsubscribe<TNotification>(INotificationHandler<TNotification> handler) where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            if (_subscribersByNotificationType.TryGetValue(typeof(TNotification), out var subscribers))
                subscribers.Remove(handler);
        }

        _logger?.Information(
            $"Handler '{handler.GetType().Name}' unsubscribed from '{typeof(TNotification).Name}'.");
    }

    /// <inheritdoc />
    public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        IReadOnlyList<INotificationHandler<TNotification>> snapshot;

        lock (_gate)
        {
            snapshot = _subscribersByNotificationType.TryGetValue(typeof(TNotification), out var subscribers)
                ? subscribers.Cast<INotificationHandler<TNotification>>().ToList()
                : Array.Empty<INotificationHandler<TNotification>>();
        }

        _logger?.Information(
            $"Publishing '{typeof(TNotification).Name}' to {snapshot.Count} subscriber(s).");

        foreach (var handler in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await handler.HandleAsync(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Warning(
                    $"Subscriber '{handler.GetType().Name}' threw while handling '{typeof(TNotification).Name}'.",
                    ex);
            }
        }

        _logger?.Information($"Publish completed for '{typeof(TNotification).Name}'.");
    }

    private List<object> GetOrCreateSubscriberList(Type notificationType)
    {
        if (!_subscribersByNotificationType.TryGetValue(notificationType, out var subscribers))
        {
            subscribers = new List<object>();
            _subscribersByNotificationType[notificationType] = subscribers;
        }

        return subscribers;
    }
}
