using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small companion module that subscribes to
/// <see cref="ClockModuleLifecycleEvent"/> and records every one it
/// observes.
/// </summary>
/// <remarks>
/// <para>
/// Exists solely to demonstrate end-to-end Event Bus publication and
/// subscription between two real, SDK-conformant modules — mirroring
/// <c>WP 4.4</c>'s own Deliverable ("its companion module... extended to
/// subscribe to it"). Holds no reference of any kind to
/// <see cref="ClockModule"/> — it depends only on the shared
/// <see cref="ClockModuleLifecycleEvent"/> data type and <see cref="IEventBus"/>
/// itself, exactly the shape ADR-0020 requires: never a direct
/// module-to-module reference.
/// </para>
/// <para>
/// Subscribes during <see cref="InitialiseAsync"/> — this module's own
/// <see cref="ModuleDescriptor.Id"/> sorts after <see cref="ClockModule"/>'s
/// ordinally, so <see cref="ModuleLifecycleManager"/>'s ascending-order
/// Initialise batch initialises <see cref="ClockModule"/> (and its own
/// <see cref="ClockModuleLifecycleTransition.Initialised"/> publish) first —
/// this module has not subscribed yet when that specific event is
/// published, and so does not observe it. It reliably observes
/// <see cref="ClockModuleLifecycleTransition.Started"/> and
/// <see cref="ClockModuleLifecycleTransition.Stopped"/>, since Module
/// Initialisation completes for every module, including this one, before
/// Module Start begins for any module — a real, load-bearing consequence
/// of the module pipeline's own batch-per-phase shape, not a bug in either
/// module. Deliberately does not unsubscribe in <see cref="StopAsync"/>:
/// <see cref="ModuleLifecycleManager"/> stops modules in descending order
/// (the reverse of Initialise), so this module — initialised after
/// <see cref="ClockModule"/> — would stop, and unsubscribe, before
/// <see cref="ClockModule"/> even reaches its own <see cref="StopAsync"/>,
/// missing the <see cref="ClockModuleLifecycleTransition.Stopped"/> event
/// entirely if it did. Holding the subscription for the Event Bus's whole
/// remaining lifetime is ADR-0028's own named, accepted trade-off, not an
/// oversight here.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.clock.observer", "Clock Lifecycle Observer", "1.0.0")]
public sealed class ClockLifecycleObserverModule : ModuleLifecycleBase, IEventHandler<ClockModuleLifecycleEvent>
{
    private readonly IEventBus _eventBus;
    private readonly ILogger? _logger;
    private readonly object _gate = new();
    private readonly List<ClockModuleLifecycleEvent> _observedEvents = [];

    /// <summary>
    /// Initialises a new instance of the <see cref="ClockLifecycleObserverModule"/> class.
    /// </summary>
    /// <param name="eventBus">
    /// The Event Bus this module subscribes to
    /// <see cref="ClockModuleLifecycleEvent"/> through, resolved via
    /// ordinary constructor injection.
    /// </param>
    /// <param name="logger">
    /// An optional logger used to record each observed event, so the
    /// real, unmodified <see cref="Core.Runtime.TempestHost"/> can be
    /// proven to have delivered it end-to-end. May be
    /// <see langword="null"/> if logging is not required.
    /// </param>
    public ClockLifecycleObserverModule(IEventBus eventBus, ILogger? logger = null)
        : base("tempest.samples.clock.observer", "Clock Lifecycle Observer", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(eventBus);

        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Gets every <see cref="ClockModuleLifecycleEvent"/> observed so far,
    /// in the order received.
    /// </summary>
    public IReadOnlyList<ClockModuleLifecycleEvent> ObservedEvents
    {
        get
        {
            lock (_gate)
                return _observedEvents.ToList();
        }
    }

    /// <inheritdoc />
    /// <remarks>Subscribes to <see cref="ClockModuleLifecycleEvent"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _eventBus.Subscribe(this);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(ClockModuleLifecycleEvent @event, CancellationToken cancellationToken)
    {
        lock (_gate)
            _observedEvents.Add(@event);

        _logger?.Information(
            $"Observed '{@event.Transition}' from module '{@event.ModuleId}' " +
            $"(correlation {@event.CorrelationId}).");

        return Task.CompletedTask;
    }
}
