using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>Soft-deletes one Calculation Domain object (<see cref="IDeletable.DeleteAsync"/>).</summary>
public sealed class DeleteCalculationObjectCommand : IWorkspaceCommand
{
    public DeleteCalculationObjectCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="DeleteCalculationObjectCommand"/>.</summary>
public sealed class DeleteCalculationObjectCommandHandler : ICommandHandler<DeleteCalculationObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public DeleteCalculationObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(DeleteCalculationObjectCommand command, CancellationToken cancellationToken)
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
