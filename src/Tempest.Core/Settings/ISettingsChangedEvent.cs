using Tempest.Core.Events;

namespace Tempest.Core.Settings;

/// <summary>
/// Published through the existing <c>IEventBus</c> whenever a setting
/// value changes — reuses the Event Bus contract rather than inventing
/// a new notification path.
/// </summary>
/// <remarks>
/// An interface, not a sealed concrete event type — <c>SettingsProvider</c>
/// publishes via <c>IEventBus.PublishAsync&lt;ISettingsChangedEvent&gt;</c>
/// with this interface as the explicit generic type argument, so a
/// subscriber that calls <c>Subscribe&lt;ISettingsChangedEvent&gt;</c>
/// receives it correctly under the Event Bus's own exact-type dispatch
/// model (<c>AT-03</c>) — dispatch is keyed on the compile-time type
/// argument supplied at each call site, not the published instance's own
/// runtime type, so both sides agreeing on <see cref="ISettingsChangedEvent"/>
/// is what makes this work, not a coincidence of implementation.
/// </remarks>
public interface ISettingsChangedEvent : IEvent
{
    /// <summary>Gets the key of the setting that changed.</summary>
    string Key { get; }

    /// <summary>Gets the value before this change.</summary>
    string OldValue { get; }

    /// <summary>Gets the value after this change.</summary>
    string NewValue { get; }
}
