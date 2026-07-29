using Tempest.Core.Events;

namespace Tempest.Core.Navigation;

/// <summary>
/// Published through the Event Bus when <see cref="INavigationProvider.Navigate"/>
/// is called, reporting which already-registered item navigation was
/// requested to.
/// </summary>
/// <remarks>
/// An ordinary <see cref="IEvent"/> — no dedicated, Navigation-specific
/// publish/subscribe mechanism exists (ADR-0032). Whatever is rendering
/// (<c>Tempest.App</c>, or a future UI shell) subscribes to this event
/// through the existing, unmodified <see cref="IEventHandler{TEvent}"/>
/// contract and performs the actual view swap using its own private
/// mapping from <see cref="NavigationItem.Id"/> to whatever it knows how to
/// render.
/// </remarks>
public sealed class NavigationRequestedEvent : IEvent
{
    /// <summary>
    /// Initialises a new instance of the <see cref="NavigationRequestedEvent"/> class.
    /// </summary>
    /// <param name="item">The item navigation was requested to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public NavigationRequestedEvent(NavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
    }

    /// <summary>
    /// Gets the item navigation was requested to.
    /// </summary>
    public NavigationItem Item { get; }
}
