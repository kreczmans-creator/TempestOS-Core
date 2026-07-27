namespace Tempest.Core.BackgroundServices;

/// <summary>
/// An <see cref="IHostedService"/> that has explicitly declared its own
/// failure to be Host-fatal, rather than isolated.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-0021, a hosted service's failure is isolated by default —
/// caught, logged, and left not affecting the Runtime Host's own state,
/// exactly like an individual module's failure (ADR-0013). Implementing
/// this marker interface, in addition to <see cref="IHostedService"/>, opts
/// a specific service out of that default: its failure is Host-fatal,
/// exactly like a platform-service failure.
/// </para>
/// <para>
/// This is a contract only. No Host-level wiring reads or acts on this
/// marker until a later work package implements it — declaring it today
/// changes no runtime behaviour.
/// </para>
/// <para>
/// Carries no members of its own: criticality is a declaration, not a
/// configurable value. A service either is critical or it isn't; there is
/// nothing else to say about it at the contract level.
/// </para>
/// </remarks>
public interface ICriticalBackgroundService : IHostedService
{
}
