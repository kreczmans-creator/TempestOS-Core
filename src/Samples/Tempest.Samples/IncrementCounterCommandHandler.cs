using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="IncrementCounterCommand"/> by adding its
/// <see cref="IncrementCounterCommand.Amount"/> to an in-memory counter.
/// </summary>
/// <remarks>
/// The reference implementation for both of a command's two ordinary
/// outcomes: a non-negative amount succeeds; a negative one is rejected as
/// an expected, foreseeable business failure — reported via
/// <see cref="CommandResult.Failure(string)"/>, not by throwing, exactly as
/// <c>Command Framework Architecture.md</c>'s Dispatch Model distinguishes
/// the two.
/// </remarks>
public sealed class IncrementCounterCommandHandler : ICommandHandler<IncrementCounterCommand>
{
    private int _counter;

    /// <summary>
    /// Gets the counter's current value.
    /// </summary>
    public int Counter => _counter;

    /// <inheritdoc />
    public Task<CommandResult> HandleAsync(IncrementCounterCommand command, CancellationToken cancellationToken)
    {
        if (command.Amount < 0)
        {
            return Task.FromResult(
                CommandResult.Failure($"Amount must be non-negative; received {command.Amount}."));
        }

        _counter += command.Amount;

        return Task.FromResult(CommandResult.Success($"Counter is now {_counter}."));
    }
}
