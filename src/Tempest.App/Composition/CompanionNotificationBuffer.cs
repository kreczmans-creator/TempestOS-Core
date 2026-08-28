using Tempest.Companion.Contracts;
using Tempest.Core.Events;
using Tempest.Core.Notifications;

namespace Tempest.App.Composition;

/// <summary>
/// A bounded, in-memory window over recent platform notifications —
/// subscribed to the Event Bus exactly as the desktop's own
/// <c>PlatformNotificationToastBridge</c> already is, so the Companion
/// sees the identical notification stream the desktop toasts
/// (<c>ADR-0046</c>: notifications are derived from events, never a
/// second pub/sub). Deliberately not a durable notification store: the
/// window starts empty at Host start and holds at most
/// <see cref="Capacity"/> entries — a disclosed boundary the
/// Companion API's own DTO documentation repeats.
/// </summary>
internal sealed class CompanionNotificationBuffer : IEventHandler<IPlatformNotification>
{
    /// <summary>The maximum number of notifications retained — oldest evicted first.</summary>
    public const int Capacity = 50;

    private readonly object _gate = new();
    private readonly List<NotificationDto> _notifications = [];

    /// <inheritdoc />
    public Task HandleAsync(IPlatformNotification @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        lock (_gate)
        {
            _notifications.Add(new NotificationDto(@event.OccurredAt, @event.Category, @event.Severity.ToString(), @event.Message));

            if (_notifications.Count > Capacity)
                _notifications.RemoveAt(0);
        }

        return Task.CompletedTask;
    }

    /// <summary>Builds the current window, most recent first.</summary>
    public NotificationListDto BuildSnapshot()
    {
        lock (_gate)
            return new NotificationListDto(DateTimeOffset.UtcNow, _notifications.AsEnumerable().Reverse().ToList());
    }
}
