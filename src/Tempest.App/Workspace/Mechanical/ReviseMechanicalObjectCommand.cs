using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Records a new content revision of one Mechanical Product Structure
/// Domain object (<see cref="IHasRevisions.ReviseAsync"/>) — the Object
/// Editor Framework's own "Editable properties" Content field (`WP
/// 10.3A`). Distinct from <see cref="RenameMechanicalObjectCommand"/>
/// (the object's own business name). The one discipline of six that had
/// no Revise command until this Work Package — `EngineeringObjectBase`
/// (`ADR-0075`) already implements <see cref="IHasRevisions"/>
/// unconditionally for every concrete Mechanical Kind, so this is a
/// missing Workspace-layer command wrapper, not a new Domain capability;
/// mirrors <see cref="Calculations.ReviseCalculationCommand"/>/
/// <see cref="Documents.ReviseDocumentCommand"/>/
/// <see cref="Manufacturing.ReviseManufacturingObjectCommand"/>/
/// <see cref="Verification.ReviseVerificationActivityCommand"/>'s own
/// identical shape exactly.
/// </summary>
public sealed class ReviseMechanicalObjectCommand : IWorkspaceCommand
{
    public ReviseMechanicalObjectCommand(Guid targetObjectId, string targetKind, string newContent, string? changeSummary = null)
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

/// <summary>Handles <see cref="ReviseMechanicalObjectCommand"/>.</summary>
public sealed class ReviseMechanicalObjectCommandHandler : ICommandHandler<ReviseMechanicalObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public ReviseMechanicalObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(ReviseMechanicalObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasRevisions revisable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be revised.");

        var revised = await revisable.ReviseAsync(command.NewContent, command.ChangeSummary, cancellationToken).ConfigureAwait(false);
        var revisionNumber = (revised as IEngineeringObject)?.CurrentRevisionNumber;

        return CommandResult.Success($"Revised '{command.TargetObjectId}' to revision {revisionNumber}.");
    }
}
