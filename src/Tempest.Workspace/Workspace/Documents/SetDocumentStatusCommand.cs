using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Documents;

/// <summary>
/// Sets one Document Domain object's own current lifecycle status
/// (<see cref="IHasLifecycle.TransitionAsync"/>, via the existing
/// <see cref="ILifecycleTransitionTable"/> — the same mechanism
/// <see cref="Calculations.SetCalculationStatusCommand"/>/
/// <see cref="Requirements.SetRequirementStatusCommand"/> already establish
/// for a different framework). Unlike Calculations' own Lock/Unlock
/// aliasing, this Work Package's own named statuses (Draft/Review/Approved/
/// Released) map directly onto <see cref="LifecycleState"/>'s own existing
/// values one-for-one — no descriptive alias is needed.
/// </summary>
public sealed class SetDocumentStatusCommand : IWorkspaceCommand
{
    public SetDocumentStatusCommand(Guid targetObjectId, string targetKind, LifecycleState status)
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

/// <summary>Handles <see cref="SetDocumentStatusCommand"/>.</summary>
public sealed class SetDocumentStatusCommandHandler : ICommandHandler<SetDocumentStatusCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="context">Where the target object is found.</param>
    /// <param name="dispatcher">Dispatches this transition's own compensation (`WP 21.1A`) — optional; <see langword="null"/> means no <see cref="CommandResult.Compensation"/> is attached.</param>
    public SetDocumentStatusCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(SetDocumentStatusCommand command, CancellationToken cancellationToken)
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
            status => new SetDocumentStatusCommand(command.TargetObjectId, command.TargetKind, status));

        return CommandResult.Success(
            $"Status set to '{command.Status}' for '{command.TargetObjectId}'.", compensation: compensation,
            undoUnavailableReason: compensation is null ? WorkspaceCommandBindings.UndoUnavailableForStatus(previousStatus, command.Status) : null);
    }
}
