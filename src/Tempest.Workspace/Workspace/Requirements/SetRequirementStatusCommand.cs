using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

/// <summary>Sets one Requirement's own current lifecycle status (<see cref="IRequirementsService.SetStatusAsync"/>).</summary>
public sealed class SetRequirementStatusCommand : IWorkspaceCommand
{
    public SetRequirementStatusCommand(Guid targetObjectId, RequirementStatus status)
    {
        TargetObjectId = targetObjectId;
        Status = status;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the requirement's own new status.</summary>
    public RequirementStatus Status { get; }
}

/// <summary>Handles <see cref="SetRequirementStatusCommand"/>.</summary>
public sealed class SetRequirementStatusCommandHandler : ICommandHandler<SetRequirementStatusCommand>
{
    private readonly IRequirementsService _requirementsService;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the requirement's own status is set.</param>
    /// <param name="dispatcher">Dispatches this status change's own compensation (`WP 21.6A`) — optional.</param>
    public SetRequirementStatusCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(SetRequirementStatusCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var current = await _requirementsService.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false)
                ?? throw new RequirementNotFoundException(command.TargetObjectId);
            var previousStatus = current.Status;

            await _requirementsService.SetStatusAsync(command.TargetObjectId, command.Status, cancellationToken).ConfigureAwait(false);

            // A compensation exists only when the lifecycle table itself
            // permits reversing this particular transition — refused
            // otherwise with the reason surfaced, rather than offered and
            // then failing on Undo (`WP 21.6A`, mirrors
            // `WorkspaceCommandBindings.StatusCompensation`/`UndoUnavailableForStatus`).
            CommandCompensation? compensation = null;
            string? undoUnavailableReason = null;

            if (_dispatcher is not null && RequirementStatusTransitions.IsPermitted(command.Status, previousStatus))
            {
                compensation = new CommandCompensation(
                    $"Status change for '{current.Identifier}'",
                    undo: ct => _dispatcher.DispatchAsync(new SetRequirementStatusCommand(command.TargetObjectId, previousStatus), ct),
                    redo: ct => _dispatcher.DispatchAsync(new SetRequirementStatusCommand(command.TargetObjectId, command.Status), ct));
            }
            else if (_dispatcher is not null)
            {
                undoUnavailableReason = $"the lifecycle does not permit reversing '{previousStatus}' → '{command.Status}'.";
            }

            return CommandResult.Success(
                $"Status set to '{command.Status}' for '{command.TargetObjectId}'.", compensation: compensation, undoUnavailableReason: undoUnavailableReason);
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (InvalidRequirementStatusTransitionException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
