using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Calculation Framework: it registers <see cref="DoubleLengthCalculationDefinition"/>
/// and executes it once during its own initialisation, then registers a
/// command demonstrating manual invocation.
/// </summary>
/// <remarks>
/// The living reference module `WP 7.1D` validates the Calculation
/// Framework against — mirrors <see cref="MaterialsSampleModule"/>'s own
/// role for the Materials Framework. Carries
/// <see cref="ModuleMetadataAttribute"/> so Discovery can read its
/// identity without instantiating it (ADR-0027).
/// </remarks>
[ModuleMetadata("tempest.samples.calculations", "Calculations Sample", "1.0.0")]
public sealed class CalculationSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="ExecuteSampleCalculationCommand"/>.
    /// </summary>
    public const string ExecuteSampleCalculationCommandId = "sample.calculations-execute";

    private readonly ICalculationEngine _calculationEngine;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationSampleModule"/> class.
    /// </summary>
    /// <param name="calculationEngine">The Calculation Framework service this module registers and executes calculations through, resolved via ordinary constructor injection.</param>
    /// <param name="commandDispatcher">The Command Framework's dispatch-side surface this module registers its handlers through.</param>
    /// <param name="commandRegistry">The Command Framework's discovery-side surface this module registers its descriptors through.</param>
    public CalculationSampleModule(
        ICalculationEngine calculationEngine,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.calculations", "Calculations Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(calculationEngine);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _calculationEngine = calculationEngine;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>Gets the Id of the calculation record produced during <see cref="InitialiseAsync"/>, once initialisation has completed.</summary>
    public Guid? SampleRecordId { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="InitialiseAsync"/> has registered this module's commands.</summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Registers <see cref="DoubleLengthCalculationDefinition"/> and
    /// executes it once with a fixed sample input — proving register/
    /// execute/record all work end to end against the real engine — then
    /// registers <see cref="ExecuteSampleCalculationCommand"/>'s handler
    /// and descriptor.
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _calculationEngine.RegisterDefinition(new DoubleLengthCalculationDefinition());

        var record = await _calculationEngine.ExecuteAsync<Quantity<Length>, Quantity<Length>>(
            DoubleLengthCalculationDefinition.Id, new Quantity<Length>(5.0, LengthUnits.Metre), cancellationToken)
            .ConfigureAwait(false);
        SampleRecordId = record.Id;

        _commandDispatcher.RegisterHandler<ExecuteSampleCalculationCommand>(
            new ExecuteSampleCalculationCommandHandler(_calculationEngine));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ExecuteSampleCalculationCommandId,
            displayName: "Execute Sample Calculation",
            category: "Sample",
            description: "Executes the trivial sample Double Length calculation.",
            createDefault: () => new ExecuteSampleCalculationCommand()));

        HasRegistered = true;
    }
}
