namespace Tempest.Core.Notifications;

/// <summary>
/// Marks a concrete notification type — a user- or system-facing
/// notice, typically derived from (or raised alongside) an
/// <see cref="Events.IEvent"/>. Mirrors <see cref="Events.IEvent"/>'s own
/// marker-only shape.
/// </summary>
public interface INotification
{
    /// <summary>When this notification was raised.</summary>
    DateTimeOffset OccurredAt { get; }
}
