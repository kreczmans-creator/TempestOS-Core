using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Mechanical;

/// <summary>Reparents one Mechanical Product Structure object (<see cref="IHasParent.MoveAsync"/>).</summary>
public sealed class MoveMechanicalObjectCommand : IWorkspaceCommand
{
    public MoveMechanicalObjectCommand(Guid targetObjectId, string targetKind, Guid? newParentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewParentId = newParentId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the new parent, or <see langword="null"/> to make this a top-level object.</summary>
    public Guid? NewParentId { get; }
}

/// <summary>Handles <see cref="MoveMechanicalObjectCommand"/>.</summary>
public sealed class MoveMechanicalObjectCommandHandler : ICommandHandler<MoveMechanicalObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public MoveMechanicalObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(MoveMechanicalObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasParent hasParent)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be moved.");

        return await WorkspaceCommandBindings.MoveResultAsync(
            _context, hasParent, command.TargetObjectId, command.TargetKind, command.NewParentId, cancellationToken).ConfigureAwait(false);
    }
}
