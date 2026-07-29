using Tempest.Core.Commands;

namespace Tempest.Core.Tests.Api;

internal sealed class RecordedApiCommand : ICommand
{
}

internal sealed class RecordingApiCommandHandler : ICommandHandler<RecordedApiCommand>
{
    private readonly Func<CommandResult>? _onHandle;

    public RecordingApiCommandHandler(Func<CommandResult>? onHandle = null)
    {
        _onHandle = onHandle;
    }

    public int CallCount { get; private set; }

    public Task<CommandResult> HandleAsync(RecordedApiCommand command, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_onHandle?.Invoke() ?? CommandResult.Success("ok"));
    }
}
