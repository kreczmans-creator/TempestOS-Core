namespace Tempest.Core.Notifications;

/// <summary>
/// How significant a <see cref="IPlatformNotification"/> is — a
/// presentation hint for whatever ultimately renders it (a future Shell
/// toast, an email, a webhook payload), never interpreted by
/// <see cref="NotificationDispatcher"/> itself.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>A routine, informational notice.</summary>
    Information,

    /// <summary>A notice that something completed successfully.</summary>
    Success,

    /// <summary>A notice of a condition worth attention, but not a failure.</summary>
    Warning,

    /// <summary>A notice that something failed.</summary>
    Error,
}
