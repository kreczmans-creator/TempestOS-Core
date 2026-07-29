namespace Tempest.Core.Notifications;

/// <summary>
/// The platform's general-purpose, ready-to-use notification shape —
/// a category, severity, and human-readable message, suitable for any
/// caller that does not need a more specific, purpose-built notification
/// type of its own.
/// </summary>
/// <remarks>
/// <para>
/// Not part of the original architecture's <c>Public Interface
/// Catalogue.md</c> draft (which named only <see cref="INotification"/>,
/// <see cref="INotificationHandler{TNotification}"/>, and
/// <see cref="INotificationDispatcher"/>) — an additive elaboration this
/// Work Package's own implementation phase introduces, mirroring
/// <c>WP 6.1</c>'s own <c>IRole</c>/<c>IIdentityService</c> and <c>WP
/// 6.4</c>'s own <c>SettingDefinition</c> precedent: filling a gap the
/// architecture package explicitly deferred ("Notification severity,"
/// "Notification categories" were named in this Work Package's own
/// implementation brief but never drafted as interface members), without
/// changing any approved interface's own shape.
/// </para>
/// <para>
/// Deliberately extends both <see cref="INotification"/> and
/// <see cref="Events.IEvent"/> — a concrete, literal realisation of
/// <see cref="INotification"/>'s own doc comment, "typically derived
/// from... an <c>IEvent</c>." A future, more specific notification type
/// is free to implement only <see cref="INotification"/> (never
/// <see cref="Events.IEvent"/>) if it is instead "raised alongside" a
/// separate event object, per that same doc comment's other named case.
/// </para>
/// <para>
/// <b>Publish against this interface, not <see cref="PlatformNotification"/>
/// itself.</b> <see cref="INotificationDispatcher"/> dispatches by exact
/// static generic type — the same design <see cref="Events.IEventBus"/>
/// already uses — so a subscriber that calls
/// <c>Subscribe&lt;IPlatformNotification&gt;</c> (as every handler of this
/// general-purpose shape does) will never observe a publish whose type
/// argument was inferred as the concrete <see cref="PlatformNotification"/>
/// instead. Callers must either declare the notification through this
/// interface before publishing (<c>IPlatformNotification n = new
/// PlatformNotification(...);</c>) or supply the type argument explicitly
/// (<c>PublishAsync&lt;IPlatformNotification&gt;(...)</c>) — found and
/// fixed against this Work Package's own sample consumers
/// (<c>NotificationSampleHostedService</c>,
/// <c>PublishSampleNotificationCommandHandler</c>) while writing their
/// integration tests; see this Work Package's own Lessons Learned.
/// </para>
/// </remarks>
public interface IPlatformNotification : INotification, Events.IEvent
{
    /// <summary>
    /// A short, caller-defined grouping (for example, <c>"Reports"</c>
    /// or <c>"Background"</c>) — free-form, not a closed set, since a
    /// notification's own subject matter is inherently open-ended,
    /// unlike its <see cref="Severity"/>.
    /// </summary>
    string Category { get; }

    /// <summary>How significant this notification is.</summary>
    NotificationSeverity Severity { get; }

    /// <summary>A human-readable description of what happened.</summary>
    string Message { get; }
}
