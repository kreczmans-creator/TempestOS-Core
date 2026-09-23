using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Calculations;

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
    private readonly ICommandDispatcher? _dispatcher;

    public DeleteCalculationObjectCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(DeleteCalculationObjectCommand command, CancellationToken cancellationToken)
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
            buildDelete: () => new DeleteCalculationObjectCommand(command.TargetObjectId, command.TargetKind),
            buildUndelete: () => new UndeleteCalculationObjectCommand(command.TargetObjectId, command.TargetKind));

        return CommandResult.Success($"Deleted '{command.TargetObjectId}'.", compensation: compensation);
    }
}
