using Tempest.Core.Notifications;

namespace Tempest.Core.Tests.Notifications;

internal sealed class RecordedNotificationA : INotification
{
    public RecordedNotificationA(string payload) => Payload = payload;

    public string Payload { get; }

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

internal sealed class RecordedNotificationB : INotification
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A configurable <see cref="INotificationHandler{TNotification}"/> that
/// records every notification it receives, in order, and optionally runs
/// a caller-supplied callback — mirrors
/// <c>Tempest.Core.Tests.Events.RecordingHandler{TEvent}</c>'s own
/// convention exactly, adapted for notifications.
/// </summary>
internal sealed class RecordingHandler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    private readonly Func<TNotification, CancellationToken, Task>? _onHandle;

    public RecordingHandler(Func<TNotification, CancellationToken, Task>? onHandle = null)
    {
        _onHandle = onHandle;
    }

    public List<TNotification> Received { get; } = [];

    public Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        Received.Add(notification);
        return _onHandle?.Invoke(notification, cancellationToken) ?? Task.CompletedTask;
    }
}
