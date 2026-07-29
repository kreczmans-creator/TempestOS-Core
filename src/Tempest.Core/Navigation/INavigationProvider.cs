namespace Tempest.Core.Navigation;

/// <summary>
/// Lets modules and plugin-loaded modules contribute navigable destinations
/// to one coherent, platform-held structure, without the Runtime Host ever
/// needing to change.
/// </summary>
/// <remarks>
/// <para>
/// A Platform Service (ADR-0032): DI-public, resolved via ordinary
/// constructor injection like <see cref="Events.IEventBus"/> and
/// <see cref="Logging.ILogger"/> — never a Host-owned collaborator. It
/// carries no authority to register, initialise, start, stop, or dispose
/// anything in the module pipeline.
/// </para>
/// <para>
/// Registration is imperative, not DI-auto-discovered or reflection-driven:
/// a module or plugin-loaded module calls <see cref="Register"/> explicitly,
/// typically from its own <c>InitialiseAsync</c>, exactly as it may call
/// <see cref="Events.IEventBus.Subscribe{TEvent}"/>. See ADR-0032 and
/// <c>Navigation Framework Architecture.md</c> for the complete registration,
/// ownership, and rendering-boundary model this implements.
/// </para>
/// </remarks>
public interface INavigationProvider
{
    /// <summary>
    /// Registers <paramref name="item"/> so it appears in <see cref="Items"/>.
    /// </summary>
    /// <param name="item">The item to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateNavigationItemException">
    /// An item with the same <see cref="NavigationItem.Id"/> is already registered.
    /// </exception>
    void Register(NavigationItem item);

    /// <summary>
    /// Removes a previously registered item. A no-op if <paramref name="id"/>
    /// was never registered, or already unregistered.
    /// </summary>
    /// <param name="id">The <see cref="NavigationItem.Id"/> of the item to remove.</param>
    void Unregister(string id);

    /// <summary>
    /// Gets every currently registered item, pre-sorted ascending by
    /// <see cref="NavigationItem.Group"/> (nulls first), then ascending by
    /// <see cref="NavigationItem.Order"/>, then ascending ordinal by
    /// <see cref="NavigationItem.Id"/>.
    /// </summary>
    /// <remarks>
    /// Returns every registered item regardless of its
    /// <see cref="NavigationItem.IsVisible"/> predicate — filtering by
    /// visibility is the caller's own decision, not something this provider
    /// decides on the caller's behalf.
    /// </remarks>
    IReadOnlyList<NavigationItem> Items { get; }

    /// <summary>
    /// Requests navigation to the item identified by <paramref name="id"/>,
    /// publishing a <see cref="NavigationRequestedEvent"/> through the Event
    /// Bus for whatever is rendering to observe and act on.
    /// </summary>
    /// <param name="id">The <see cref="NavigationItem.Id"/> of the item to navigate to.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <exception cref="NavigationItemNotFoundException">
    /// No item with <paramref name="id"/> is registered.
    /// </exception>
    Task Navigate(string id, CancellationToken cancellationToken = default);
}
