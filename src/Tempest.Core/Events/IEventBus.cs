namespace Tempest.Core.Events;

/// <summary>
/// Lets modules publish and subscribe to events without depending on each
/// other directly.
/// </summary>
/// <remarks>
/// <para>
/// A Platform Service (ADR-0020): DI-public, resolved via ordinary
/// constructor injection like <see cref="Configuration.IConfigurationProvider"/>
/// and <see cref="Logging.ILogger"/> — never a Host-owned collaborator. It
/// carries no authority to register, initialise, start, stop, or dispose
/// anything.
/// </para>
/// <para>
/// Subscription is imperative, not DI-auto-discovered: a subscriber calls
/// <see cref="Subscribe{TEvent}"/> explicitly, typically passing itself.
/// <see cref="PublishAsync{TEvent}"/> dispatches to every current subscriber
/// of exactly <typeparamref name="TEvent"/>, sequentially, in subscription
/// order, over an independent snapshot taken at the start of that call — see
/// ADR-0028 for the complete dispatch, failure-isolation, and re-entrancy
/// model this implements.
/// </para>
/// </remarks>
public interface IEventBus
{
    /// <summary>
    /// Subscribes <paramref name="handler"/> to every future publication of
    /// exactly <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke on each publication.</param>
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;

    /// <summary>
    /// Removes a previously registered subscription. A no-op if
    /// <paramref name="handler"/> was never subscribed, or already
    /// unsubscribed, for <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;

    /// <summary>
    /// Publishes <paramref name="event"/> to every current subscriber of
    /// exactly <typeparamref name="TEvent"/>, sequentially, in subscription
    /// order. Publishing with zero subscribers is a no-op, not an error. A
    /// subscriber's own exception is caught, logged, and never rethrown here
    /// — see ADR-0028.
    /// </summary>
    /// <typeparam name="TEvent">The event type being published.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">
    /// A token observed between subscribers, never mid-<c>HandleAsync</c>
    /// call. <see cref="OperationCanceledException"/> is never isolated —
    /// it propagates directly to the caller.
    /// </param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : IEvent;
}
