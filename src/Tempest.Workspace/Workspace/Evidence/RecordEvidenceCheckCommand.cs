using Tempest.Core.Commands;
using Tempest.Core.Evidence;

namespace Tempest.Workspace.Evidence;

/// <summary>Records a check against a piece of evidence and moves it to <see cref="EvidenceStatus.Checked"/> (<see cref="IEvidenceService.RecordCheckAsync"/>).</summary>
public sealed class RecordEvidenceCheckCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="RecordEvidenceCheckCommand"/> class.</summary>
    public RecordEvidenceCheckCommand(
        Guid targetObjectId, string targetKind, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkerOrganisation);
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        CheckerName = checkerName;
        CheckerOrganisation = checkerOrganisation;
        Statement = statement;
        Outcome = outcome;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the checker's name, as typed.</summary>
    public string CheckerName { get; }

    /// <summary>Gets the checker's organisation, as typed.</summary>
    public string CheckerOrganisation { get; }

    /// <summary>Gets the checker's own statement, verbatim.</summary>
    public string Statement { get; }

    /// <summary>Gets what the checker concluded.</summary>
    public CheckOutcome Outcome { get; }
}

/// <summary>Handles <see cref="RecordEvidenceCheckCommand"/>.</summary>
public sealed class RecordEvidenceCheckCommandHandler : ICommandHandler<RecordEvidenceCheckCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="RecordEvidenceCheckCommandHandler"/> class.</summary>
    public RecordEvidenceCheckCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    /// <remarks>Refused, not thrown, when the evidence is not Draft, or — with <c>Evidence:IndependentCheck</c> on — when the acting principal is also the evidence's own author.</remarks>
    public async Task<CommandResult> HandleAsync(RecordEvidenceCheckCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.RecordCheckAsync(
            command.TargetObjectId, command.CheckerName, command.CheckerOrganisation, command.Statement, command.Outcome, cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Checked: {command.Outcome}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The check was refused.");
    }
}
