using Tempest.App.Workspace;
using Tempest.Core.Commands;

namespace Tempest.Core.Tests.Workspace;

/// <summary>A minimal, real <see cref="IWorkspaceCommand"/> — `WP 10.2A`'s own real Rename/Delete dispatch tests' fake target command, mirroring <see cref="TestWorkspaceViewFactory"/>'s own dedicated-test-helper-file precedent.</summary>
public sealed class TestWorkspaceCommand(Guid targetObjectId, string targetKind, string? note = null) : IWorkspaceCommand
{
    public Guid TargetObjectId { get; } = targetObjectId;

    public string TargetKind { get; } = targetKind;

    public string? Note { get; } = note;
}

/// <summary>Records every <see cref="TestWorkspaceCommand"/> it handles, so a test can assert real dispatch happened, with the real arguments, rather than merely that no exception was thrown.</summary>
public sealed class RecordingTestWorkspaceCommandHandler : ICommandHandler<TestWorkspaceCommand>
{
    public List<TestWorkspaceCommand> Handled { get; } = [];

    public Task<CommandResult> HandleAsync(TestWorkspaceCommand command, CancellationToken cancellationToken)
    {
        Handled.Add(command);
        return Task.FromResult(CommandResult.Success($"Handled '{command.TargetObjectId}'."));
    }
}
