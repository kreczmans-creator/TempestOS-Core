namespace Tempest.Core.BackgroundServices;

/// <summary>
/// Starts and stops every discovered <see cref="IHostedService"/>, in deterministic order,
/// with per-service failure isolation.
/// </summary>
/// <remarks>
/// A Host-owned collaborator (ADR-0017, applied to this new component per ADR-0029) —
/// constructed directly by <c>TempestHost</c>, never registered into the dependency
/// injection container. Carries no orchestration authority over anything beyond the
/// hosted services it was constructed with — it cannot discover new services, register
/// modules, or reach into any other Host-owned collaborator. See ADR-0029 and
/// <c>Background Services Architecture.md</c> for the complete design.
/// </remarks>
public interface IHostedServiceManager
{
    /// <summary>
    /// Gets a snapshot of every discovered hosted service's own current lifecycle status,
    /// in the same deterministic order they start in.
    /// </summary>
    IReadOnlyCollection<HostedServiceStatus> Services { get; }

    /// <summary>
    /// Starts every discovered hosted service, sequentially, in ascending order by the
    /// service's own <see cref="Type.FullName"/>.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token observed between services, never mid-<see cref="IHostedService.StartAsync"/>
    /// call. <see cref="OperationCanceledException"/> is never isolated — it propagates
    /// directly to the caller.
    /// </param>
    /// <remarks>
    /// A non-critical service's own exception is caught, logged, and recorded on its own
    /// <see cref="HostedServiceStatus"/> — the batch continues with the next service
    /// (ADR-0021). A service implementing <see cref="ICriticalBackgroundService"/>'s own
    /// exception is never caught here — it propagates immediately, aborting the remainder
    /// of this call.
    /// </remarks>
    Task StartAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops every hosted service that reached <see cref="HostedServiceState.Running"/>,
    /// sequentially, in the reverse of <see cref="StartAllAsync"/>'s own order.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token observed between services, never mid-<see cref="IHostedService.StopAsync"/>
    /// call.
    /// </param>
    /// <remarks>
    /// Failure isolation mirrors <see cref="StartAllAsync"/> exactly: a non-critical
    /// service's exception is isolated; a critical service's exception propagates
    /// immediately.
    /// </remarks>
    Task StopAllAsync(CancellationToken cancellationToken);
}
