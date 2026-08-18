using Tempest.Core.Logging;

namespace Tempest.Core.Events;

/// <summary>
/// The concrete <see cref="IEventBus"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Subscribers are held in a single, lock-guarded dictionary keyed by exact
/// event type, mirroring <see cref="Modules.RuntimeModuleManager"/>'s own
/// <c>_gate</c> pattern. <see cref="PublishAsync{TEvent}"/> takes an
/// immutable snapshot of the current subscriber list for
/// <typeparamref name="TEvent"/> under the lock, then dispatches outside
/// it — so a handler that subscribes or unsubscribes during its own
/// <c>HandleAsync</c> call (including for the same event type) does not
/// mutate the list this dispatch is currently iterating, and a nested,
/// re-entrant <see cref="PublishAsync{TEvent}"/> call from within a handler
/// is free to take its own, independent snapshot without deadlocking.
/// </para>
/// <para>
/// Dispatch is sequential and awaited, one subscriber at a time, in
/// subscription order — the same established sequential-batch shape
/// <see cref="Modules.ModuleLifecycleManager"/>'s own <c>RunBatchAsync</c>
/// already uses. Cancellation is checked between subscribers, never
/// mid-<c>HandleAsync</c>, and <see cref="OperationCanceledException"/> is
/// never isolated — it propagates directly to the publisher. Every other
/// subscriber exception is caught, logged at <see cref="LogLevel.Error"/>,
/// and never rethrown — see ADR-0028 for the complete reasoning.
/// </para>
/// <para>
/// <b>Capability enforcement and component-scope propagation (ADR-0111,
/// WP 13.2A).</b> Each subscriber entry captures its own registering
/// component principal (<see langword="null"/> = first-party) at
/// <see cref="Subscribe{TEvent}"/> time — the internal
/// <c>_subscribersByEventType</c> value type stores <c>(handler, owner)</c>
/// pairs rather than bare handler references; <see cref="IEventBus"/>'s own
/// public contract is untouched. <see cref="PublishAsync{TEvent}"/> checks
/// the <i>publisher's</i> own permission once, before taking the subscriber
/// snapshot: if the ambient component principal is non-null, it must hold
/// <c>plugin.events.publish:&lt;FullTypeName&gt;</c> for
/// <typeparamref name="TEvent"/>. Then, for <i>each</i> subscriber, its own
/// captured owner — not the publisher's — is pushed onto
/// <see cref="Identity.ICurrentComponentAccessor"/> via
/// <see cref="Identity.CurrentComponentAccessor.BeginScope"/> immediately
/// before that subscriber's own <c>HandleAsync</c> call, and popped
/// immediately after, so a plugin's own event handler correctly observes
/// itself — never whichever component happened to publish — as the current
/// component while it runs, letting it correctly attribute any further
/// registrations/publishes it makes from within its own handler. A
/// <see cref="Identity.PermissionDeniedException"/> thrown by a subscriber's
/// own further capability-gated action is caught by the existing
/// per-subscriber <see langword="catch"/> block exactly like any other
/// subscriber exception already is — only the one, once-per-publish
/// publisher check above can propagate a <see cref="Identity.PermissionDeniedException"/>
/// out of this method.
/// </para>
/// </remarks>
public sealed class EventBus : IEventBus
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Subscription>> _subscribersByEventType = new();
    private readonly ILogger? _logger;
    private readonly Identity.CurrentComponentAccessor? _currentComponentAccessor;
    private readonly Identity.IPermissionEvaluator? _permissionEvaluator;

    /// <summary>
    /// Initialises a new instance of the <see cref="EventBus"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record subscription and dispatch activity
    /// via the logging abstraction. May be <see langword="null"/> if
    /// logging is not required.
    /// </param>
    /// <param name="currentComponentAccessor">
    /// An optional, <b>concrete</b>-typed accessor — <see cref="EventBus"/>
    /// is the one collaborator in this Work Package that must call
    /// <see cref="Identity.CurrentComponentAccessor.BeginScope"/>, not merely
    /// read <see cref="Identity.ICurrentComponentAccessor.Current"/>
    /// (ADR-0111). <see langword="null"/> — the default — reproduces today's
    /// exact unconditional behaviour: no publisher check, no scope pushed
    /// around any subscriber.
    /// </param>
    /// <param name="permissionEvaluator">
    /// An optional evaluator used to enforce
    /// <c>plugin.events.publish:&lt;FullTypeName&gt;</c> against the
    /// publisher (ADR-0111). <see langword="null"/> — the default — no-ops
    /// the check.
    /// </param>
    public EventBus(
        ILogger? logger = null,
        Identity.CurrentComponentAccessor? currentComponentAccessor = null,
        Identity.IPermissionEvaluator? permissionEvaluator = null)
    {
        _logger = logger;
        _currentComponentAccessor = currentComponentAccessor;
        _permissionEvaluator = permissionEvaluator;
    }

    /// <inheritdoc />
    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var owner = _currentComponentAccessor?.Current;

        lock (_gate)
        {
            GetOrCreateSubscriberList(typeof(TEvent)).Add(new Subscription(handler, owner));
        }

        _logger?.Information(
            $"Handler '{handler.GetType().Name}' subscribed to '{typeof(TEvent).Name}'.");
    }

    /// <inheritdoc />
    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            // Removes only the first matching entry, exactly like the
            // original List<object>.Remove(handler) this replaces (object
            // equality, effectively reference equality since no IEventHandler
            // implementation overrides Equals) — not every matching entry,
            // in case the same handler instance was ever subscribed twice.
            if (_subscribersByEventType.TryGetValue(typeof(TEvent), out var subscribers))
            {
                var index = subscribers.FindIndex(subscription => ReferenceEquals(subscription.Handler, handler));

                if (index >= 0)
                    subscribers.RemoveAt(index);
            }
        }

        _logger?.Information(
            $"Handler '{handler.GetType().Name}' unsubscribed from '{typeof(TEvent).Name}'.");
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var publisher = _currentComponentAccessor?.Current;

        if (!Plugins.PluginTrustPermission.IsFirstParty(publisher))
        {
            _permissionEvaluator?.RequirePermission(
                publisher!, new Identity.Permission(Plugins.PluginCapability.EventPublish(typeof(TEvent).FullName!)));
        }

        List<Subscription> snapshot;

        lock (_gate)
        {
            snapshot = _subscribersByEventType.TryGetValue(typeof(TEvent), out var subscribers)
                ? [.. subscribers]
                : [];
        }

        _logger?.Information(
            $"Publishing '{typeof(TEvent).Name}' to {snapshot.Count} subscriber(s).");

        foreach (var subscription in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handler = (IEventHandler<TEvent>)subscription.Handler;

            try
            {
                using var scope = (_currentComponentAccessor is not null && subscription.Owner is not null)
                    ? _currentComponentAccessor.BeginScope(subscription.Owner)
                    : null;

                await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Error(
                    $"Subscriber '{handler.GetType().Name}' threw while handling '{typeof(TEvent).Name}'.",
                    ex);
            }
        }

        _logger?.Information($"Publish completed for '{typeof(TEvent).Name}'.");
    }

    private List<Subscription> GetOrCreateSubscriberList(Type eventType)
    {
        if (!_subscribersByEventType.TryGetValue(eventType, out var subscribers))
        {
            subscribers = new List<Subscription>();
            _subscribersByEventType[eventType] = subscribers;
        }

        return subscribers;
    }

    /// <summary>
    /// One subscriber entry: the subscribed handler (stored as <see cref="object"/>,
    /// type-erased exactly as the previous <c>List&lt;object&gt;</c> storage
    /// was, cast back to <see cref="IEventHandler{TEvent}"/> at dispatch)
    /// alongside the component principal that was current at the moment it
    /// subscribed (ADR-0111).
    /// </summary>
    private readonly record struct Subscription(object Handler, Identity.IPrincipal? Owner);
}
