using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Input;
using Tempest.Core.Macros;
using Tempest.Core.Requirements;
using Tempest.Core.Settings;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Resolves every Platform Service <see cref="MainWindow"/>'s own
/// collaborators need from the already-built platform — extracted,
/// `WP 12.0B` (`ADR-0103`), from <see cref="MainWindow"/>'s own previous
/// constructor-top resolution block, unmodified in behaviour. A
/// collaborator under `ADR-0103`: constructed once by
/// <see cref="MainWindow"/> (the composition root), mirroring
/// <c>EngineeringWorkspaceComposer</c>'s own established
/// "Desktop-specific composition step" shape — resolves already-registered
/// Platform Services, never constructs or registers a new one into
/// <c>TempestHost</c>'s own DI container.
/// </summary>
internal sealed class DesktopCompositionRoot
{
    /// <summary>Gets the resolved <see cref="ISettingsProvider"/>.</summary>
    public ISettingsProvider SettingsProvider { get; }

    /// <summary>Gets the resolved <see cref="ICommandRegistry"/>.</summary>
    public ICommandRegistry CommandRegistry { get; }

    /// <summary>Gets the resolved <see cref="ICommandDispatcher"/>.</summary>
    public ICommandDispatcher CommandDispatcher { get; }

    /// <summary>Gets the resolved <see cref="EngineeringDomainContext"/>.</summary>
    public EngineeringDomainContext DomainContext { get; }

    /// <summary>
    /// Gets the resolved <see cref="IRequirementsService"/> — the Object
    /// Editor's own real Requirements Owner/Priority section needs this
    /// directly (`WP 10.7A`): the data (Owner/Priority) lives only in
    /// <see cref="IRequirementsService"/>'s own Requirement DTO, never on
    /// the <see cref="EngineeringDomainContext.Repository"/> object graph
    /// itself.
    /// </summary>
    public IRequirementsService RequirementsService { get; }

    /// <summary>Gets the resolved <see cref="IDiagnosticsProvider"/>.</summary>
    public IDiagnosticsProvider Diagnostics { get; }

    /// <summary>Gets the resolved <see cref="IEventBus"/>.</summary>
    public IEventBus EventBus { get; }

    /// <summary>Gets the resolved <see cref="IMacroManager"/>.</summary>
    public IMacroManager MacroManager { get; }

    /// <summary>Gets the resolved <see cref="IInputBindingRegistry"/>.</summary>
    public IInputBindingRegistry InputBindingRegistry { get; }

    /// <summary>
    /// Gets the Host's own <see cref="Tempest.Core.Logging.ILogger"/> — so a
    /// Desktop-local persisted state can report a corrupt stored value rather
    /// than discarding it silently (`WP-D2`, `TD-112`).
    /// </summary>
    public Tempest.Core.Logging.ILogger Logger { get; }

    /// <summary>Gets the resolved <see cref="Tempest.Core.Notifications.INotificationDispatcher"/> — the channel every real platform-notification producer publishes through (`TD-58`: the toast bridge must listen here, not only on the event bus).</summary>
    public Tempest.Core.Notifications.INotificationDispatcher NotificationDispatcher { get; }

    /// <summary>Initialises a new instance of the <see cref="DesktopCompositionRoot"/> class, resolving every Platform Service <see cref="MainWindow"/>'s own collaborators need from <paramref name="services"/>.</summary>
    /// <param name="services">The already-started <see cref="ITempestServiceProvider"/> (<c>host.Services</c>) this step resolves from — never registers into.</param>
    public DesktopCompositionRoot(Tempest.Core.DependencyInjection.ITempestServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        SettingsProvider = (ISettingsProvider)services.GetService(typeof(ISettingsProvider));
        CommandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));
        CommandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        DomainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        RequirementsService = (IRequirementsService)services.GetService(typeof(IRequirementsService));
        Diagnostics = (IDiagnosticsProvider)services.GetService(typeof(IDiagnosticsProvider));
        EventBus = (IEventBus)services.GetService(typeof(IEventBus));
        MacroManager = (IMacroManager)services.GetService(typeof(IMacroManager));
        InputBindingRegistry = (IInputBindingRegistry)services.GetService(typeof(IInputBindingRegistry));
        NotificationDispatcher = (Tempest.Core.Notifications.INotificationDispatcher)services.GetService(typeof(Tempest.Core.Notifications.INotificationDispatcher));
        Logger = (Tempest.Core.Logging.ILogger)services.GetService(typeof(Tempest.Core.Logging.ILogger));
    }
}
