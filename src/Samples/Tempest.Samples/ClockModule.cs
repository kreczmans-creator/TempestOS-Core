using Tempest.Core.Events;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that tracks its own lifecycle
/// timestamps and running state in memory, and publishes each lifecycle
/// transition through the Event Bus.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 4.4</c> through <c>WP 4.7</c> extend
/// and validate against — see <c>Sample Module Architecture.md</c>. Carries
/// <see cref="ModuleMetadataAttribute"/> so Discovery can read its identity
/// without instantiating it (ADR-0027), freeing its constructor to request
/// <see cref="IEventBus"/> — a DI-public platform service — via ordinary
/// constructor injection, exactly as <c>Building a Module.md</c> documents
/// for an attribute-carrying module. See ADR-0028 and <c>Event Bus
/// Architecture.md</c> for the Event Bus's own dispatch and failure model.
/// </para>
/// <para>
/// Each lifecycle method records real, observable state, then publishes a
/// <see cref="ClockModuleLifecycleEvent"/> reporting the transition just
/// completed. Publishing is fire-and-forget with respect to subscribers:
/// per ADR-0028, a subscriber's own failure is isolated by the bus itself
/// and never propagates back here.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.clock", "System Clock", "1.0.0")]
public sealed class ClockModule : ModuleLifecycleBase
{
    private readonly IEventBus _eventBus;
    private readonly Guid _correlationId = Guid.NewGuid();

    /// <summary>
    /// Initialises a new instance of the <see cref="ClockModule"/> class.
    /// </summary>
    /// <param name="eventBus">
    /// The Event Bus this module publishes its lifecycle transitions
    /// through, resolved via ordinary constructor injection.
    /// </param>
    public ClockModule(IEventBus eventBus)
        : base("tempest.samples.clock", "System Clock", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(eventBus);

        _eventBus = eventBus;
    }

    /// <summary>
    /// Gets the moment <see cref="InitialiseAsync"/> completed, or
    /// <see langword="null"/> if it has not run yet.
    /// </summary>
    public DateTimeOffset? InitialisedAt { get; private set; }

    /// <summary>
    /// Gets the moment <see cref="StartAsync"/> completed, or
    /// <see langword="null"/> if it has not run yet.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// Gets the moment <see cref="StopAsync"/> completed, or
    /// <see langword="null"/> if it has not run yet.
    /// </summary>
    public DateTimeOffset? StoppedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the clock is currently running —
    /// <see langword="true"/> from the moment <see cref="StartAsync"/>
    /// completes until <see cref="StopAsync"/> completes.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets how long the clock has been running, computed from
    /// <see cref="StartedAt"/>, or <see langword="null"/> if it is not
    /// currently running.
    /// </summary>
    public TimeSpan? Uptime => IsRunning && StartedAt is { } started
        ? DateTimeOffset.UtcNow - started
        : null;

    /// <inheritdoc />
    /// <remarks>
    /// Records <see cref="InitialisedAt"/>, then publishes a
    /// <see cref="ClockModuleLifecycleTransition.Initialised"/> event.
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        InitialisedAt = DateTimeOffset.UtcNow;

        await PublishLifecycleEventAsync(ClockModuleLifecycleTransition.Initialised, InitialisedAt.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Records <see cref="StartedAt"/> and sets <see cref="IsRunning"/>,
    /// then publishes a <see cref="ClockModuleLifecycleTransition.Started"/>
    /// event.
    /// </remarks>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        StartedAt = DateTimeOffset.UtcNow;
        IsRunning = true;

        await PublishLifecycleEventAsync(ClockModuleLifecycleTransition.Started, StartedAt.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Records <see cref="StoppedAt"/> and clears <see cref="IsRunning"/>,
    /// then publishes a <see cref="ClockModuleLifecycleTransition.Stopped"/>
    /// event.
    /// </remarks>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        StoppedAt = DateTimeOffset.UtcNow;
        IsRunning = false;

        await PublishLifecycleEventAsync(ClockModuleLifecycleTransition.Stopped, StoppedAt.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task PublishLifecycleEventAsync(
        ClockModuleLifecycleTransition transition,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken) =>
        _eventBus.PublishAsync(
            new ClockModuleLifecycleEvent(Id, Name, transition, timestamp, _correlationId),
            cancellationToken);
}
