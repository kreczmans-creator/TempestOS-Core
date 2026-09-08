using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Records a new content revision of one Calculation Domain object
/// (<see cref="IHasRevisions.ReviseAsync"/>) — the Calculation Management
/// scope's own "Edit" capability. Distinct from
/// <see cref="RenameCalculationObjectCommand"/> (the object's own business
/// name) and from <see cref="ExecuteCalculationCommand"/> (a new
/// evidentiary <c>CalculationRecord</c>) — this only revises the object's
/// own descriptive content (e.g. a calculation's own written method
/// statement).
/// </summary>
public sealed class ReviseCalculationCommand : IWorkspaceCommand
{
    public ReviseCalculationCommand(Guid targetObjectId, string targetKind, string newContent, string? changeSummary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentNullException.ThrowIfNull(newContent);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewContent = newContent;
        ChangeSummary = changeSummary;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the new revision content.</summary>
    public string NewContent { get; }

    /// <summary>Gets an optional summary of what changed.</summary>
    public string? ChangeSummary { get; }
}

/// <summary>Handles <see cref="ReviseCalculationCommand"/>.</summary>
public sealed class ReviseCalculationCommandHandler : ICommandHandler<ReviseCalculationCommand>
{
    private readonly EngineeringDomainContext _context;

    public ReviseCalculationCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(ReviseCalculationCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasRevisions revisable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be revised.");

        var revised = await revisable.ReviseAsync(command.NewContent, command.ChangeSummary, cancellationToken).ConfigureAwait(false);
        var revisionNumber = (revised as IEngineeringObject)?.CurrentRevisionNumber;

        return CommandResult.Success($"Revised '{command.TargetObjectId}' to revision {revisionNumber}.");
    }
}
