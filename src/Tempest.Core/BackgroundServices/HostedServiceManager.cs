using System.Collections.ObjectModel;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;

namespace Tempest.Core.BackgroundServices;

/// <summary>
/// The concrete <see cref="IHostedServiceManager"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// On construction, takes an ordered snapshot (ascending, ordinal, by
/// <see cref="Type.FullName"/>) of the hosted service types supplied to it — the
/// deterministic ordering key a hosted service has, since unlike a module it carries no
/// <c>Id</c>. Mirrors <see cref="Modules.ModuleLifecycleManager"/>'s own sequential
/// batch-orchestration shape (<c>RunBatchAsync</c>) closely, with one addition: a
/// service implementing <see cref="ICriticalBackgroundService"/> is never isolated — its
/// exception propagates immediately, aborting the remainder of the current batch call
/// (ADR-0021/ADR-0029).
/// </para>
/// <para>
/// An instance is resolved through the <see cref="ITempestServiceProvider"/> supplied at
/// construction, the first time a service is started, and the same resolved instance is
/// reused for its later stop call. There is no separate "registration" stage or
/// duplicate-identity concept here, unlike <see cref="Modules.RuntimeModuleManager"/> —
/// ordering is by type, and a hosted service carries no metadata a duplicate could
/// collide on.
/// </para>
/// </remarks>
public sealed class HostedServiceManager : IHostedServiceManager
{
    private readonly object _gate = new();
    private readonly List<TrackedHostedService> _orderedServices;
    private readonly ITempestServiceProvider _serviceProvider;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="HostedServiceManager"/> class, taking
    /// an ordered snapshot of the given hosted service types.
    /// </summary>
    /// <param name="hostedServiceTypes">
    /// The hosted service types to start and stop, typically the result of
    /// <see cref="IHostedServiceDiscoveryService.DiscoverHostedServiceTypes()"/>.
    /// </param>
    /// <param name="serviceProvider">
    /// The service provider used to construct hosted service instances. The caller is
    /// expected to have registered every hosted service's own concrete type with it —
    /// see <c>HostedServiceCollectionExtensions.AddDiscoveredHostedServices</c> — before
    /// passing it here.
    /// </param>
    /// <param name="logger">
    /// An optional logger used to record start/stop activity via the logging
    /// abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public HostedServiceManager(
        IEnumerable<Type> hostedServiceTypes,
        ITempestServiceProvider serviceProvider,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(hostedServiceTypes);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _serviceProvider = serviceProvider;
        _logger = logger;

        _orderedServices = hostedServiceTypes
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => new TrackedHostedService(type))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<HostedServiceStatus> Services
    {
        get
        {
            lock (_gate)
            {
                var snapshot = _orderedServices
                    .Select(tracked => new HostedServiceStatus(tracked.ServiceType, tracked.State, tracked.FailureReason))
                    .ToList();

                return new ReadOnlyCollection<HostedServiceStatus>(snapshot);
            }
        }
    }

    /// <inheritdoc />
    public Task StartAllAsync(CancellationToken cancellationToken) =>
        RunBatchAsync(_orderedServices, StartServiceAsync, cancellationToken);

    /// <inheritdoc />
    public Task StopAllAsync(CancellationToken cancellationToken) =>
        RunBatchAsync(Enumerable.Reverse(_orderedServices), StopServiceAsync, cancellationToken);

    private static async Task RunBatchAsync(
        IEnumerable<TrackedHostedService> services,
        Func<TrackedHostedService, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        foreach (var tracked in services)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // OperationCanceledException propagates uncaught (never isolated); a
            // critical service's own exception also propagates uncaught, aborting
            // the remainder of this batch - both handled inside the per-service
            // methods below, not here, since only they know whether a given
            // exception came from a critical service.
            await operation(tracked, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StartServiceAsync(TrackedHostedService tracked, CancellationToken cancellationToken)
    {
        lock (_gate)
            tracked.State = HostedServiceState.Starting;

        _logger?.Information($"Hosted service '{tracked.ServiceType.FullName}' -> Starting.");

        try
        {
            tracked.Instance = (IHostedService)_serviceProvider.GetService(tracked.ServiceType);

            await tracked.Instance.StartAsync(cancellationToken).ConfigureAwait(false);

            lock (_gate)
                tracked.State = HostedServiceState.Running;

            _logger?.Information($"Hosted service '{tracked.ServiceType.FullName}' -> Running.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                tracked.State = HostedServiceState.Failed;
                tracked.FailureReason = ex;
            }

            if (tracked.Instance is ICriticalBackgroundService)
            {
                _logger?.Critical(
                    $"Critical hosted service '{tracked.ServiceType.FullName}' failed to start.",
                    ex);

                throw;
            }

            _logger?.Error(
                $"Hosted service '{tracked.ServiceType.FullName}' failed to start; isolated.",
                ex);
        }
    }

    private async Task StopServiceAsync(TrackedHostedService tracked, CancellationToken cancellationToken)
    {
        // Only a service that actually reached Running has anything live to stop -
        // one that never started (still Registered), or that already failed to
        // start, is left exactly as it is.
        if (tracked.State != HostedServiceState.Running)
            return;

        lock (_gate)
            tracked.State = HostedServiceState.Stopping;

        _logger?.Information($"Hosted service '{tracked.ServiceType.FullName}' -> Stopping.");

        try
        {
            await tracked.Instance!.StopAsync(cancellationToken).ConfigureAwait(false);

            lock (_gate)
                tracked.State = HostedServiceState.Stopped;

            _logger?.Information($"Hosted service '{tracked.ServiceType.FullName}' -> Stopped.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                tracked.State = HostedServiceState.Failed;
                tracked.FailureReason = ex;
            }

            if (tracked.Instance is ICriticalBackgroundService)
            {
                _logger?.Critical(
                    $"Critical hosted service '{tracked.ServiceType.FullName}' failed to stop.",
                    ex);

                throw;
            }

            _logger?.Error(
                $"Hosted service '{tracked.ServiceType.FullName}' failed to stop; isolated.",
                ex);
        }
    }

    private sealed class TrackedHostedService
    {
        public TrackedHostedService(Type serviceType)
        {
            ServiceType = serviceType;
            State = HostedServiceState.Registered;
        }

        public Type ServiceType { get; }

        public HostedServiceState State { get; set; }

        public IHostedService? Instance { get; set; }

        public Exception? FailureReason { get; set; }
    }
}
