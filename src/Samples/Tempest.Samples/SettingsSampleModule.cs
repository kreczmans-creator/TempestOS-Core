using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Core.Modules;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Settings Framework: it registers a setting definition and two
/// commands (get/set) during its own initialisation, and subscribes to
/// <see cref="ISettingsChangedEvent"/> to observe every change made
/// through it.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 6.4</c> validates the Settings
/// Framework against — mirrors <see cref="DiagnosticsSampleModule"/>'s
/// own role for Diagnostics and <see cref="IdentitySampleModule"/>'s own
/// role for Identity &amp; Permissions. Carries
/// <see cref="ModuleMetadataAttribute"/> so Discovery can read its
/// identity without instantiating it (ADR-0027), freeing its constructor
/// to request <see cref="ISettingsProvider"/>, <see cref="IEventBus"/>,
/// <see cref="ICommandDispatcher"/>, and <see cref="ICommandRegistry"/> —
/// all DI-public platform services — via ordinary constructor injection.
/// </para>
/// <para>
/// Subscribes to <see cref="ISettingsChangedEvent"/> during
/// <see cref="InitialiseAsync"/>, exactly mirroring
/// <see cref="ClockLifecycleObserverModule"/>'s own subscribe-and-record
/// pattern — proving the Event Bus's own exact-type dispatch correctly
/// delivers an event published against an interface type
/// (<see cref="ISettingsChangedEvent"/>), not merely a sealed concrete
/// type, since every other existing event in this codebase
/// (<c>ClockModuleLifecycleEvent</c>, <c>NavigationRequestedEvent</c>) is
/// itself the type both sides agree on.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.settings", "Settings Sample", "1.0.0")]
public sealed class SettingsSampleModule : ModuleLifecycleBase, IEventHandler<ISettingsChangedEvent>
{
    /// <summary>
    /// The setting key this module registers and demonstrates.
    /// </summary>
    public const string SampleSettingKey = "sample.greeting";

    /// <summary>
    /// <see cref="SampleSettingKey"/>'s own default value, used until
    /// something writes a new one.
    /// </summary>
    public const string SampleSettingDefaultValue = "Hello, TempestOS!";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="GetSampleSettingCommand"/>.
    /// </summary>
    public const string GetSampleSettingCommandId = "sample.settings-get";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="SetSampleSettingCommand"/>.
    /// </summary>
    public const string SetSampleSettingCommandId = "sample.settings-set";

    private readonly ISettingsProvider _settingsProvider;
    private readonly IEventBus _eventBus;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;
    private readonly object _gate = new();
    private readonly List<ISettingsChangedEvent> _observedChanges = [];

    /// <summary>
    /// Initialises a new instance of the <see cref="SettingsSampleModule"/> class.
    /// </summary>
    /// <param name="settingsProvider">
    /// The Settings service this module registers its definition and
    /// commands through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="eventBus">
    /// The Event Bus this module subscribes to
    /// <see cref="ISettingsChangedEvent"/> through, resolved via ordinary
    /// constructor injection.
    /// </param>
    /// <param name="commandDispatcher">
    /// The Command Framework's dispatch-side surface this module registers
    /// its handlers through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandRegistry">
    /// The Command Framework's discovery-side surface this module
    /// registers its descriptors through, resolved via ordinary constructor
    /// injection.
    /// </param>
    public SettingsSampleModule(
        ISettingsProvider settingsProvider,
        IEventBus eventBus,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.settings", "Settings Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _settingsProvider = settingsProvider;
        _eventBus = eventBus;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's setting definition and commands.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <summary>
    /// Gets every <see cref="ISettingsChangedEvent"/> observed so far, in
    /// the order received.
    /// </summary>
    public IReadOnlyList<ISettingsChangedEvent> ObservedChanges
    {
        get
        {
            lock (_gate)
                return _observedChanges.ToList();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Registers <see cref="SampleSettingKey"/>'s own definition, subscribes
    /// to <see cref="ISettingsChangedEvent"/>, then registers
    /// <see cref="GetSampleSettingCommand"/> and
    /// <see cref="SetSampleSettingCommand"/>'s handlers and descriptors.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _settingsProvider.RegisterDefinition(
            new SettingDefinition(SampleSettingKey, "Sample Greeting", SampleSettingDefaultValue));

        _eventBus.Subscribe(this);

        _commandDispatcher.RegisterHandler<GetSampleSettingCommand>(
            new GetSampleSettingCommandHandler(_settingsProvider));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: GetSampleSettingCommandId,
            displayName: "Get Sample Setting",
            category: "Sample",
            description: "Reads the current value of the sample setting.",
            createDefault: () => new GetSampleSettingCommand()));

        _commandDispatcher.RegisterHandler<SetSampleSettingCommand>(
            new SetSampleSettingCommandHandler(_settingsProvider));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: SetSampleSettingCommandId,
            displayName: "Set Sample Setting",
            category: "Sample",
            description: "Writes a new value for the sample setting.",
            createDefault: () => new SetSampleSettingCommand("Updated via sample command")));

        HasRegistered = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(ISettingsChangedEvent @event, CancellationToken cancellationToken)
    {
        lock (_gate)
            _observedChanges.Add(@event);

        return Task.CompletedTask;
    }
}
