namespace Tempest.Core.Notifications;

/// <summary>
/// The base exception thrown when a Notification operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Audit.AuditException"/>'s own base-only pattern —
/// no subtype is defined in this release; every current Notification
/// failure mode is already covered by an existing exception type
/// (<see cref="ArgumentNullException"/> for invalid input). This base
/// type exists for the approved contract's own sake and for a future
/// subtype, never thrown directly.
/// </remarks>
public class NotificationException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="NotificationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public NotificationException(string message)
        : base(message)
    {
    }
}
