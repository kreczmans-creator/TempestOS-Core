using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Manufacturing;

/// <summary>
/// Sets one Manufacturing Domain object's own current lifecycle status
/// (<see cref="IHasLifecycle.TransitionAsync"/>, via the existing
/// <see cref="ILifecycleTransitionTable"/> — the same mechanism every
/// prior real-discipline Work Package already establishes). This Work
/// Package's own named "Release"/"Archive" verbs map directly onto
/// <see cref="LifecycleState.Released"/>/<see cref="LifecycleState.Archived"/>
/// — the identical "already matches 1:1, no aliasing needed" finding
/// `WP 9.4A` already made for Document Management's own Draft/Review/
/// Approved/Released statuses.
/// </summary>
public sealed class SetManufacturingObjectStatusCommand : IWorkspaceCommand
{
    public SetManufacturingObjectStatusCommand(Guid targetObjectId, string targetKind, LifecycleState status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Status = status;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the object's own new status.</summary>
    public LifecycleState Status { get; }
}

/// <summary>Handles <see cref="SetManufacturingObjectStatusCommand"/>.</summary>
public sealed class SetManufacturingObjectStatusCommandHandler : ICommandHandler<SetManufacturingObjectStatusCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly ICommandDispatcher? _dispatcher;

    public SetManufacturingObjectStatusCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(SetManufacturingObjectStatusCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasLifecycle lifecycle)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind has no lifecycle status.");

        var previousStatus = lifecycle.Status;
        var sourceName = (target as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();

        try
        {
            await lifecycle.TransitionAsync(command.Status, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidLifecycleTransitionException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var compensation = WorkspaceCommandBindings.StatusCompensation(
            _context, _dispatcher, command.TargetObjectId, command.TargetKind, sourceName, previousStatus, command.Status,
            status => new SetManufacturingObjectStatusCommand(command.TargetObjectId, command.TargetKind, status));

        return CommandResult.Success(
            $"Status set to '{command.Status}' for '{command.TargetObjectId}'.", compensation: compensation,
            undoUnavailableReason: compensation is null ? WorkspaceCommandBindings.UndoUnavailableForStatus(previousStatus, command.Status) : null);
    }
}
