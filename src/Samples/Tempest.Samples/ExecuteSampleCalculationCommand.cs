using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler executes
/// <see cref="DoubleLengthCalculationDefinition"/> through
/// <see cref="Tempest.Core.Calculations.ICalculationEngine"/>.
/// </summary>
/// <remarks>Carries no data — see <see cref="ExecuteSampleCalculationCommandHandler"/>.</remarks>
public sealed class ExecuteSampleCalculationCommand : ICommand
{
}
