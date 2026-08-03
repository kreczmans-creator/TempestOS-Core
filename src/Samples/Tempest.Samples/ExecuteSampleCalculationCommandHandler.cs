using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="ExecuteSampleCalculationCommand"/> by executing
/// <see cref="DoubleLengthCalculationDefinition"/> with a fixed sample
/// input through <see cref="ICalculationEngine"/>.
/// </summary>
public sealed class ExecuteSampleCalculationCommandHandler : ICommandHandler<ExecuteSampleCalculationCommand>
{
    private readonly ICalculationEngine _calculationEngine;

    /// <summary>
    /// Initialises a new instance of the <see cref="ExecuteSampleCalculationCommandHandler"/> class.
    /// </summary>
    /// <param name="calculationEngine">The Calculation Framework service this handler executes through.</param>
    public ExecuteSampleCalculationCommandHandler(ICalculationEngine calculationEngine)
    {
        ArgumentNullException.ThrowIfNull(calculationEngine);

        _calculationEngine = calculationEngine;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ExecuteSampleCalculationCommand command, CancellationToken cancellationToken)
    {
        var record = await _calculationEngine.ExecuteAsync<Quantity<Length>, Quantity<Length>>(
            DoubleLengthCalculationDefinition.Id, new Quantity<Length>(3.0, LengthUnits.Metre), cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success($"Executed calculation, produced record '{record.Id}' with result {record.Result}.");
    }
}
