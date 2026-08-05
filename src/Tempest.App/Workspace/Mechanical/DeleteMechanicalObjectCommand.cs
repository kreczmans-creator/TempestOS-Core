using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>Soft-deletes one Mechanical Product Structure object (<see cref="IDeletable.DeleteAsync"/>).</summary>
public sealed class DeleteMechanicalObjectCommand : IWorkspaceCommand
{
    public DeleteMechanicalObjectCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="DeleteMechanicalObjectCommand"/>.</summary>
public sealed class DeleteMechanicalObjectCommandHandler : ICommandHandler<DeleteMechanicalObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public DeleteMechanicalObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(DeleteMechanicalObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IDeletable deletable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be deleted.");

        try
        {
            await deletable.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (EngineeringObjectHasChildrenException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Deleted '{command.TargetObjectId}'.");
    }
}
