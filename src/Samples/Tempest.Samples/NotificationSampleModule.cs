using Tempest.Core.Commands;
using Tempest.Core.Modules;
using Tempest.Core.Notifications;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Notification Framework: it subscribes to
/// <see cref="IPlatformNotification"/> during its own initialisation,
/// records every one it observes, and registers a command that publishes
/// a new one on demand.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 6.2</c> validates the Notification
/// Framework against — mirrors <see cref="SettingsSampleModule"/>'s own
/// role for Settings and <see cref="AuditSampleModule"/>'s own role for
/// Audit. Carries <see cref="ModuleMetadataAttribute"/> so Discovery can
/// read its identity without instantiating it (ADR-0027), freeing its
/// constructor to request <see cref="INotificationDispatcher"/>,
/// <see cref="ICommandDispatcher"/>, and <see cref="ICommandRegistry"/> —
/// all DI-public platform services — via ordinary constructor injection.
/// </para>
/// <para>
/// Subscribes during <see cref="InitialiseAsync"/> — since Module
/// Initialisation (Phase 8) completes before Hosted Services Started
/// (Phase 8.1), this module reliably observes
/// <see cref="NotificationSampleHostedService"/>'s own "started"
/// notification when both run together in the same Host, demonstrating
/// "Background notifications" end to end. Deliberately does not
/// unsubscribe in <see cref="StopAsync"/>, mirroring
/// <see cref="ClockLifecycleObserverModule"/>'s own reasoning: modules
/// stop in descending order, and this module — likely initialised before
/// a later-sorting module — would otherwise miss a notification
/// published during another module's own <see cref="StopAsync"/>.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.notifications", "Notification Sample", "1.0.0")]
public sealed class NotificationSampleModule : ModuleLifecycleBase, INotificationHandler<IPlatformNotification>
{
    /// <summary>
    /// The <see cref="IPlatformNotification.Category"/> this module's own
    /// commands publish under.
    /// </summary>
    public const string SampleCategory = "Sample";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="PublishSampleNotificationCommand"/>.
    /// </summary>
    public const string PublishSampleNotificationCommandId = "sample.notification-publish";

    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;
    private readonly object _gate = new();
    private readonly List<IPlatformNotification> _observedNotifications = [];

    /// <summary>
    /// Initialises a new instance of the <see cref="NotificationSampleModule"/> class.
    /// </summary>
    /// <param name="notificationDispatcher">
    /// The Notification service this module subscribes to and publishes
    /// through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandDispatcher">
    /// The Command Framework's dispatch-side surface this module registers
    /// its handler through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandRegistry">
    /// The Command Framework's discovery-side surface this module
    /// registers its descriptor through, resolved via ordinary constructor
    /// injection.
    /// </param>
    public NotificationSampleModule(
        INotificationDispatcher notificationDispatcher,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.notifications", "Notification Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(notificationDispatcher);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _notificationDispatcher = notificationDispatcher;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's command.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <summary>
    /// Gets every <see cref="IPlatformNotification"/> observed so far, in
    /// the order received.
    /// </summary>
    public IReadOnlyList<IPlatformNotification> ObservedNotifications
    {
        get
        {
            lock (_gate)
                return _observedNotifications.ToList();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Subscribes to <see cref="IPlatformNotification"/>, then registers
    /// <see cref="PublishSampleNotificationCommand"/>'s handler and
    /// descriptor.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _notificationDispatcher.Subscribe(this);

        _commandDispatcher.RegisterHandler<PublishSampleNotificationCommand>(
            new PublishSampleNotificationCommandHandler(_notificationDispatcher));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PublishSampleNotificationCommandId,
            displayName: "Publish Sample Notification",
            category: "Sample",
            description: "Publishes a sample notification at a chosen severity.",
            createDefault: () => new PublishSampleNotificationCommand(NotificationSeverity.Information, "Sample notification")));

        HasRegistered = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(IPlatformNotification notification, CancellationToken cancellationToken)
    {
        lock (_gate)
            _observedNotifications.Add(notification);

        return Task.CompletedTask;
    }
}
