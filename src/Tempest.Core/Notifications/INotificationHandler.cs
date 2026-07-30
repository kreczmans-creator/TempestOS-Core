namespace Tempest.Core.Notifications;

/// <summary>
/// Handles one <typeparamref name="TNotification"/>. Subscribed
/// imperatively at runtime — never resolved generically through the
/// container (<c>RD-0040</c>) — mirroring <see cref="Events.IEventHandler{TEvent}"/>.
/// </summary>
/// <typeparam name="TNotification">The notification type this handler reacts to.</typeparam>
public interface INotificationHandler<TNotification> where TNotification : INotification
{
    /// <summary>
    /// Handles a published notification.
    /// </summary>
    /// <param name="notification">The notification that was published.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}
