using Tempest.Core.Events;
using Tempest.Core.Notifications;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Notification Framework's own first real Desktop consumer
/// (`WP 10.5B` scope: "Expand the notification system to include...
/// background task notifications") — subscribes to <see cref="IPlatformNotification"/>
/// via the existing <see cref="IEventBus"/> (unchanged, `WP 5.x`) and
/// forwards every publication to a real <see cref="ToastHost"/>.
/// </summary>
/// <remarks>
/// Before this class, <see cref="IPlatformNotification"/> was published
/// (`NotificationDispatcher`, `Tempest.Samples.NotificationSampleModule`)
/// but never consumed anywhere in <c>Tempest.Desktop</c> — confirmed
/// directly, by a whole-repository search. This closes that gap: any
/// module (a background task, a long-running import, a future
/// scheduled job) that already knows how to publish a notification the
/// console `WorkspaceShell` path could always observe now reaches a real
/// visible surface in the graphical Workspace too, without that module
/// needing any Desktop-specific knowledge — the same "publish once,
/// reach every subscriber" principle the event bus already existed to
/// provide, simply never exercised by this layer before.
/// </remarks>
internal sealed class PlatformNotificationToastBridge : IEventHandler<IPlatformNotification>
{
    private readonly ToastHost _toastHost;

    /// <summary>Initialises a new instance of the <see cref="PlatformNotificationToastBridge"/> class.</summary>
    public PlatformNotificationToastBridge(ToastHost toastHost)
    {
        ArgumentNullException.ThrowIfNull(toastHost);
        _toastHost = toastHost;
    }

    /// <inheritdoc />
    public Task HandleAsync(IPlatformNotification @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        _toastHost.Show($"[{@event.Category}] {@event.Message}", Map(@event.Severity));
        return Task.CompletedTask;
    }

    private static Theming.FeedbackSeverity Map(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Information => Theming.FeedbackSeverity.Info,
        NotificationSeverity.Success => Theming.FeedbackSeverity.Success,
        NotificationSeverity.Warning => Theming.FeedbackSeverity.Warning,
        NotificationSeverity.Error => Theming.FeedbackSeverity.Error,
        _ => Theming.FeedbackSeverity.Info,
    };
}
