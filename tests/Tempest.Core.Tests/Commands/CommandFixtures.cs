using Tempest.Core.Commands;

namespace Tempest.Core.Tests.Commands;

internal sealed class RecordedCommandA : ICommand
{
    public RecordedCommandA(string payload = "payload")
    {
        Payload = payload;
    }

    public string Payload { get; }
}

internal sealed class RecordedCommandB : ICommand
{
}

/// <summary>
/// A configurable <see cref="ICommandHandler{TCommand}"/> fixture, mirroring
/// <c>Tempest.Core.Tests.Events.RecordingHandler{TEvent}</c>'s own shape:
/// records every command it handles, and its behaviour (succeed, fail, or
/// throw) is supplied by the test.
/// </summary>
internal sealed class RecordingCommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
{
    private readonly Func<TCommand, CancellationToken, Task<CommandResult>> _handle;
    private readonly List<TCommand> _received = [];

    public RecordingCommandHandler(Func<TCommand, CancellationToken, Task<CommandResult>>? handle = null)
    {
        _handle = handle ?? ((_, _) => Task.FromResult(CommandResult.Success()));
    }

    public IReadOnlyList<TCommand> Received => _received;

    public Task<CommandResult> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        _received.Add(command);
        return _handle(command, cancellationToken);
    }
}
