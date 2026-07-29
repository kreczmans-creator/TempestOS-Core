namespace Tempest.Core.Notifications;

/// <summary>
/// Dispatches a notification to every subscribed handler. Failure
/// isolation mirrors <see cref="Events.IEventBus"/>'s own unconditional
/// per-subscriber isolation (<c>ADR-0028</c>).
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Subscribes <paramref name="handler"/> to every future publication
    /// of exactly <typeparamref name="TNotification"/>.
    /// </summary>
    /// <typeparam name="TNotification">The notification type to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke on each publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    void Subscribe<TNotification>(INotificationHandler<TNotification> handler) where TNotification : INotification;

    /// <summary>
    /// Removes a previously registered subscription. A no-op if
    /// <paramref name="handler"/> was never subscribed, or already
    /// unsubscribed, for <typeparamref name="TNotification"/>.
    /// </summary>
    /// <typeparam name="TNotification">The notification type to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    void Unsubscribe<TNotification>(INotificationHandler<TNotification> handler) where TNotification : INotification;

    /// <summary>
    /// Publishes <paramref name="notification"/> to every current
    /// subscriber of exactly <typeparamref name="TNotification"/>,
    /// sequentially, in subscription order. Publishing with zero
    /// subscribers is a no-op, not an error. A subscriber's own
    /// exception is caught, logged, and never rethrown here.
    /// </summary>
    /// <typeparam name="TNotification">The notification type being published.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">
    /// A token observed between subscribers, never mid-<c>HandleAsync</c>
    /// call. <see cref="OperationCanceledException"/> is never isolated —
    /// it propagates directly to the caller.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is <see langword="null"/>.</exception>
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
}
