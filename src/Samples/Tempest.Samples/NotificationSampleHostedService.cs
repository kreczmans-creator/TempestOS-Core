using Tempest.Core.BackgroundServices;
using Tempest.Core.Notifications;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference hosted service that publishes a
/// <see cref="PlatformNotification"/> on <see cref="StartAsync"/> and
/// another on <see cref="StopAsync"/> — demonstrating "Background
/// notifications" and Host Lifecycle integration (Phases 8.1/10.1)
/// concretely, rather than leaving either as a theoretical capability.
/// </summary>
/// <remarks>
/// <para>
/// This is the first genuine, non-infrastructure <see cref="IHostedService"/>
/// this codebase ships — every prior Work Package's own Background
/// Services coverage (`WP 4.5`) proved the infrastructure itself, but
/// deliberately shipped with zero real consumers
/// (`docs/governance/Quality/Technical Debt Register.md`'s own `AT-07`).
/// `AT-07`'s own revisit trigger names `WP 6.3` (REST API) as its
/// intended retiree; this Work Package did not set out to claim that
/// milestone, and does not — see this Work Package's own Platform Impact
/// Assessment for the full, disclosed reasoning. This class exists
/// specifically to prove the Notification Framework's own "Background
/// notifications" and "Host lifecycle integration" deliverables
/// concretely, not to pre-empt `WP 6.3`'s own scope.
/// </para>
/// <para>
/// Isolated by default (`ADR-0021`) — does not implement
/// <see cref="ICriticalBackgroundService"/>; a failure here is not
/// Host-fatal.
/// </para>
/// </remarks>
public sealed class NotificationSampleHostedService : IHostedService
{
    /// <summary>
    /// The <see cref="IPlatformNotification.Category"/> this hosted
    /// service publishes under.
    /// </summary>
    public const string Category = "Sample.Background";

    /// <summary>
    /// The message published by <see cref="StartAsync"/>.
    /// </summary>
    public const string StartedMessage = "Notification sample hosted service started.";

    /// <summary>
    /// The message published by <see cref="StopAsync"/>.
    /// </summary>
    public const string StoppedMessage = "Notification sample hosted service stopped.";

    private readonly INotificationDispatcher _notificationDispatcher;

    /// <summary>
    /// Initialises a new instance of the <see cref="NotificationSampleHostedService"/> class.
    /// </summary>
    /// <param name="notificationDispatcher">
    /// The Notification service this hosted service publishes through,
    /// resolved via ordinary constructor injection.
    /// </param>
    public NotificationSampleHostedService(INotificationDispatcher notificationDispatcher)
    {
        ArgumentNullException.ThrowIfNull(notificationDispatcher);

        _notificationDispatcher = notificationDispatcher;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) =>
        _notificationDispatcher.PublishAsync<IPlatformNotification>(
            new PlatformNotification(Category, NotificationSeverity.Information, StartedMessage),
            cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) =>
        _notificationDispatcher.PublishAsync<IPlatformNotification>(
            new PlatformNotification(Category, NotificationSeverity.Information, StoppedMessage),
            cancellationToken);
}
