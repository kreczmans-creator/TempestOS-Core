using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command incrementing <see cref="CommandSampleModule"/>'s own
/// in-memory counter by <see cref="Amount"/>.
/// </summary>
/// <remarks>
/// Demonstrates both outcomes a command handler can report: a non-negative
/// <see cref="Amount"/> succeeds (<see cref="CommandResult.Success"/>); a
/// negative one is an expected, foreseeable failure
/// (<see cref="CommandResult.Failure"/>) — see
/// <see cref="IncrementCounterCommandHandler"/>.
/// </remarks>
public sealed class IncrementCounterCommand : ICommand
{
    /// <summary>
    /// Initialises a new instance of the <see cref="IncrementCounterCommand"/> class.
    /// </summary>
    /// <param name="amount">The amount to add to the counter.</param>
    public IncrementCounterCommand(int amount)
    {
        Amount = amount;
    }

    /// <summary>
    /// Gets the amount to add to the counter.
    /// </summary>
    public int Amount { get; }
}
