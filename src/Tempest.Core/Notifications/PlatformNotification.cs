namespace Tempest.Core.Notifications;

/// <summary>
/// The concrete, immutable <see cref="IPlatformNotification"/> implementation.
/// </summary>
public sealed class PlatformNotification : IPlatformNotification
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PlatformNotification"/> class.
    /// </summary>
    /// <param name="category">A short, caller-defined grouping.</param>
    /// <param name="severity">How significant this notification is.</param>
    /// <param name="message">A human-readable description of what happened.</param>
    /// <param name="occurredAt">
    /// When this notification occurred. Defaults to
    /// <see cref="DateTimeOffset.UtcNow"/> if not supplied.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="category"/> or <paramref name="message"/> is
    /// <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public PlatformNotification(string category, NotificationSeverity severity, string message, DateTimeOffset? occurredAt = null)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category must not be null, empty, or whitespace.", nameof(category));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message must not be null, empty, or whitespace.", nameof(message));

        Category = category;
        Severity = severity;
        Message = message;
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public string Category { get; }

    /// <inheritdoc />
    public NotificationSeverity Severity { get; }

    /// <inheritdoc />
    public string Message { get; }

    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; }
}
