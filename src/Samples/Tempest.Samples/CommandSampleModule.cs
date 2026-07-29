using Tempest.Core.Commands;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that registers two commands
/// with the Command Framework during its own lifecycle: one demonstrating
/// both of a command's ordinary outcomes (success and expected failure),
/// and one demonstrating navigation integration.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 5.1B</c> validates the Command
/// Framework against — mirrors <see cref="ClockModule"/>'s own role for the
/// Event Bus and <see cref="NavigationSampleModule"/>'s own role for
/// Navigation. Carries <see cref="ModuleMetadataAttribute"/> so Discovery
/// can read its identity without instantiating it (ADR-0027), freeing its
/// constructor to request <see cref="ICommandDispatcher"/>,
/// <see cref="ICommandRegistry"/>, and <see cref="INavigationProvider"/> —
/// all DI-public platform services — via ordinary constructor injection.
/// </para>
/// <para>
/// Registers both a handler (dispatch-side) and a descriptor
/// (discovery-side) for each of its two commands during
/// <see cref="InitialiseAsync"/>, exactly as <c>Command Framework
/// Architecture.md</c>'s own Registration Model describes — two calls, not
/// one, mirroring the Event Bus/Navigation registration shape.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.commands", "Command Sample", "1.0.0")]
public sealed class CommandSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The <see cref="CommandDescriptor.Id"/> this module registers for
    /// <see cref="IncrementCounterCommand"/>.
    /// </summary>
    public const string IncrementCounterCommandId = "sample.increment-counter";

    /// <summary>
    /// The <see cref="CommandDescriptor.Id"/> this module registers for
    /// <see cref="NavigateToSampleHomeCommand"/>.
    /// </summary>
    public const string NavigateHomeCommandId = "sample.navigate-home";

    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;
    private readonly INavigationProvider _navigationProvider;
    private readonly IncrementCounterCommandHandler _incrementCounterHandler = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="CommandSampleModule"/> class.
    /// </summary>
    /// <param name="commandDispatcher">
    /// The Command Framework's dispatch-side surface this module registers
    /// its handlers through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandRegistry">
    /// The Command Framework's discovery-side surface this module registers
    /// its descriptors through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="navigationProvider">
    /// The Navigation Framework <see cref="NavigateToSampleHomeCommandHandler"/>
    /// navigates through, resolved via ordinary constructor injection.
    /// </param>
    public CommandSampleModule(
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry,
        INavigationProvider navigationProvider)
        : base("tempest.samples.commands", "Command Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
        _navigationProvider = navigationProvider;
    }

    /// <summary>
    /// Gets the sample counter's current value, as last set by a dispatched
    /// <see cref="IncrementCounterCommand"/>.
    /// </summary>
    public int Counter => _incrementCounterHandler.Counter;

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's commands.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Registers this module's two commands — handler and descriptor for
    /// each.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _commandDispatcher.RegisterHandler<IncrementCounterCommand>(_incrementCounterHandler);
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: IncrementCounterCommandId,
            displayName: "Increment Sample Counter",
            category: "Sample",
            description: "Adds one to the sample counter.",
            createDefault: () => new IncrementCounterCommand(1)));

        _commandDispatcher.RegisterHandler<NavigateToSampleHomeCommand>(
            new NavigateToSampleHomeCommandHandler(_navigationProvider));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: NavigateHomeCommandId,
            displayName: "Go to Sample Home",
            category: "Sample",
            description: "Navigates to the Navigation Sample's Home page.",
            createDefault: () => new NavigateToSampleHomeCommand()));

        HasRegistered = true;

        return Task.CompletedTask;
    }
}
