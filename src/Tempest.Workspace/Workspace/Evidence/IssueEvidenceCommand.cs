using Tempest.Core.Commands;
using Tempest.Core.Evidence;

namespace Tempest.Workspace.Evidence;

/// <summary>Issues a checked piece of evidence to the client and moves it to <see cref="EvidenceStatus.Issued"/> (<see cref="IEvidenceService.IssueAsync"/>).</summary>
public sealed class IssueEvidenceCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="IssueEvidenceCommand"/> class.</summary>
    public IssueEvidenceCommand(Guid targetObjectId, string targetKind, string issueReference, string revision, string client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(client);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        IssueReference = issueReference;
        Revision = revision;
        Client = client;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the issue's own reference.</summary>
    public string IssueReference { get; }

    /// <summary>Gets the revision issued.</summary>
    public string Revision { get; }

    /// <summary>Gets the client the evidence is issued to.</summary>
    public string Client { get; }
}

/// <summary>Handles <see cref="IssueEvidenceCommand"/>.</summary>
public sealed class IssueEvidenceCommandHandler : ICommandHandler<IssueEvidenceCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="IssueEvidenceCommandHandler"/> class.</summary>
    public IssueEvidenceCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    /// <remarks>Refused, not thrown, unless the evidence is <see cref="EvidenceStatus.Checked"/>.</remarks>
    public async Task<CommandResult> HandleAsync(IssueEvidenceCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.IssueAsync(command.TargetObjectId, command.IssueReference, command.Revision, command.Client, cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Issued as '{command.IssueReference}'.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The issue was refused.");
    }
}
