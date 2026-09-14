using Tempest.Core.Commands;
using Tempest.Core.Evidence;

namespace Tempest.Workspace.Evidence;

/// <summary>Reopens an Issued piece of evidence as a new Draft revision, its issued revision staying readable (<see cref="IEvidenceService.ReviseAsync"/>).</summary>
public sealed class ReviseEvidenceCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="ReviseEvidenceCommand"/> class.</summary>
    public ReviseEvidenceCommand(Guid targetObjectId, string targetKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }
}

/// <summary>Handles <see cref="ReviseEvidenceCommand"/>.</summary>
public sealed class ReviseEvidenceCommandHandler : ICommandHandler<ReviseEvidenceCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="ReviseEvidenceCommandHandler"/> class.</summary>
    public ReviseEvidenceCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    /// <remarks>Refused, not thrown, unless the evidence is <see cref="EvidenceStatus.Issued"/>. On success the result's own subject is the new revision's own instance — its Id is unchanged, but the stale, superseded instance a caller may still hold must not be mutated through again.</remarks>
    public async Task<CommandResult> HandleAsync(ReviseEvidenceCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.ReviseAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Revised — reopened as Draft.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The revision was refused.");
    }
}
