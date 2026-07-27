namespace Tempest.Core.BackgroundServices;

/// <summary>
/// The lifecycle state of one hosted service, as tracked by
/// <see cref="IHostedServiceManager"/>.
/// </summary>
public enum HostedServiceState
{
    /// <summary>Discovered and registered, but <see cref="IHostedService.StartAsync"/> has not been called yet.</summary>
    Registered,

    /// <summary><see cref="IHostedService.StartAsync"/> is in progress.</summary>
    Starting,

    /// <summary><see cref="IHostedService.StartAsync"/> completed successfully.</summary>
    Running,

    /// <summary><see cref="IHostedService.StopAsync"/> is in progress.</summary>
    Stopping,

    /// <summary><see cref="IHostedService.StopAsync"/> completed successfully.</summary>
    Stopped,

    /// <summary>
    /// <see cref="IHostedService.StartAsync"/> or <see cref="IHostedService.StopAsync"/>
    /// threw. Per ADR-0021/ADR-0029, this is isolated (does not fault the Host) unless the
    /// service implements <see cref="ICriticalBackgroundService"/>.
    /// </summary>
    Failed,
}
