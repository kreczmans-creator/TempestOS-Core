using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Mechanical;

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
    private readonly ICommandDispatcher? _dispatcher;

    public DeleteMechanicalObjectCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(DeleteMechanicalObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IDeletable deletable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be deleted.");

        var sourceName = (target as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();

        try
        {
            await deletable.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (EngineeringObjectHasChildrenException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var compensation = WorkspaceCommandBindings.DeleteCompensation(
            _context, _dispatcher, command.TargetObjectId, command.TargetKind, sourceName,
            buildDelete: () => new DeleteMechanicalObjectCommand(command.TargetObjectId, command.TargetKind),
            buildUndelete: () => new UndeleteMechanicalObjectCommand(command.TargetObjectId, command.TargetKind));

        return CommandResult.Success($"Deleted '{command.TargetObjectId}'.", compensation: compensation);
    }
}
