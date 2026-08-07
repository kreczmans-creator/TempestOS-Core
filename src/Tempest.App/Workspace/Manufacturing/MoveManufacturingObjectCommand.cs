using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>Reparents one Manufacturing Domain object (<see cref="IHasParent.MoveAsync"/>) — for a Routing/Operation pair, this is also how an Operation step is added to, or removed from, a Routing's own sequence (`ADR-0091`).</summary>
public sealed class MoveManufacturingObjectCommand : IWorkspaceCommand
{
    public MoveManufacturingObjectCommand(Guid targetObjectId, string targetKind, Guid? newParentId)
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

/// <summary>Handles <see cref="MoveManufacturingObjectCommand"/>.</summary>
public sealed class MoveManufacturingObjectCommandHandler : ICommandHandler<MoveManufacturingObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public MoveManufacturingObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(MoveManufacturingObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasParent hasParent)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be moved.");

        try
        {
            await hasParent.MoveAsync(command.NewParentId, cancellationToken).ConfigureAwait(false);
        }
        catch (CircularParentAssignmentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success(command.NewParentId is { } parentId
            ? $"Moved '{command.TargetObjectId}' under '{parentId}'."
            : $"Moved '{command.TargetObjectId}' to top level.");
    }
}
