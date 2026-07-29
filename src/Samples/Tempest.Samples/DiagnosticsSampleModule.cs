using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates
/// <see cref="IDiagnosticsProvider"/>: it observes the Host's own
/// lifecycle state directly during its own lifecycle methods, and
/// registers a command (<see cref="GetDiagnosticsSummaryCommand"/>)
/// whose handler reads the same service.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 5.2</c> validates Diagnostics
/// against — mirrors <see cref="CommandSampleModule"/>'s own role for
/// the Command Framework. Carries <see cref="ModuleMetadataAttribute"/>
/// so Discovery can read its identity without instantiating it
/// (ADR-0027), freeing its constructor to request
/// <see cref="IDiagnosticsProvider"/>, <see cref="ICommandDispatcher"/>,
/// and <see cref="ICommandRegistry"/> — all DI-public platform services
/// — via ordinary constructor injection.
/// </para>
/// <para>
/// <see cref="ObservedHostStateDuringInitialise"/> is captured during
/// <see cref="InitialiseAsync"/> — at this point in the Host Lifecycle,
/// <see cref="IDiagnosticsProvider.HostedServices"/> is expected to be
/// empty (Hosted Services Started has not happened yet), which this
/// module deliberately observes and records
/// (<see cref="ObservedHostedServiceCountDuringInitialise"/>) rather than
/// hiding, as a concrete, real demonstration of `Diagnostics Architecture.md`'s
/// own "not yet available is a normal, honestly-reported state" point.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.diagnostics", "Diagnostics Sample", "1.0.0")]
public sealed class DiagnosticsSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="GetDiagnosticsSummaryCommand"/>.
    /// </summary>
    public const string GetDiagnosticsSummaryCommandId = "sample.diagnostics-summary";

    private readonly IDiagnosticsProvider _diagnosticsProvider;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="DiagnosticsSampleModule"/> class.
    /// </summary>
    /// <param name="diagnosticsProvider">
    /// The Diagnostics service this module observes, resolved via ordinary
    /// constructor injection.
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
    public DiagnosticsSampleModule(
        IDiagnosticsProvider diagnosticsProvider,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.diagnostics", "Diagnostics Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(diagnosticsProvider);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _diagnosticsProvider = diagnosticsProvider;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>
    /// Gets the Host's own <see cref="HostState"/>, as observed by this
    /// module during its own <see cref="InitialiseAsync"/>.
    /// </summary>
    public HostState? ObservedHostStateDuringInitialise { get; private set; }

    /// <summary>
    /// Gets how many modules <see cref="IDiagnosticsProvider.Modules"/>
    /// reported during this module's own <see cref="InitialiseAsync"/>.
    /// </summary>
    public int ObservedModuleCountDuringInitialise { get; private set; }

    /// <summary>
    /// Gets how many hosted services
    /// <see cref="IDiagnosticsProvider.HostedServices"/> reported during
    /// this module's own <see cref="InitialiseAsync"/> — legitimately
    /// <c>0</c> at this point in the Host Lifecycle, see this type's own
    /// remarks.
    /// </summary>
    public int ObservedHostedServiceCountDuringInitialise { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's command.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Observes Diagnostics directly, then registers
    /// <see cref="GetDiagnosticsSummaryCommand"/>'s handler and descriptor.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        ObservedHostStateDuringInitialise = _diagnosticsProvider.HostState;
        ObservedModuleCountDuringInitialise = _diagnosticsProvider.Modules.Count;
        ObservedHostedServiceCountDuringInitialise = _diagnosticsProvider.HostedServices.Count;

        _commandDispatcher.RegisterHandler<GetDiagnosticsSummaryCommand>(
            new GetDiagnosticsSummaryCommandHandler(_diagnosticsProvider));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: GetDiagnosticsSummaryCommandId,
            displayName: "Show Platform Status",
            category: "Sample",
            description: "Reports the Host's own current lifecycle state.",
            createDefault: () => new GetDiagnosticsSummaryCommand()));

        HasRegistered = true;

        return Task.CompletedTask;
    }
}
