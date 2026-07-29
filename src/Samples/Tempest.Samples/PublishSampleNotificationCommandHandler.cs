using Tempest.Core.Commands;
using Tempest.Core.Notifications;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="PublishSampleNotificationCommand"/> by publishing a
/// <see cref="PlatformNotification"/> through
/// <see cref="INotificationDispatcher"/>.
/// </summary>
public sealed class PublishSampleNotificationCommandHandler : ICommandHandler<PublishSampleNotificationCommand>
{
    private readonly INotificationDispatcher _notificationDispatcher;

    /// <summary>
    /// Initialises a new instance of the <see cref="PublishSampleNotificationCommandHandler"/> class.
    /// </summary>
    /// <param name="notificationDispatcher">The Notification service this handler publishes through.</param>
    public PublishSampleNotificationCommandHandler(INotificationDispatcher notificationDispatcher)
    {
        ArgumentNullException.ThrowIfNull(notificationDispatcher);

        _notificationDispatcher = notificationDispatcher;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(PublishSampleNotificationCommand command, CancellationToken cancellationToken)
    {
        IPlatformNotification notification = new PlatformNotification(NotificationSampleModule.SampleCategory, command.Severity, command.Message);

        await _notificationDispatcher.PublishAsync(notification, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Published '{command.Severity}' notification: '{command.Message}'.");
    }
}
