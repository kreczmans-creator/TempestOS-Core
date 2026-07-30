using Tempest.Core.Commands;
using Tempest.Core.Notifications;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler publishes a
/// <see cref="PlatformNotification"/> through
/// <see cref="Tempest.Core.Notifications.INotificationDispatcher"/>.
/// </summary>
/// <remarks>
/// Demonstrates the Command Framework and the Notification Framework
/// interacting — see <see cref="PublishSampleNotificationCommandHandler"/>.
/// </remarks>
public sealed class PublishSampleNotificationCommand : ICommand
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PublishSampleNotificationCommand"/> class.
    /// </summary>
    /// <param name="severity">The severity to publish.</param>
    /// <param name="message">The message to publish.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public PublishSampleNotificationCommand(NotificationSeverity severity, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message must not be null, empty, or whitespace.", nameof(message));

        Severity = severity;
        Message = message;
    }

    /// <summary>Gets the severity to publish.</summary>
    public NotificationSeverity Severity { get; }

    /// <summary>Gets the message to publish.</summary>
    public string Message { get; }
}
