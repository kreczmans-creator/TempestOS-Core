namespace Tempest.Core.Events;

/// <summary>
/// Reacts to a published event of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The event type this handler reacts to.</typeparam>
/// <remarks>
/// <para>
/// A module (or other future subscriber) implements this interface once
/// per event type it wants to observe, and registers it with the future
/// event bus (ADR-0020) — not yet implemented; declaring this interface
/// today introduces no new runtime behaviour.
/// </para>
/// <para>
/// A handler that throws must not prevent any other subscriber to the same
/// event from also being invoked, and must not fault the Runtime Host — see
/// `WP 4.4`'s own acceptance criteria in <c>docs/releases/v0.4.0/WorkPackages.md</c>.
/// This contract does not itself enforce that isolation; the future event
/// bus implementation does, mirroring
/// <see cref="Tempest.Core.Modules.ModuleLifecycleManager"/>'s existing
/// per-module failure isolation.
/// </para>
/// </remarks>
public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    /// <summary>
    /// Handles a published event.
    /// </summary>
    /// <param name="event">The event that was published.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
